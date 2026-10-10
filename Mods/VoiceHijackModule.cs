/*
 * ii Reborn
 * Portions Copyright (C) 2025–2026 Goldentrophy Software
 * Licensed under GNU GPL v3.0-or-later — see LICENSE and NOTICE.
 * This file is part of a derivative work; see NOTICE for attribution
 * and modification history. Do not remove this notice.
 */
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Photon.Voice.PUN;
using Photon.Voice.Unity;
using System.Collections.Generic;
using System.Linq;
using iiMenu.Extensions;

namespace iiMenu.Mods
{
    public static class VoiceHijackModule
    {
        public static NetPlayer talkThroughTarget;
        public static bool talkThroughKeepTarget;

        private static float talkThroughNullLogDelay;
        private static int talkThroughAppliedViewId;
        private static float talkThroughClaimTime;
        private static bool talkThroughReassert1Done;
        private static bool talkThroughReassert2Done;
        private static float talkThroughVerboseDelay;
        private static float talkThroughAutoDelay;
        private static float talkThroughReassertDelay;
        private static readonly Dictionary<int, float> talkThroughRigFirstSeen = new Dictionary<int, float>();
        public static void VoiceHijack()
        {
            if (Time.time < talkThroughAutoDelay) return;
            talkThroughAutoDelay = Time.time + 0.1f;
            if (talkThroughTarget != null)
            {
                UpdateTalkThrough();
                return;
            }
            List<NetPlayer> candidates = GetTalkThroughCandidates();
            if (candidates.Count == 0) return;
            bool verbose = Time.time > talkThroughVerboseDelay;
            if (verbose)
            {
                talkThroughVerboseDelay = Time.time + 1f;
            }
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                NetPlayer candidate = candidates[i];
                if (!IsVoiceSlotClaimable(candidate))
                {
                    if (talkThroughRigFirstSeen.Remove(candidate.ActorNumber))
                    {
                    }
                    continue;
                }
                if (!talkThroughRigFirstSeen.ContainsKey(candidate.ActorNumber))
                {
                    talkThroughRigFirstSeen[candidate.ActorNumber] = Time.time;
                }
                if (TrySetTalkThrough(candidate))
                {
                    return;
                }
            }
        }
        public static void UpdateTalkThrough()
        {
            if (talkThroughTarget == null) return;
            Recorder rec = GetTalkRecorder();
            if (rec == null) return;
            bool stillHere = NetworkSystem.Instance != null
                && NetworkSystem.Instance.AllNetPlayers != null
                && NetworkSystem.Instance.AllNetPlayers.Contains(talkThroughTarget);
            if (!stillHere)
            {
                talkThroughTarget = null;
                DisableTalkThrough();
                return;
            }
            int stableId;
            bool hasStableId = TryGetTargetViewId(talkThroughTarget, out stableId);
            if (rec.UserData is int currentId && hasStableId && currentId == stableId)
            {
                if (hasStableId) RunScheduledReasserts(stableId);
                return;
            }
            if (Time.time < talkThroughReassertDelay) return;
            talkThroughReassertDelay = Time.time + 2f;
            VRRig rig = GetVRRigFromPlayer(talkThroughTarget);
            if (rig == null) return;
            PhotonView view = rig.GetComponent<PhotonView>();
            if (view == null && rig.netView != null) view = rig.netView.GetView;
            if (view != null) ApplyRecorderUserData(view.ViewID);
        }
        public static void DisableTalkThrough()
        {
            talkThroughTarget = null;
            if (GorillaTagger.Instance != null && GorillaTagger.Instance.myVRRig != null)
            {
                PhotonView view = GorillaTagger.Instance.myVRRig.GetComponent<PhotonView>();
                if (view != null) ApplyRecorderUserData(view.ViewID);
            }
            Recorder rec = GetTalkRecorder();
            if (rec != null) rec.VoiceDetection = true;
        }
        private static bool TrySetTalkThrough(NetPlayer player)
        {
            if (player == null || player == NetworkSystem.Instance.LocalPlayer) return false;
            if (GetTalkRecorder() == null) return false;
            if (!IsVoiceSlotClaimable(player)) return false;
            VRRig rig = GetVRRigFromPlayer(player);
            if (rig == null || rig.IsLocal()) return false;
            PhotonView view = rig.GetComponent<PhotonView>();
            if (view == null && rig.netView != null) view = rig.netView.GetView;
            if (view == null) return false;
            talkThroughTarget = player;
            talkThroughClaimTime = Time.time;
            talkThroughReassert1Done = false;
            talkThroughReassert2Done = false;

            ApplyRecorderUserData(view.ViewID);
            EnsureMainMicTransmitting();
            return true;
        }
        private static bool IsVoiceSlotClaimable(NetPlayer player)
        {
            if (player == null) return false;
            VRRig rig = GetVRRigFromPlayer(player);
            if (rig == null) return false;

            PhotonVoiceView voiceView = rig.GetComponent<PhotonVoiceView>();
            if (voiceView == null) return false;
            var speaker = voiceView.SpeakerInUse;
            return speaker == null || !speaker.IsLinked;
        }
        private static void ApplyRecorderUserData(int viewId)
        {
            Recorder rec = GetTalkRecorder();
            if (rec == null) return;
            rec.UserData = viewId;
            talkThroughAppliedViewId = viewId;
            try
            {
                rec.RestartRecording(force: true);
            }
            catch { }
        }
        private static void EnsureMainMicTransmitting()
        {
            Recorder mic = GetTalkRecorder();
            if (mic == null) return;
            if (PhotonNetwork.InRoom) mic.IsRecording = true;
            mic.TransmitEnabled = true;
            mic.VoiceDetection = false;
        }
        private static void RunScheduledReasserts(int targetViewId)
        {
            float age = Time.time - talkThroughClaimTime;
            if (!talkThroughReassert1Done && age >= 2.5f)
            {
                talkThroughReassert1Done = true;
                ApplyRecorderUserData(targetViewId);
            }
            else if (!talkThroughReassert2Done && age >= 6f)
            {
                talkThroughReassert2Done = true;
                ApplyRecorderUserData(targetViewId);
            }
        }
        private static bool TryGetTargetViewId(NetPlayer player, out int viewId)
        {
            viewId = 0;
            VRRig rig = GetVRRigFromPlayer(player);
            if (rig == null) return false;
            PhotonView view = rig.GetComponent<PhotonView>();
            if (view == null && rig.netView != null) view = rig.netView.GetView;
            if (view == null) return false;
            viewId = view.ViewID;
            return true;
        }
        private static List<NetPlayer> GetTalkThroughCandidates()
        {
            List<NetPlayer> list = new List<NetPlayer>();
            if (NetworkSystem.Instance == null || NetworkSystem.Instance.AllNetPlayers == null) return list;
            foreach (NetPlayer p in NetworkSystem.Instance.AllNetPlayers)
            {
                if (p != null && p != NetworkSystem.Instance.LocalPlayer)
                    list.Add(p);
            }
            list.Sort((a, b) => a.ActorNumber.CompareTo(b.ActorNumber));
            return list;
        }
        private static Recorder GetTalkRecorder()
        {
            if (GorillaTagger.Instance != null && GorillaTagger.Instance.myRecorder != null)
                return GorillaTagger.Instance.myRecorder;

            if (NetworkSystem.Instance != null)
                return NetworkSystem.Instance.LocalRecorder;

            return null;
        }
        public static VRRig GetVRRigFromPlayer(NetPlayer p)
        {
            // GorillaParent.vrrigs no longer exists in the current game version;
            // delegate to the project's rig lookup (GorillaGameManager + VRRigCache fallback).
            return iiMenu.Utilities.RigUtilities.GetVRRigFromPlayer(p);
        }
        public static string CleanPlayerName(string input, int length = 12)
        {
            if (input.Length > length) input = input.Substring(0, length - 1);
            return input;
        }
    }
}
