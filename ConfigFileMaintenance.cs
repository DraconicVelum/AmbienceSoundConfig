using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AmbienceSoundConfig
{
    internal static class ConfigFileMaintenance
    {
        private static string PluginConfigFilePath => AmbienceSoundConfig.PluginConfigFilePath;
        private const string ExtraSfxSplitSection = AmbienceSoundConfig.ExtraSfxSplitSection;
        private const string ExtraClipSplitSection = AmbienceSoundConfig.ExtraClipSplitSection;

        internal static bool IsMainMenuName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.Replace("(Clone)", "").Trim().Equals("Amb_MainMenu", StringComparison.OrdinalIgnoreCase);
        }

        internal static string RemoveMainMenuFromSharedList(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            var entries = new List<string>();
            foreach (var entry in raw.Split(',', '\n', '\r'))
                if (!string.IsNullOrWhiteSpace(entry) && !IsMainMenuName(entry)) entries.Add(entry.Trim());
            return string.Join(", ", entries);
        }

        internal static bool IsOlderConfigVersion(string currentVersion, string targetVersion)
        {
            if (string.IsNullOrEmpty(currentVersion))
                return File.Exists(PluginConfigFilePath);

            if (!System.Version.TryParse(currentVersion, out var current))
                return true;

            if (!System.Version.TryParse(targetVersion, out var target))
                return false;

            return current < target;
        }

        internal static ConfigEntry<float> BindBoatVolume(ConfigFile config, float previousOceanVolume)
        {
            bool migrate = File.Exists(config.ConfigFilePath) && !HasConfigEntry(config.ConfigFilePath, "Ambience", "Boat Volume");
            var entry = config.Bind("Ambience", "Boat Volume", 1.0f,
                new ConfigDescription("Multiplier for boat water, wake, sails, sail crank, rudder audio and looping splashes. Independent of Ocean Volume.", new AcceptableValueRange<float>(0f, 5f)));
            if (migrate) entry.Value = previousOceanVolume;
            return entry;
        }

        internal static bool HasConfigEntry(string path, string section, string key)
        {
            return ReadConfigValue(path, section, key) != null;
        }

        internal static string ReadConfigValue(string path, string section, string key)
        {
            if (!File.Exists(path)) return null;
            bool inSection = false;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    inSection = line.Substring(1, line.Length - 2).Trim().Equals(section, StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (!inSection || line.StartsWith("#")) continue;
                int separator = line.IndexOf('=');
                if (separator >= 0 && line.Substring(0, separator).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                    return line.Substring(separator + 1).Trim();
            }
            return null;
        }

        internal static void EnsureCustomVolumeSections()
        {
            if (SoundSettings.IsInitialized) return;
            if (string.IsNullOrEmpty(PluginConfigFilePath))
                return;

            try
            {
                if (!File.Exists(PluginConfigFilePath))
                    return;

                string text = File.ReadAllText(PluginConfigFilePath);
                bool changed = false;
                var builder = new StringBuilder(text.TrimEnd());

                if (!HasSection(text, ExtraSfxSplitSection))
                {
                    AppendExtraSfxSplitSection(builder);
                    changed = true;
                }

                if (!HasSection(text, ExtraClipSplitSection))
                {
                    AppendExtraClipsSplitSection(builder);
                    changed = true;
                }

                if (changed)
                    File.WriteAllText(PluginConfigFilePath, builder.ToString() + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AmbienceSoundConfig] Could not seed custom volume config sections: " + ex.Message);
            }
        }

        internal static void SaveConfigPreservingCustomSections(ConfigFile config)
        {
            SaveConfigPreservingCustomSections(config, CustomSectionSnapshot.Capture(PluginConfigFilePath));
        }

        internal static void SaveConfigPreservingCustomSections(ConfigFile config, CustomSectionSnapshot snapshot)
        {
            config.Save();
            snapshot.Restore(PluginConfigFilePath);
            EnsureCustomVolumeSections();
        }

        internal static void ReloadConfigPreservingCustomSections(ConfigFile config)
        {
            var snapshot = CustomSectionSnapshot.Capture(PluginConfigFilePath);
            config.Reload();
            snapshot.Restore(PluginConfigFilePath);
            EnsureCustomVolumeSections();
        }

        private static void AppendExtraSfxSplitSection(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine($"[{ExtraSfxSplitSection}]");
            builder.AppendLine("## Config-only per-sound SFX volumes. Add one ZSFX prefab per line.");
            builder.AppendLine("## Format: prefab_name = volume");
            builder.AppendLine("## Example: sfx_boar_idle = 0.25");
        }

        private static void AppendExtraClipsSplitSection(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine($"[{ExtraClipSplitSection}]");
            builder.AppendLine("## Config-only per-clip volumes. Add one AudioClip name per line.");
            builder.AppendLine("## Format: clip_name = volume");
            builder.AppendLine("## Example: MeadowAmbience = 0.25");
        }

        private static string ReadRawSection(string text, string sectionName)
        {
            var builder = new StringBuilder();
            bool inSection = false;

            using (var reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        if (inSection)
                            break;

                        string current = trimmed.Substring(1, trimmed.Length - 2).Trim();
                        inSection = current.Equals(sectionName, StringComparison.OrdinalIgnoreCase);
                    }

                    if (inSection)
                        builder.AppendLine(line);
                }
            }

            return builder.Length > 0 ? builder.ToString().TrimEnd() + Environment.NewLine : null;
        }

        private static string RemoveSection(string text, string sectionName)
        {
            var builder = new StringBuilder();
            bool skip = false;

            using (var reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        string current = trimmed.Substring(1, trimmed.Length - 2).Trim();
                        skip = current.Equals(sectionName, StringComparison.OrdinalIgnoreCase);
                    }

                    if (!skip)
                        builder.AppendLine(line);
                }
            }

            return builder.ToString();
        }

        private static bool HasSection(string text, string sectionName)
        {
            using (var reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (!line.StartsWith("[") || !line.EndsWith("]"))
                        continue;

                    string current = line.Substring(1, line.Length - 2).Trim();
                    if (current.Equals(sectionName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        internal sealed class CustomSectionSnapshot
        {
            internal static CustomSectionSnapshot Empty => new CustomSectionSnapshot(null, null);
            private readonly string _extraSfxSplit;
            private readonly string _extraClipsSplit;

            private CustomSectionSnapshot(string extraSfxSplit, string extraClipsSplit)
            {
                _extraSfxSplit = extraSfxSplit;
                _extraClipsSplit = extraClipsSplit;
            }

            internal CustomSectionSnapshot WithoutMainMenuOverride()
            {
                if (string.IsNullOrEmpty(_extraSfxSplit)) return this;
                var builder = new StringBuilder();
                using (var reader = new StringReader(_extraSfxSplit))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        int separator = line.IndexOf('=');
                        if (separator >= 0 && IsMainMenuName(line.Substring(0, separator))) continue;
                        builder.AppendLine(line);
                    }
                }
                return new CustomSectionSnapshot(builder.ToString(), _extraClipsSplit);
            }

            internal static CustomSectionSnapshot Capture(string path)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return new CustomSectionSnapshot(null, null);

                string text = File.ReadAllText(path);
                return new CustomSectionSnapshot(
                    ReadRawSection(text, ExtraSfxSplitSection),
                    ReadRawSection(text, ExtraClipSplitSection)
                );
            }

            internal void Restore(string path)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return;

                string text = File.ReadAllText(path);

                // Also remove stale orphan entries when the section was deleted
                // from disk. ConfigFile.Save may otherwise resurrect them.
                text = RemoveSection(text, ExtraSfxSplitSection);
                text = RemoveSection(text, ExtraClipSplitSection);

                var builder = new StringBuilder(text.TrimEnd());

                if (!string.IsNullOrWhiteSpace(_extraSfxSplit))
                {
                    builder.AppendLine();
                    builder.AppendLine();
                    builder.Append(_extraSfxSplit.TrimEnd());
                }

                if (!string.IsNullOrWhiteSpace(_extraClipsSplit))
                {
                    builder.AppendLine();
                    builder.AppendLine();
                    builder.Append(_extraClipsSplit.TrimEnd());
                }

                File.WriteAllText(path, builder.ToString() + Environment.NewLine);
            }
        }

    }
}



