using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AmbienceSoundConfig
{
    // Serialized sound references can point to siblings or detached effects.
    // Do not rely solely on GetComponentInParent to identify their owner.
    [HarmonyPatch]
    internal static class ComponentAudioOwnership_Patch
    {
        private static readonly FieldInfo LowLoops = AccessTools.Field(typeof(ShieldGenerator), "m_lowLoopInstances");

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ShipEffects), "Awake");
            yield return AccessTools.Method(typeof(Ship), "Awake");
            yield return AccessTools.Method(typeof(Ship), "UpdateSailSize");
            yield return AccessTools.Method(typeof(ShieldGenerator), "Start");
            yield return AccessTools.Method(typeof(ShieldGenerator), "UpdateShield");
        }

        private static void Prefix(object __instance)
        {
            // Register before Awake reads the prefab volumes used as fade targets.
            if (__instance is ShipEffects ship) RegisterShip(ship);
            if (__instance is Ship boat)
            {
                RegisterRoot(boat.m_rudderObject, AudioVolumeController.OwnerCategory.Boat, false, SoundChannel.BoatRudder);
                BoatControlAudio_Patch.RegisterEffects(boat.m_changeSailPosEffect);
            }
        }

        private static void Postfix(object __instance)
        {
            if (__instance is ShieldGenerator generator)
            {
                RegisterRoot(generator.gameObject, AudioVolumeController.OwnerCategory.Shield, true);
                RegisterRoot(generator.m_enabledObject, AudioVolumeController.OwnerCategory.Shield, true);
                RegisterRoot(generator.m_disabledObject, AudioVolumeController.OwnerCategory.Shield, true);
                var instances = LowLoops?.GetValue(generator) as GameObject[];
                if (instances != null)
                    foreach (var root in instances)
                        RegisterRoot(root, AudioVolumeController.OwnerCategory.Shield, true, SoundChannel.ShieldLowFuel);
            }
        }

        private static void RegisterShip(ShipEffects ship)
        {
            RegisterRoot(ship.m_inWaterSoundRoot, AudioVolumeController.OwnerCategory.Boat, false, SoundChannel.BoatWater);
            RegisterRoot(ship.m_wakeSoundRoot, AudioVolumeController.OwnerCategory.Boat, false, SoundChannel.BoatWake);
            RegisterRoot(ship.m_splashEffects, AudioVolumeController.OwnerCategory.Boat, true, SoundChannel.BoatSplash);
            AudioVolumeController.RegisterOwner(ship.m_sailSound, AudioVolumeController.OwnerCategory.Boat, SoundChannel.BoatSails);
        }

        internal static void RegisterRoot(GameObject root, AudioVolumeController.OwnerCategory category, bool loopsOnly, SoundChannel? channel = null)
        {
            if (root == null) return;
            foreach (var source in root.GetComponentsInChildren<AudioSource>(true))
                if (!loopsOnly || source.loop) AudioVolumeController.RegisterOwner(source, category, channel);
        }
    }
}
