using HarmonyLib;
using UnityEngine;

namespace AmbienceSoundConfig
{
    [HarmonyPatch(typeof(ZSFX))]
    internal static class ZSFX_VolumeScale_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(ZSFX.Play))]
        private static void Postfix(ZSFX __instance)
        {
            var src = __instance.GetComponent<AudioSource>();

            if (src == null || src.clip == null)
                return;

            if (src.isPlaying)
                SoundPlayLogger.Log("ZSFX", src.clip.name, __instance.gameObject);

            SoundPlayLogger.Log("SFX", __instance.gameObject.name, __instance.gameObject);
            AudioVolumeController.Apply(src);
        }
    }
}
