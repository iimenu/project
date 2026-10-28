using iiMenu.Extensions;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using HarmonyLib;

namespace iiMenu.Patches.Safety
{
    public static class AntiCrashCritterCore
    {
        private static readonly Dictionary<int, float> _lastRpc = new Dictionary<int, float>();
        private static readonly Dictionary<(int, int), (int from, int to, float time)> _recentGrab = new Dictionary<(int, int), (int from, int to, float time)>();
        private const float MIN_INTERVAL = 0.05f;
        private const float MUTUAL_GRAB_WINDOW = 0.10f;
        private static float _lastCleanup;

        public static bool LocalTransferInProgress { get; private set; }
        public static void BeginLocalTransfer() => LocalTransferInProgress = true;
        public static void EndLocalTransfer() => LocalTransferInProgress = false;

        public static bool AllowTransfer(PhotonView view, Player newOwner)
        {
            if (!PhotonNetwork.IsMasterClient) return true;
            if (view == null || newOwner == null) return true;
            if (view.Owner != null && view.Owner.IsLocal
                && !LocalTransferInProgress && !newOwner.IsLocal)
                return false;
            return true;
        }

        public static bool ValidateIncoming(CrittersManager manager, PhotonMessageInfo info,
                                            int actorId1 = -1, int actorId2 = -1)
        {
            if (manager == null) return false;

            Player sender = null;
            try { sender = info.Sender; } catch { }
            if (sender == null) return true;

            float now = Time.realtimeSinceStartup;

            if (_lastRpc.TryGetValue(sender.ActorNumber, out float last)
                && now - last < MIN_INTERVAL)
                return false;
            _lastRpc[sender.ActorNumber] = now;

            MaybeCleanup(now);
            if (sender.IsLocal) return true;
            if (actorId1 >= 0 && actorId2 >= 0 && actorId1 == actorId2)
                return false;
            if (actorId1 >= 0 && actorId2 >= 0 && IsMutualGrab(actorId1, actorId2, now))
                return false;
            Player guardOwner = GetGuardOwner(manager);
            if (guardOwner == null) return true;
            if (guardOwner.ActorNumber != sender.ActorNumber) return false;

            return true;
        }

        private static bool IsMutualGrab(int a, int b, float now)
        {
            var key = (Mathf.Min(a, b), Mathf.Max(a, b));
            if (_recentGrab.TryGetValue(key, out var prev))
            {
                if (prev.from == b && prev.to == a && now - prev.time < MUTUAL_GRAB_WINDOW)
                {
                    _recentGrab.Remove(key);
                    return true;
                }
            }
            _recentGrab[key] = (a, b, now);
            return false;
        }

        private static void MaybeCleanup(float now)
        {
            if (now - _lastCleanup < 30f) return;
            _lastCleanup = now;

            var staleR = new List<int>();
            foreach (var kv in _lastRpc)
                if (now - kv.Value > 60f) staleR.Add(kv.Key);
            foreach (var k in staleR) _lastRpc.Remove(k);

            var staleG = new List<(int, int)>();
            foreach (var kv in _recentGrab)
                if (now - kv.Value.time > 2f) staleG.Add(kv.Key);
            foreach (var k in staleG) _recentGrab.Remove(k);
        }

        public static Player GetGuardOwner(CrittersManager manager)
        {
            object guard = null;
            try
            {
                var t = typeof(CrittersManager);
                var f = t.GetField("guard", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null) guard = f.GetValue(manager);
                if (guard == null)
                {
                    var p = t.GetProperty("guard", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (p != null) guard = p.GetValue(manager);
                }
            }
            catch { return null; }
            if (guard == null) return null;

            var gt = guard.GetType();
            foreach (var name in new[] { "currentOwner", "owner", "Owner" })
            {
                var p = gt.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && typeof(Player).IsAssignableFrom(p.PropertyType))
                { try { return (Player)p.GetValue(guard); } catch { } }
                var f = gt.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && typeof(Player).IsAssignableFrom(f.FieldType))
                { try { return (Player)f.GetValue(guard); } catch { } }
            }
            if (guard is Component comp)
            {
                var pv = comp.GetComponentInParent<PhotonView>();
                if (pv != null) return pv.Owner;
            }
            return null;
        }
    }
    [HarmonyPatch]
    public static class Patch_RPC_Grabbed
    {
        [HarmonyPrepare] public static bool Prepare() => FindMethod() != null;

        [HarmonyTargetMethod]
        public static MethodBase FindMethod()
        {
            try
            {
                return AccessTools.GetDeclaredMethods(typeof(CrittersManager))
                    .FirstOrDefault(m => m.Name == "RemoteCrittersActorGrabbedby");
            }
            catch { return null; }
        }

        [HarmonyPrefix]
        public static bool Prefix(object[] __args)
        {
            PhotonMessageInfo info = default;
            int a1 = -1, a2 = -1;
            int count = 0;

            if (__args != null)
            {
                for (int i = 0; i < __args.Length; i++)
                {
                    if (__args[i] is PhotonMessageInfo pmi) info = pmi;
                    else if (__args[i] is int iv)
                    {
                        if (count == 0) a1 = iv;
                        else if (count == 1) a2 = iv;
                        count++;
                    }
                }
            }

            return AntiCrashCritterCore.ValidateIncoming(CrittersManager.instance, info, a1, a2);
        }
    }

    [HarmonyPatch]
    public static class Patch_RPC_Released
    {
        [HarmonyPrepare] public static bool Prepare() => FindMethod() != null;

        [HarmonyTargetMethod]
        public static MethodBase FindMethod()
        {
            try
            {
                return AccessTools.GetDeclaredMethods(typeof(CrittersManager))
                    .FirstOrDefault(m => m.Name == "RemoteCritterActorReleased");
            }
            catch { return null; }
        }

        [HarmonyPrefix]
        public static bool Prefix(object[] __args)
        {
            PhotonMessageInfo info = default;
            int a1 = -1;

            if (__args != null)
            {
                for (int i = 0; i < __args.Length; i++)
                {
                    if (__args[i] is PhotonMessageInfo pmi) info = pmi;
                    else if (__args[i] is int iv && a1 < 0) a1 = iv;
                }
            }

            return AntiCrashCritterCore.ValidateIncoming(CrittersManager.instance, info, a1, -1);
        }
    }
}