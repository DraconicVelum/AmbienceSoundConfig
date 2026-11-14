# <p align="center">Ambience Sound Config</p>
<p align="center">
<a href="https://github.com/DraconicVelum/AmbienceSoundConfig/releases/latest"><img alt="release" src="https://img.shields.io/github/release/DraconicVelum/AmbienceSoundConfig.svg?style=popout"></a>
<a href="#featured-in"><img alt="downloads" src="https://img.shields.io/github/downloads/DraconicVelum/AmbienceSoundConfig/total.svg?style=popout"></a>
<a href="https://github.com/DraconicVelum/AmbienceSoundConfig/blob/main/LICENSE"><img alt="license" src="https://img.shields.io/github/license/DraconicVelum/AmbienceSoundConfig.svg?style=popout"></a>
</p>
<hr>

## Description

Tame Valheim's wild ambience.

| Category | Individual sound controls |
| --- | --- |
| Wind Volume | Wind ambience |
| Ocean Volume | Distant ocean, shore and wave sounds, ocean background ambience |
| Boat Volume | Water against the hull, wake while sailing, sails, boat splashes |
| Ambient Loop Volume | Biome and weather ambience, main menu ambience |
| Shield Hum Volume | Shield dome hum, generator hum, low-fuel hum |
| Extra SFX Volume | Custom sounds matched by prefab or object name |
| Extra Clip Volume | Custom sounds matched by AudioClip name |

Expand a category to adjust its individual sounds. Built-in sound controls are available. Each category has a **Mute/Unmute** button that restores its last positive volume, plus a reset button for its individual sound levels.

Ocean background ambience is affected by both **Ocean Volume** and **Ambient Loop Volume**.

### Advanced sound control

Expand **Extra SFX Volume** or **Extra Clip Volume** and click **Add sound**. Each row contains a sound name, its own volume slider and an **X** button to remove it. Finish editing the name to apply it.

- **Extra SFX** matches prefab or object names; **Extra Clip** matches AudioClip names.
- Each category's main volume multiplies the volumes of its individual rows.
- Blank rows do not affect audio; duplicate names in the same list are rejected.
- **Reset levels** restores the individual row volumes.
- Q/E tab navigation is blocked while an Extra name field is being edited or selected.

Enable **Enable Sound Logging** to discover SFX and clip names. Names are written to the console and `AmbienceSoundConfig_SoundLog.txt` next to the config file. **Log Only Unique** avoids repeated entries.

### Configuration

Adjust volumes live in the game's audio settings or through the optional **BepInEx Configuration Manager (F1)**. Changes are saved automatically; consecutive slider changes are grouped to reduce disk writes.

The config file is `BepInEx/config/com.draconicvelum.ambiencesoundconfig.cfg`. Individual built-in settings use the `Sounds - Wind/Ocean/Boat/Ambient/Shield` sections. Custom rows use `Extra SFX Rows` and `Extra Clips Rows` with stable row identifiers, names, volumes and ordering.

**Volume Slider Max** sets the maximum for this mod's sliders. It defaults to `1.0` (100%) and can be configured from `0.0` to `5.0`.

After editing the config file while the game is running, use **Reload Config** in a compatible config manager. Existing config values are preserved when upgrading. A backup ending in `.pre-2.9.0.bak` is created when legacy Extra settings are first converted to rows.

---

## Requirements

- [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
- (Optional) [BepInEx Configuration Manager](https://thunderstore.io/c/valheim/p/BepInEx/BepInExConfigurationManager/)

---

## Installation

1. Install through Thunderstore.

## Manual Installation

1. Download the mod from [releases](https://github.com/DraconicVelum/AmbienceSoundConfig/releases/latest).
2. Extract to your BepInEx plugins folder.

When updating manually, replace the previous `AmbienceSoundConfig.dll` and restart the game. Keep only one copy of the plugin installed.

---

## Licensing

Licensed under the [GPLv3.0](https://github.com/DraconicVelum/AmbienceSoundConfig/blob/main/LICENSE).
