/*
 * ii Reborn
 * Portions Copyright (C) 2025–2026 Goldentrophy Software
 * Licensed under GNU GPL v3.0-or-later — see LICENSE and NOTICE.
 * This file is part of a derivative work; see NOTICE for attribution
 * and modification history. Do not remove this notice.
 */

using HarmonyLib;
using iiMenu.Managers;
using Photon.Voice;
using Photon.Voice.Unity;

namespace iiMenu.Patches.Menu
{
    [HarmonyPatch(typeof(Recorder))]
    public class RecorderPatch
    {
        public static bool enabled = true;

        [HarmonyPatch(nameof(Recorder.CreateLocalVoiceAudioAndSource))]
        [HarmonyPrefix]
        public static void Prefix(Recorder __instance)
        {
            if (enabled)
            {
                __instance.SourceType = Recorder.InputSourceType.Factory;
                __instance.InputFactory = () => VoiceManager.Get();
            }
        }

        [HarmonyPatch(nameof(Recorder.CreateLocalVoiceAudioAndSource))]
        [HarmonyPostfix]
        public static void Postfix(Recorder __instance)
        {
            if (enabled)
                __instance.SourceType = Recorder.InputSourceType.Microphone;
        }
    }
}
