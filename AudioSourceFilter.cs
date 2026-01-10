using System;
using System.Collections.Generic;
using UnityEngine;

namespace AmbienceSoundConfig
{
    internal static class AudioSourceFilter
    {
        private static readonly HashSet<string> _targets =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static void Refresh()
        {
            _targets.Clear();

            var raw = AmbienceSoundConfig.ExtraSfxList.Value;
            if (string.IsNullOrEmpty(raw))
                return;

            foreach (var s in raw.Split(','))
            {
                var n = Normalize(s);
                if (!string.IsNullOrEmpty(n))
                    _targets.Add(n);
            }
        }

        internal static void Apply(AudioSource src)
        {
            if (src == null || _targets.Count == 0)
                return;

            string name = Normalize(src.gameObject.name);
            if (!Matches(name))
                return;

            if (AmbienceSoundConfig.ExtraSfxVolume.Value <= 0f)
            {
                src.mute = true;
                return;
            }

            src.mute = false;
            src.volume *= AmbienceSoundConfig.ExtraSfxVolume.Value;
        }

        internal static void Apply(AudioSource src, ref float volume)
        {
            if (src == null || _targets.Count == 0)
                return;

            string name = Normalize(src.gameObject.name);
            if (!Matches(name))
                return;

            if (AmbienceSoundConfig.ExtraSfxVolume.Value <= 0f)
            {
                volume = 0f;
                return;
            }

            volume *= AmbienceSoundConfig.ExtraSfxVolume.Value;
        }

        private static bool Matches(string name)
        {
            foreach (var t in _targets)
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
    }
}
