/*
 * ii Reborn
 * Portions Copyright (C) 2025–2026 Goldentrophy Software
 * Licensed under GNU GPL v3.0-or-later — see LICENSE and NOTICE.
 * This file is part of a derivative work; see NOTICE for attribution
 * and modification history. Do not remove this notice.
 */

using GorillaNetworking;
using HarmonyLib;
using iiMenu.Mods;

namespace iiMenu.Patches.Menu
{
    [HarmonyPatch(typeof(ZoneManagement), nameof(ZoneManagement.SetActiveZones))]
    public class ZonePatch
    {
        public static bool enabled;

        static bool Prefix(ref GTZone[] zones)
        {
            if (!enabled || zones == null || zones.Length <= 1)
                return true;

            GTZone singleZone = zones[zones.Length - 1];
            zones = new GTZone[] { singleZone };
            return true;
        }
    }

    [HarmonyPatch(typeof(ZoneManagement), nameof(ZoneManagement.AddSceneToForceStayLoaded))]
    public class ForceStayLoadedPatch
    {
        static bool Prefix(string sceneName)
        {
            if (ZonePatch.enabled && sceneName == "City")
                return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(GorillaNetworkJoinTrigger), nameof(GorillaNetworkJoinTrigger.OnBoxTriggered))]
    public class NetworkTriggerOptimizePatch
    {
        static void Postfix(GorillaNetworkJoinTrigger __instance)
        {
            if (ZonePatch.enabled && __instance != null)
                Important.OnNetworkTrigger(__instance);
        }
    }
}
