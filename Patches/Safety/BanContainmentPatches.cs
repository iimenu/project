/*
 * ii Reborn
 * Portions Copyright (C) 2025–2026 Goldentrophy Software
 * Licensed under GNU GPL v3.0-or-later — see LICENSE and NOTICE.
 * This file is part of a derivative work; see NOTICE for attribution
 * and modification history. Do not remove this notice.
 */

using Backtrace.Unity;
using Backtrace.Unity.Model;
using GorillaNetworking;
using HarmonyLib;
using iiMenu.Managers;
using PlayFab;
using UnityEngine;
using static iiMenu.Patches.PatchHandler;

namespace iiMenu.Patches.Safety
{
    public class BanContainmentPatches
    {
        
        public static bool ContainmentEnabled = true;

        [SecurityPatch]
        [HarmonyPatch(typeof(Gorillanalytics), nameof(Gorillanalytics.UploadGorillanalytics))]
        public class NoGorillanalyticsUpload
        {
            private static bool Prefix() => false;
        }

        [SecurityPatch]
        [HarmonyPatch(typeof(GorillaServer), nameof(GorillaServer.UploadGorillanalytics))]
        public class NoGorillanalyticsCloudScript
        {
            private static bool Prefix() => false;
        }

        [SecurityPatch]
        [HarmonyPatch(typeof(BacktraceManager), nameof(BacktraceManager.Awake))]
        public class NoBacktraceReports
        {
            private static void Postfix(BacktraceManager __instance)
            {
                try
                {
                    __instance.backtraceSampleRate = 0.0;
                    BacktraceClient client = __instance.GetComponent<BacktraceClient>();
                    if (client != null)
                        client.BeforeSend = (BacktraceData data) => null;
                }
                catch { }
            }
        }

        [HarmonyPatch(typeof(GorillaVRConstraint), nameof(GorillaVRConstraint.Tick))]
        public class NoVRConstraintLatch
        {
            private static bool Prefix() =>
                !ContainmentEnabled;
        }
    }
}
