/*
 * ii Reborn
 * Portions Copyright (C) 2025–2026 Goldentrophy Software
 * Licensed under GNU GPL v3.0-or-later — see LICENSE and NOTICE.
 * This file is part of a derivative work; see NOTICE for attribution
 * and modification history. Do not remove this notice.
 */

namespace iiMenu.Managers
{
    public class LogManager
    {
        public static void Log(object log)
        {
            if (Plugin.PluginLogger != null)
                Plugin.PluginLogger.LogInfo(log);
            else
                UnityEngine.Debug.Log(log);
        }

        public static void Log(object log, object[] args)
        {
            string msg = string.Format(log.ToString(), args);
            if (Plugin.PluginLogger != null)
                Plugin.PluginLogger.LogInfo(msg);
            else
                UnityEngine.Debug.Log(msg);
        }

        public static void LogError(object log)
        {
            if (Plugin.PluginLogger != null)
                Plugin.PluginLogger.LogError(log);
            else
                UnityEngine.Debug.LogError(log);
        }

        public static void LogError(object log, object[] args)
        {
            string msg = string.Format(log.ToString(), args);
            if (Plugin.PluginLogger != null)
                Plugin.PluginLogger.LogError(msg);
            else
                UnityEngine.Debug.LogError(msg);
        }

        public static void LogWarning(object log)
        {
            if (Plugin.PluginLogger != null)
                Plugin.PluginLogger.LogDebug(log);
            else
                UnityEngine.Debug.LogWarning(log);
        }

        public static void LogWarning(object log, object[] args)
        {
            string msg = string.Format(log.ToString(), args);
            if (Plugin.PluginLogger != null)
                Plugin.PluginLogger.LogDebug(msg);
            else
                UnityEngine.Debug.LogWarning(msg);
        }
    }
}
