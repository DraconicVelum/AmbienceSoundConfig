using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace AmbienceSoundConfig
{
    // Serialized sound references can point to siblings or detached effects.
    // Do not rely solely on GetComponentInParent to identify their owner.
    [HarmonyPatch]
    internal static class ComponentAudioOwnership_Patch
    {
        private static readonly FieldInfo LowLoops = AccessTools.Field(typeof(ShieldGenerator), "m_lowLoopInstances");
        private sealed class ShipRegistration
        {
            internal GameObject Rudder;
            internal EffectList Effects;
        }
        private sealed class ShieldRegistration
        {
            internal bool Initialized;
            internal GameObject Enabled, Disabled;
            internal readonly List<GameObject> LowLoops = new List<GameObject>();
        }
        private static ConditionalWeakTable<Ship, ShipRegistration> Ships = new ConditionalWeakTable<Ship, ShipRegistration>();
        private static ConditionalWeakTable<ShieldGenerator, ShieldRegistration> Shields = new ConditionalWeakTable<ShieldGenerator, ShieldRegistration>();

        internal static void Reset()
        {
            Ships = new ConditionalWeakTable<Ship, ShipRegistration>();
            Shields = new ConditionalWeakTable<ShieldGenerator, ShieldRegistration>();
        }

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
                if (!Ships.TryGetValue(boat, out var registration)) { registration = new ShipRegistration(); Ships.Add(boat, registration); }
                if (!ReferenceEquals(registration.Rudder, boat.m_rudderObject))
                {
                    registration.Rudder = boat.m_rudderObject;
                    RegisterRoot(boat.m_rudderObject, AudioVolumeController.OwnerCategory.Boat, false, SoundChannel.BoatRudder);
                }
                if (!ReferenceEquals(registration.Effects, boat.m_changeSailPosEffect))
                {
                    registration.Effects = boat.m_changeSailPosEffect;
                    BoatControlAudio_Patch.RegisterEffects(boat.m_changeSailPosEffect);
                }
            }
        }

        private static void Postfix(object __instance)
        {
            if (__instance is ShieldGenerator generator)
            {
                if (!Shields.TryGetValue(generator, out var registration)) { registration = new ShieldRegistration(); Shields.Add(generator, registration); }
                if (!registration.Initialized) RegisterRoot(generator.gameObject, AudioVolumeController.OwnerCategory.Shield, true);
                if (!registration.Initialized || !ReferenceEquals(registration.Enabled, generator.m_enabledObject))
                    RegisterRoot(generator.m_enabledObject, AudioVolumeController.OwnerCategory.Shield, true);
                if (!registration.Initialized || !ReferenceEquals(registration.Disabled, generator.m_disabledObject))
                    RegisterRoot(generator.m_disabledObject, AudioVolumeController.OwnerCategory.Shield, true);
                registration.Initialized = true;
                registration.Enabled = generator.m_enabledObject;
                registration.Disabled = generator.m_disabledObject;
                var instances = LowLoops?.GetValue(generator) as GameObject[];
                int count = instances != null ? instances.Length : 0;
                for (int index = 0; index < count; index++)
                {
                    var root = instances[index];
                    if (index < registration.LowLoops.Count && ReferenceEquals(registration.LowLoops[index], root)) continue;
                    RegisterRoot(root, AudioVolumeController.OwnerCategory.Shield, true, SoundChannel.ShieldLowFuel);
                    if (index < registration.LowLoops.Count) registration.LowLoops[index] = root;
                    else registration.LowLoops.Add(root);
                }
                if (registration.LowLoops.Count > count) registration.LowLoops.RemoveRange(count, registration.LowLoops.Count - count);
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
