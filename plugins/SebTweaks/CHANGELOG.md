## 1.1.3
- Rally manual transmission now shares one setting with SebTruck, so toggling it in either menu works, and the two mods no longer override each other. Your existing SebTweaks setting carries over.
- Rally manual shifting now turns off while on foot, matching the base game.
- Move FOV, pixelation, view distance, and vsync into SebCore so only one copy runs when SebUltrawide is also installed; saved settings are kept. Requires SebCore 1.1.1.
- View distance Max now uses SebUltrawide's values (shadow distance 8000, LOD bias 12, far clip 250000; was 3000 / 6 / 100000).
- Saved FOV is now held every frame like SebUltrawide, instead of re-applied on a 0.25s poll.
- Respect SebTruck's mirror-quality setting independently of main-view pixelation.
- Use SebCore's shared sliders, page controls, and safe preference helpers.
- Support patch version bumps through the shared release scripts.
- Update SebCore dependency and keep assembly and package versions consistent.

## 1.1.2
- Fix world-space HUD label positioning when renderer dimensions differ from the camera output. (Thanks to flyen256 for reporting)
- Reapply saved FOV and pixelation settings when freecam/location transitions rebuild cameras. (Thanks to flyen256 for reporting)

## 1.1.1
- Update dependency metadata for SebCore 1.1.0.

## 1.1.0
- Rename Atmosphere page back to World and add Gravity multiplier.
- Add Rally page with manual transmission and debug info toggles.
- Fix Rally/freecam pixelation camera handoff to match SebUltrawide behavior.

## 1.0.4
- Cheats: rework Refill layout with freeze column + percent labels.
- Graphics: simplify VSync to On/Off (default On).

## 1.0.3
- Remove God Mode page, add freeze toggles to Energy/Fuel sliders.
- Add Temperature slider with freeze option.
- Update options table in the README.

## 1.0.2
- Add Graphics page with FOV + renderer options shared with SebUltrawide (unified settings).
- Add VSync option to Graphics page.
- Perf: Cache `FogVolume` reflection lookup.

## 1.0.1
- Rename World page to Atmosphere; add world light brightness + color sliders.
- Add Time & Weather page and move time/weather controls there.
- Move Ice Cracking toggle to Gameplay page.

## 1.0.0
- Initial release.
