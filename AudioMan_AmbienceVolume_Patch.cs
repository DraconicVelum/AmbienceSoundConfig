using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace AmbienceSoundConfig
{
    // Redirect managed volume reads and writes: fades must see the unscaled volume.
    [HarmonyPatch]
    public static class AudioMan_AmbienceVolume_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(AudioMan), "Awake");
            yield return AccessTools.Method(typeof(AudioMan), "UpdateWindAmbience");
            yield return AccessTools.Method(typeof(AudioMan), "UpdateOceanAmbiance");
            yield return AccessTools.Method(typeof(AudioMan), "UpdateAmbientLoop");
            yield return AccessTools.Method(typeof(ShipEffects), "Awake");
            yield return AccessTools.Method(typeof(ShipEffects), "FadeSound");
            yield return AccessTools.Method(typeof(ZSFX), "Play");
            yield return AccessTools.Method(typeof(ZSFX), "CustomUpdate");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var getter = AccessTools.PropertyGetter(typeof(AudioSource), "volume");
            var setter = AccessTools.PropertySetter(typeof(AudioSource), "volume");
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(getter))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(AudioVolumeController), nameof(AudioVolumeController.GetGameVolume));
                }
                else if (instruction.Calls(setter))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(AudioVolumeController), nameof(AudioVolumeController.SetGameVolume));
                }
                yield return instruction;
            }
        }

        public static void ApplyWindVolumeWrapper() => AudioVolumeController.Refresh();
        public static void ApplyOceanVolumeWrapper() => AudioVolumeController.Refresh();
        public static void ApplyAmbientLoopVolumeWrapper() => AudioVolumeController.Refresh();
        public static void ApplyShieldHumVolumeWrapper() => AudioVolumeController.Refresh();
    }
}
