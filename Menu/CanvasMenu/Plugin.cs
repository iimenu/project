using System;
using System.Collections;
using UnityEngine;

namespace iiMenu.CanvasMenuUI
{
    public static class PluginInfo
    {
        public static string Name => iiMenu.PluginInfo.Name;
        public static string Version => iiMenu.PluginInfo.Version;
    }

    public sealed class ThemePresetInfo
    {
        public string Name;
    }

    public class Plugin
    {
        public static readonly Plugin Log = new Plugin();

        public bool IsLicensed => true;

        public int selectedThemeIndex;

        public string DisplayDiscordName = "Local";
        public string DisplayDiscordId = "-";
        public string DisplayLicenseKey = "local";
        public string DisplayHwid = "-";

        public string statusMessage = string.Empty;
        public Color statusColor = Color.white;

        public bool autoReauth;
        public bool disableNetworkTriggers;
        public bool playerTracers;
        public bool lavaTrails;
        public bool speedDetection;
        public bool showThreatScores;
        public bool badgeTracers;
        public bool roomAnalyticsEnabled;
        public bool testBadgeMode;
        public bool showOverlay;
        public bool uiVisible;

        public static readonly ThemePresetInfo[] colourPresets =
        {
            new ThemePresetInfo { Name = "Midnight" },
            new ThemePresetInfo { Name = "Dark" },
            new ThemePresetInfo { Name = "Purple" },
            new ThemePresetInfo { Name = "Blue" },
            new ThemePresetInfo { Name = "Green" },
            new ThemePresetInfo { Name = "Red" },
            new ThemePresetInfo { Name = "Teal" },
            new ThemePresetInfo { Name = "Orange" },
            new ThemePresetInfo { Name = "Pink" },
            new ThemePresetInfo { Name = "Void" },
        };

        public void LogDebug(string message) => UnityEngine.Debug.Log(message);
        public void LogInfo(string message) => UnityEngine.Debug.Log(message);
        public void LogError(string message) => UnityEngine.Debug.LogError(message);
        public void SendNotification_WM(string title, string message) => iiMenu.Managers.NotificationManager.SendNotification(message);

        public string GetAnalyticsSummary_WM() => string.Empty;
    }

    public static class LogoCache
    {
        private static Texture2D cached;
        private static bool attempted;

        public static bool TryGetCached(out Texture2D texture)
        {
            texture = cached;
            return texture != null;
        }

        public static IEnumerator EnsureTexture(Action<Texture2D> apply)
        {
            if (cached == null && !attempted)
            {
                attempted = true;

                try
                {
                    cached = iiMenu.Utilities.AssetUtilities.LoadTextureFromResource(
                        $"{iiMenu.PluginInfo.ClientResourcePath}.icon.png");
                }
                catch { }
            }

            if (cached != null && apply != null)
                apply(cached);

            yield break;
        }
    }

    public static class DiscordAvatarCache
    {
        public static IEnumerator FetchToCache(Plugin plugin)
        {
            yield break;
        }

        public static bool TryApplyCached(Plugin plugin, Renderer target)
        {
            return false;
        }

        public static IEnumerator EnsureAvatar(Plugin plugin, Renderer target, Action done)
        {
            yield break;
        }
    }
}
