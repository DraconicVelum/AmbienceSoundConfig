using HarmonyLib;
using UnityEngine;

namespace AmbienceSoundConfig
{
    [HarmonyPatch(typeof(AudioSource))]
    internal static class AudioSource_DoAll_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(AudioSource.Play), new System.Type[0])]
        private static void Postfix_Play(AudioSource __instance)
        {
            SoundPlayLogger.Log("SFX", __instance.gameObject.name, __instance.gameObject);
            AudioSourceFilter.Apply(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(AudioSource.Play), new[] { typeof(ulong) })]
        private static void Postfix_PlayDelayed(AudioSource __instance)
        {
            AudioSourceFilter.Apply(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(AudioSource.PlayOneShot), new[] { typeof(AudioClip) })]
        private static void Postfix_PlayOneShot(AudioSource __instance)
        {
            AudioSourceFilter.Apply(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch("set_volume")]
        private static void Prefix_SetVolume(AudioSource __instance, ref float value)
        {
            AudioSourceFilter.Apply(__instance, ref value);
        }
    }
}
