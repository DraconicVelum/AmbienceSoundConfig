using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AmbienceSoundConfig
{
    internal static class AudioVolumeController
    {
        internal enum OwnerCategory { Boat, Shield }
        private sealed class VolumeState
        {
            internal float GameVolume;
            internal float OutputVolume;

        }

        private static readonly Dictionary<AudioSource, VolumeState> States = new Dictionary<AudioSource, VolumeState>();
        private static readonly Dictionary<AudioSource, OwnerCategory> Owners = new Dictionary<AudioSource, OwnerCategory>();
        private static readonly Dictionary<AudioSource, SoundChannel> Channels = new Dictionary<AudioSource, SoundChannel>();
        private static readonly FieldInfo Wind = AccessTools.Field(typeof(AudioMan), "m_windLoopSource");
        private static readonly FieldInfo Ocean = AccessTools.Field(typeof(AudioMan), "m_oceanAmbientSource");
        private static readonly FieldInfo Ambient = AccessTools.Field(typeof(AudioMan), "m_ambientLoopSource");
        private static readonly FieldInfo Shield = AccessTools.Field(typeof(AudioMan), "m_shieldHumSource");
        private static float nextScan;

        private static VolumeState GetState(AudioSource source)
        {
            if (!States.TryGetValue(source, out var state))
            {
                state = new VolumeState { GameVolume = source.volume, OutputVolume = source.volume };
                States.Add(source, state);
            }
            // Honour writes from callers outside the patched game methods.
            if (!Mathf.Approximately(source.volume, state.OutputVolume))
                state.GameVolume = source.volume;
            return state;
        }

        internal static float GetGameVolume(AudioSource source) => GetState(source).GameVolume;

        internal static void SetGameVolume(AudioSource source, float volume)
        {
            var state = GetState(source);
            state.GameVolume = Mathf.Clamp01(volume);
            Apply(source, state);
        }

        internal static void Apply(AudioSource source)
        {
            if (source != null) Apply(source, GetState(source));
        }

        internal static void RegisterOwner(AudioSource source, OwnerCategory category, SoundChannel? channel = null)
        {
            if (source == null) return;
            var selected = channel ?? (category == OwnerCategory.Boat ? SoundChannel.BoatWater : SoundChannel.ShieldGenerator);
            if (Owners.TryGetValue(source, out var previousOwner) && previousOwner == category &&
                Channels.TryGetValue(source, out var previousChannel) && Priority(previousChannel) > Priority(selected))
                selected = previousChannel;
            Owners[source] = category;
            Channels[source] = selected;
            Apply(source);
        }

        private static int Priority(SoundChannel channel)
        {
            switch (channel)
            {
                case SoundChannel.BoatSails: case SoundChannel.BoatCrank: case SoundChannel.BoatRudder: case SoundChannel.ShieldLowFuel: return 2;
                case SoundChannel.BoatWake: case SoundChannel.BoatSplash: return 1;
                default: return 0;
            }
        }

        private static void Apply(AudioSource source, VolumeState state)
        {
            float multiplier = GetAmbienceMultiplier(source);
            if (AudioSourceFilter.TryGetSfxMultiplier(source.gameObject.name, out float sfx)) multiplier *= sfx;
            if (AudioSourceFilter.TryGetClipMultiplier(source.clip, out float clip)) multiplier *= clip;
            state.OutputVolume = Mathf.Clamp01(state.GameVolume * multiplier);
            source.volume = state.OutputVolume;
        }

        private static bool IsSource(FieldInfo field, AudioSource source)
        {
            return AudioMan.instance != null && (field?.GetValue(AudioMan.instance) as AudioSource) == source;
        }

        internal static float GetAmbienceMultiplier(AudioSource source)
        {

            if (Owners.TryGetValue(source, out var owner))
                return (owner == OwnerCategory.Boat ? AmbienceSoundConfig.BoatVolume.Value * Individual(source, SoundGroup.Boat, Channels[source]) : AmbienceSoundConfig.ShieldHumVolume.Value * Individual(source, SoundGroup.Shield, Channels[source]));
            if (IsSource(Wind, source)) return AmbienceSoundConfig.WindVolume.Value * Individual(source, SoundGroup.Wind, SoundChannel.Wind);
            if (IsSource(Ocean, source)) return AmbienceSoundConfig.OceanVolume.Value * Individual(source, SoundGroup.Ocean, SoundChannel.Ocean);
            if (IsSource(Shield, source)) return AmbienceSoundConfig.ShieldHumVolume.Value * Individual(source, SoundGroup.Shield, SoundChannel.ShieldDome);
            if (source.loop && source.GetComponentInParent<ShieldGenerator>() != null)
                return AmbienceSoundConfig.ShieldHumVolume.Value * Individual(source, SoundGroup.Shield, SoundChannel.ShieldGenerator);
            if (ConfigFileMaintenance.IsMainMenuName(source.gameObject.name))
                return AmbienceSoundConfig.AmbientLoopVolume.Value * Individual(source, SoundGroup.Ambient, SoundChannel.MainMenu);
            if (IsSource(Ambient, source))
            {
                float ocean = IsOceanLoop(source) ? AmbienceSoundConfig.OceanVolume.Value * Individual(source, SoundGroup.Ocean, SoundChannel.OceanBackground) : 1f;
                return AmbienceSoundConfig.AmbientLoopVolume.Value * Individual(source, SoundGroup.Ambient, SoundChannel.AmbientBackground) * ocean;
            }
            var ship = source.GetComponentInParent<ShipEffects>();
            if (ship != null)
            {
                SoundChannel? channel = source == ship.m_sailSound ? SoundChannel.BoatSails :
                    IsUnder(source.transform, ship.m_wakeSoundRoot) ? SoundChannel.BoatWake :
                    IsUnder(source.transform, ship.m_inWaterSoundRoot) ? SoundChannel.BoatWater :
                    source.loop && IsUnder(source.transform, ship.m_splashEffects) ? SoundChannel.BoatSplash : (SoundChannel?)null;
                return channel.HasValue ? AmbienceSoundConfig.BoatVolume.Value * Individual(source, SoundGroup.Boat, channel.Value) : 1f;
            }
            if (source.loop && IsOceanLoop(source))
                return AmbienceSoundConfig.OceanVolume.Value * Individual(source, SoundGroup.Ocean, SoundChannel.ShoreWaves);
            return 1f;
        }

        private static float Individual(AudioSource source, SoundGroup group, SoundChannel channel)
        {
            if (!SoundSettings.IsInitialized) return 1f;
            return SoundSettings.ChannelMultiplier(channel) * SoundSettings.SavedMultiplier(group, source.gameObject.name, source.clip != null ? source.clip.name : null);
        }
        private static bool IsUnder(Transform source, GameObject root)
        {
            return root != null && source.IsChildOf(root.transform);
        }

        private static bool IsOceanLoop(AudioSource source)
        {
            return HasOceanName(source.gameObject.name) || HasOceanName(source.clip != null ? source.clip.name : null);
        }

        private static bool HasOceanName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf("ocean", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("shore", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("wave", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static void Tick()
        {
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.5f;
                foreach (var source in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                {
                    if (source.isPlaying && source.clip != null)
                        SoundPlayLogger.Log("CLIP", source.clip.name, source.gameObject);
                    // Include autoplay loops with no ZSFX component.
                    if (GetAmbienceMultiplier(source) != 1f ||
                        AudioSourceFilter.TryGetSfxMultiplier(source.gameObject.name, out _) ||
                        AudioSourceFilter.TryGetClipMultiplier(source.clip, out _)) Apply(source);
                }
            }
            Refresh();
        }

        internal static void Refresh()
        {
            var dead = new List<AudioSource>();
            foreach (var pair in States)
            {
                if (pair.Key == null) dead.Add(pair.Key);
                else Apply(pair.Key, GetState(pair.Key));
            }
            foreach (var source in dead)
            {
                States.Remove(source);
                Owners.Remove(source);
                Channels.Remove(source);
            }
        }

        internal static void Restore()
        {
            foreach (var pair in States)
                if (pair.Key != null) pair.Key.volume = pair.Value.GameVolume;
            States.Clear();
            Owners.Clear();
            Channels.Clear();
        }
    }
}

