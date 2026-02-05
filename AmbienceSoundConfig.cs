using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using UnityEngine;

namespace AmbienceSoundConfig
{
    [BepInPlugin("com.draconicvelum.ambiencesoundconfig", "Ambience Sound Config", "2.6.8")]
    public class AmbienceSoundConfig : BaseUnityPlugin
    {
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


        private static readonly Harmony harmony = new Harmony("com.draconicvelum.ambiencesoundconfig");

        private void Awake()
        {
            MasterVolume = Config.Bind("Ambience", "Master Volume", 0.25f,
                new ConfigDescription("Controls overall ambience loudness multiplier.", new AcceptableValueRange<float>(0f, 1f)));

            WindVolume = Config.Bind("Ambience", "Wind Volume", 1.0f,
                new ConfigDescription("Volume for wind ambience (multiplied by Master Volume).", new AcceptableValueRange<float>(0f, 1f)));

            OceanVolume = Config.Bind("Ambience", "Ocean Volume", 1.0f,
                new ConfigDescription("Volume for ocean ambience (multiplied by Master Volume).", new AcceptableValueRange<float>(0f, 1f)));

            AmbientLoopVolume = Config.Bind("Ambience", "Ambient Loop Volume", 1.0f,
                new ConfigDescription("Volume for background ambient loop (multiplied by Master Volume).", new AcceptableValueRange<float>(0f, 1f)));

            ShieldHumVolume = Config.Bind("Ambience", "Shield Hum Volume", 1.0f,
                new ConfigDescription("Volume for shield dome hum (multiplied by Master Volume).", new AcceptableValueRange<float>(0f, 1f)));

            ExtraSfxList = Config.Bind(
                "Extra SFX",
                "Extra SFX Prefabs",
                "",
                "Comma separated list of ZSFX prefab names affected by Extra SFX Volume."
            );

            ExtraSfxVolume = Config.Bind(
                "Extra SFX",
                "Extra SFX Volume",
                1.0f,
                new ConfigDescription(
                    "Independent volume multiplier for listed sound effects.",
                    new AcceptableValueRange<float>(0f, 1f)
                )
            );
            ExtraClipList = Config.Bind(
                "Extra Clips",
                "Extra Clip Names",
                "",
                "Comma separated list of AudioClip names to mute or scale."
            );

            ExtraClipVolume = Config.Bind(
                "Extra Clips",
                "Extra Clip Volume",
                1.0f,
                new ConfigDescription(
                    "Independent volume multiplier for listed clip names.",
                    new AcceptableValueRange<float>(0f, 1f)
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

            ExtraSfxList.SettingChanged += (_, __) => AudioSourceFilter.Refresh();
            ExtraSfxVolume.SettingChanged += (_, __) =>
            {
                foreach (var src in UnityEngine.Object.FindObjectsOfType<AudioSource>())
                    AudioSourceFilter.Apply(src);
            };

            ExtraClipList.SettingChanged += (_, __) =>
            {
                AudioSourceFilter.Refresh();
            };

            ExtraClipVolume.SettingChanged += (_, __) =>
            {};

            AudioSourceFilter.Refresh();
            SoundPlayLogger.Init(Config);

            harmony.PatchAll(typeof(AudioSource_DoAll_Patch));
            harmony.PatchAll(typeof(AudioSource_PlayOneShotScale_Patch));
            harmony.PatchAll(typeof(ZSFX_VolumeScale_Patch));
            harmony.PatchAll(typeof(AudioMan_AmbienceVolume_Patch));
            harmony.PatchAll(typeof(SettingsInject));

            Config.ConfigReloaded += OnReloaded;
            Logger.LogInfo("Ambience Sound Config (v2.6.8) loaded.");

        }

        private void OnReloaded(object sender, EventArgs e)
        {
            AudioSourceFilter.Refresh();
            AudioSourceFilter.ApplyToRunningSources();

            ApplyAllVolumes();
            SettingsInject.SyncUI();
        }

        public static void ApplyAllVolumes()
        {
            AudioMan_AmbienceVolume_Patch.ApplyWindVolumeWrapper();
            AudioMan_AmbienceVolume_Patch.ApplyOceanVolumeWrapper();
            AudioMan_AmbienceVolume_Patch.ApplyAmbientLoopVolumeWrapper();
            AudioMan_AmbienceVolume_Patch.ApplyShieldHumVolumeWrapper();
        }
    }
}
