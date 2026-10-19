using HarmonyLib;
using iiMenu.Managers;
using iiMenu.Menu;
using iiMenu.Utilities;
using UnityEngine;

namespace iiMenu.Patches.Menu
{
    public class IKPatch
    {
        public static readonly Vector3 DefaultLeftElbowDir = new Vector3(-0.35f, -0.85f, -0.4f).normalized;
        public static readonly Vector3 DefaultRightElbowDir = new Vector3(0.35f, -0.85f, -0.4f).normalized;

        public static bool HasFrozenHands;
        public static Vector3 FrozenLeftHandWorldPos;
        public static Quaternion FrozenLeftHandWorldRot = Quaternion.identity;
        public static Vector3 FrozenRightHandWorldPos;
        public static Quaternion FrozenRightHandWorldRot = Quaternion.identity;
        public static Vector3 FrozenLeftHandLocalPos;
        public static Vector3 FrozenRightHandLocalPos;

        [HarmonyPatch(typeof(GorillaIK), nameof(GorillaIK.GetShoulderLocalTargetPos_Left))]
        public class LeftTargetPatch
        {
            public static bool Prefix(GorillaIK __instance, bool updatedIK, ref Vector3 __result)
            {
                if (__instance == null) return true;
                VRRig rig = __instance.myRig ?? __instance.GetComponent<VRRig>();
                if (rig == null) return true;
                if (rig == VRRig.LocalRig && (!VRRig.LocalRig.enabled || Main.ghostException))
                {
                    if (HasFrozenHands && __instance.leftUpperArm != null && __instance.leftUpperArm.parent != null)
                    {
                        __result = __instance.leftUpperArm.parent.InverseTransformPoint(FrozenLeftHandWorldPos);
                        return false;
                    }
                    if (HasFrozenHands)
                    {
                        __result = FrozenLeftHandLocalPos;
                        return false;
                    }
                }
                else if (Main.GhostRig != null && rig == Main.GhostRig)
                {
                    (Vector3 pos, _, _, _, _) = ControllerUtilities.GetTrueLeftHand();
                    if (__instance.leftUpperArm != null && __instance.leftUpperArm.parent != null)
                    {
                        __result = __instance.leftUpperArm.parent.InverseTransformPoint(pos);
                        return false;
                    }
                    __result = pos;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(GorillaIK), nameof(GorillaIK.GetShoulderLocalTargetPos_Right))]
        public class RightTargetPatch
        {
            public static bool Prefix(GorillaIK __instance, bool updatedIK, ref Vector3 __result)
            {
                if (__instance == null) return true;
                VRRig rig = __instance.myRig ?? __instance.GetComponent<VRRig>();
                if (rig == null) return true;
                if (rig == VRRig.LocalRig && (!VRRig.LocalRig.enabled || Main.ghostException))
                {
                    if (HasFrozenHands && __instance.rightUpperArm != null && __instance.rightUpperArm.parent != null)
                    {
                        __result = __instance.rightUpperArm.parent.InverseTransformPoint(FrozenRightHandWorldPos);
                        return false;
                    }
                    if (HasFrozenHands)
                    {
                        __result = FrozenRightHandLocalPos;
                        return false;
                    }
                }
                else if (Main.GhostRig != null && rig == Main.GhostRig)
                {
                    (Vector3 pos, _, _, _, _) = ControllerUtilities.GetTrueRightHand();
                    if (__instance.rightUpperArm != null && __instance.rightUpperArm.parent != null)
                    {
                        __result = __instance.rightUpperArm.parent.InverseTransformPoint(pos);
                        return false;
                    }
                    __result = pos;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(GorillaIK), nameof(GorillaIK.ResetIKData))]
        public class ResetIKDataPatch
        {
            public static void Postfix(GorillaIK __instance)
            {
                if (__instance == null) return;
                VRRig rig = __instance.myRig ?? __instance.GetComponent<VRRig>();
                if (rig == null) return;
                if ((rig == VRRig.LocalRig && (!VRRig.LocalRig.enabled || Main.ghostException)) || (Main.GhostRig != null && rig == Main.GhostRig))
                {
                    __instance.leftElbowDirection = DefaultLeftElbowDir;
                    __instance.lerpLeftElbowDirection = DefaultLeftElbowDir;
                    __instance.rightElbowDirection = DefaultRightElbowDir;
                    __instance.lerpRightElbowDirection = DefaultRightElbowDir;
                }
            }
        }

        [HarmonyPatch(typeof(GorillaIKMgr), nameof(GorillaIKMgr.LateUpdate))]
        public class LateUpdatePatch
        {
            public static void Prefix(GorillaIKMgr __instance)
            {
                if (VRRig.LocalRig != null)
                {
                    if (VRRig.LocalRig.enabled && !Main.ghostException)
                    {
                        if (VRRig.LocalRig.leftHandTransform != null && VRRig.LocalRig.leftHandTransform.localPosition.sqrMagnitude > 0.01f)
                        {
                            FrozenLeftHandWorldPos = VRRig.LocalRig.leftHandTransform.position;
                            FrozenLeftHandWorldRot = VRRig.LocalRig.leftHandTransform.rotation;
                            FrozenLeftHandLocalPos = VRRig.LocalRig.leftHandTransform.localPosition;
                            HasFrozenHands = true;
                        }
                        if (VRRig.LocalRig.rightHandTransform != null && VRRig.LocalRig.rightHandTransform.localPosition.sqrMagnitude > 0.01f)
                        {
                            FrozenRightHandWorldPos = VRRig.LocalRig.rightHandTransform.position;
                            FrozenRightHandWorldRot = VRRig.LocalRig.rightHandTransform.rotation;
                            FrozenRightHandLocalPos = VRRig.LocalRig.rightHandTransform.localPosition;
                            HasFrozenHands = true;
                        }
                    }
                    else
                    {
                        GorillaIK localIK = VRRig.LocalRig.GetComponent<GorillaIK>();
                        if (localIK != null)
                        {
                            if (localIK.leftElbowDirection == Vector3.zero)
                            {
                                localIK.leftElbowDirection = DefaultLeftElbowDir;
                                localIK.lerpLeftElbowDirection = DefaultLeftElbowDir;
                            }
                            if (localIK.rightElbowDirection == Vector3.zero)
                            {
                                localIK.rightElbowDirection = DefaultRightElbowDir;
                                localIK.lerpRightElbowDirection = DefaultRightElbowDir;
                            }
                        }
                    }
                }

                if (Main.GhostRig != null && Main.GhostRig.gameObject.activeSelf)
                {
                    GorillaIK ghostIK = Main.GhostRig.GetComponent<GorillaIK>();
                    if (ghostIK != null)
                    {
                        (Vector3 lPos, _, _, _, _) = ControllerUtilities.GetTrueLeftHand();
                        (Vector3 rPos, _, _, _, _) = ControllerUtilities.GetTrueRightHand();
                        ghostIK.OverrideTargetPos(true, lPos);
                        ghostIK.OverrideTargetPos(false, rPos);
                        ghostIK.leftElbowDirection = DefaultLeftElbowDir;
                        ghostIK.rightElbowDirection = DefaultRightElbowDir;
                        ghostIK.lerpLeftElbowDirection = DefaultLeftElbowDir;
                        ghostIK.lerpRightElbowDirection = DefaultRightElbowDir;
                    }
                }
            }

            public static void Postfix(GorillaIKMgr __instance)
            {
                if (VRRig.LocalRig != null && (!VRRig.LocalRig.enabled || Main.ghostException) && HasFrozenHands && EmoteManager.Kyle == null)
                {
                    if (VRRig.LocalRig.leftHandTransform != null)
                    {
                        VRRig.LocalRig.leftHandTransform.position = FrozenLeftHandWorldPos;
                        VRRig.LocalRig.leftHandTransform.rotation = FrozenLeftHandWorldRot;
                    }
                    if (VRRig.LocalRig.rightHandTransform != null)
                    {
                        VRRig.LocalRig.rightHandTransform.position = FrozenRightHandWorldPos;
                        VRRig.LocalRig.rightHandTransform.rotation = FrozenRightHandWorldRot;
                    }
                    if (VRRig.LocalRig.leftHand != null && VRRig.LocalRig.leftHand.rigTarget != null)
                    {
                        VRRig.LocalRig.leftHand.rigTarget.position = FrozenLeftHandWorldPos;
                        VRRig.LocalRig.leftHand.rigTarget.rotation = FrozenLeftHandWorldRot;
                    }
                    if (VRRig.LocalRig.rightHand != null && VRRig.LocalRig.rightHand.rigTarget != null)
                    {
                        VRRig.LocalRig.rightHand.rigTarget.position = FrozenRightHandWorldPos;
                        VRRig.LocalRig.rightHand.rigTarget.rotation = FrozenRightHandWorldRot;
                    }
                }

                if (Main.GhostRig != null && Main.GhostRig.gameObject.activeSelf)
                {
                    (Vector3 lPos, Quaternion lRot, _, _, _) = ControllerUtilities.GetTrueLeftHand();
                    (Vector3 rPos, Quaternion rRot, _, _, _) = ControllerUtilities.GetTrueRightHand();

                    if (Main.GhostRig.leftHandTransform != null)
                    {
                        Main.GhostRig.leftHandTransform.position = lPos;
                        Main.GhostRig.leftHandTransform.rotation = lRot;
                    }
                    if (Main.GhostRig.rightHandTransform != null)
                    {
                        Main.GhostRig.rightHandTransform.position = rPos;
                        Main.GhostRig.rightHandTransform.rotation = rRot;
                    }
                    if (Main.GhostRig.leftHand != null && Main.GhostRig.leftHand.rigTarget != null)
                    {
                        Main.GhostRig.leftHand.rigTarget.position = lPos;
                        Main.GhostRig.leftHand.rigTarget.rotation = lRot;
                    }
                    if (Main.GhostRig.rightHand != null && Main.GhostRig.rightHand.rigTarget != null)
                    {
                        Main.GhostRig.rightHand.rigTarget.position = rPos;
                        Main.GhostRig.rightHand.rigTarget.rotation = rRot;
                    }
                }
            }
        }
    }
}
