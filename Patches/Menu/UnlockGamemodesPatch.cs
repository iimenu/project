using HarmonyLib;
using GorillaGameModes;
using System.Collections.Generic;

namespace iiMenu.Patches.Menu
{
    [HarmonyPatch(typeof(GameModeZoneMapping), nameof(GameModeZoneMapping.GetModesForZone))]
    public class UnlockGamemodesPatch
    {
        public static bool enabled;

        private static void Postfix(ref HashSet<GameModeType> __result)
        {
            if (enabled && __result != null)
            {
                var newSet = new HashSet<GameModeType>(__result);
                newSet.Add(GameModeType.HuntDown);
                newSet.Add(GameModeType.Paintbrawl);
                newSet.Add(GameModeType.Ambush);
                newSet.Add(GameModeType.FreezeTag);
                newSet.Add(GameModeType.Ghost);
                newSet.Add(GameModeType.Guardian);
                __result = newSet;
            }
        }
    }
}
