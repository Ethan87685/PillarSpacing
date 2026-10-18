# Pillar Spacing

A Cities: Skylines II code mod that sets the maximum distance between pillars on elevated roads, tracks and paths, each with its own setting. Written for game version 1.6, without Harmony.

Paradox Mods: https://mods.paradoxplaza.com/mods/162558/Windows

## Features

- Separate pillar spacing for **roads** (including bridges built with roads and tram roads), **tracks** (train, tram, subway) and **paths** (pedestrian, bicycle).
- Spacing from 40 m to 1000 m. Above 200 m, the maximum segment length is raised automatically.
- Settings apply immediately and are re-applied every time a save is loaded.
- One-click restore to vanilla.
- Interface in English and Simplified Chinese. "Auto" works with translation mods.

## Settings

| Group | Setting | Range | Default (vanilla) |
|---|---|---|---|
| General | Enable custom pillar spacing | on/off | on |
| Roads | Road pillar spacing | 40–1000 m | 80 |
| Tracks | Track pillar spacing | 40–1000 m | 80 |
| Paths | Path pillar spacing | 40–1000 m | 40 |
| Restore | Restore vanilla | button | |
| Language | Interface language | Auto / English / 简体中文 | Auto |

Dropdown steps: every 10 m from 40 to 200 m, every 25 m from 200 to 400 m, every 50 m from 400 to 1000 m.

## How it works

- The net tool splits elevated segments by `NetGeometryData.m_ElevatedLength` and places pillars at the split points. Vanilla is 80 m for roads and tracks, 40 m for paths. Segments are limited by `m_EdgeLengthRange.max` (200 m in vanilla), which is raised when the spacing goes above 200 m.
- Prefab bridges (cable-stayed, suspension, arch, grand bridge) place their towers proportionally along the bridge, so there is no spacing to change. All networks built from fixed segments (prefab bridges, dams) are skipped.
- Draw bridges, lift bridges and piers already use their maximum segment length as spacing and are not changed.
- Only affects roads built after the change.

## Files

| File | Purpose |
|---|---|
| `Mod.cs` | Entry point: settings, localization, system registration |
| `Setting.cs` | Options page |
| `Localization.cs` | English and Chinese texts, language switching |
| `PillarSpacingSystem.cs` | Changes pillar spacing on prefabs, stores and restores vanilla values |

## Building

1. Install the Modding Toolchain from the game's Options → Modding.
2. Create a project from the "Cities: Skylines II Mod" template named `PillarSpacing`, then replace the source files with the ones in this repository.
3. Run `dotnet build`. The mod is copied to the local Mods folder automatically.

## Notes

- Do not use together with ConfigurableElevatedRoad or BridgePillarAdjuster: they change the same values.
- Safe to remove: saves stay intact.

## License

MIT
