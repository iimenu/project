/*
 * ii Reborn
 * Portions Copyright (C) 2025–2026 Goldentrophy Software
 * Licensed under GNU GPL v3.0-or-later — see LICENSE and NOTICE.
 * This file is part of a derivative work; see NOTICE for attribution
 * and modification history. Do not remove this notice.
 */

using ExitGames.Client.Photon;
using GorillaLocomotion;
using iiMenu.Classes.Menu;
using iiMenu.Extensions;
using iiMenu.Managers;
using iiMenu.Menu;
using iiMenu.Patches.Menu;
using Photon.Pun;
using Photon.Realtime;
using Photon.Voice.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Valve.Newtonsoft.Json.Linq;
using static iiMenu.Menu.Main;
using static iiMenu.Utilities.AssetUtilities;
using static iiMenu.Utilities.FileUtilities;
using Random = UnityEngine.Random;

namespace iiMenu.Mods
{
    public static class Sound
    {
        public static bool LoopAudio = false;
        public static bool OverlapAudio = false;
        public static int BindMode;
        public static string Subdirectory = "";

        public static float LocalVolume
        {
            get => SoundboardManager.LocalVolume;
            set { SoundboardManager.LocalVolume = Mathf.Clamp(value, 0f, 2f); SoundboardManager.ApplySettings(); }
        }
        public static float MicVolume
        {
            get => SoundboardManager.MicVolume;
            set { SoundboardManager.MicVolume = Mathf.Clamp(value, 0f, 2f); SoundboardManager.ApplySettings(); }
        }
        public static bool HighQuality
        {
            get => SoundboardManager.HighQuality;
            set { SoundboardManager.HighQuality = value; SoundboardManager.ApplySettings(); SoundboardManager.EnsureMicHighQuality(); }
        }

        public static void ChangeLocalVolume(bool positive = true)
        {
            float v = LocalVolume + (positive ? 0.1f : -0.1f);
            v = Mathf.Clamp(v, 0f, 2f);
            LocalVolume = v;
            try { Buttons.GetIndex("Soundboard Local Volume").overlapText = $"Soundboard Local Volume <color=grey>[</color><color=green>{Mathf.RoundToInt(v * 100)}%</color><color=grey>]</color>"; } catch { }
        }

        public static void ChangeMicVolume(bool positive = true)
        {
            float v = MicVolume + (positive ? 0.1f : -0.1f);
            v = Mathf.Clamp(v, 0f, 2f);
            MicVolume = v;
            try { Buttons.GetIndex("Soundboard Mic Volume").overlapText = $"Soundboard Mic Volume <color=grey>[</color><color=green>{Mathf.RoundToInt(v * 100)}%</color><color=grey>]</color>"; } catch { }
        }

        public static bool oldOst;
        private static AudioSource ostSource;
        private static string ostRoom;
        private static string currentOstZone = "";
        private static float ostMusicMuteScan;
        private static bool isLoadingOst;
        private static readonly List<AudioSource> mutedMusicSources = new List<AudioSource>();

        public static void EnableOldOst()
        {
            oldOst = true;
            ostRoom = null;
            currentOstZone = "";
            UpdateOldOst();
        }

        public static void DisableOldOst()
        {
            oldOst = false;
            currentOstZone = "";
            StopOldOst();
        }

        public static void UpdateOldOst()
        {
            if (!oldOst) return;

            try
            {
                string zone = GetCurrentZoneName();
                string room = NetworkSystem.Instance != null ? NetworkSystem.Instance.RoomName : "";

                if (zone != currentOstZone || room != ostRoom)
                {
                    currentOstZone = zone;
                    ostRoom = room;
                    StopOldOst();
                    CoroutineManager.instance.StartCoroutine(LoadOldOst(zone));
                }

                if (Time.time > ostMusicMuteScan)
                {
                    ostMusicMuteScan = Time.time + 0.5f;
                    MuteVanillaMusic();
                }
            }
            catch { }
        }

        private static string GetCurrentZoneName()
        {
            try
            {
                GTZone zone = Important.GetCurrentZone();
                if (zone != GTZone.none)
                {
                    string zn = zone.ToString();
                    return char.ToUpper(zn[0]) + zn.Substring(1);
                }
            }
            catch { }

            try
            {
                if (ZoneManagement.instance != null && ZoneManagement.instance.activeZones != null && ZoneManagement.instance.activeZones.Count > 0)
                {
                    string zn = ZoneManagement.instance.activeZones[ZoneManagement.instance.activeZones.Count - 1].ToString();
                    return char.ToUpper(zn[0]) + zn.Substring(1);
                }
            }
            catch { }

            return "Forest";
        }

        private static string FindOstFile(string zoneName)
        {
            if (string.IsNullOrEmpty(zoneName))
                return null;

            string clean = zoneName.ToLower().Trim();
            string cleanNoS = clean.TrimEnd('s');

            List<string> dirs = new List<string>();
            try
            {
                string gamePath = GetGamePath();
                if (!string.IsNullOrEmpty(gamePath))
                {
                    dirs.Add(Path.Combine(gamePath, PluginInfo.BaseDirectory));
                    dirs.Add(Path.Combine(gamePath, "iiReborn"));
                    dirs.Add(Path.Combine(gamePath, "BepInEx", "plugins", PluginInfo.BaseDirectory));
                    dirs.Add(Path.Combine(gamePath, "BepInEx", "plugins"));
                    dirs.Add(gamePath);

                    string parent = Directory.GetParent(gamePath)?.FullName;
                    if (!string.IsNullOrEmpty(parent))
                    {
                        dirs.Add(Path.Combine(parent, "Gorilla Tag", "iiReborn"));
                        dirs.Add(Path.Combine(parent, "GorillaTag", "iiReborn"));
                    }
                }
            }
            catch { }

            dirs.Add(PluginInfo.BaseDirectory);
            dirs.Add(Path.Combine("BepInEx", "plugins", PluginInfo.BaseDirectory));
            dirs.Add(Path.Combine("BepInEx", "plugins"));

            string[] candidates = new[]
            {
                $"OG{zoneName}OST.mp3",
                $"OG{clean}OST.mp3",
                $"OG{cleanNoS}OST.mp3",
                $"OG{clean}sOST.mp3",
                $"OG{char.ToUpper(clean[0]) + clean.Substring(1)}OST.mp3",
                $"OG{char.ToUpper(cleanNoS[0]) + cleanNoS.Substring(1)}OST.mp3",
                $"OG{char.ToUpper(cleanNoS[0]) + cleanNoS.Substring(1)}sOST.mp3",
                $"OG{zoneName}OST.ogg",
                $"OG{clean}OST.ogg",
                $"OG{cleanNoS}OST.ogg",
                $"OG{zoneName}OST.wav",
                $"OG{clean}OST.wav",
                $"OG{cleanNoS}OST.wav",
                $"{clean}.mp3"
            };

            foreach (string dir in dirs.Distinct())
            {
                if (!Directory.Exists(dir)) continue;

                foreach (string name in candidates)
                {
                    string p = Path.Combine(dir, name);
                    if (File.Exists(p)) return p;
                }

                try
                {
                    foreach (string file in Directory.GetFiles(dir))
                    {
                        string fName = Path.GetFileName(file).ToLower();
                        if ((fName.Contains(clean) || fName.Contains(cleanNoS)) &&
                            (fName.EndsWith(".mp3") || fName.EndsWith(".ogg") || fName.EndsWith(".wav")))
                            return file;
                    }
                }
                catch { }
            }

            return null;
        }

        private static Transform FindMapSpeaker()
        {
            try
            {
                foreach (AudioSource source in UnityEngine.Object.FindObjectsOfType<AudioSource>())
                {
                    if (source == null || source == ostSource || !source.gameObject.activeInHierarchy) continue;
                    if (source.GetComponent<VRRig>() != null || source.GetComponentInParent<VRRig>() != null) continue;

                    if (source.outputAudioMixerGroup != null && source.outputAudioMixerGroup.name.ToLower().Contains("music"))
                        return source.transform;
                }

                GorillaSpeakerLoudness gsl = UnityEngine.Object.FindObjectsOfType<GorillaSpeakerLoudness>()
                    .FirstOrDefault(s => s != null && s.GetComponent<VRRig>() == null && s.gameObject.activeInHierarchy && s.speaker != null);
                if (gsl != null)
                    return gsl.transform;

                foreach (GameObject go in UnityEngine.Object.FindObjectsOfType<GameObject>())
                {
                    if (go == null || !go.activeInHierarchy || go.GetComponent<VRRig>() != null) continue;
                    string n = go.name.ToLower();
                    if (n.Contains("speaker") || n.Contains("radio") || n.Contains("jukebox"))
                        return go.transform;
                }
            }
            catch { }
            return null;
        }

        private static IEnumerator LoadOldOst(string zone)
        {
            if (isLoadingOst)
                yield break;

            isLoadingOst = true;
            string path = FindOstFile(zone);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                NotificationManager.SendNotification($"<color=grey>[</color><color=purple>OLD OST</color><color=grey>]</color> Missing {zone} OST (place OG{zone}OST.mp3 in iiReborn)", 4000);
                isLoadingOst = false;
                yield break;
            }

            AudioType audioType = AudioType.MPEG;
            if (path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)) audioType = AudioType.OGGVORBIS;
            else if (path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) audioType = AudioType.WAV;

            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip("file://" + path, audioType))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    isLoadingOst = false;
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null)
                {
                    isLoadingOst = false;
                    yield break;
                }

                GameObject host = new GameObject("iiMenu OldOST");
                UnityEngine.Object.DontDestroyOnLoad(host);
                ostSource = host.AddComponent<AudioSource>();
                ostSource.clip = clip;
                ostSource.loop = true;
                ostSource.volume = 0.6f;

                Transform speakerTr = FindMapSpeaker();
                if (speakerTr != null)
                {
                    ostSource.transform.position = speakerTr.position;
                    ostSource.transform.SetParent(speakerTr, true);
                    ostSource.spatialBlend = 1f;
                    ostSource.minDistance = 4f;
                    ostSource.maxDistance = 55f;
                    ostSource.rolloffMode = AudioRolloffMode.Logarithmic;
                    ostSource.dopplerLevel = 0f;
                }
                else
                {
                    ostSource.spatialBlend = 0f;
                }

                ostSource.Play();
                NotificationManager.SendNotification($"<color=grey>[</color><color=purple>OLD OST</color><color=grey>]</color> Playing {zone} OST ({Path.GetFileName(path)})", 4000);
            }
            isLoadingOst = false;
        }

        private static void StopOldOst()
        {
            if (ostSource != null)
            {
                ostSource.Stop();
                UnityEngine.Object.Destroy(ostSource.gameObject);
                ostSource = null;
            }

            foreach (AudioSource source in mutedMusicSources)
            {
                if (source != null)
                    source.mute = false;
            }
            mutedMusicSources.Clear();
            ostMusicMuteScan = 0f;
        }

        private static void MuteVanillaMusic()
        {
            foreach (AudioSource source in UnityEngine.Object.FindObjectsOfType<AudioSource>())
            {
                if (source == null || source == ostSource || source.outputAudioMixerGroup == null) continue;
                if (!source.outputAudioMixerGroup.name.ToLower().Contains("music")) continue;

                if (!mutedMusicSources.Contains(source))
                {
                    mutedMusicSources.Add(source);
                    source.mute = true;
                }
            }
        }

        public static void LoadSoundboard(bool openCategory = true)
        {
            if (!Directory.Exists($"{PluginInfo.BaseDirectory}/Sounds" + Subdirectory))
                Directory.CreateDirectory($"{PluginInfo.BaseDirectory}/Sounds" + Subdirectory);

            List<string> enabledSounds = (from binfo in Buttons.buttons[Buttons.GetCategory("Soundboard")] where binfo.enabled select binfo.overlapText).ToList();
            List<ButtonInfo> soundButtons = new List<ButtonInfo>();
            bool atRoot = Subdirectory == "";

            if (!atRoot)
                soundButtons.Add(new ButtonInfo { buttonText = "Exit Parent Directory", overlapText = "Exit " + Subdirectory.Split("/")[^1], method = ExitParentDirectory, isTogglable = false, toolTip = "Returns you back to the last folder." });

            soundButtons.Add(new ButtonInfo { buttonText = "Exit Soundboard", method = () => Buttons.CurrentCategoryName = "Sound Mods", isTogglable = false, toolTip = "Returns you back to the sound mods." });

            if (atRoot)
            {
                soundButtons.Add(new ButtonInfo { buttonText = "MyInstants", method = LoadSoundLibrary, isTogglable = false, toolTip = "Browse the MyInstants library. One click downloads and plays." });
                soundButtons.Add(new ButtonInfo { buttonText = "Downloaded Files", method =() => OpenFolder("MyInstants"), isTogglable = false, toolTip = "Plays back the sounds you downloaded from MyInstants." });
            }
            else
            {
                soundButtons.Add(new ButtonInfo { buttonText = "Stop All Sounds", method = StopAllSounds, isTogglable = false, toolTip = "Stops all currently playing sounds." });
                soundButtons.Add(new ButtonInfo { buttonText = "Open Sound Folder", method = OpenSoundFolder, isTogglable = false, toolTip = "Opens your Sounds folder — drop custom mp3/wav/ogg here." });

            string[] folders = Directory.GetDirectories($"{PluginInfo.BaseDirectory}/Sounds" + Subdirectory);
            soundButtons.AddRange(from folder in folders
            let substringLength = ($"{PluginInfo.BaseDirectory}/Sounds" + Subdirectory + "/").Length
            let FolderName = folder.Replace("\\", "/")[substringLength..]
            select new ButtonInfo
            {
                buttonText = "SoundboardFolder" + FolderName.Hash(),
                overlapText = $"<sprite name=\"Folder\">  {FolderName}  ",
                method = () => OpenFolder(FolderName),
                isTogglable = false,
                toolTip = "Opens the " + FolderName + " folder."
            });

            string[] files = Directory.GetFiles($"{PluginInfo.BaseDirectory}/Sounds" + Subdirectory);
            if (!RecorderPatch.enabled || Buttons.GetIndex("Legacy Microphone").enabled)
                NotificationManager.SendNotification($"<color=grey>[</color><color=red>WARNING</color><color=grey>]</color> You are using the legacy microphone system. Modern soundboard features will not be implemented.");
            foreach (string file in files)
            {
                // These used to be hardcoded character offsets into the full path, sized
                // for the old "iisStupidMenu" base directory. BaseDirectory is "iiReborn"
                // now, five characters shorter, so the offsets cut into the filename: the
                // name lost its first five characters, and the path handed to the loader
                // came out as "s/song.mp3" instead of "Sounds/song.mp3". The file was
                // then never found, the clip was always null, and PlaySoundboardSound
                // returned silently, so the button reported itself enabled while nothing
                // played. The format was irrelevant; mp3 and wav both failed. Derive both
                // from the path itself instead of slicing at a fixed offset.
                string soundName = RemoveFileExtension(Path.GetFileName(file)).Replace("_", " ");
                string relativePath = $"Sounds{Subdirectory}/{Path.GetFileName(file)}";

                if (RecorderPatch.enabled)
                {
                    var buttonInfo = new ButtonInfo
                    {
                        buttonText = "SoundboardSound" + soundName.Hash(),
                        overlapText = soundName,
                        toolTip = "Instantly plays \"" + soundName + "\" locally + through mic (dual volume in Soundboard Settings)."
                    };
                    if (OverlapAudio)
                    {
                        buttonInfo.method = () => PlayAudio(relativePath);
                        buttonInfo.isTogglable = false;
                    }
                    else
                    {
                        buttonInfo.method = () => PlaySoundboardSound(relativePath, buttonInfo, LoopAudio, BindMode > 0);
                        buttonInfo.disableMethod = () => StopSoundboardSound(buttonInfo);
                    }

                    soundButtons.Add(buttonInfo);
                } else
                {
                    if (BindMode > 0)
                    {
                        bool enabled = enabledSounds.Contains(soundName);
                        soundButtons.Add(new ButtonInfo { buttonText = "SoundboardSound" + soundName.Hash(), overlapText = soundName, method = () => PrepareBindAudio(relativePath), disableMethod = StopAllSounds, enabled = enabled, toolTip = "Plays \"" + soundName + "\" through your microphone." });

                    }
                    else
                    {
                        if (LoopAudio)
                        {
                            bool enabled = enabledSounds.Contains(soundName);
                            soundButtons.Add(new ButtonInfo { buttonText = "SoundboardSound" + soundName.Hash(), overlapText = soundName, enableMethod = () => PlayAudio(relativePath), disableMethod = StopAllSounds, enabled = enabled, toolTip = "Plays \"" + soundName + "\" through your microphone." });
                        }
                        else
                            soundButtons.Add(new ButtonInfo { buttonText = "SoundboardSound" + soundName.Hash(), overlapText = soundName, method = () => PlayAudio(relativePath), isTogglable = false, toolTip = "Plays \"" + soundName + "\" through your microphone." });
                    }
                }


            }
            }

            Buttons.buttons[Buttons.GetCategory("Soundboard")] = soundButtons.ToArray();

            if (openCategory)
                Buttons.CurrentCategoryName = "Soundboard";
        }

        public static void ExitParentDirectory()
        {
            Subdirectory = RemoveLastDirectory(Subdirectory);
            LoadSoundboard();
        }

        public static void OpenFolder(string folder)
        {
            Subdirectory = "/" + folder;
            LoadSoundboard();
        }

        private const string MyInstantsCategory = "MyInstants";
        private static string lastMyInstantsQuery = "";

        public static void LoadSoundLibrary() =>
            LoadMyInstantsRecent();

        public static void LoadMyInstantsRecent()
        {
            ShowMyInstantsLoading("Loading Sounds...");
            try
            {
                if (CoroutineManager.instance == null)
                {
                    BuildMyInstantsButtons(null, "Recently Uploaded", "Could not start the request — rejoin a map and try again");
                    return;
                }

                CoroutineManager.instance.StartCoroutine(FetchMyInstantsCo(SoundboardManager.MyInstantsRecentUrl, "Recently Uploaded"));
            }
            catch { }
        }

        private static List<ButtonInfo> MyInstantsHeaderButtons() =>
            new List<ButtonInfo>
            {
                new ButtonInfo { buttonText = "Exit MyInstants", method = () => LoadSoundboard(), isTogglable = false, toolTip = "Returns you back to the soundboard." },
                new ButtonInfo { buttonText = "Search MyInstants", method = SearchMyInstantsPrompt, isTogglable = false, toolTip = "Search MyInstants by name." },
                new ButtonInfo { buttonText = "Recently Uploaded", method = LoadMyInstantsRecent, isTogglable = false, toolTip = "Show the newest MyInstants uploads." },
                new ButtonInfo { buttonText = "Server Library", method = LoadServerSoundLibrary, isTogglable = false, toolTip = "The original menu sound library, hosted on the menu's own server." }
            };

        private static void ShowMyInstantsLoading(string message)
        {
            int cat = Buttons.GetCategory(MyInstantsCategory);
            List<ButtonInfo> soundbuttons = MyInstantsHeaderButtons();
            soundbuttons.Add(new ButtonInfo { buttonText = message, label = true });

            Buttons.buttons[cat] = soundbuttons.ToArray();
            Buttons.CurrentCategoryName = MyInstantsCategory;
        }

        public static void SearchMyInstantsPrompt()
        {
            PromptText("What would you like to search MyInstants for?", () =>
            {
                string q = keyboardInput?.Trim() ?? "";

                if (!SoundboardManager.IsValidQuery(q))
                {
                    NotificationManager.SendNotification("<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Type at least 2 characters to search MyInstants.");
                    LoadMyInstantsRecent();
                    return;
                }

                lastMyInstantsQuery = q;
                ShowMyInstantsLoading($"Searching for {q}...");

                string url = SoundboardManager.MyInstantsSearchUrl + UnityWebRequest.EscapeURL(q);
                try { CoroutineManager.instance.StartCoroutine(FetchMyInstantsCo(url, $"Results for {q}")); } catch { }
            }, () => LoadMyInstantsRecent(), "Search", "Cancel");
        }

        public static void RetryMyInstants()
        {
            if (SoundboardManager.IsValidQuery(lastMyInstantsQuery))
            {
                ShowMyInstantsLoading($"Searching for {lastMyInstantsQuery}...");
                string url = SoundboardManager.MyInstantsSearchUrl + UnityWebRequest.EscapeURL(lastMyInstantsQuery);
                try { CoroutineManager.instance.StartCoroutine(FetchMyInstantsCo(url, $"Results for {lastMyInstantsQuery}")); } catch { }
                return;
            }

            lastMyInstantsQuery = "";
            LoadMyInstantsRecent();
        }

        private static bool myInstantsFetchActive;
        private static float myInstantsFetchStart;

        private static IEnumerator FetchMyInstantsCo(string url, string header)
        {
            List<SoundboardManager.MyInstantEntry> list = new List<SoundboardManager.MyInstantEntry>();
            string error = null;

            myInstantsFetchActive = true;
            myInstantsFetchStart = Time.unscaledTime;
            CoroutineManager.instance.StartCoroutine(MyInstantsWatchdog(header));

            yield return SoundboardManager.FetchEntries(url, entries => list = entries, reason => error = reason);

            myInstantsFetchActive = false;
            try { BuildMyInstantsButtons(list, header, error); }
            catch
            {
                BuildMyInstantsButtons(null, header, "Failed to process the MyInstants response");
            }
        }

        private static IEnumerator MyInstantsWatchdog(string header)
        {
            float deadline = myInstantsFetchStart + 25f;
            while (Time.unscaledTime < deadline && myInstantsFetchActive)
                yield return null;

            if (myInstantsFetchActive)
            {
                myInstantsFetchActive = false;
                BuildMyInstantsButtons(null, header, "Request timed out — check your connection and retry");
            }
        }

        private static void BuildMyInstantsButtons(List<SoundboardManager.MyInstantEntry> entries, string header, string error)
        {
            int cat = Buttons.GetCategory(MyInstantsCategory);
            List<ButtonInfo> soundbuttons = MyInstantsHeaderButtons();

            if (entries == null || entries.Count == 0)
            {
                soundbuttons.Add(new ButtonInfo { buttonText = "MyInstants Error", overlapText = string.IsNullOrEmpty(error) ? "No Results" : error, label = true });
                soundbuttons.Add(new ButtonInfo { buttonText = "Retry MyInstants", method = RetryMyInstants, isTogglable = false, toolTip = "Try the last MyInstants request again." });
                Buttons.buttons[cat] = soundbuttons.ToArray();
                Buttons.CurrentCategoryName = MyInstantsCategory;
                return;
            }

            soundbuttons.Add(new ButtonInfo { buttonText = header + " Label", overlapText = header + $" <color=grey>[{entries.Count}]</color>", label = true });

            int idx = 0;
            foreach (SoundboardManager.MyInstantEntry e in entries)
            {
                string capturedTitle = e.title;
                string capturedMp3 = e.mp3;
                string display = capturedTitle.Length > 28 ? capturedTitle.Substring(0, 28) + "…" : capturedTitle;
                idx++;

                soundbuttons.Add(new ButtonInfo
                {
                    buttonText = "MyInst" + capturedTitle.Hash() + "_" + idx,
                    overlapText = display,
                    method = () =>
                    {
                        try { CoroutineManager.instance.StartCoroutine(SoundboardManager.DownloadAndPlayMyInstants(capturedMp3, capturedTitle)); } catch { }
                    },
                    isTogglable = false,
                    toolTip = $"Download and play \"{capturedTitle}\". Also saved to Sounds/MyInstants so it shows up as a normal soundboard sound."
                });
            }

            Buttons.buttons[cat] = soundbuttons.ToArray();
            Buttons.CurrentCategoryName = MyInstantsCategory;
        }

        public static void LoadServerSoundLibrary()
        {
            List<ButtonInfo> soundbuttons;
            try
            {
                string library = GetHttp($"{PluginInfo.ServerResourcePath}/Audio/Mods/Fun/Soundboard/SoundLibrary.txt");
                string[] audios = AlphabetizeNoSkip(library.Split("\n"));
                soundbuttons = new List<ButtonInfo>
                {
                    new ButtonInfo { buttonText = "Exit MyInstants", method = () => LoadSoundboard(), isTogglable = false, toolTip = "Returns you back to the soundboard." },
                    new ButtonInfo { buttonText = "Back to MyInstants", method = LoadMyInstantsRecent, isTogglable = false, toolTip = "Back to the MyInstants browser." }
                };
                int index = 0;
                foreach (string audio in audios)
                {
                    if (audio.Length > 2)
                    {
                        index++;
                        string[] Data = audio.Split(";");
                        soundbuttons.Add(new ButtonInfo { buttonText = "SoundboardDownload" + index, overlapText = Data[0], method = () => DownloadSound(Data[0], $"{PluginInfo.ServerResourcePath}/Audio/Mods/Fun/Soundboard/Sounds/{Data[1]}"), isTogglable = false, toolTip = "Downloads " + Data[0] + " to your soundboard and plays it." });
                    }
                }
            }
            catch
            {
                soundbuttons = new List<ButtonInfo>
                {
                    new ButtonInfo { buttonText = "Exit MyInstants", method = () => LoadSoundboard(), isTogglable = false, toolTip = "Returns you back to the soundboard." },
                    new ButtonInfo { buttonText = "Could not reach the server library.", label = true }
                };
            }
            Buttons.buttons[Buttons.GetCategory(MyInstantsCategory)] = soundbuttons.ToArray();
            Buttons.CurrentCategoryName = MyInstantsCategory;
        }

        public static void DownloadSound(string name, string url)
        {
            if (name.Contains(".."))
                name = name.Replace("..", "");

            if (name.Contains(":"))
                return;

            string filename = $"Sounds{Subdirectory}/{name}.{GetFileExtension(url)}";
            if (File.Exists($"{PluginInfo.BaseDirectory}/{filename}"))
                File.Delete($"{PluginInfo.BaseDirectory}/{filename}");
            
            audioFilePool.Remove(name);
            
            AudioClip soundDownloaded = LoadSoundFromURL(url, filename);
            if (soundDownloaded != null)
                SoundboardManager.PlayHighQuality(soundDownloaded);
            
            NotificationManager.SendNotification("<color=grey>[</color><color=green>SUCCESS</color><color=grey>]</color> Successfully downloaded " + name + " to the soundboard.");
        }

        public static bool AudioIsPlaying;
        public static float RecoverTime = -1f;

        public static void PlayAudio(AudioClip sound, bool disableMicrophone = false)
        {
            if (sound == null)
                return;

            SoundboardManager.ApplySettings();
            if (SoundboardManager.HighQuality) SoundboardManager.EnsureMicHighQuality();

            if (!PhotonNetwork.InRoom)
            {
                SoundboardManager.PlayHighQuality(sound, disableMicrophone);
                AudioIsPlaying = true;
                RecoverTime = Time.time + sound.length;
                return;
            }

            if (RecorderPatch.enabled)
            {
                SoundboardManager.PlayHighQuality(sound, disableMicrophone);
            }
            else
            {
                GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.AudioClip;
                GorillaTagger.Instance.myRecorder.AudioClip = sound;
                GorillaTagger.Instance.myRecorder.RestartRecording(true);
            }

            GorillaTagger.Instance.myRecorder.DebugEchoMode = true;
            if (!LoopAudio)
            {
                AudioIsPlaying = true;
                RecoverTime = Time.time + sound.length + 0.4f;
            }
        }

        private static readonly Dictionary<ButtonInfo, (Guid id, AudioClip clip)> activeSounds = new Dictionary<ButtonInfo, (Guid id, AudioClip clip)>();

        public static void PlaySoundboardSound(object file, ButtonInfo info, bool loopAudio, bool bind)
        {
            bool[] bindings = {
                rightPrimary,
                rightSecondary,
                leftPrimary,
                leftSecondary,
                leftGrab,
                rightGrab,
                leftTrigger > 0.5f,
                rightTrigger > 0.5f,
                leftJoystickClick,
                rightJoystickClick
            };

            AudioClip clip = null;
            if (file is string filePath)
                clip = LoadSoundFromFile(filePath);
            else if (file is AudioClip audioClip)
                clip = audioClip;

            if (clip == null)
                return;

            bool shouldPlay = true;
            if (bind && BindMode > 0)
            {
                bool bindPressed = bindings[BindMode - 1];
                shouldPlay = bindPressed && !lastBindPressed; 
                lastBindPressed = bindPressed;
            }

            if (shouldPlay && !activeSounds.ContainsKey(info))
            {
                if (RecorderPatch.enabled)
                {
                    SoundboardManager.ApplySettings();
                    Guid id = SoundboardManager.InjectMic(clip, false, SoundboardManager.MicVolume);
                    SoundboardManager.PlayLocalPreview(clip);
                    if (id == Guid.Empty) id = Guid.NewGuid();
                    activeSounds[info] = (id, clip);
                }
            }

            var ids = VoiceManager.Get().AudioClips.Select(c => c.Id).ToHashSet();
            var finished = activeSounds.Where(kvp => !ids.Contains(kvp.Value.id)).ToList();

            foreach (var kvp in finished)
            {
                ButtonInfo finishedInfo = kvp.Key;
                AudioClip finishedClip = kvp.Value.clip;
                activeSounds.Remove(finishedInfo);

                if (loopAudio)
                {
                    Guid newId = SoundboardManager.InjectMic(finishedClip, false, SoundboardManager.MicVolume);
                    SoundboardManager.PlayLocalPreview(finishedClip);
                    if (newId == Guid.Empty) newId = Guid.NewGuid();
                    activeSounds[finishedInfo] = (newId, finishedClip);
                }
                else
                {
                    if (finishedInfo.enabled)
                        Toggle(finishedInfo);
                }
            }
        }

        public static void StopSoundboardSound(ButtonInfo info)
        {
            SoundboardManager.StopLocalPreview();
            if (activeSounds != null)
            {
                if (activeSounds.ContainsKey(info))
                {
                    try { VoiceManager.Get().StopAudioClip(activeSounds[info].id); } catch { }
                    activeSounds.Remove(info);
                }
            }
        }
        public static void PlayAudio(string file)
        {
            AudioClip sound = LoadSoundFromFile(file);
            if (sound == null) return;
            PlayAudio(sound);
        }

        public static void StopAllSounds() // used to be FixMicrophone
        {
            SoundboardManager.StopLocalPreview();

            if (PhotonNetwork.InRoom)
            {
                if (RecorderPatch.enabled)
                {
                    if (activeSounds != null)
                        foreach (ButtonInfo info in activeSounds.Keys)
                            info.enabled = false;
                    activeSounds.Clear();
                    VoiceManager.Get().StopAudioClips();
                    GorillaTagger.Instance.myRecorder.DebugEchoMode = false;
                }
                else
                {
                    GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.Microphone;
                    GorillaTagger.Instance.myRecorder.AudioClip = null;
                    GorillaTagger.Instance.myRecorder.RestartRecording(true);
                    GorillaTagger.Instance.myRecorder.DebugEchoMode = false;
                }
            }

            AudioIsPlaying = false;
            RecoverTime = -1f;
        }

        public static void FixMicrophone()
        {
            GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.Microphone;
            GorillaTagger.Instance.myRecorder.AudioClip = null;
            GorillaTagger.Instance.myRecorder.RestartRecording(true);
            GorillaTagger.Instance.myRecorder.DebugEchoMode = false;
        }

        private static bool lastBindPressed;
        public static void PrepareBindAudio(string file)
        {
            bool[] bindings = {
                rightPrimary,
                rightSecondary,
                leftPrimary,
                leftSecondary,
                leftGrab,
                rightGrab,
                leftTrigger > 0.5f,
                rightTrigger > 0.5f,
                leftJoystickClick,
                rightJoystickClick
            };

            bool bindPressed = bindings[BindMode - 1];
            if (bindPressed && !lastBindPressed)
            {
                if (GorillaTagger.Instance.myRecorder.SourceType == Recorder.InputSourceType.AudioClip)
                    FixMicrophone();
                else
                    PlayAudio(file);
            }
            lastBindPressed = bindPressed;
        }

        public static void OpenSoundFolder()
        {
            string filePath = GetGamePath() + $"/{PluginInfo.BaseDirectory}/Sounds";
            Process.Start(filePath);
        }

        public static void SoundBindings(bool positive = true)
        {
            string[] names = {
                "None",
                "A",
                "B",
                "X",
                "Y",
                "Left Grip",
                "Right Grip",
                "Left Trigger",
                "Right Trigger",
                "Left Joystick",
                "Right Joystick"
            };

            if (positive)
                BindMode++;
            else
                BindMode--;

            BindMode %= names.Length;
            if (BindMode < 0)
                BindMode = names.Length - 1;

            Buttons.GetIndex("Sound Bindings").overlapText = "Sound Bindings <color=grey>[</color><color=green>" + names[BindMode] + "</color><color=grey>]</color>";
        }

        public static float sendEffectDelay;
        public static void BetaPlayTag(int id, float volume)
        {
            int lowest = int.MaxValue;
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                if (player.ActorNumber < lowest)
                    lowest = player.ActorNumber;
            }

            bool isLowest = PhotonNetwork.LocalPlayer.ActorNumber == lowest;

            if (!isLowest)
                NotificationManager.SendNotification("<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> You are not the lowest actor number.");
            else
            {
                if (Time.time > sendEffectDelay)
                {
                    object[] soundSendData = { id, volume, false };
                    object[] sendEventData = { PhotonNetwork.ServerTimestamp, (byte)3, soundSendData };

                    try
                    {
                        PhotonNetwork.RaiseEvent(3, sendEventData, new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendUnreliable);
                    }
                    catch { }
                    RPCProtection();

                    sendEffectDelay = Time.time + 0.2f;
                }
            }
        }

        private static float soundSpamDelay;
        public static void SoundSpam(int soundId, bool constant = false)
        {
            if (rightGrab || constant)
            {
                if (Time.time > soundSpamDelay)
                    soundSpamDelay = Time.time + 0.1f;
                else
                    return;

                if (PhotonNetwork.InRoom)
                {
                    GorillaTagger.Instance.myVRRig.SendRPC("RPC_PlayHandTap", RpcTarget.All, soundId, false, 999999f);
                    RPCProtection();
                }
                else
                    VRRig.LocalRig.PlayHandTapLocal(soundId, false, 999999f);
            }
        }

        public static void JmancurlySoundSpam() =>
            SoundSpam(Random.Range(336, 338));

        public static void RandomSoundSpam() =>
            SoundSpam(Random.Range(0, GTPlayer.Instance.materialData.Count));

        public static void CrystalSoundSpam()
        {
            int[] sounds = {
                Random.Range(40,54),
                Random.Range(214,221)
            };
            SoundSpam(sounds[Random.Range(0, 1)]);
        }

        private static bool squeakToggle;
        public static void SqueakSoundSpam()
        {
            if (Time.time > soundSpamDelay)
                squeakToggle = !squeakToggle;
            
            SoundSpam(squeakToggle ? 75 : 76);
        }

        private static bool sirenToggle;
        public static void SirenSoundSpam()
        {
            if (Time.time > soundSpamDelay)
                sirenToggle = !sirenToggle;

            SoundSpam(sirenToggle ? 48 : 50);
        }

        public static int soundId;
        public static void DecreaseSoundID()
        {
            soundId--;
            if (soundId < 0)
                soundId = GTPlayer.Instance.materialData.Count - 1;

            Buttons.GetIndex("Custom Sound Spam").overlapText = "Custom Sound Spam <color=grey>[</color><color=green>" + soundId + "</color><color=grey>]</color>";
        }

        public static void IncreaseSoundID()
        {
            soundId++;
            soundId %= GTPlayer.Instance.materialData.Count;

            Buttons.GetIndex("Custom Sound Spam").overlapText = "Custom Sound Spam <color=grey>[</color><color=green>" + soundId + "</color><color=grey>]</color>";
        }

        public static void CustomSoundSpam() => SoundSpam(soundId);

        public static void BetaSoundSpam(int id)
        {
            if (rightGrab)
                BetaPlayTag(id, 999999f);
        }
    }
}
