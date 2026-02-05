using HarmonyLib;
using UnityEngine;

namespace AmbienceSoundConfig
{
    [HarmonyPatch(typeof(ZSFX))]
    internal static class ZSFX_VolumeScale_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(ZSFX.CustomUpdate))]
        private static void Postfix(ZSFX __instance)
        {
            if (!AudioSourceFilter.HasClipTargets)
                return;

            var src = __instance.GetComponent<AudioSource>();

            if (src == null || src.clip == null)
                return;

            if (src.isPlaying)
                SoundPlayLogger.Log("ZSFX", src.clip.name, __instance.gameObject);

            float mult = 1f;

            if (AudioSourceFilter.ShouldMuteClip(src.clip))
                mult = AudioSourceFilter.ClipMultiplier;

            __instance.SetVolumeModifier(mult <= 0f ? 0f : mult);
        }
    }
}