## 1.0.5
- Turn indicators now automatically cancel after a full turn cycle to mimic real-world behavior.
- Fix proximity radio and engine sounds being audible when out of the truck with the ignition off.
- Add volume options for ignition and turn signal SFX.
- Play click sound when ignition is turned off.

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
