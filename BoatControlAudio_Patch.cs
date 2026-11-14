using HarmonyLib;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace AmbienceSoundConfig
{
    [HarmonyPatch]
    internal static class BoatControlAudio_Patch
    {
        private sealed class BoatEffect { }
        private static ConditionalWeakTable<EffectList, BoatEffect> Effects = new ConditionalWeakTable<EffectList, BoatEffect>();

        internal static void Reset() => Effects = new ConditionalWeakTable<EffectList, BoatEffect>();

        internal static void RegisterEffects(EffectList effects)
        {
            if (effects != null) Effects.GetValue(effects, _ => new BoatEffect());
        }

        private static MethodBase TargetMethod() => AccessTools.Method(typeof(EffectList), "Create");

        private static void Postfix(EffectList __instance, GameObject[] __result)
        {
            if (__result == null || !Effects.TryGetValue(__instance, out _)) return;
            // Stock sail-position effects are muted vibration-only emitters.
            // Leave those untouched; cover audible replacements added by mods.
            foreach (var root in __result)
            {
                if (root == null) continue;
                foreach (var source in root.GetComponentsInChildren<AudioSource>(true))
                    if (!source.mute) AudioVolumeController.RegisterOwner(source, AudioVolumeController.OwnerCategory.Boat, SoundChannel.BoatCrank);
            }
        }
    }
}
