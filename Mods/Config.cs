using iiMenu.Classes.Menu;
using iiMenu.Managers;
using iiMenu.Menu;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Valve.Newtonsoft.Json;
using static iiMenu.Menu.Main;

namespace iiMenu.Mods
{
    public class CommunityConfigItem
    {
        public string name { get; set; }
        public string author { get; set; }
        public string description { get; set; }
        public string url { get; set; }
        public string content { get; set; }
    }

    public static class Config
    {
        public static string ConfigsFolder => Path.Combine(PluginInfo.BaseDirectory, "Configs");
        public static string ApiConfigFile => Path.Combine(ConfigsFolder, "community_api.txt");

        public static string CommunityConfigsApiUrl
        {
            get
            {
                if (File.Exists(ApiConfigFile))
                {
                    try
                    {
                        string custom = File.ReadAllText(ApiConfigFile).Trim();
                        if (!string.IsNullOrEmpty(custom) && custom.StartsWith("http"))
                            return custom;
                    }
                    catch { }
                }
                return $"{PluginInfo.ServerResourcePath}/CommunityConfigs/index.json";
            }
        }

        public static readonly List<CommunityConfigItem> fetchedConfigs = new List<CommunityConfigItem>();
        public static bool isFetching;

        public static void EnsureFolder()
        {
            if (!Directory.Exists(ConfigsFolder))
                Directory.CreateDirectory(ConfigsFolder);
        }

        public static void EnterConfigsTab() =>
            Buttons.CurrentCategoryName = "Configs";

        public static void UploadConfigToDiscord()
        {
            EnsureFolder();
            string text = Settings.SavePreferencesToText();
            GUIUtility.systemCopyBuffer = text;
            try
            {
                string fileName = $"Community_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                File.WriteAllText(Path.Combine(ConfigsFolder, fileName), text);
            }
            catch { }

            try
            {
                Process.Start(serverLink);
            }
            catch { }

            NotificationManager.SendNotification("<color=grey>[</color><color=green>DISCORD</color><color=grey>]</color> Config copied to clipboard! Paste it into the Discord configs channel.");
        }

        public static void EnterCommunityConfigs()
        {
            Buttons.CurrentCategoryName = "Community Configs";
            PopulateCommunityConfigsCategory();
        }

        public static void FetchCommunityConfigs(bool force = false)
        {
            if (isFetching && !force) return;
            CoroutineManager.instance.StartCoroutine(FetchCommunityConfigsCoroutine());
        }

        private static IEnumerator FetchCommunityConfigsCoroutine()
        {
            isFetching = true;
            PopulateCommunityConfigsCategory();

            string url = CommunityConfigsApiUrl;
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                try { req.SetRequestHeader("User-Agent", "Mozilla/5.0"); } catch { }
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success && req.downloadHandler != null)
                {
                    string text = req.downloadHandler.text;
                    List<CommunityConfigItem> parsed = ParseCommunityConfigs(text);
                    if (parsed != null && parsed.Count > 0)
                    {
                        fetchedConfigs.Clear();
                        fetchedConfigs.AddRange(parsed);
                        NotificationManager.SendNotification($"<color=grey>[</color><color=green>CONFIG</color><color=grey>]</color> Loaded {fetchedConfigs.Count} community config(s) from API.");
                    }
                }
                else
                {
                    NotificationManager.SendNotification("<color=grey>[</color><color=yellow>CONFIG</color><color=grey>]</color> API offline, showing default community configs.");
                }
            }

            isFetching = false;
            PopulateCommunityConfigsCategory();
            if (Buttons.CurrentCategoryName == "Community Configs")
                ReloadMenu();
        }

        private static List<CommunityConfigItem> ParseCommunityConfigs(string raw)
        {
            List<CommunityConfigItem> results = new List<CommunityConfigItem>();
            if (string.IsNullOrWhiteSpace(raw)) return results;

            try
            {
                var list = JsonConvert.DeserializeObject<List<CommunityConfigItem>>(raw);
                if (list != null && list.Count > 0)
                    return list;
            }
            catch { }

            try
            {
                var dict = JsonConvert.DeserializeObject<Dictionary<string, List<CommunityConfigItem>>>(raw);
                if (dict != null && dict.TryGetValue("configs", out var confList) && confList.Count > 0)
                    return confList;
            }
            catch { }

            try
            {
                string[] lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
                    string[] parts = trimmed.Contains("|") ? trimmed.Split('|') : trimmed.Split(';');
                    if (parts.Length >= 2)
                    {
                        results.Add(new CommunityConfigItem
                        {
                            name = parts[0].Trim(),
                            url = parts[1].Trim(),
                            author = parts.Length > 2 ? parts[2].Trim() : "Community",
                            description = parts.Length > 3 ? parts[3].Trim() : null
                        });
                    }
                }
            }
            catch { }

            return results;
        }

        public static void PopulateCommunityConfigsCategory()
        {
            int cat = Buttons.GetCategory("Community Configs");
            if (cat < 0) return;

            List<ButtonInfo> list = new List<ButtonInfo>
            {
                new ButtonInfo { buttonText = "Exit Community Configs", method = () => Buttons.CurrentCategoryName = "Configs", isTogglable = false, toolTip = "Returns you back to the configs menu." },
                new ButtonInfo { buttonText = "Upload Config to Discord", method = UploadConfigToDiscord, isTogglable = false, toolTip = "Copies your config to the clipboard and opens the Discord server." },
            };

            if (isFetching)
            {
                list.Add(new ButtonInfo { buttonText = "Fetching Configs from API...", label = true, isTogglable = false });
            }
            else if (fetchedConfigs.Count > 0)
            {
                foreach (CommunityConfigItem item in fetchedConfigs)
                {
                    CommunityConfigItem captured = item;
                    string label = captured.name;
                    string authorTag = !string.IsNullOrEmpty(captured.author) ? $" <color=grey>[</color><color=green>{captured.author}</color><color=grey>]</color>" : "";
                    list.Add(new ButtonInfo
                    {
                        buttonText = label,
                        overlapText = label + authorTag,
                        method = () => DownloadAndApplyCommunityConfig(captured),
                        isTogglable = false,
                        toolTip = captured.description ?? $"Download and apply {captured.name} from API."
                    });
                }
            }
                list.Add(new ButtonInfo { buttonText = "No Community Configs Loaded", label = true, isTogglable = false, toolTip = "Use Refresh from API above to fetch community configs." });

            Buttons.buttons[cat] = list.ToArray();
        }

        public static void DownloadAndApplyCommunityConfig(CommunityConfigItem item)
        {
            if (item == null) return;
            if (!string.IsNullOrEmpty(item.content))
            {
                ApplyConfigContent(item.name, item.content);
                return;
            }

            if (!string.IsNullOrEmpty(item.url))
            {
                CoroutineManager.instance.StartCoroutine(DownloadConfigCoroutine(item));
                return;
            }

            NotificationManager.SendNotification("<color=grey>[</color><color=red>CONFIG</color><color=grey>]</color> Invalid config URL.");
        }

        private static IEnumerator DownloadConfigCoroutine(CommunityConfigItem item)
        {
            NotificationManager.SendNotification($"<color=grey>[</color><color=purple>CONFIG</color><color=grey>]</color> Downloading {item.name}...");
            using (UnityWebRequest req = UnityWebRequest.Get(item.url))
            {
                req.timeout = 10;
                try { req.SetRequestHeader("User-Agent", "Mozilla/5.0"); } catch { }
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success && req.downloadHandler != null)
                {
                    string text = req.downloadHandler.text;
                    ApplyConfigContent(item.name, text);
                }
                else
                {
                    NotificationManager.SendNotification($"<color=grey>[</color><color=red>CONFIG</color><color=grey>]</color> Failed to download {item.name}.");
                }
            }
        }

        private static void ApplyConfigContent(string name, string content)
        {
            EnsureFolder();
            try
            {
                string safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
                string filePath = Path.Combine(ConfigsFolder, $"{safeName}.txt");
                File.WriteAllText(filePath, content);
                Settings.LoadPreferencesFromText(content);
                VRRig.LocalRig.PlayHandTapLocal(50, rightHand, 0.4f);
                NotificationManager.SendNotification($"<color=grey>[</color><color=green>CONFIG</color><color=grey>]</color> Downloaded & applied {name}!");
            }
            catch (Exception ex)
            {
                NotificationManager.SendNotification($"<color=grey>[</color><color=red>CONFIG</color><color=grey>]</color> Error applying config: {ex.Message}");
            }
        }

        public static void ApplyCommunityPreset(string name, string[] mods)
        {
            Settings.Panic();
            if (mods != null)
            {
                foreach (string mod in mods)
                    Toggle(mod);
            }

            EnsureFolder();
            try
            {
                string text = Settings.SavePreferencesToText();
                File.WriteAllText(Path.Combine(ConfigsFolder, $"{name.Replace(" ", "_")}.txt"), text);
            }
            catch { }

            NotificationManager.SendNotification($"<color=grey>[</color><color=green>CONFIG</color><color=grey>]</color> Applied {name} successfully.");
        }

        public static void EnterDownloadedConfigs()
        {
            int cat = Buttons.GetCategory("Downloaded Config Files");
            if (cat < 0) return;

            EnsureFolder();

            List<ButtonInfo> list = new List<ButtonInfo>
            {
                new ButtonInfo { buttonText = "Exit Downloaded Configs", method = () => Buttons.CurrentCategoryName = "Configs", isTogglable = false, toolTip = "Returns you back to the configs menu." },
                new ButtonInfo { buttonText = "Open Configs Folder", method = () => Process.Start(ConfigsFolder), isTogglable = false, toolTip = "Opens the configs folder in Explorer." },
                new ButtonInfo { buttonText = "Refresh Configs", method = EnterDownloadedConfigs, isTogglable = false, toolTip = "Refreshes the downloaded configs list." }
            };

            string[] files = Directory.GetFiles(ConfigsFolder, "*.txt");
            if (files.Length == 0)
                list.Add(new ButtonInfo { buttonText = "No Configs Found", label = true, isTogglable = false, toolTip = "No saved config files found in folder." });
            else
            {
                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    string capturedPath = file;
                    list.Add(new ButtonInfo
                    {
                        buttonText = $"Load {fileName}",
                        overlapText = fileName,
                        method = () => LoadConfigFile(capturedPath),
                        isTogglable = false,
                        toolTip = $"Loads {fileName}."
                    });
                }
            }

            Buttons.buttons[cat] = list.ToArray();
            Buttons.CurrentCategoryName = "Downloaded Config Files";
        }

        public static void LoadConfigFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    string text = File.ReadAllText(path);
                    Settings.LoadPreferencesFromText(text);
                    NotificationManager.SendNotification($"<color=grey>[</color><color=green>CONFIG</color><color=grey>]</color> Loaded {Path.GetFileName(path)}.");
                }
            }
            catch (Exception ex)
            {
                NotificationManager.SendNotification($"<color=grey>[</color><color=red>CONFIG</color><color=grey>]</color> Failed to load: {ex.Message}");
            }
        }

        public static void ImportConfigFile()
        {
            EnsureFolder();

            string clip = GUIUtility.systemCopyBuffer;
            if (!string.IsNullOrWhiteSpace(clip) && clip.Contains(";;"))
            {
                try
                {
                    string fileName = $"Imported_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                    File.WriteAllText(Path.Combine(ConfigsFolder, fileName), clip);
                    Settings.LoadPreferencesFromText(clip);
                    NotificationManager.SendNotification("<color=grey>[</color><color=green>CONFIG</color><color=grey>]</color> Imported config from clipboard.");
                    return;
                }
                catch { }
            }

            string importPath = Path.Combine(ConfigsFolder, "import.txt");
            if (File.Exists(importPath))
            {
                try
                {
                    string text = File.ReadAllText(importPath);
                    Settings.LoadPreferencesFromText(text);
                    NotificationManager.SendNotification("<color=grey>[</color><color=green>CONFIG</color><color=grey>]</color> Imported config from import.txt.");
                    return;
                }
                catch { }
            }

            try
            {
                Process.Start(ConfigsFolder);
            }
            catch { }

            NotificationManager.SendNotification("<color=grey>[</color><color=yellow>CONFIG</color><color=grey>]</color> Copy config to clipboard or add import.txt to Configs folder.", 5000);
        }

        public static void SaveConfig()
        {
            EnsureFolder();
            try
            {
                string text = Settings.SavePreferencesToText();
                string fileName = $"Config_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                File.WriteAllText(Path.Combine(ConfigsFolder, fileName), text);
                File.WriteAllText(Path.Combine(ConfigsFolder, "LastSavedConfig.txt"), text);
                NotificationManager.SendNotification($"<color=grey>[</color><color=green>CONFIG</color><color=grey>]</color> Saved config: {fileName}");
            }
            catch (Exception ex)
            {
                NotificationManager.SendNotification($"<color=grey>[</color><color=red>CONFIG</color><color=grey>]</color> Failed to save: {ex.Message}");
            }
        }

        public static void ClearAllConfig()
        {
            Settings.Panic();
            if (favorites != null)
            {
                favorites.Clear();
                favorites.Add("Exit Favorite Mods");
            }
            NotificationManager.SendNotification("<color=grey>[</color><color=red>CONFIG</color><color=grey>]</color> Cleared all active configurations.");
        }
    }
}
