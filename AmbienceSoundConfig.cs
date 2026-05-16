using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace AmbienceSoundConfig
{
    [BepInPlugin("com.draconicvelum.ambiencesoundconfig", "Ambience Sound Config", AmbienceSoundConfig.PluginVersion)]
    public class AmbienceSoundConfig : BaseUnityPlugin
    {
        public const string PluginVersion = "2.7.8";
        public const string ExtraSfxSplitSection = "Extra SFX Split";
        public const string ExtraClipSplitSection = "Extra Clips Split";
        public static ConfigEntry<string> ConfigVersion;
        public static ConfigEntry<bool> ReloadConfigButton;
        public static ConfigEntry<float> VolumeSliderMax;
        public static ConfigEntry<float> MasterVolume;
        public static ConfigEntry<float> WindVolume;
        public static ConfigEntry<float> OceanVolume;
        public static ConfigEntry<float> AmbientLoopVolume;
        public static ConfigEntry<float> ShieldHumVolume;
        public static ConfigEntry<string> ExtraSfxList;
        public static ConfigEntry<float> ExtraSfxVolume;
        public static ConfigEntry<string> ExtraClipList;
        public static ConfigEntry<float> ExtraClipVolume;
        public static ConfigEntry<bool> EnableSoundLogging;
        public static ConfigEntry<bool> LogOnlyUnique;
        public static string PluginConfigFilePath;


        private static readonly Harmony harmony = new Harmony("com.draconicvelum.ambiencesoundconfig");

        private void Awake()
        {
            PluginConfigFilePath = Config.ConfigFilePath;
            var startupCustomSections = CustomSectionSnapshot.Capture(PluginConfigFilePath);
            string fileConfigVersion = ReadConfigVersion();
            bool configNeedsUpdate = IsOlderConfigVersion(fileConfigVersion, PluginVersion);

            ConfigVersion = Config.Bind("General", "Config Version", PluginVersion,
                "Internal config version. The mod updates older config files to the latest format while keeping your values.");

            ReloadConfigButton = Config.Bind("General", "Reload Config", false,
                new ConfigDescription(
                    "Button for compatible config manager mods. Reloads this config file and reapplies split SFX/clip volumes.",
                    null,
                    new BepInEx.ConfigurationManager.ConfigurationManagerAttributes
                    {
                        CustomDrawer = DrawReloadConfigButton,
                        HideDefaultButton = true
                    }
                ));

            VolumeSliderMax = Config.Bind("General", "Volume Slider Max", 1.0f,
                new ConfigDescription("Config-only maximum value used by this mod's volume sliders and volume config entries. Default is 1.0; valid range is 0.0 to 5.0.", new AcceptableValueRange<float>(0f, 5f)));

            MasterVolume = Config.Bind("Ambience", "Master Volume", 0.25f,
                new ConfigDescription("Controls overall ambience loudness multiplier.", new AcceptableValueRange<float>(0f, 5f)));

            WindVolume = Config.Bind("Ambience", "Wind Volume", 1.0f,
                new ConfigDescription("Volume for wind ambience (multiplied by Master Volume).", new AcceptableValueRange<float>(0f, 5f)));

            OceanVolume = Config.Bind("Ambience", "Ocean Volume", 1.0f,
                new ConfigDescription("Volume for ocean ambience (multiplied by Master Volume).", new AcceptableValueRange<float>(0f, 5f)));

            AmbientLoopVolume = Config.Bind("Ambience", "Ambient Loop Volume", 1.0f,
                new ConfigDescription("Volume for background ambient loop (multiplied by Master Volume).", new AcceptableValueRange<float>(0f, 5f)));

            ShieldHumVolume = Config.Bind("Ambience", "Shield Hum Volume", 1.0f,
                new ConfigDescription("Volume for shield dome hum (multiplied by Master Volume).", new AcceptableValueRange<float>(0f, 5f)));

            ExtraSfxList = Config.Bind(
                "Extra SFX",
                "Extra SFX Prefabs",
                "",
                $"Legacy comma separated list of ZSFX prefab names affected by Extra SFX Volume. For per-sound volumes, add entries under [{ExtraSfxSplitSection}]."
            );

            ExtraSfxVolume = Config.Bind(
                "Extra SFX",
                "Extra SFX Volume",
                1.0f,
                new ConfigDescription(
                    "Legacy shared volume multiplier for sound effects listed in Extra SFX Prefabs.",
                    new AcceptableValueRange<float>(0f, 5f)
                )
            );

            ExtraClipList = Config.Bind(
                "Extra Clips",
                "Extra Clip Names",
                "",
                $"Legacy comma separated list of AudioClip names affected by Extra Clip Volume. For per-clip volumes, add entries under [{ExtraClipSplitSection}]."
            );

            ExtraClipVolume = Config.Bind(
                "Extra Clips",
                "Extra Clip Volume",
                1.0f,
                new ConfigDescription(
                    "Legacy shared volume multiplier for clip names listed in Extra Clip Names.",
                    new AcceptableValueRange<float>(0f, 5f)
                )
            );

            EnableSoundLogging = Config.Bind(
                "Logging",
                "Enable Sound Logging",
                false,
                "Logs all played SFX and AudioClips to console and a file next to this config."
            );

            LogOnlyUnique = Config.Bind(
                "Logging",
                "Log Only Unique",
                true,
                "If enabled, each sound name is logged only once per session."
            );

            VolumeSliderMax.SettingChanged += (_, __) =>
            {
                ClampAllVolumeConfigs();
                AudioSourceFilter.Refresh();
                AudioSourceFilter.ApplyToRunningSources();
                ApplyAllVolumes();
                SettingsInject.SyncUI();
                SaveConfigPreservingCustomSections(Config);
            };

            MasterVolume.SettingChanged += (_, __) => ClampVolumeConfig(MasterVolume);
            WindVolume.SettingChanged += (_, __) => ClampVolumeConfig(WindVolume);
            OceanVolume.SettingChanged += (_, __) => ClampVolumeConfig(OceanVolume);
            AmbientLoopVolume.SettingChanged += (_, __) => ClampVolumeConfig(AmbientLoopVolume);
            ShieldHumVolume.SettingChanged += (_, __) => ClampVolumeConfig(ShieldHumVolume);

            ExtraSfxList.SettingChanged += (_, __) => AudioSourceFilter.Refresh();
            ExtraSfxVolume.SettingChanged += (_, __) =>
            {
                ClampVolumeConfig(ExtraSfxVolume);
                AudioSourceFilter.Refresh();
                foreach (var src in UnityEngine.Object.FindObjectsOfType<AudioSource>())
                    AudioSourceFilter.Apply(src);
            };

            ExtraClipList.SettingChanged += (_, __) =>
            {
                AudioSourceFilter.Refresh();
                AudioSourceFilter.ApplyToRunningSources();
            };

            ExtraClipVolume.SettingChanged += (_, __) =>
            {
                ClampVolumeConfig(ExtraClipVolume);
                AudioSourceFilter.Refresh();
                AudioSourceFilter.ApplyToRunningSources();
            };

            ClampAllVolumeConfigs();
            UpdateConfigFileIfNeeded(configNeedsUpdate, startupCustomSections);
            EnsureCustomVolumeSections();
            AudioSourceFilter.Refresh();
            SoundPlayLogger.Init(Config);

            harmony.PatchAll(typeof(AudioSource_DoAll_Patch));
            harmony.PatchAll(typeof(AudioSource_PlayOneShotScale_Patch));
            harmony.PatchAll(typeof(ZSFX_VolumeScale_Patch));
            harmony.PatchAll(typeof(AudioMan_AmbienceVolume_Patch));
            harmony.PatchAll(typeof(SettingsInject));

            Config.ConfigReloaded += OnReloaded;
            Logger.LogInfo($"Ambience Sound Config (v{PluginVersion}) loaded.");

        }

        private void OnReloaded(object sender, EventArgs e)
        {
            ClampAllVolumeConfigs();
            EnsureCustomVolumeSections();
            AudioSourceFilter.Refresh();
            AudioSourceFilter.ApplyToRunningSources();

            ApplyAllVolumes();
            SettingsInject.SyncUI();
        }

        public static void ReloadConfigFromManager()
        {
            try
            {
                ReloadConfigPreservingCustomSections(ConfigVersion.ConfigFile);
                AudioSourceFilter.Refresh();
                AudioSourceFilter.ApplyToRunningSources();
                ApplyAllVolumes();
                SettingsInject.SyncUI();
                Debug.Log("[AmbienceSoundConfig] Config reloaded from config manager button.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AmbienceSoundConfig] Config manager reload failed: " + ex.Message);
            }
        }

        private static void DrawReloadConfigButton(ConfigEntryBase entry)
        {
            if (UnityEngine.GUILayout.Button("Reload Config"))
                ReloadConfigFromManager();
        }

        public static void ApplyAllVolumes()
        {
            AudioMan_AmbienceVolume_Patch.ApplyWindVolumeWrapper();
            AudioMan_AmbienceVolume_Patch.ApplyOceanVolumeWrapper();
            AudioMan_AmbienceVolume_Patch.ApplyAmbientLoopVolumeWrapper();
            AudioMan_AmbienceVolume_Patch.ApplyShieldHumVolumeWrapper();
        }

        public static float GetVolumeSliderMax()
        {
            return Mathf.Clamp(VolumeSliderMax.Value, 0f, 5f);
        }

        public static float ClampConfiguredVolume(float value)
        {
            return Mathf.Clamp(value, 0f, GetVolumeSliderMax());
        }

        private static void ClampVolumeConfig(ConfigEntry<float> entry)
        {
            float clamped = ClampConfiguredVolume(entry.Value);
            if (!Mathf.Approximately(entry.Value, clamped))
                entry.Value = clamped;
        }

        private static void ClampAllVolumeConfigs()
        {
            ClampVolumeConfig(MasterVolume);
            ClampVolumeConfig(WindVolume);
            ClampVolumeConfig(OceanVolume);
            ClampVolumeConfig(AmbientLoopVolume);
            ClampVolumeConfig(ShieldHumVolume);
            ClampVolumeConfig(ExtraSfxVolume);
            ClampVolumeConfig(ExtraClipVolume);
        }

        private static string ReadConfigVersion()
        {
            if (string.IsNullOrEmpty(PluginConfigFilePath) || !File.Exists(PluginConfigFilePath))
                return null;

            try
            {
                foreach (var rawLine in File.ReadAllLines(PluginConfigFilePath))
                {
                    string line = rawLine.Trim();
                    if (!line.StartsWith("Config Version =", StringComparison.OrdinalIgnoreCase))
                        continue;

                    int separator = line.IndexOf('=');
                    if (separator >= 0)
                        return line.Substring(separator + 1).Trim();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AmbienceSoundConfig] Could not read config version: " + ex.Message);
            }

            return null;
        }

        private static bool IsOlderConfigVersion(string currentVersion, string targetVersion)
        {
            if (string.IsNullOrEmpty(currentVersion))
                return File.Exists(PluginConfigFilePath);

            if (!Version.TryParse(currentVersion, out var current))
                return true;

            if (!Version.TryParse(targetVersion, out var target))
                return false;

            return current < target;
        }

        private void UpdateConfigFileIfNeeded(bool configNeedsUpdate, CustomSectionSnapshot startupCustomSections)
        {
            if (!configNeedsUpdate)
                return;

            try
            {
                ConfigVersion.Value = PluginVersion;
                SaveConfigPreservingCustomSections(Config, startupCustomSections);
                Logger.LogInfo($"Updated config file to v{PluginVersion} while keeping existing values.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Could not update config file: " + ex.Message);
            }
        }

        internal static void EnsureCustomVolumeSections()
        {
            if (string.IsNullOrEmpty(PluginConfigFilePath))
                return;

            try
            {
                if (!File.Exists(PluginConfigFilePath))
                    return;

                string text = RemoveLegacyEntryLines(File.ReadAllText(PluginConfigFilePath));
                bool changed = text != File.ReadAllText(PluginConfigFilePath);
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

        private static void SaveConfigPreservingCustomSections(ConfigFile config, CustomSectionSnapshot snapshot)
        {
            config.Save();
            snapshot.Restore(PluginConfigFilePath);
            EnsureCustomVolumeSections();
        }

        private static void ReloadConfigPreservingCustomSections(ConfigFile config)
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

        private static string RemoveLegacyEntryLines(string text)
        {
            var builder = new StringBuilder();
            using (var reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("Extra SFX Entries =", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("Extra Clip Entries =", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    builder.AppendLine(line);
                }
            }

            return builder.ToString().TrimEnd() + Environment.NewLine;
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

        private sealed class CustomSectionSnapshot
        {
            private readonly string _extraSfxSplit;
            private readonly string _extraClipsSplit;

            private CustomSectionSnapshot(string extraSfxSplit, string extraClipsSplit)
            {
                _extraSfxSplit = extraSfxSplit;
                _extraClipsSplit = extraClipsSplit;
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

                if (!string.IsNullOrWhiteSpace(_extraSfxSplit))
                    text = RemoveSection(text, ExtraSfxSplitSection);

                if (!string.IsNullOrWhiteSpace(_extraClipsSplit))
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

namespace BepInEx.ConfigurationManager
{
    using BepInEx.Configuration;
    using System;

    public sealed class ConfigurationManagerAttributes
    {
        public Action<ConfigEntryBase> CustomDrawer;
        public bool? HideDefaultButton;
    }
}
