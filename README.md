# <p align="center">Ambience Sound Config</p>
<p align="center">
<a href="https://github.com/DraconicVelum/AmbienceSoundConfig/releases/latest"><img alt="release" src="https://img.shields.io/github/release/DraconicVelum/AmbienceSoundConfig.svg?style=popout"></a>
<a href="#featured-in"><img alt="downloads" src="https://img.shields.io/github/downloads/DraconicVelum/AmbienceSoundConfig/total.svg?style=popout"></a>
<a href="https://github.com/DraconicVelum/AmbienceSoundConfig/blob/main/LICENSE"><img alt="license" src="https://img.shields.io/github/license/DraconicVelum/AmbienceSoundConfig.svg?style=popout"></a>
</p>

## Description
Tame Valheim's wild ambience.

This mod adds volume sliders for:

- Wind ambience
- Ocean ambience
- Background ambient loop
- Shield hum
- Master ambience volume

### Advanced sound control

- **Extra SFX Volume** (v2.5.0+)
  Control specific SFX sources by prefab / object name.
  Configurable with comma-separated SFX names in config.

- **Extra Clip Volume** (v2.6.8+)
  Control specific AudioClips directly.
  Should work with ZSFX, ambient loops, creature loops, UI sounds, and modded clips.

- **Split SFX and Clip Volumes** (v2.7.8+)
  Add one sound per line in the config-only split sections:

```ini
[Extra SFX Split]
Amb_MainMenu = 0.0

[Extra Clips Split]
Ui_Click_01 = 0.1
```

- **Optional Sound Logging**
  Logs played clips and SFX names to console and file with timestamps.
  Useful for discovering names to add to your control lists.

Adjust live in-game on the game audio settings or using the **BepInEx Configuration Manager (F1)**,
or edit your `.cfg` file manually under `BepInEx/config/com.draconicvelum.ambiencesoundconfig.cfg`.

There is also a **Reload Config** button for compatible config manager mods.
Use it after editing the split sections while the game is running.

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

---

## Licensing

Licensed under the [GPLv3.0](https://github.com/DraconicVelum/AmbienceSoundConfig/blob/main/LICENSE).<br>
Feel free to fork it and update as you like, pull requests etc.<br>
Any ideas are welcome and I will add them if I can, and if I can't I will ask your help too.
