## 1.1.3
- Fix the 3D view being squeezed on screens narrower than 16:9 (16:10 laptops, Steam Deck, 4:3); those keep the game's own sizing now.
- Fix decimal `aspect_ratio` values such as `2.39` being read as `239` on systems that use a comma for decimals.
- Fix the rear-view mirror image being stretched on ultrawide.
- Split-screen views keep the game's layout instead of being forced to full screen.
- The HUD now updates its width when the resolution or fullscreen mode changes, and returns to normal when the window is no longer ultrawide.
- A game update that renames something this mod hooks into now logs a warning instead of failing silently.
- Remove the `perf_logging` and `perf_log_interval_seconds` options.
- Move FOV, pixelation, view distance, and vsync into SebCore so only one copy runs when SebTweaks is also installed; saved settings are kept. Requires SebCore 1.1.1.
- View distance Max keeps this mod's values (shadow distance 8000, LOD bias 12, far clip 250000); SebTweaks now uses them too.
- The world-space HUD label fix also moved to SebCore.
- Respect SebTruck's mirror-quality setting independently of main-view pixelation.
- Use SebCore's shared graphics slider rows and Back footer.
- Update SebCore dependency for the preference reset fixes.
- Keep assembly and package versions consistent and allow builds without deploying to the game profile.

## 1.1.2
- Fix world-space HUD label positioning when renderer dimensions differ from the camera output. (Thanks to flyen256 for reporting)
- Reapply saved FOV and pixelation settings when freecam/location transitions rebuild cameras. (Thanks to flyen256 for reporting)

## 1.1.1
- Add VSync control to the Graphics menu using the shared SebTweaks VSync preference keys.
- Use SebCore's shared cartridge window base, UI utilities, config migration helper, and Harmony patch helper.

## 1.1.0
- Add compatibility with the 2.0 game update.
- Fix Rally mode title screen camera.
- Increase value of Max View Distance.

## 1.0.1
- Add Reset Defaults button to the in-game graphics menu.
- 1st-person FOV slider now defaults to 90.

## 1.0.0
- Rename to `SebUltrawide`.
- Launch from `SebCore` (no standalone desktop icon).

<hr/>

## 1.2.0
- Add `default` aspect mode to disable ultrawide fixes (vanilla presentation) while keeping pixelation usable.
- Improve `aspect_ratio=auto` to use the current window aspect ratio (rounded) to avoid CRT edge smearing.
- Fix world-space HUD labels (interaction + inventory label) drifting when pixelation changes the render texture size.
- Tune `Pixelation` Large strength (less aggressive).
- Increase `View Distance` Max (higher far clip / LOD / shadow distance).
- Add `Fog` multiplier slider to the in-game menu.
- Clean up/standardize in-game menu spacing and labels.

## 1.1.2
- Clean up in-game settings menu labels (rename `Pixelation` header to `Renderer`; put `View Distance` label on the slider).

## 1.1.1
- Add `Pixelation` slider to `wide.exe` menu (None/Finer/Fine/Default/Large).
- Add `View Distance` slider to `wide.exe` menu (Near/Default/Far/Max).
- Apply view distance changes to gameplay camera far clip + quality LOD/shadow distance.
- Add config options to control `wide.exe` icon visibility/position on the main menu desktop.
- Split performance logging into a separate toggle (`perf_logging`).
- Re-categorize config options for easier navigation.
- Misc internal cleanup.

## 1.1.0
- Add in-game mod menu (`wide.exe`) for configuring in-game options.
- Add separate FOV sliders for `1st Per.` and `3rd Per.` (max 110).
- Skip FOV overrides while inside buildings.
- Fix camera stretch in `CamDotExe`.
- Internal refactor/split `Plugin.cs` into partials.

## 1.0.6
- Refresh README screenshots.

## 1.0.5
- Rename `enable_ultrawide_mode` to `enable_mod`.
- Add `enable_hud_fix` toggle to control HUD scaling behavior.

## 1.0.4
- Remove the FOV override patching and related config entries.
- Simplify configuration docs to match current options.

## 1.0.3
- Force menu cameras to use the target aspect ratio on intro/choose screens.
- Apply aspect fixes to primary gameplay cameras on refresh.
- Rescale overlays after FOV overrides to keep transitions aligned.
- Refresh README icon and screenshots.

## 1.0.2
- Default `aspect_ratio` to `auto` and clarify config docs.
- Add README screenshots and refresh packaging notes.

## 1.0.1
- Update manifest/README wording.
- Packaging metadata refresh.

## 1.0.0
- Initial public release.
- Ultrawide camera and UI overlay fixes for Easy Delivery Co.
