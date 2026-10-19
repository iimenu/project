/*
 * ii Reborn
 * Portions Copyright (C) 2025–2026 Goldentrophy Software
 * Licensed under GNU GPL v3.0-or-later — see LICENSE and NOTICE.
 * This file is part of a derivative work; see NOTICE for attribution
 * and modification history. Do not remove this notice.
 */

using HarmonyLib;
using iiMenu.Mods;
using Photon.Voice;
using Photon.Voice.Unity;
using System.Collections.Generic;

namespace iiMenu.Patches.Menu
{
    [HarmonyPatch(typeof(Speaker), nameof(Speaker.OnAudioFrame))]
    public class SpeakerPatch
    {
        public static bool enabled;
        public static Speaker targetSpeaker;
        public static FrameOut<float> frameOut;
        public static readonly List<float> SampleQueue = new List<float>();
        public static readonly object locked = new object();

        static void Postfix(Speaker __instance, FrameOut<float> frame)
        {
            if (!enabled || targetSpeaker == null || __instance != targetSpeaker)
                return;

            var src = frame?.Buf;
            if (src == null || src.Length == 0)
                return;

            frameOut = frame;
            lock (locked)
            {
                SampleQueue.AddRange(src);
                int maxSamples = 48000;
                try
                {
                    if (targetSpeaker.RemoteVoiceLink?.Info != null && targetSpeaker.RemoteVoiceLink.Info.SamplingRate > 0)
                        maxSamples = targetSpeaker.RemoteVoiceLink.Info.SamplingRate;
                }
                catch { }

                if (SampleQueue.Count > maxSamples)
                    SampleQueue.RemoveRange(0, SampleQueue.Count - maxSamples);
            }
        }
    }
}
