using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace AmbienceSoundConfig
{
    internal static class AudioSourceFilter
    {
        private static readonly Dictionary<string, float> _nameTargets =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, float> _clipTargets =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        internal static bool HasClipTargets => _clipTargets.Count > 0;

        internal static void Refresh()
        {
            AudioVolumeController.Invalidate();
            _nameTargets.Clear();
            _clipTargets.Clear();

            if (SoundSettings.IsInitialized)
            {
                foreach (var row in SoundSettings.GetRows(false))
                    if (!string.IsNullOrWhiteSpace(row.Name.Value)) _nameTargets[Normalize(row.Name.Value)] = row.Volume.Value;
                foreach (var row in SoundSettings.GetRows(true))
                    if (!string.IsNullOrWhiteSpace(row.Name.Value)) _clipTargets[Normalize(row.Name.Value)] = row.Volume.Value;
                return;
            }

            ParseSharedList(AmbienceSoundConfig.ExtraSfxList.Value, AmbienceSoundConfig.ExtraSfxVolume.Value, _nameTargets);
            ParseConfigVolumeSection(AmbienceSoundConfig.ExtraSfxSplitSection, _nameTargets);

            ParseSharedList(AmbienceSoundConfig.ExtraClipList.Value, AmbienceSoundConfig.ExtraClipVolume.Value, _clipTargets);
            ParseConfigVolumeSection(AmbienceSoundConfig.ExtraClipSplitSection, _clipTargets);
        }

        internal static void Apply(AudioSource src) => AudioVolumeController.Apply(src);

        internal static bool ShouldMuteClip(AudioClip clip)
        {
            return TryGetClipMultiplier(clip, out _);
        }

        internal static bool TryGetClipMultiplier(AudioClip clip, out float multiplier)
        {
            multiplier = 1f;
            bool match = clip != null && TryMatch(_clipTargets, clip.name, out multiplier);
            if (match && SoundSettings.IsInitialized) multiplier *= AmbienceSoundConfig.ExtraClipVolume.Value;
            return match;
        }

        internal static void ApplyToRunningSources()
        {
            foreach (var source in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                AudioVolumeController.Apply(source);
        }

        internal static bool TryGetSfxMultiplier(string goName, out float multiplier)
        {
            bool match = TryMatch(_nameTargets, goName, out multiplier);
            if (match && SoundSettings.IsInitialized) multiplier *= AmbienceSoundConfig.ExtraSfxVolume.Value;
            return match;
        }
        private static bool TryMatch(Dictionary<string, float> targets, string sourceName, out float multiplier)
        {
            multiplier = 1f;

            if (targets.Count == 0)
                return false;

            string name = Normalize(sourceName);

            if (targets.TryGetValue(name, out multiplier))
                return true;

            int longest = -1;
            foreach (var target in targets)
            {
                if (name.StartsWith(target.Key, StringComparison.OrdinalIgnoreCase) ||
                    name.IndexOf(target.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (target.Key.Length > longest)
                    {
                        multiplier = target.Value;
                        longest = target.Key.Length;
                    }
                }
            }

            return longest >= 0;
        }

        private static void ParseSharedList(string raw, float sharedVolume, Dictionary<string, float> target)
        {
            if (string.IsNullOrEmpty(raw))
                return;

            foreach (var s in raw.Split(',', '\n', '\r'))
            {
                var n = Normalize(s);
                if (!string.IsNullOrEmpty(n))
                    target[n] = AmbienceSoundConfig.ClampConfiguredVolume(sharedVolume);
            }
        }

        private static void ParseConfigVolumeSection(string sectionName, Dictionary<string, float> target)
        {
            string path = AmbienceSoundConfig.PluginConfigFilePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            bool inSection = false;

            foreach (var rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    string currentSection = line.Substring(1, line.Length - 2).Trim();
                    inSection = currentSection.Equals(sectionName, StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (inSection)
                    ParseVolumeLine(line, target);
            }
        }

        private static void ParseVolumeLine(string line, Dictionary<string, float> target)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                return;

            string name = trimmed;
            string volumeText = null;

            int separator = trimmed.IndexOf('=');
            if (separator < 0)
                separator = trimmed.IndexOf(':');

            if (separator >= 0)
            {
                name = trimmed.Substring(0, separator);
                volumeText = trimmed.Substring(separator + 1);
            }

            name = Normalize(name);
            if (string.IsNullOrEmpty(name))
                return;

            float volume = 1f;
            if (!string.IsNullOrEmpty(volumeText) &&
                float.TryParse(volumeText.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                volume = parsed;
            }

            target[name] = AmbienceSoundConfig.ClampConfiguredVolume(volume);
        }

        private static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            int idx = name.IndexOf("(Clone)", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                name = name.Substring(0, idx);

            return name.Trim();
        }
    }
}


