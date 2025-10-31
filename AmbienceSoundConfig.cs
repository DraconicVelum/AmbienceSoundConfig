using BepInEx;
using BepInEx.Configuration;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace AmbienceSoundConfig
{
    [BepInPlugin("com.draconicvelum.ambiencesoundconfig", "Ambience Sound Config", "2.0.2")]
    public class AmbienceSoundConfig : BaseUnityPlugin
    {
        // Config entries (float sliders)
        public static ConfigEntry<float> MasterVolume;
        public static ConfigEntry<float> WindVolume;
        public static ConfigEntry<float> OceanVolume;
        public static ConfigEntry<float> AmbientLoopVolume;
        public static ConfigEntry<float> ShieldHumVolume;

        private static readonly Harmony harmony = new Harmony("com.draconicvelum.ambiencesoundconfig");
        private static bool _configManagerInstalled;

        private void Awake()
        {
            _configManagerInstalled = Chainloader.PluginInfos.ContainsKey("com.bepinex.configurationmanager");

            // Main volume sliders
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

            // Live slider updates (instant)
            MasterVolume.SettingChanged += (_, __) => ApplyAllVolumes();
            WindVolume.SettingChanged += (_, __) => ApplyVolume("Wind");
            OceanVolume.SettingChanged += (_, __) => ApplyVolume("Ocean");
            AmbientLoopVolume.SettingChanged += (_, __) => ApplyVolume("Ambient");
            ShieldHumVolume.SettingChanged += (_, __) => ApplyVolume("Shield");

            harmony.PatchAll();

            Logger.LogInfo("Ambience Sound Config (Volume Edition) loaded.");
            Logger.LogInfo(_configManagerInstalled
                ? "Configuration Manager detected — sliders update in real time."
                : "Configuration Manager not found — volumes applied from config file on load.");
            LogVolumes();
        }

        private void ApplyAllVolumes()
        {
            ApplyVolume("Wind");
            ApplyVolume("Ocean");
            ApplyVolume("Ambient");
            ApplyVolume("Shield");
            Config.Save();
            Logger.LogInfo($"[MasterVolume] set to {MasterVolume.Value:0.00}");
        }

        private void ApplyVolume(string type)
        {
            if (AudioMan.instance == null)
                return;

            var flags = BindingFlags.NonPublic | BindingFlags.Instance;

            void SetVolume(string fieldName, float baseVol)
            {
                var field = typeof(AudioMan).GetField(fieldName, flags);
                if (field?.GetValue(AudioMan.instance) is AudioSource source)
                {
                    source.volume = Mathf.Clamp01(baseVol * MasterVolume.Value);
                }
            }

            switch (type)
            {
                case "Wind": SetVolume("m_windLoopSource", WindVolume.Value); break;
                case "Ocean": SetVolume("m_oceanAmbientSource", OceanVolume.Value); break;
                case "Ambient": SetVolume("m_ambientLoopSource", AmbientLoopVolume.Value); break;
                case "Shield": SetVolume("m_shieldHumSource", ShieldHumVolume.Value); break;
            }

            Config.Save();
            Logger.LogInfo($"[{type}] volume applied ({GetVolume(type):0.00} × Master {MasterVolume.Value:0.00})");
        }

        private float GetVolume(string type)
        {
            return type switch
            {
                "Wind" => WindVolume.Value,
                "Ocean" => OceanVolume.Value,
                "Ambient" => AmbientLoopVolume.Value,
                "Shield" => ShieldHumVolume.Value,
                _ => 1f
            };
        }

        private void LogVolumes()
        {
            Logger.LogInfo(
                $"Master: {MasterVolume.Value:0.00}, Wind: {WindVolume.Value:0.00}, Ocean: {OceanVolume.Value:0.00}, Ambient: {AmbientLoopVolume.Value:0.00}, Shield: {ShieldHumVolume.Value:0.00}");
        }
    }

    [HarmonyPatch(typeof(AudioMan))]
    public static class AudioMan_AmbienceVolume_Patch
    {
        [HarmonyPostfix, HarmonyPatch("UpdateWindAmbience")]
        static void ApplyWindVolume(ref AudioSource ___m_windLoopSource)
        {
            if (___m_windLoopSource)
                ___m_windLoopSource.volume = AmbienceSoundConfig.WindVolume.Value * AmbienceSoundConfig.MasterVolume.Value;
        }

        [HarmonyPostfix, HarmonyPatch("UpdateOceanAmbiance")]
        static void ApplyOceanVolume(ref AudioSource ___m_oceanAmbientSource)
        {
            if (___m_oceanAmbientSource)
                ___m_oceanAmbientSource.volume = AmbienceSoundConfig.OceanVolume.Value * AmbienceSoundConfig.MasterVolume.Value;
        }

        [HarmonyPostfix, HarmonyPatch("UpdateAmbientLoop")]
        static void ApplyAmbientLoopVolume(ref AudioSource ___m_ambientLoopSource)
        {
            if (___m_ambientLoopSource)
                ___m_ambientLoopSource.volume = AmbienceSoundConfig.AmbientLoopVolume.Value * AmbienceSoundConfig.MasterVolume.Value;
        }

        [HarmonyPostfix, HarmonyPatch("UpdateShieldHum")]
        static void ApplyShieldHumVolume(ref AudioSource ___m_shieldHumSource)
        {
            if (___m_shieldHumSource)
                ___m_shieldHumSource.volume = AmbienceSoundConfig.ShieldHumVolume.Value * AmbienceSoundConfig.MasterVolume.Value;
        }
    }
}