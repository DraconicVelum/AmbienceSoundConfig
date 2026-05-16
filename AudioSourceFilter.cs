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

        private static readonly Dictionary<AudioSource, float> _baseVolume =
            new Dictionary<AudioSource, float>();

        internal static bool HasClipTargets => _clipTargets.Count > 0;

        internal static void Refresh()
        {
            _nameTargets.Clear();
            _clipTargets.Clear();

            ParseSharedList(AmbienceSoundConfig.ExtraSfxList.Value, AmbienceSoundConfig.ExtraSfxVolume.Value, _nameTargets);
            ParseConfigVolumeSection(AmbienceSoundConfig.ExtraSfxSplitSection, _nameTargets);

            ParseSharedList(AmbienceSoundConfig.ExtraClipList.Value, AmbienceSoundConfig.ExtraClipVolume.Value, _clipTargets);
            ParseConfigVolumeSection(AmbienceSoundConfig.ExtraClipSplitSection, _clipTargets);
        }

        internal static void Apply(AudioSource src)
        {
            if (src == null || _nameTargets.Count == 0)
                return;

            if (!TryGetSfxMultiplier(src.gameObject.name, out float mult))
                return;

            ApplyVolume(src, mult);
        }

        internal static void Apply(AudioSource src, ref float volume)
        {
            if (src == null || _nameTargets.Count == 0)
                return;

            if (!TryGetSfxMultiplier(src.gameObject.name, out float mult))
                return;

            volume = Scale(volume, mult);
        }

        internal static bool ShouldMuteClip(AudioClip clip)
        {
            return TryGetClipMultiplier(clip, out _);
        }

        internal static bool TryGetClipMultiplier(AudioClip clip, out float multiplier)
        {
            multiplier = 1f;
            return clip != null && TryMatch(_clipTargets, clip.name, out multiplier);
        }

        internal static void ApplyToRunningSources()
        {
            var dead = new List<AudioSource>();
            foreach (var kv in _baseVolume)
                if (kv.Key == null)
                    dead.Add(kv.Key);

            foreach (var d in dead)
                _baseVolume.Remove(d);

            foreach (var src in UnityEngine.Object.FindObjectsOfType<AudioSource>())
            {
                if (src == null || src.clip == null)
                    continue;

                float mult = 1f;
                bool match = HasClipTargets && TryGetClipMultiplier(src.clip, out mult);

                if (!match)
                {
                    if (_baseVolume.ContainsKey(src))
                        _baseVolume.Remove(src);

                    src.mute = false;
                    continue;
                }

                if (!_baseVolume.ContainsKey(src))
                    _baseVolume[src] = src.volume;

                float baseVol = _baseVolume[src];

                if (mult <= 0f)
                {
                    src.mute = true;
                }
                else
                {
                    src.mute = false;
                    src.volume = Mathf.Clamp01(baseVol * mult);
                }
            }
        }

        private static bool TryGetSfxMultiplier(string goName, out float multiplier)
        {
            return TryMatch(_nameTargets, goName, out multiplier);
        }

        private static void ApplyVolume(AudioSource src, float mult)
        {
            if (mult <= 0f)
            {
                src.mute = true;
                return;
            }

            src.mute = false;
            src.volume = Mathf.Clamp01(src.volume * mult);
        }

        private static float Scale(float v, float mult)
        {
            if (mult <= 0f) return 0f;
            return v * mult;
        }

        private static bool TryMatch(Dictionary<string, float> targets, string sourceName, out float multiplier)
        {
            multiplier = 1f;

            if (targets.Count == 0)
                return false;

            string name = Normalize(sourceName);

            if (targets.TryGetValue(name, out multiplier))
                return true;

            foreach (var target in targets)
            {
                if (name.StartsWith(target.Key, StringComparison.OrdinalIgnoreCase) ||
                    name.IndexOf(target.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    multiplier = target.Value;
                    return true;
                }
            }

            return false;
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
