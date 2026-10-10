/*
 * ii Reborn
 * Portions Copyright (C) 2025–2026 Goldentrophy Software
 * Licensed under GNU GPL v3.0-or-later — see LICENSE and NOTICE.
 * This file is part of a derivative work; see NOTICE for attribution
 * and modification history. Do not remove this notice.
 */

using GorillaLocomotion.Gameplay;
using HarmonyLib;
using Photon.Pun;
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
    }
}
