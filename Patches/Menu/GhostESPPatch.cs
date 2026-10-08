using HarmonyLib;
using GorillaTagScripts;
using UnityEngine;

namespace iiMenu.Patches.Menu
{
    [HarmonyPatch(typeof(GorillaAmbushManager), nameof(GorillaAmbushManager.UpdatePlayerAppearance))]
    public class GhostESPPatch
    {
        public static bool enabled;

        private static readonly System.Reflection.FieldInfo fScryingPlane = AccessTools.Field(typeof(GorillaAmbushManager), "scryingPlane");
        private static readonly System.Reflection.FieldInfo fScryingPlane3p = AccessTools.Field(typeof(GorillaAmbushManager), "scryingPlane3p");

        public static void Enable()
        {
            enabled = true;
            if (GorillaTagger.Instance != null && GorillaTagger.Instance.offlineVRRig != null && GorillaGameManager.instance != null)
            {
                GorillaGameManager.instance.UpdatePlayerAppearance(GorillaTagger.Instance.offlineVRRig);
            }
        }

        public static void Disable()
        {
            enabled = false;
            if (GorillaTagger.Instance != null && GorillaTagger.Instance.offlineVRRig != null && GorillaGameManager.instance != null)
            {
                GorillaGameManager.instance.UpdatePlayerAppearance(GorillaTagger.Instance.offlineVRRig);
            }
        }

        private static void Postfix(GorillaAmbushManager __instance, VRRig rig)
        {
            if (enabled && __instance.isGhostTag && rig.isOfflineVRRig)
            {
                var scryingPlane = (MeshRenderer)fScryingPlane.GetValue(__instance);
                if (scryingPlane != null)
                {
                    scryingPlane.enabled = true;
                }
                var scryingPlane3p = (MeshRenderer)fScryingPlane3p.GetValue(__instance);
                if (scryingPlane3p != null)
                {
                    scryingPlane3p.enabled = true;
                }
            }
        }
    }

    [HarmonyPatch(typeof(VRRig), nameof(VRRig.SetInvisibleToLocalPlayer))]
    public class GhostNameESPPatch
    {
        private static void Prefix(ref bool invisible)
        {
            if (GhostESPPatch.enabled && invisible)
            {
                invisible = false;
            }
        }
    }
}
