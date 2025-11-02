using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AmbienceSoundConfig
{
    [HarmonyPatch(typeof(AudioMan))]
    public static class AudioMan_AmbienceVolume_Patch
    {
        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Dictionary<string, FieldInfo> CachedFields = new Dictionary<string, FieldInfo>();

        private static AudioSource GetPrivateSource(string fieldName)
        {
            if (AudioMan.instance == null)
                return null;

            if (!CachedFields.TryGetValue(fieldName, out var field))
            {
                field = typeof(AudioMan).GetField(fieldName, Flags);
                if (field != null)
                    CachedFields[fieldName] = field;
            }

            return field?.GetValue(AudioMan.instance) as AudioSource;
        }

        public static void ApplyWindVolumeWrapper()
        {
            var src = GetPrivateSource("m_windLoopSource");
            if (src != null)
                src.volume = AmbienceSoundConfig.WindVolume.Value * AmbienceSoundConfig.MasterVolume.Value;
        }

        public static void ApplyOceanVolumeWrapper()
        {
            var src = GetPrivateSource("m_oceanAmbientSource");
            if (src != null)
                src.volume = AmbienceSoundConfig.OceanVolume.Value * AmbienceSoundConfig.MasterVolume.Value;
        }

        public static void ApplyAmbientLoopVolumeWrapper()
        {
            var src = GetPrivateSource("m_ambientLoopSource");
            if (src != null)
                src.volume = AmbienceSoundConfig.AmbientLoopVolume.Value * AmbienceSoundConfig.MasterVolume.Value;
        }

        public static void ApplyShieldHumVolumeWrapper()
        {
            var src = GetPrivateSource("m_shieldHumSource");
            if (src != null)
                src.volume = AmbienceSoundConfig.ShieldHumVolume.Value * AmbienceSoundConfig.MasterVolume.Value;
        }

        [HarmonyPostfix, HarmonyPatch("UpdateWindAmbience")]
        public static void ApplyWindVolume(ref AudioSource ___m_windLoopSource) => ApplyWindVolumeWrapper();

        [HarmonyPostfix, HarmonyPatch("UpdateOceanAmbiance")]
        public static void ApplyOceanVolume(ref AudioSource ___m_oceanAmbientSource) => ApplyOceanVolumeWrapper();

        [HarmonyPostfix, HarmonyPatch("UpdateAmbientLoop")]
        public static void ApplyAmbientLoopVolume(ref AudioSource ___m_ambientLoopSource) => ApplyAmbientLoopVolumeWrapper();

        [HarmonyPostfix, HarmonyPatch("UpdateShieldHum")]
        public static void ApplyShieldHumVolume(ref AudioSource ___m_shieldHumSource) => ApplyShieldHumVolumeWrapper();
    }
}