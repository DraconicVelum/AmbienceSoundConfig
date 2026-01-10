using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using UnityEngine;

namespace AmbienceSoundConfig
{
    [BepInPlugin("com.draconicvelum.ambiencesoundconfig", "Ambience Sound Config", "2.5.0")]
    public class AmbienceSoundConfig : BaseUnityPlugin
    {
        public static ConfigEntry<float> MasterVolume;
        public static ConfigEntry<float> WindVolume;
        public static ConfigEntry<float> OceanVolume;
        public static ConfigEntry<float> AmbientLoopVolume;
        public static ConfigEntry<float> ShieldHumVolume;
        public static ConfigEntry<string> ExtraSfxList;
        public static ConfigEntry<float> ExtraSfxVolume;

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
            ExtraSfxList.SettingChanged += (_, __) => AudioSourceFilter.Refresh();
            ExtraSfxVolume.SettingChanged += (_, __) =>
            {
                foreach (var src in UnityEngine.Object.FindObjectsOfType<AudioSource>())
                    AudioSourceFilter.Apply(src);
            };
            AudioSourceFilter.Refresh();

            harmony.PatchAll(typeof(AudioSource_DoAll_Patch));

            harmony.PatchAll(typeof(AudioMan_AmbienceVolume_Patch));
            harmony.PatchAll(typeof(SettingsInject));

            Config.ConfigReloaded += OnReloaded;

            Logger.LogInfo("Ambience Sound Config (v2.5.0) loaded.");
        }

        private void OnReloaded(object sender, EventArgs e)
        {
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
