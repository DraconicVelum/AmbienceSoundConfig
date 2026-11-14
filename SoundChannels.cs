namespace AmbienceSoundConfig
{
    internal enum SoundChannel
    {
        Wind, Ocean, ShoreWaves, OceanBackground,
        BoatWater, BoatWake, BoatSails, BoatCrank, BoatRudder, BoatSplash,
        AmbientBackground, MainMenu, ShieldDome, ShieldGenerator, ShieldLowFuel
    }

    internal static class SoundChannels
    {
        internal sealed class Definition
        {
            internal SoundChannel Channel;
            internal SoundGroup Group;
            internal string Label;
            internal Definition(SoundChannel channel, SoundGroup group, string label) { Channel = channel; Group = group; Label = label; }
        }

        internal static readonly Definition[] All = {
            new Definition(SoundChannel.Wind, SoundGroup.Wind, "Wind ambience"),
            new Definition(SoundChannel.Ocean, SoundGroup.Ocean, "Distant ocean"),
            new Definition(SoundChannel.ShoreWaves, SoundGroup.Ocean, "Shore and wave sounds"),
            new Definition(SoundChannel.OceanBackground, SoundGroup.Ocean, "Ocean background ambience"),
            new Definition(SoundChannel.BoatWater, SoundGroup.Boat, "Water against the hull"),
            new Definition(SoundChannel.BoatWake, SoundGroup.Boat, "Wake while sailing"),
            new Definition(SoundChannel.BoatSails, SoundGroup.Boat, "Sails"),
            new Definition(SoundChannel.BoatSplash, SoundGroup.Boat, "Boat splashes"),
            new Definition(SoundChannel.AmbientBackground, SoundGroup.Ambient, "Biome and weather ambience"),
            new Definition(SoundChannel.MainMenu, SoundGroup.Ambient, "Main menu ambience"),
            new Definition(SoundChannel.ShieldDome, SoundGroup.Shield, "Shield dome hum"),
            new Definition(SoundChannel.ShieldGenerator, SoundGroup.Shield, "Generator hum"),
            new Definition(SoundChannel.ShieldLowFuel, SoundGroup.Shield, "Low fuel hum")
        };

        internal static string GroupLabel(SoundGroup group)
        {
            return group == SoundGroup.Ambient ? "Ambience" : group == SoundGroup.Shield ? "Shield" : group.ToString();
        }

        internal static string DescribeVariation(SoundGroup group, string name)
        {
            name = name.ToLowerInvariant();
            switch (group)
            {
                case SoundGroup.Wind: return "Wind ambience";
                case SoundGroup.Ocean: return name.Contains("ocean_ambient_loop") ? "Distant ocean" : name.Contains("ambient_loop") ? "Ocean background ambience" : "Shore and wave sounds";
                case SoundGroup.Ambient: return name.Contains("mainmenu") ? "Main menu ambience" : "Biome and weather ambience";
                case SoundGroup.Shield: return name.Contains("low") ? "Low fuel hum" : name.Contains("dome") || name.Contains("shieldhum") ? "Shield dome hum" : "Generator hum";
                case SoundGroup.Boat:
                    if (name.Contains("crank") || name.Contains("sail_change")) return "Sail raising and lowering";
                    if (name.Contains("sail") || name.Contains("cloth")) return "Sails";
                    if (name.Contains("rudder") || name.Contains("paddle")) return "Rudder";
                    if (name.Contains("wake")) return "Wake while sailing";
                    if (name.Contains("splash")) return "Boat splashes";
                    return "Water against the hull";
                default: return "Ambience";
            }
        }
    }
}
