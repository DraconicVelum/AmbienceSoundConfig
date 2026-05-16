using HarmonyLib;
using UnityEngine;

namespace AmbienceSoundConfig
{
    [HarmonyPatch(typeof(AudioSource))]
    internal static class AudioSource_PlayOneShotScale_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(AudioSource.PlayOneShot), new[] { typeof(AudioClip), typeof(float) })]
        private static void Prefix(AudioSource __instance, AudioClip clip, ref float volumeScale)
        {
            SoundPlayLogger.Log("CLIP", clip?.name, __instance ? __instance.gameObject : null);

            if (clip == null)
                return;

            if (!AudioSourceFilter.HasClipTargets)
                return;

            if (!AudioSourceFilter.TryGetClipMultiplier(clip, out float mult))
                return;

            volumeScale = mult <= 0f ? 0f : volumeScale * mult;
        }
        [HarmonyPrefix]
        [HarmonyPatch(nameof(AudioSource.PlayOneShot), new[] { typeof(AudioClip) })]
        private static void Prefix_NoScale(AudioSource __instance, AudioClip clip)
        {
            SoundPlayLogger.Log("CLIP", clip?.name, __instance ? __instance.gameObject : null);
        }
    }
}
