using System;
using System.Collections.Generic;
using UnityEngine;

namespace AmbienceSoundConfig
{
    internal static class AudioSourceFilter
    {
        private static readonly HashSet<string> _nameTargets =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> _clipTargets =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static void Refresh()
        {
            _nameTargets.Clear();
            _clipTargets.Clear();

            var raw = AmbienceSoundConfig.ExtraSfxList.Value;
            if (!string.IsNullOrEmpty(raw))
            {
                foreach (var s in raw.Split(','))
                {
                    var n = Normalize(s);
                    if (!string.IsNullOrEmpty(n))
                        _nameTargets.Add(n);
                }
            }

            var rawClips = AmbienceSoundConfig.ExtraClipList.Value;
            if (!string.IsNullOrEmpty(rawClips))
            {
                foreach (var s in rawClips.Split(','))
                {
                    var n = Normalize(s);
                    if (!string.IsNullOrEmpty(n))
                        _clipTargets.Add(n);
                }
            }
        }

        internal static void Apply(AudioSource src)
        {
            if (src == null || _nameTargets.Count == 0)
                return;

            if (!MatchName(src.gameObject.name))
                return;

            ApplyVolume(src, AmbienceSoundConfig.ExtraSfxVolume.Value);
        }

        internal static void Apply(AudioSource src, ref float volume)
        {
            if (src == null || _nameTargets.Count == 0)
                return;

            if (!MatchName(src.gameObject.name))
                return;

            volume = Scale(volume, AmbienceSoundConfig.ExtraSfxVolume.Value);
        }

        internal static bool ShouldMuteClip(AudioClip clip)
        {
            if (clip == null || _clipTargets.Count == 0)
                return false;

            return MatchClip(clip.name);
        }

        internal static float ClipMultiplier => AmbienceSoundConfig.ExtraClipVolume.Value;

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

        private static bool MatchName(string goName)
        {
            if (_nameTargets.Count == 0)
                return false;

            string name = Normalize(goName);

            foreach (var t in _nameTargets)
            {
                if (name.Equals(t, StringComparison.OrdinalIgnoreCase)) return true;
                if (name.StartsWith(t, StringComparison.OrdinalIgnoreCase)) return true;
                if (name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static bool MatchClip(string clipName)
        {
            string name = Normalize(clipName);

            foreach (var t in _clipTargets)
            {
                if (name.Equals(t, StringComparison.OrdinalIgnoreCase)) return true;
                if (name.StartsWith(t, StringComparison.OrdinalIgnoreCase)) return true;
                if (name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
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
        private static readonly System.Collections.Generic.Dictionary<AudioSource, float> _baseVolume
            = new System.Collections.Generic.Dictionary<AudioSource, float>();
        internal static bool HasClipTargets => _clipTargets.Count > 0;
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

                bool match = HasClipTargets && ShouldMuteClip(src.clip);

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
                float mult = ClipMultiplier;

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
    }
}
