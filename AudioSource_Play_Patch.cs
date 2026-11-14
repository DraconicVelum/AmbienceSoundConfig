using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AmbienceSoundConfig
{
    // These public entry points have managed bodies in the supplied Unity build.
    // Apply before playback so short, non-ZSFX sounds also use the custom filters.
    [HarmonyPatch]
    internal static class AudioSource_Play_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(AudioSource), nameof(AudioSource.Play), new System.Type[0]);
            yield return AccessTools.Method(typeof(AudioSource), nameof(AudioSource.Play), new[] { typeof(ulong) });
            yield return AccessTools.Method(typeof(AudioSource), nameof(AudioSource.PlayDelayed), new[] { typeof(float) });
            yield return AccessTools.Method(typeof(AudioSource), nameof(AudioSource.PlayScheduled), new[] { typeof(double) });
        }

        private static void Prefix(AudioSource __instance)
        {
            SoundPlayLogger.Log("SFX", __instance.gameObject.name, __instance.gameObject);
            SoundPlayLogger.Log("CLIP", __instance.clip?.name, __instance.gameObject);
            AudioVolumeController.Apply(__instance);
        }
    }
}
