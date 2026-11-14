using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AmbienceSoundConfig
{
    internal enum SoundGroup { Wind, Ocean, Boat, Ambient, Shield }

    // Independent of Unity: persistence, legacy import and row editing can be
    // tested with the actual BepInEx ConfigFile outside the game.
    internal static class SoundSettings
    {
        internal sealed class Sound
        {
            internal SoundGroup Group;
            internal string Name;
            internal ConfigEntry<float> Volume;
            internal bool IsChannel;
        }

        internal sealed class ExtraRow
        {
            internal string Id;
            internal bool IsClip;
            internal ConfigEntry<string> Name;
            internal ConfigEntry<float> Volume;
            internal ConfigEntry<int> Order;
        }

        private static ConfigFile config;
        private static Func<float> maximum;
        private static Action changed;
        private static readonly Dictionary<string, Sound> Sounds = new Dictionary<string, Sound>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<ExtraRow> Rows = new List<ExtraRow>();
        private static readonly Dictionary<SoundChannel, Sound> Controls = new Dictionary<SoundChannel, Sound>();
        internal static bool IsInitialized => config != null;
        internal static bool MigratedLegacy { get; private set; }
        internal static int Revision { get; private set; }

        internal static void Initialize(ConfigFile file, Func<float> getMaximum, Action onChanged,
            ConfigEntry<string> legacySfx, ConfigEntry<float> sfxMaster,
            ConfigEntry<string> legacyClips, ConfigEntry<float> clipMaster, bool skipLegacyMainMenu)
        {
            config = file;
            maximum = getMaximum;
            changed = onChanged;
            Sounds.Clear();
            Controls.Clear();
            Rows.Clear();
            var schema = config.Bind("General", "Sound Rows Version", 0, "Internal version of the expandable per-sound controls.");
            MigratedLegacy = schema.Value < 1;
            LoadSavedSounds();
            SeedControls();
            LoadRows();
            if (schema.Value < 1)
            {
                string backup = file.ConfigFilePath + ".pre-2.9.0.bak";
                if (File.Exists(file.ConfigFilePath) && !File.Exists(backup)) File.Copy(file.ConfigFilePath, backup);
                Import(false, legacySfx.Value, sfxMaster, skipLegacyMainMenu);
                Import(true, legacyClips.Value, clipMaster, false);
                legacySfx.Value = "";
                legacyClips.Value = "";
                schema.Value = 1;
            }
            ClampAll();
            Touch();
        }

        internal static void Reload()
        {
            if (!IsInitialized) return;
            LoadRows();
            LoadSavedSounds();
            SeedControls();
            ClampAll();
            Touch();
        }

        internal static void Reset()
        {
            config = null;
            changed = null;
            maximum = null;
            Sounds.Clear();
            Controls.Clear();
            Rows.Clear();
            MigratedLegacy = false;
            Revision++;
        }

        private static string Section(bool clips) => clips ? "Extra Clips Rows" : "Extra SFX Rows";
        private static string SoundSection(SoundGroup group) => "Sounds - " + group;
        private static string Encode(string name) => Convert.ToBase64String(Encoding.UTF8.GetBytes(name)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        private static string Decode(string key)
        {
            string encoded = key.Replace('-', '+').Replace('_', '/');
            return Encoding.UTF8.GetString(Convert.FromBase64String(encoded.PadRight((encoded.Length + 3) / 4 * 4, '=')));
        }

        internal static string SoundName(string source, string clip)
        {
            source = Normalize(source);
            return string.IsNullOrWhiteSpace(clip) ? source : source + " / " + clip.Trim();
        }

        internal static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            return name.Replace("(Clone)", "").Trim();
        }

        internal static Sound Observe(SoundGroup group, string source, string clip)
        {
            if (!IsInitialized || string.IsNullOrWhiteSpace(clip)) return null;
            return BindSound(group, SoundName(source, clip));
        }

        private static Sound BindSound(SoundGroup group, string name, string description = null)
        {
            string key = group + "|" + name;
            if (Sounds.TryGetValue(key, out var sound)) return sound;
            sound = new Sound { Group = group, Name = name, Volume = config.Bind(SoundSection(group), Encode(name), 1f,
                new ConfigDescription("Individual multiplier: " + (description ?? name), new AcceptableValueRange<float>(0f, 5f))) };
            sound.Volume.Value = Clamp(sound.Volume.Value);
            Sounds.Add(key, sound);
            Touch();
            return sound;
        }

        internal static float Multiplier(SoundGroup group, string source, string clip)
        {
            return Observe(group, source, clip)?.Volume.Value ?? 1f;
        }

        internal static float ChannelMultiplier(SoundChannel channel)
        {
            return Controls.TryGetValue(channel, out var control) ? control.Volume.Value : 1f;
        }

        internal static float SavedMultiplier(SoundGroup group, string source, string clip)
        {
            string key = group + "|" + SoundName(source, clip);
            return Sounds.TryGetValue(key, out var sound) ? sound.Volume.Value : 1f;
        }

        private static void SeedControls()
        {
            foreach (var definition in SoundChannels.All)
            {
                var sound = BindSound(definition.Group, "Control: " + definition.Channel, definition.Label);
                sound.IsChannel = true;
                sound.Name = definition.Label;
                Controls[definition.Channel] = sound;
            }
        }

        internal static List<Sound> GetControls(SoundGroup? group)
        {
            var controls = SoundChannels.All.Where(item => !group.HasValue || item.Group == group.Value)
                .Select(item => Controls[item.Channel]).ToList();
            controls.AddRange(GetSounds(group).Where(sound => sound.Volume.Value != 1f && sound.Name.IndexOf("vibration_only", StringComparison.OrdinalIgnoreCase) < 0));
            return controls;
        }

        internal static string DisplayName(Sound sound, int variation)
        {
            if (sound.IsChannel) return sound.Name;
            return SoundChannels.DescribeVariation(sound.Group, sound.Name) + " (saved variation " + variation + ")";
        }

        internal static void ResetControls(SoundGroup? group)
        {
            foreach (var sound in Sounds.Values)
                if (!group.HasValue || sound.Group == group.Value) sound.Volume.Value = Clamp(1f);
            Touch();
        }

        internal static void RemoveLegacyMaster()
        {
            if (!ReadSection("Ambience").TryGetValue("Master Volume", out string rawMaster)) return;
            if (!float.TryParse(rawMaster, NumberStyles.Float, CultureInfo.InvariantCulture, out float gain) || float.IsNaN(gain) || float.IsInfinity(gain)) gain = 1f;
            gain = Math.Max(0f, Math.Min(5f, gain));
            var legacy = config.Bind("Ambience", "Master Volume", gain,
                new ConfigDescription("Legacy ambience master, migrated to individual controls.", new AcceptableValueRange<float>(0f, 5f)));
            if (gain <= 0)
            {
                foreach (string key in new[] { "Wind Volume", "Ocean Volume", "Boat Volume", "Ambient Loop Volume", "Shield Hum Volume" })
                    config.Bind("Ambience", key, 1f).Value = 0;
            }
            else
            {
                foreach (var definition in SoundChannels.All)
                {
                    // Ocean-biome audio also passes through AmbientBackground;
                    // folding into both channels would apply the old gain twice.
                    if (definition.Channel == SoundChannel.OceanBackground) continue;
                    var sound = Controls[definition.Channel];
                    var saved = ReadSection(SoundSection(definition.Group));
                    float original = 1f;
                    if (saved.TryGetValue(Encode("Control: " + definition.Channel), out string text) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)) original = parsed;
                    sound.Volume.Value = Clamp(original * gain);
                }
            }
            config.Remove(legacy.Definition);
            Touch();
        }

        internal static List<Sound> GetSounds(SoundGroup? group)
        {
            return Sounds.Values.Where(sound => !sound.IsChannel && (!group.HasValue || sound.Group == group.Value))
                .OrderBy(sound => sound.Group).ThenBy(sound => sound.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        internal static List<ExtraRow> GetRows(bool clips) => Rows.Where(row => row.IsClip == clips).OrderBy(row => row.Order.Value).ThenBy(row => row.Id).ToList();

        internal static ExtraRow AddRow(bool clips, string name = "", float volume = 1f)
        {
            var row = BindRow(clips, Guid.NewGuid().ToString("N"));
            row.Name.Value = name.Trim();
            row.Volume.Value = Clamp(volume);
            Touch();
            return row;
        }

        private static ExtraRow BindRow(bool clips, string id)
        {
            int nextOrder = Rows.Where(item => item.IsClip == clips).Select(item => item.Order.Value).DefaultIfEmpty(-1).Max() + 1;
            var row = new ExtraRow {
                Id = id, IsClip = clips,
                Name = config.Bind(Section(clips), id + ".Name", "", "Sound name. Empty rows do not affect audio."),
                Volume = config.Bind(Section(clips), id + ".Volume", 1f,
                    new ConfigDescription("Individual sound multiplier.", new AcceptableValueRange<float>(0f, 5f))),
                Order = config.Bind(Section(clips), id + ".Order", nextOrder, "Row order in the audio menu.")
            };
            row.Name.SettingChanged += (_, __) => Touch();
            row.Order.SettingChanged += (_, __) => Touch();
            Rows.Add(row);
            return row;
        }

        internal static bool Rename(ExtraRow row, string name, out string error)
        {
            name = (name ?? "").Trim();
            if (name.IndexOfAny(new[] { '\r', '\n', '\t' }) >= 0)
            {
                error = "Use one sound name per row.";
                return false;
            }
            if (name.Length > 0 && Rows.Any(other => other != row && other.IsClip == row.IsClip &&
                Normalize(other.Name.Value).Equals(Normalize(name), StringComparison.OrdinalIgnoreCase)))
            {
                error = "This sound already has a row.";
                return false;
            }
            row.Name.Value = name;
            error = null;
            Touch();
            return true;
        }

        internal static void Remove(ExtraRow row)
        {
            if (!Rows.Remove(row)) return;
            config.Remove(row.Name.Definition);
            config.Remove(row.Volume.Definition);
            config.Remove(row.Order.Definition);
            Touch();
        }

        internal static bool TryExtra(bool clips, string name, out float value)
        {
            value = 1f;
            name = Normalize(name);
            var candidates = Rows.Where(row => row.IsClip == clips && !string.IsNullOrWhiteSpace(row.Name.Value))
                .OrderByDescending(row => Normalize(row.Name.Value).Length);
            var exact = candidates.FirstOrDefault(row => Normalize(row.Name.Value).Equals(name, StringComparison.OrdinalIgnoreCase));
            var match = exact ?? candidates.FirstOrDefault(row => name.IndexOf(Normalize(row.Name.Value), StringComparison.OrdinalIgnoreCase) >= 0);
            if (match == null) return false;
            value = match.Volume.Value;
            return true;
        }

        internal static void ClampAll()
        {
            foreach (var sound in Sounds.Values) sound.Volume.Value = Clamp(sound.Volume.Value);
            foreach (var row in Rows) row.Volume.Value = Clamp(row.Volume.Value);
        }

        private static float Clamp(float value)
        {
            float limit = maximum?.Invoke() ?? 1f;
            if (float.IsNaN(limit) || float.IsInfinity(limit)) limit = 1f;
            if (float.IsNaN(value) || float.IsInfinity(value)) value = 1f;
            return Math.Max(0f, Math.Min(Math.Max(0f, Math.Min(5f, limit)), value));
        }

        private static void Touch() { Revision++; changed?.Invoke(); }

        private static Dictionary<string, string> ReadSection(string section)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(config.ConfigFilePath)) return values;
            bool active = false;
            foreach (var raw in File.ReadAllLines(config.ConfigFilePath))
            {
                string line = raw.Trim();
                if (line.StartsWith("[") && line.EndsWith("]")) { active = line.Substring(1, line.Length - 2).Trim().Equals(section, StringComparison.OrdinalIgnoreCase); continue; }
                if (!active || line.StartsWith("#")) continue;
                int separator = line.IndexOf('=');
                if (separator >= 0) values[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
            }
            return values;
        }

        private static void LoadSavedSounds()
        {
            foreach (SoundGroup group in Enum.GetValues(typeof(SoundGroup)))
                foreach (var key in ReadSection(SoundSection(group)).Keys)
                {
                    try { string name = Decode(key); if (name.Length > 0 && !name.StartsWith("Control: ", StringComparison.Ordinal)) BindSound(group, name); }
                    catch (FormatException) { /* Keep unrecognized entries as BepInEx orphans. */ }
                }
        }

        private static void LoadRows()
        {
            var wanted = new HashSet<string>();
            foreach (bool clips in new[] { false, true })
                foreach (var key in ReadSection(Section(clips)).Keys)
                {
                    if (!key.EndsWith(".Name", StringComparison.OrdinalIgnoreCase)) continue;
                    string id = key.Substring(0, key.Length - 5);
                    if (Guid.TryParseExact(id, "N", out _)) wanted.Add(clips + "|" + id);
                }
            foreach (var row in Rows.ToArray())
                if (!wanted.Contains(row.IsClip + "|" + row.Id)) Remove(row);
            foreach (string item in wanted)
            {
                var parts = item.Split('|');
                bool clips = bool.Parse(parts[0]);
                if (!Rows.Any(row => row.IsClip == clips && row.Id == parts[1])) BindRow(clips, parts[1]);
            }
        }

        private static void Import(bool clips, string legacy, ConfigEntry<float> parent, bool skipMainMenu)
        {
            var values = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in (legacy ?? "").Split(',', '\n', '\r'))
                if (!string.IsNullOrWhiteSpace(name)) values[Normalize(name)] = parent.Value;
            string section = clips ? "Extra Clips Split" : "Extra SFX Split";
            foreach (var pair in ReadSection(section))
                if (float.TryParse(pair.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float volume)) values[Normalize(pair.Key)] = volume;
            if (skipMainMenu) values.Remove("Amb_MainMenu");
            foreach (var pair in values)
                if (pair.Key.Length > 0 && !Rows.Any(row => row.IsClip == clips && Normalize(row.Name.Value).Equals(pair.Key, StringComparison.OrdinalIgnoreCase)))
                    AddRow(clips, pair.Key, pair.Value);
            // Old split values were absolute, bypassing the shared slider.
            // Move all effective values into rows, with a neutral category master.
            if (values.Count > 0) parent.Value = 1f;
        }
    }
}
