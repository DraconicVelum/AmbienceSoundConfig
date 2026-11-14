using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.IO;
using static AmbienceSoundConfig.ConfigFileMaintenance;
using UnityEngine;

namespace AmbienceSoundConfig
{
    [BepInPlugin("com.draconicvelum.ambiencesoundconfig", "Ambience Sound Config", AmbienceSoundConfig.PluginVersion)]
    public class AmbienceSoundConfig : BaseUnityPlugin
    {
        public const string PluginVersion = "2.9.9";
        public const string ExtraSfxSplitSection = "Extra SFX Split";
        public const string ExtraClipSplitSection = "Extra Clips Split";
        public static ConfigEntry<string> ConfigVersion;
        public static ConfigEntry<bool> ReloadConfigButton;
        public static ConfigEntry<float> VolumeSliderMax;
        public static ConfigEntry<float> WindVolume;
        public static ConfigEntry<float> OceanVolume;
        public static ConfigEntry<float> BoatVolume;
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
        private bool configDirty;
        private bool audioConfigDirty;
        private float saveConfigAt;

        private void Awake()
        {
            PluginConfigFilePath = Config.ConfigFilePath;
            // Bind and clamp everything before writing. BepInEx otherwise saves
            // each Bind/Value change, before custom sections can be preserved.
            Config.SaveOnConfigSet = false;
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

            WindVolume = Config.Bind("Ambience", "Wind Volume", 1.0f,
                new ConfigDescription("Volume for wind ambience.", new AcceptableValueRange<float>(0f, 5f)));

            OceanVolume = Config.Bind("Ambience", "Ocean Volume", 1.0f,
                new ConfigDescription("Multiplier for ocean ambience and shore/wave loops. Ocean background loops also use Ambient Loop Volume. Boat sounds use Boat Volume.", new AcceptableValueRange<float>(0f, 5f)));

            BoatVolume = ConfigFileMaintenance.BindBoatVolume(Config, OceanVolume.Value);

            AmbientLoopVolume = Config.Bind("Ambience", "Ambient Loop Volume", 1.0f,
                new ConfigDescription("Multiplier for background ambient loops and Amb_MainMenu, preserving biome transitions and fades.", new AcceptableValueRange<float>(0f, 5f)));

            ShieldHumVolume = Config.Bind("Ambience", "Shield Hum Volume", 1.0f,
                new ConfigDescription("Multiplier for shield dome hum and Shield Generator loops, including low-fuel loops. Does not affect one-shot impacts or fuel interactions.", new AcceptableValueRange<float>(0f, 5f)));

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

            if (IsOlderConfigVersion(fileConfigVersion, "2.8.1"))
            {
                startupCustomSections = startupCustomSections.WithoutMainMenuOverride();
                ExtraSfxList.Value = ConfigFileMaintenance.RemoveMainMenuFromSharedList(ExtraSfxList.Value);
            }

            SoundSettings.Initialize(Config, GetVolumeSliderMax, MarkConfigDirty,
                ExtraSfxList, ExtraSfxVolume, ExtraClipList, ExtraClipVolume,
                IsOlderConfigVersion(fileConfigVersion, "2.8.1"));
            SoundSettings.RemoveLegacyMaster();
            if (SoundSettings.MigratedLegacy) startupCustomSections = CustomSectionSnapshot.Empty;
            // Legacy comma-list keys are import-only; row entries replace them.
            Config.Remove(ExtraSfxList.Definition);
            Config.Remove(ExtraClipList.Definition);

            VolumeSliderMax.SettingChanged += (_, __) =>
            {
                ClampAllVolumeConfigs();
                SoundSettings.ClampAll();
                AudioSourceFilter.Refresh();
                AudioSourceFilter.ApplyToRunningSources();
                ApplyAllVolumes();
                SettingsInject.SyncUI();
                MarkConfigDirty();
            };

            WindVolume.SettingChanged += (_, __) => ClampVolumeConfig(WindVolume);
            OceanVolume.SettingChanged += (_, __) => ClampVolumeConfig(OceanVolume);
            BoatVolume.SettingChanged += (_, __) => ClampVolumeConfig(BoatVolume);
            AmbientLoopVolume.SettingChanged += (_, __) => ClampVolumeConfig(AmbientLoopVolume);
            ShieldHumVolume.SettingChanged += (_, __) => ClampVolumeConfig(ShieldHumVolume);

            ExtraSfxList.SettingChanged += (_, __) => AudioSourceFilter.Refresh();
            ExtraSfxVolume.SettingChanged += (_, __) =>
            {
                ClampVolumeConfig(ExtraSfxVolume);
            };

            ExtraClipList.SettingChanged += (_, __) =>
            {
                AudioSourceFilter.Refresh();
                AudioSourceFilter.ApplyToRunningSources();
            };

            ExtraClipVolume.SettingChanged += (_, __) =>
            {
                ClampVolumeConfig(ExtraClipVolume);
            };

            ClampAllVolumeConfigs();
            UpdateConfigFileIfNeeded(configNeedsUpdate, startupCustomSections);
            EnsureCustomVolumeSections();
            AudioSourceFilter.Refresh();
            SoundPlayLogger.Init(Config);

            harmony.PatchAll(typeof(AudioSource_Play_Patch));
            harmony.PatchAll(typeof(AudioSource_PlayOneShotScale_Patch));
            harmony.PatchAll(typeof(ZSFX_VolumeScale_Patch));
            harmony.PatchAll(typeof(AudioMan_AmbienceVolume_Patch));
            harmony.PatchAll(typeof(ComponentAudioOwnership_Patch));
            harmony.PatchAll(typeof(BoatControlAudio_Patch));
            harmony.PatchAll(typeof(SettingsInject));
            harmony.PatchAll(typeof(AudioSettingsNavigation_Patch));
            harmony.PatchAll(typeof(AudioTextFocus_Patch));

            Config.ConfigReloaded += OnReloaded;
            Config.SettingChanged += OnSettingChanged;
            Logger.LogInfo($"Ambience Sound Config (v{PluginVersion}) loaded.");

        }

        private void OnSettingChanged(object sender, SettingChangedEventArgs e) => MarkConfigDirty();

        private void MarkConfigDirty()
        {
            configDirty = audioConfigDirty = true;
            saveConfigAt = Time.unscaledTime + .35f;
        }

        private void LateUpdate()
        {
            if (audioConfigDirty)
            {
                audioConfigDirty = false;
                SoundSettings.ClampAll();
                AudioSourceFilter.Refresh();
                AudioVolumeController.Refresh();
            }
            AudioVolumeController.Tick();
            SettingsInject.Tick();
            if (configDirty && Time.unscaledTime >= saveConfigAt)
            {
                SaveConfigPreservingCustomSections(Config);
                configDirty = false;
            }
        }

        private void OnDestroy()
        {
            SettingsInject.Cleanup();
            Config.ConfigReloaded -= OnReloaded;
            Config.SettingChanged -= OnSettingChanged;
            if (configDirty) SaveConfigPreservingCustomSections(Config);
            harmony.UnpatchSelf();
            AudioVolumeController.Restore();
            ComponentAudioOwnership_Patch.Reset();
            BoatControlAudio_Patch.Reset();
            SoundSettings.Reset();
        }

        private void OnReloaded(object sender, EventArgs e)
        {
            ClampAllVolumeConfigs();
            SoundSettings.Reload();
            SoundSettings.RemoveLegacyMaster();
            if (IsOlderConfigVersion(ConfigVersion.Value, PluginVersion))
                ConfigVersion.Value = PluginVersion;
            MarkConfigDirty();
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
            AudioVolumeController.Refresh();
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
            ClampVolumeConfig(WindVolume);
            ClampVolumeConfig(OceanVolume);
            ClampVolumeConfig(BoatVolume);
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
                return ConfigFileMaintenance.ReadConfigValue(PluginConfigFilePath, "General", "Config Version");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AmbienceSoundConfig] Could not read config version: " + ex.Message);
            }

            return null;
        }

        private void UpdateConfigFileIfNeeded(bool configNeedsUpdate, CustomSectionSnapshot startupCustomSections)
        {
            try
            {
                if (configNeedsUpdate) ConfigVersion.Value = PluginVersion;
                SaveConfigPreservingCustomSections(Config, startupCustomSections);
                if (configNeedsUpdate)
                    Logger.LogInfo($"Updated config file to v{PluginVersion} while keeping existing values.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Could not update config file: " + ex.Message);
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



