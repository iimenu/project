using HarmonyLib;
using GorillaTagScripts;
using UnityEngine;

namespace iiMenu.Patches.Menu
{
    [HarmonyPatch(typeof(GorillaAmbushManager), nameof(GorillaAmbushManager.UpdatePlayerAppearance))]
    public class GhostESPPatch
    {
        public static bool enabled;

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
                var scryingPlane = Traverse.Create(__instance).Field("scryingPlane").GetValue<MeshRenderer>();
                if (scryingPlane != null)
                {
                    scryingPlane.enabled = true;
                }
                var scryingPlane3p = Traverse.Create(__instance).Field("scryingPlane3p").GetValue<MeshRenderer>();
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
