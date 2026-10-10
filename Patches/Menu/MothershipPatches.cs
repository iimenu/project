/*
 * ii Reborn
 * Portions Copyright (C) 2025–2026 Goldentrophy Software
 * Licensed under GNU GPL v3.0-or-later — see LICENSE and NOTICE.
 * This file is part of a derivative work; see NOTICE for attribution
 * and modification history. Do not remove this notice.
 */

using HarmonyLib;

namespace iiMenu.Patches.Menu
{
    public class MothershipPatches
    {
        public static bool enabled;

        [HarmonyPatch(typeof(GorillaTelemetry), nameof(GorillaTelemetry.EnqueueTelemetryEvent))]
        public class EnqueueTelemetryEvent
        {
            public static bool Prefix() => !enabled;
        }

        [HarmonyPatch(typeof(GorillaTelemetry), nameof(GorillaTelemetry.FlushMothershipTelemetry))]
        public class FlushMothershipTelemetry
        {
            public static bool Prefix() => !enabled;
        }

        [HarmonyPatch(typeof(GorillaTelemetry), nameof(GorillaTelemetry.EnqueueZoneEvent))]
        public class EnqueueZoneEvent
        {
            public static bool Prefix() => !enabled;
        }

        [HarmonyPatch(typeof(GorillaTelemetry), nameof(GorillaTelemetry.PostGameModeEvent))]
        public class PostGameModeEvent
        {
            public static bool Prefix() => !enabled;
        }
    }
}
