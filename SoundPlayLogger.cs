using BepInEx;
using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AmbienceSoundConfig
{
    internal static class SoundPlayLogger
    {
        private static readonly HashSet<string> _seen =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static string _logPath;

        internal static void Init(ConfigFile config)
        {
            try
            {
                string dir = Path.GetDirectoryName(config.ConfigFilePath);
                _logPath = Path.Combine(dir, "AmbienceSoundConfig_SoundLog.txt");
            }
            catch
            {
                _logPath = Path.Combine(Paths.ConfigPath,
                    "AmbienceSoundConfig_SoundLog.txt");
            }
        }

        internal static void Log(string type, string name, GameObject go = null)
        {
            if (!AmbienceSoundConfig.EnableSoundLogging.Value)
                return;

            if (string.IsNullOrEmpty(name))
                return;

            if (AmbienceSoundConfig.LogOnlyUnique.Value &&
                !_seen.Add(type + "|" + name))
                return;

            string time = DateTime.Now.ToString("HH:mm:ss.fff");

            string line = go != null
                ? $"[{time}] {type}: {name} @ {go.name}"
                : $"[{time}] {type}: {name}";

            Debug.Log("[ASC-LOG] " + line);

            try
            {
                File.AppendAllText(_logPath, line + Environment.NewLine);
            }
            catch { }
        }
    }
}