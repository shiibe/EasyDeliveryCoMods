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
