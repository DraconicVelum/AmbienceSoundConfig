using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
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
            internal float Multiplier = 1f;
            internal int Revision = -1;
            internal AudioClip Clip;
            internal AudioMan AudioManager;
            internal bool Controlled;
        }

        private static readonly Dictionary<AudioSource, VolumeState> States = new Dictionary<AudioSource, VolumeState>();
        // Neutral sources may pass through ZSFX's volume hooks every frame.
        // Cache their classification without retaining them or polling them.
        private static ConditionalWeakTable<AudioSource, VolumeState> Cache = new ConditionalWeakTable<AudioSource, VolumeState>();
        private static readonly List<AudioSource> RefreshSources = new List<AudioSource>();
        private static readonly Dictionary<AudioSource, OwnerCategory> Owners = new Dictionary<AudioSource, OwnerCategory>();
        private static readonly Dictionary<AudioSource, SoundChannel> Channels = new Dictionary<AudioSource, SoundChannel>();
        private static readonly FieldInfo Wind = AccessTools.Field(typeof(AudioMan), "m_windLoopSource");
        private static readonly FieldInfo Ocean = AccessTools.Field(typeof(AudioMan), "m_oceanAmbientSource");
        private static readonly FieldInfo Ambient = AccessTools.Field(typeof(AudioMan), "m_ambientLoopSource");
        private static readonly FieldInfo Shield = AccessTools.Field(typeof(AudioMan), "m_shieldHumSource");
        private static float nextScan;
        private static float nextRefresh;
        private static int revision;

        private static VolumeState GetState(AudioSource source)
        {
            if (!Cache.TryGetValue(source, out var state))
            {
                state = new VolumeState { GameVolume = source.volume, OutputVolume = source.volume };
                Cache.Add(source, state);
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
            if (source == null) return;
            var state = GetState(source);
            // Playback may reuse a pooled emitter with a different name/parent.
            state.Revision = -1;
            Apply(source, state);
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
            var currentClip = source.clip;
            if (state.Revision != revision || !ReferenceEquals(state.Clip, currentClip) ||
                !ReferenceEquals(state.AudioManager, AudioMan.instance))
            {
                state.Controlled = TryGetAmbienceMultiplier(source, out float multiplier);
                if (AudioSourceFilter.TryGetSfxMultiplier(source.gameObject.name, out float sfx)) { multiplier *= sfx; state.Controlled = true; }
                if (AudioSourceFilter.TryGetClipMultiplier(currentClip, out float clip)) { multiplier *= clip; state.Controlled = true; }
                state.Multiplier = multiplier;
                state.Clip = currentClip;
                state.AudioManager = AudioMan.instance;
                state.Revision = revision;
                if (state.Controlled) { if (!States.ContainsKey(source)) States.Add(source, state); }
                else States.Remove(source);
            }
            state.OutputVolume = Mathf.Clamp01(state.GameVolume * state.Multiplier);
            // Unchanged loops do not need another native volume setter call.
            if (source.volume != state.OutputVolume) source.volume = state.OutputVolume;
        }

        private static bool IsSource(FieldInfo field, AudioSource source)
        {
            return AudioMan.instance != null && (field?.GetValue(AudioMan.instance) as AudioSource) == source;
        }

        internal static float GetAmbienceMultiplier(AudioSource source)
        {
            TryGetAmbienceMultiplier(source, out float multiplier);
            return multiplier;
        }

        private static bool TryGetAmbienceMultiplier(AudioSource source, out float multiplier)
        {
            multiplier = 1f;
            if (Owners.TryGetValue(source, out var owner))
            { multiplier = owner == OwnerCategory.Boat ? AmbienceSoundConfig.BoatVolume.Value * Individual(source, SoundGroup.Boat, Channels[source]) : AmbienceSoundConfig.ShieldHumVolume.Value * Individual(source, SoundGroup.Shield, Channels[source]); return true; }
            if (IsSource(Wind, source)) { multiplier = AmbienceSoundConfig.WindVolume.Value * Individual(source, SoundGroup.Wind, SoundChannel.Wind); return true; }
            if (IsSource(Ocean, source)) { multiplier = AmbienceSoundConfig.OceanVolume.Value * Individual(source, SoundGroup.Ocean, SoundChannel.Ocean); return true; }
            if (IsSource(Shield, source)) { multiplier = AmbienceSoundConfig.ShieldHumVolume.Value * Individual(source, SoundGroup.Shield, SoundChannel.ShieldDome); return true; }
            if (source.loop && source.GetComponentInParent<ShieldGenerator>() != null)
            { multiplier = AmbienceSoundConfig.ShieldHumVolume.Value * Individual(source, SoundGroup.Shield, SoundChannel.ShieldGenerator); return true; }
            if (ConfigFileMaintenance.IsMainMenuName(source.gameObject.name))
            { multiplier = AmbienceSoundConfig.AmbientLoopVolume.Value * Individual(source, SoundGroup.Ambient, SoundChannel.MainMenu); return true; }
            if (IsSource(Ambient, source))
            {
                float ocean = IsOceanLoop(source) ? AmbienceSoundConfig.OceanVolume.Value * Individual(source, SoundGroup.Ocean, SoundChannel.OceanBackground) : 1f;
                multiplier = AmbienceSoundConfig.AmbientLoopVolume.Value * Individual(source, SoundGroup.Ambient, SoundChannel.AmbientBackground) * ocean;
                return true;
            }
            var ship = source.GetComponentInParent<ShipEffects>();
            if (ship != null)
            {
                SoundChannel? channel = source == ship.m_sailSound ? SoundChannel.BoatSails :
                    IsUnder(source.transform, ship.m_wakeSoundRoot) ? SoundChannel.BoatWake :
                    IsUnder(source.transform, ship.m_inWaterSoundRoot) ? SoundChannel.BoatWater :
                    source.loop && IsUnder(source.transform, ship.m_splashEffects) ? SoundChannel.BoatSplash : (SoundChannel?)null;
                if (!channel.HasValue) return false;
                multiplier = AmbienceSoundConfig.BoatVolume.Value * Individual(source, SoundGroup.Boat, channel.Value);
                return true;
            }
            if (source.loop && IsOceanLoop(source))
            { multiplier = AmbienceSoundConfig.OceanVolume.Value * Individual(source, SoundGroup.Ocean, SoundChannel.ShoreWaves); return true; }
            return false;
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
                nextScan = Time.unscaledTime + 5f;
                foreach (var source in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                {
                    if (SoundPlayLogger.Enabled && source.isPlaying && source.clip != null)
                        SoundPlayLogger.Log("CLIP", source.clip.name, source.gameObject);
                    // Coarse fallback for playOnAwake and emitters reparented
                    // without a managed Play call. Never do this every frame.
                    Apply(source);
                }
            }
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + .1f;
                RefreshTracked();
            }
        }

        internal static void Refresh()
        {
            Invalidate();
            RefreshTracked();
        }

        internal static void Invalidate() { unchecked { revision++; } }

        private static void RefreshTracked()
        {
            // Reuse storage, and allow a renamed/reconfigured source to leave
            // the tracked set while its old output volume is restored.
            RefreshSources.Clear();
            foreach (var source in States.Keys) RefreshSources.Add(source);
            foreach (var source in RefreshSources)
            {
                if (source == null)
                {
                    States.Remove(source); Owners.Remove(source); Channels.Remove(source);
                }
                else Apply(source, GetState(source));
            }
            RefreshSources.Clear();
        }

        internal static void Restore()
        {
            foreach (var pair in States)
                if (pair.Key != null) pair.Key.volume = pair.Value.GameVolume;
            States.Clear();
            Cache = new ConditionalWeakTable<AudioSource, VolumeState>();
            RefreshSources.Clear();
            Owners.Clear();
            Channels.Clear();
            nextScan = nextRefresh = 0f;
        }
    }
}

