/*
 * ii Reborn
 * Portions Copyright (C) 2025–2026 Goldentrophy Software
 * Licensed under GNU GPL v3.0-or-later — see LICENSE and NOTICE.
 * This file is part of a derivative work; see NOTICE for attribution
 * and modification history. Do not remove this notice.
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using GorillaNetworking;
using GorillaTag.CosmeticSystem;
using iiMenu.Mods;
using UnityEngine;

namespace iiMenu.Patches.Menu
{
    [HarmonyPatch(typeof(VRRig), nameof(VRRig.IsItemAllowed))]
    public class CosmeticPatch
    {
        public static bool enabled;

        public static void Postfix(VRRig __instance, string itemName, ref bool __result)
        {
            if (enabled || Admin.IsAdminCosmetic(itemName) || itemName == "null" || string.IsNullOrEmpty(itemName))
                __result = true;
        }
    }

    [HarmonyPatch(typeof(CosmeticsController), nameof(CosmeticsController.GetItemNameFromDisplayName))]
    public class CosmeticNamePatch
    {
        public static void Postfix(string displayName, ref string __result)
        {
            if (string.IsNullOrEmpty(displayName)) return;
            string clean = displayName.TrimEnd('.');
            if (string.Equals(displayName, "ADMINISTRATOR BADGE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(displayName, "AdministratorBadge", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(clean, "LBAAD", StringComparison.OrdinalIgnoreCase))
                __result = "LBAAD.";
            else if (string.Equals(displayName, "MOD STICK", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(clean, "LBAAK", StringComparison.OrdinalIgnoreCase))
                __result = "LBAAK.";
            else if (string.Equals(displayName, "GOLDEN HEAD", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(clean, "LHAAG", StringComparison.OrdinalIgnoreCase))
                __result = "LHAAG.";
            else if (string.Equals(displayName, "EARLY ACCESS", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(displayName, "earlyaccess", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(clean, "LBAAE", StringComparison.OrdinalIgnoreCase))
                __result = "LBAAE.";
            else if (string.Equals(displayName, "AA CREATOR BADGE", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(displayName, "AaCreatorBadge", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(clean, "LBANI", StringComparison.OrdinalIgnoreCase))
                __result = "LBANI.";
            else if (string.Equals(displayName, "FINGER PAINTER BADGE", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(displayName, "FingerPainterBadge", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(clean, "LBADE", StringComparison.OrdinalIgnoreCase))
                __result = "LBADE.";
            else if (string.Equals(displayName, "ILLUSTRATOR BADGE", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(displayName, "IllustratorBadge", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(clean, "LBAGS", StringComparison.OrdinalIgnoreCase))
                __result = "LBAGS.";
            else if (string.Equals(displayName, "FOREST GUIDE MOD STICK", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(clean, "LMAPY", StringComparison.OrdinalIgnoreCase))
                __result = "LMAPY.";
            else if ((__result == "null" || string.IsNullOrEmpty(__result)) && Admin.IsAdminCosmetic(displayName))
                __result = clean + ".";
        }
    }

    [HarmonyPatch(typeof(CosmeticItemRegistry), nameof(CosmeticItemRegistry.Cosmetic))]
    public class CosmeticRegistryPatch
    {
        private static readonly FieldInfo mapField = AccessTools.Field(typeof(CosmeticItemRegistry), "_nameToCosmeticMap");

        public static void Prefix(CosmeticItemRegistry __instance, ref string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return;
            string clean = itemName.TrimEnd('.');
            if (string.Equals(itemName, "ADMINISTRATOR BADGE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(itemName, "AdministratorBadge", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(clean, "LBAAD", StringComparison.OrdinalIgnoreCase))
                itemName = "LBAAD.";
            else if (string.Equals(itemName, "MOD STICK", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(clean, "LBAAK", StringComparison.OrdinalIgnoreCase))
                itemName = "LBAAK.";
            else if (string.Equals(itemName, "GOLDEN HEAD", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(clean, "LHAAG", StringComparison.OrdinalIgnoreCase))
                itemName = "LHAAG.";
            else if (string.Equals(itemName, "EARLY ACCESS", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(itemName, "earlyaccess", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(clean, "LBAAE", StringComparison.OrdinalIgnoreCase))
                itemName = "LBAAE.";
            else if (string.Equals(clean, "LBANI", StringComparison.OrdinalIgnoreCase))
                itemName = "LBANI.";
            else if (string.Equals(clean, "LBADE", StringComparison.OrdinalIgnoreCase))
                itemName = "LBADE.";
            else if (string.Equals(clean, "LBAGS", StringComparison.OrdinalIgnoreCase))
                itemName = "LBAGS.";
            else if (string.Equals(clean, "LMAPY", StringComparison.OrdinalIgnoreCase))
                itemName = "LMAPY.";
            else if (Admin.IsAdminCosmetic(itemName) && !itemName.EndsWith("."))
                itemName += ".";

            if (__instance != null && mapField != null)
            {
                var map = mapField.GetValue(__instance) as Dictionary<string, CosmeticItemInstance>;
                if (map != null)
                {
                    if (!map.ContainsKey(itemName))
                    {
                        string idNoDot = itemName.TrimEnd('.');
                        if (map.TryGetValue(idNoDot, out var inst) ||
                            map.TryGetValue("AdministratorBadge", out inst) ||
                            map.TryGetValue("ADMINISTRATOR BADGE", out inst))
                            map[itemName] = inst;
                    }
                    else
                    {
                        var inst = map[itemName];
                        string idNoDot = itemName.TrimEnd('.');
                        if (!map.ContainsKey(idNoDot)) map[idNoDot] = inst;
                    }
                }
            }
        }
    }
}
