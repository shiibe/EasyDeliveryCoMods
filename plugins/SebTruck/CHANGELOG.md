## 1.1.1
- Use SebBinds' shared Rally shift/direct-gear bindings for Story manual transmission controls.
- Limit SebTruck's binding page to ignition, indicators, and gearbox toggle actions.
- Always show the Rally HUD in Rally mode and remove Rally HUD visibility toggles.

## 1.1.0
- Update mod to work with new 2.0 Rally update.
- Fix ignition and turn signal behavior after the 2.0 game update.

**Rally Mode**:
- Add a Rally Auto/Manual transmission setting to easily toggle the manual mode on or off.
- Use the game's vanilla Rally manual-transmission path instead of SebTruck's Story-mode manual transmission logic.

**Story Mode**:
- Keep SebTruck's custom manual transmission isolated to Story mode. In the future, I'll look into porting the Rally driving mechanics to Story mode, but for now, the two modes are separate for technical reasons (Rally uses a different vehicle controller and input path than Story mode).
- Add the Rally HUD style for Speedometer/Tachometer/Gear Indicators in Story mode.
- Adjust simulated manual transmission curves/gear ratios.
- Reset gear to 1 when the player gets out of the Truck.

## 1.0.6
**Turn Signals**:
- Fix: Turn signals now remain active while the player is outside the truck (as long as the ignition is on).

**General**:
- Decrease time it takes for truck tempurature to drop after the ignition is turned off (`30s -> 15s`).
- Fix: Truck paint selection works properly.
- Perf: Reduce per-frame hierarchy scans (headlight tuning + paint/tailgate).

## 1.0.5
**Turn Signals**:
- Add turn signal emitter materials so the turn signals actually glow when active/blinking.
- Add dedicated emissive atlases for left/right/hazards (and brake variants) to control indicator glow.
- Add world-space amber turn signal spot lights (front+rear) with an in-game intensity slider.
- Add volume slider for turn signal click SFX.
- Fix: Turn signals now automatically cancel after a full turn cycle to mimic real-world behavior.
- Fix: Turn signals now pause while the pause menu is open.

**Ignition**:
- Mute the engine and radio proximity sounds while the ignition is off and the player is out of the truck.
- Turning the ignition off now plays the headlight click sound.
- Ignition SFX now defaults to 15% volume.

## 1.0.4
- Manual transmission braking: treat negative `driveInput.y` as brake/reverse input (better controller/wheel support), while still honoring handbrake.
- Add `SebTruckApi` (stable interop surface) for setting manual gear without hard mod dependencies.

## 1.0.3
- Manual transmission: restore neutral rev + overrev engine SFX (including distortion/stutter) after plugin split.
- Manual transmission: resetting the truck (reset key / ice crack / explosion) now returns to gear 1.
- Headlights: headlight distance/brightness tuning applies reliably across different headlight hierarchies.
- Headlght Dist: cap at 1.00x since values above 1.0 are ineffective.

## 1.0.2
- Add Tweaks menu selectors for bobblehead (locked-aware) and truck paint (locked-aware).

## 1.0.1
- Manual transmission: change virtual gear spacing to a geometric progression (steeper early gears, tighter high gears).
- Speed multiplier now also scales the virtual gearbox/RPM mapping so manual + auto feel stays consistent.

## 1.0.0
- Initial SebTruck release.
