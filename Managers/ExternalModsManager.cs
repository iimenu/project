/*
 * ii Reborn
 * Portions Copyright (C) 2025–2026 Goldentrophy Software
 * Licensed under GNU GPL v3.0-or-later — see LICENSE and NOTICE.
 * This file is part of a derivative work; see NOTICE for attribution
 * and modification history. Do not remove this notice.
 */
using iiMenu.Classes.Menu;
using iiMenu.Menu;
using iiMenu.Mods;
using iiMenu.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine.Networking;

namespace iiMenu.Managers
{
    public static class ExternalModsManager
    {
        private static string PluginsFolder => FileUtilities.GetGamePath() + "/BepInEx/plugins";

        public static void EnterExternalMods()
        {
            RefreshExternalModsButtons();
            Buttons.CurrentCategoryName = "External Mods";
        }

        public static void RefreshExternalModsButtons()
        {
            int cat = Buttons.GetCategory("External Mods");
            if (cat < 0) return;

            List<ButtonInfo> list = new List<ButtonInfo>();
            list.Add(new ButtonInfo { buttonText = "Exit External Mods", method = () => Buttons.CurrentCategoryName = "Main", isTogglable = false, toolTip = "Back to main." });
            list.Add(new ButtonInfo { buttonText = "Restart Gorilla Tag", method = () => Important.RestartGame(), isTogglable = false, toolTip = "Restarts Gorilla Tag so newly installed mods load. Required after installing." });

            List<TelemetryClient.ModEntry> mods = TelemetryClient.VerifiedMods;

            if (mods.Count == 0)
            {
                list.Add(new ButtonInfo { buttonText = "No Mods Available", label = true, isTogglable = false, toolTip = "No mods are available right now." });
            }
            else
            {
                foreach (TelemetryClient.ModEntry mod in mods)
                {
                    TelemetryClient.ModEntry captured = mod;
                    string installed = IsInstalled(captured.File) ? "<color=green>INSTALLED</color>" : "<color=red>NOT INSTALLED</color>";
                    list.Add(new ButtonInfo
                    {
                        buttonText = $"Install {captured.Name}",
                        overlapText = $"{captured.Name} <color=grey>[</color>{installed}<color=grey>]</color>",
                        method = () => DownloadPinned(captured),
                        isTogglable = false,
                        toolTip = "Downloads and installs this mod. Restart Gorilla Tag after."
                    });
                }
            }

            list.Add(new ButtonInfo { buttonText = "Open Plugins Folder", method = () => System.Diagnostics.Process.Start(PluginsFolder), isTogglable = false, toolTip = "Opens BepInEx/plugins in Explorer." });
            list.Add(new ButtonInfo { buttonText = "Refresh List", method = () => RefreshExternalModsButtons(), isTogglable = false, toolTip = "Refreshes installed status." });

            Buttons.buttons[cat] = list.ToArray();
        }

        public static bool IsInstalled(string file)
        {
            try
            {
                if (!Directory.Exists(PluginsFolder))
                    return false;
                return File.Exists(Path.Combine(PluginsFolder, file));
            }
            catch { return false; }
        }

        public static void DownloadPinned(TelemetryClient.ModEntry mod)
        {
            if (CoroutineManager.instance == null)
            {
                NotificationManager.SendNotification("<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> CoroutineManager not ready.");
                return;
            }
            CoroutineManager.instance.StartCoroutine(DownloadPinnedRoutine(mod));
        }

        private static IEnumerator DownloadPinnedRoutine(TelemetryClient.ModEntry mod)
        {
            string url = $"https://github.com/{mod.Repo}/releases/download/{mod.Tag}/{mod.File}";

            NotificationManager.SendNotification($"<color=grey>[</color><color=cyan>EXTERNAL</color><color=grey>]</color> Downloading {mod.Name} {mod.Tag}...");

            byte[] data = null;
            using (UnityWebRequest dl = UnityWebRequest.Get(url))
            {
                dl.SetRequestHeader("User-Agent", "ii-Reborn");
                dl.downloadHandler = new DownloadHandlerBuffer();
                dl.timeout = 60;
                yield return dl.SendWebRequest();

                if (dl.result != UnityWebRequest.Result.Success)
                {
                    NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Download failed: {dl.error}");
                    LogManager.LogError($"ExternalMods pinned dl {url} -> {dl.error}");
                    yield break;
                }
                data = dl.downloadHandler.data;
            }

            if (data == null || data.Length < 1024)
            {
                NotificationManager.SendNotification("<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Downloaded file too small/corrupt.");
                yield break;
            }

            string got;
            using (SHA256 sha = SHA256.Create())
                got = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();

            if (got != mod.Sha256.ToLowerInvariant())
            {
                NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> {mod.Name} did not pass verification, nothing was installed.");
                LogManager.LogError($"ExternalMods hash mismatch {url} got {got} expected {mod.Sha256}");
                yield break;
            }

            try
            {
                if (!Directory.Exists(PluginsFolder))
                    Directory.CreateDirectory(PluginsFolder);

                string dest = Path.Combine(PluginsFolder, mod.File);
                if (File.Exists(dest))
                {
                    try { File.Delete(dest); } catch { }
                }
                File.WriteAllBytes(dest, data);
                NotificationManager.SendNotification($"<color=grey>[</color><color=green>SUCCESS</color><color=grey>]</color> Installed {mod.Name} {mod.Tag}. Restart Gorilla Tag to load.", 7000);
                LogManager.Log($"ExternalMods verified+installed {mod.Name} {mod.Tag} -> {dest} ({data.Length} bytes)");
                RefreshExternalModsButtons();
            }
            catch (Exception e)
            {
                NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Could not save {mod.File}: {e.Message}");
                LogManager.LogError($"ExternalMods save failed: {e}");
            }
        }
    }
}
