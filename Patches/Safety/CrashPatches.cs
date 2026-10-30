/*
 * ii Reborn
 * Portions Copyright (C) 2025–2026 Goldentrophy Software
 * Licensed under GNU GPL v3.0-or-later — see LICENSE and NOTICE.
 * This file is part of a derivative work; see NOTICE for attribution
 * and modification history. Do not remove this notice.
 */

using GorillaLocomotion.Gameplay;
using HarmonyLib;
using iiMenu.Extensions;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static iiMenu.Patches.PatchHandler;

namespace iiMenu.Patches.Safety
{
    public class CrashPatches
    {
        [SecurityPatch]
        [HarmonyPatch(typeof(VRRig), "UpdateRopeData")]
        public class UpdateRopeDataPatch
        {
            private static void Prefix(VRRig __instance, ref int ___grabbedRopeIndex, ref int ___grabbedRopeBoneIndex, bool ___grabbedRopeIsPhotonView)
            {
                if (___grabbedRopeIndex != -1)
                {
                    if (___grabbedRopeIsPhotonView)
                    {
                        PhotonView photonView = PhotonView.Find(___grabbedRopeIndex);
                        if (photonView == null)
                            ___grabbedRopeIndex = -1;
                    }
                    else
                    {
                        if (RopeSwingManager.instance == null || !RopeSwingManager.instance.TryGetRope(___grabbedRopeIndex, out var result) || result == null)
                            ___grabbedRopeIndex = -1;
                        else
                            ___grabbedRopeBoneIndex = Mathf.Clamp(___grabbedRopeBoneIndex, 0, int.MaxValue);
                    }
                }
            }
        }

        [SecurityPatch]
        [HarmonyPatch(typeof(GorillaRopeSwing), "GetBone")]
        public class GetBonePatch
        {
            private static bool Prefix(GorillaRopeSwing __instance, ref Transform __result, int index, Transform[] ___nodes)
            {
                if (___nodes == null || ___nodes.Length == 0)
                {
                    __result = __instance.transform;
                    return false;
                }

                if (index < 0)
                {
                    __result = ___nodes[0];
                    return false;
                }
                
                if (index >= ___nodes.Length)
                {
                    __result = ___nodes[___nodes.Length - 1];
                    return false;
                }

                __result = ___nodes[index];
                return false;
            }
        }

        [SecurityPatch]
        [HarmonyPatch(typeof(CrittersManager), "RemoteCrittersActorGrabbedby")]
        public class RemoteCrittersActorGrabbedbyPatch
        {
            private static bool Prefix(CrittersManager __instance, int grabbedActorID, int grabberActorID)
            {
                if (grabbedActorID == grabberActorID || grabbedActorID < 0 || grabberActorID < 0) return false;
                if (__instance == null || __instance.actorById == null) return true;
                if (!__instance.actorById.TryGetValue(grabbedActorID, out _) || !__instance.actorById.TryGetValue(grabberActorID, out CrittersActor grabber)) return false;
                int current = grabber.parentActorId;
                int depth = 0;
                while (current != -1 && depth < 30)
                {
                    if (current == grabbedActorID) return false;
                    if (__instance.actorById.TryGetValue(current, out CrittersActor parent))
                        current = parent.parentActorId;
                    else
                        break;
                    depth++;
                }
                return depth < 30;
            }
        }

        [SecurityPatch]
        [HarmonyPatch(typeof(CrittersActor), "GrabbedBy")]
        public class CrittersActorGrabbedByPatch
        {
            private static bool Prefix(CrittersActor __instance, CrittersActor grabbingActor)
            {
                if (__instance == null || grabbingActor == null || __instance == grabbingActor) return false;
                CrittersActor current = grabbingActor;
                int depth = 0;
                while (current != null && depth < 30)
                {
                    if (current == __instance) return false;
                    if (CrittersManager.instance != null && CrittersManager.instance.actorById != null && current.parentActorId != -1 && CrittersManager.instance.actorById.TryGetValue(current.parentActorId, out CrittersActor parent))
                        current = parent;
                    else
                        break;
                    depth++;
                }
                return depth < 30;
            }
        }

        [SecurityPatch]
        [HarmonyPatch(typeof(CrittersActor), "GetRootActor", typeof(int))]
        public class CrittersActorGetRootActorPatch
        {
            private static bool Prefix(int actorId, ref CrittersActor __result)
            {
                if (CrittersManager.instance == null || CrittersManager.instance.actorById == null)
                {
                    __result = null;
                    return false;
                }
                int currentId = actorId;
                CrittersActor currentActor = null;
                int depth = 0;
                while (currentId != -1 && depth < 30)
                {
                    if (!CrittersManager.instance.actorById.TryGetValue(currentId, out CrittersActor next) || next == null)
                        break;
                    currentActor = next;
                    if (currentActor.parentActorId == currentId)
                    {
                        currentActor.parentActorId = -1;
                        break;
                    }
                    currentId = currentActor.parentActorId;
                    depth++;
                }
                if (depth >= 30 && currentActor != null)
                    currentActor.parentActorId = -1;
                __result = currentActor;
                return false;
            }
        }
    }

    public static class AntiCrashCritterCore
    {
        private static readonly Dictionary<int, float> _lastRpc = new Dictionary<int, float>();
        private const float MIN_INTERVAL = 0.05f;

        public static bool LocalTransferInProgress { get; private set; } = false;
        public static void BeginLocalTransfer() => LocalTransferInProgress = true;
        public static void EndLocalTransfer() => LocalTransferInProgress = false;

        public static bool AllowTransfer(PhotonView view, Player newOwner)
        {
            if (!PhotonNetwork.IsMasterClient) return true;
            if (view == null || newOwner == null) return true;
            if (view.Owner != null && view.Owner.IsLocal && !LocalTransferInProgress && !newOwner.IsLocal)
                return false;
            return true;
        }

        public static bool ValidateIncoming(CrittersManager manager, PhotonMessageInfo info)
        {
            if (manager == null) return false;

            Player sender = null;
            try { sender = info.Sender; } catch { }
            if (sender == null) return true;

            float now = Time.realtimeSinceStartup;
            if (_lastRpc.TryGetValue(sender.ActorNumber, out float last) && now - last < MIN_INTERVAL)
                return false;
            _lastRpc[sender.ActorNumber] = now;

            if (!PhotonNetwork.IsMasterClient) return true;
            if (sender.IsLocal) return true;

            Player guardOwner = GetGuardOwner(manager);
            if (guardOwner == null) return true;
            return guardOwner.ActorNumber == sender.ActorNumber;
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
                if (p != null)
                {
                    if (typeof(Player).IsAssignableFrom(p.PropertyType))
                    {
                        try { return (Player)p.GetValue(guard); } catch { }
                    }
                    try
                    {
                        object val = p.GetValue(guard);
                        if (val is NetPlayer netPlayer)
                            return PhotonNetwork.CurrentRoom?.GetPlayer(netPlayer.ActorNumber);
                    }
                    catch { }
                }
                var f = gt.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null)
                {
                    if (typeof(Player).IsAssignableFrom(f.FieldType))
                    {
                        try { return (Player)f.GetValue(guard); } catch { }
                    }
                    try
                    {
                        object val = f.GetValue(guard);
                        if (val is NetPlayer netPlayer)
                            return PhotonNetwork.CurrentRoom?.GetPlayer(netPlayer.ActorNumber);
                    }
                    catch { }
                }
            }
            if (guard is Component comp)
            {
                var pv = comp.GetComponentInParent<PhotonView>();
                if (pv != null) return pv.Owner;
            }
            return null;
        }
    }

    [SecurityPatch]
    [HarmonyPatch]
    public static class Patch_TransferOwnership_Player
    {
        [HarmonyPrepare]
        public static bool Prepare()
            => AccessTools.Method(typeof(PhotonView), nameof(PhotonView.TransferOwnership), new[] { typeof(Player) }) != null;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PhotonView), nameof(PhotonView.TransferOwnership), new[] { typeof(Player) })]
        public static bool Prefix(PhotonView __instance, Player newOwner)
            => AntiCrashCritterCore.AllowTransfer(__instance, newOwner);
    }

    [SecurityPatch]
    [HarmonyPatch]
    public static class Patch_TransferOwnership_Id
    {
        [HarmonyPrepare]
        public static bool Prepare()
            => AccessTools.Method(typeof(PhotonView), nameof(PhotonView.TransferOwnership), new[] { typeof(int) }) != null;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PhotonView), nameof(PhotonView.TransferOwnership), new[] { typeof(int) })]
        public static bool Prefix(PhotonView __instance, int newOwnerId)
        {
            Player p = PhotonNetwork.CurrentRoom?.GetPlayer(newOwnerId);
            return AntiCrashCritterCore.AllowTransfer(__instance, p);
        }
    }

    [SecurityPatch]
    [HarmonyPatch]
    public static class Patch_RPC_Grabbed
    {
        [HarmonyPrepare]
        public static bool Prepare() => FindMethod() != null;

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
            if (__args != null)
                for (int i = __args.Length - 1; i >= 0; i--)
                    if (__args[i] is PhotonMessageInfo pmi) { info = pmi; break; }
            return AntiCrashCritterCore.ValidateIncoming(CrittersManager.instance, info);
        }
    }

    [SecurityPatch]
    [HarmonyPatch]
    public static class Patch_RPC_Released
    {
        [HarmonyPrepare]
        public static bool Prepare() => FindMethod() != null;

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
            if (__args != null)
                for (int i = __args.Length - 1; i >= 0; i--)
                    if (__args[i] is PhotonMessageInfo pmi) { info = pmi; break; }
            return AntiCrashCritterCore.ValidateIncoming(CrittersManager.instance, info);
        }
    }
}
