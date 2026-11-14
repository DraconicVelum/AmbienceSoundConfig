## Changelog

### v2.9.8
- Updated audio hooks for Valheim 1.0, preserving ambient transitions and fades.
- Improved Ocean and Ambient Loop coverage, including shore waves and ocean background loops.
- Added Boat Volume with separate controls for hull water, wake, sails and splashes.
- Fixed Shield Hum coverage for Shield Generator and low-fuel loops.
- Added main menu ambience to Ambient Loop Volume.
- Replaced the audio menu with expandable categories and individual sound sliders available immediately.
- Added descriptive sound labels, Mute/Unmute and volume reset buttons.
- Replaced comma-separated Extra lists with editable sound rows, each with its own volume slider.
- Removed the redundant Ambience Master slider and migrated existing settings.
- Improved config generation, migration and reload while preserving existing volume settings.
- Reduced repeated layout work, audio source searches and config writes when adjusting sliders.

### v2.7.8
- Added split SFX and clip volume sections.
- Added Volume Slider Max.
- Added config updater and Reload Config button.

### v2.6.9
- Added legacy fallback hook to LoadSettings for Valheim 0.221.4 compatibility.

### v2.6.8
- Added Extra Clip Volume.
- Added optional sound play logging.
- Split SFX name filter and clip name filter to prevent cross-muting.

### v2.5.0
- Added Extra SFX Volume slider.
- Extra SFX are configurable through a comma-separated list in the config file.
