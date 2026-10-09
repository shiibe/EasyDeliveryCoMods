## 1.1.1
- Add shared ranged and discrete slider rows, navigation footers, and page indicators for the mod suite.
- Fix global preference reset to clear all binding schemes, axes, and runtime caches, then restore defaults.
- Limit preference reset to this mod suite and validate release packaging before creating archives.

## 1.1.0
- Add shared UI helpers, config migration, Harmony patch, and PlayerPrefs utility APIs for dependent mods.
- Add a shared cartridge open helper that ensures listeners before launching app windows.
- Keep the SebCore clear-prefs action scoped to known mod `PlayerPrefs` only.

## 1.0.2
- Launcher: wider/consistent cartridge buttons, restored hover expansion, and slightly tighter column spacing.
- Add `CartridgeWindowBase` (optional) + update template/docs for cartridge authors.

## 1.0.1
- Add API to register custom cartridges in SebCore.
- Add paging to cartridge launcher when more than 10.
- Settings button moved above Back; Settings back returns to cartridges; Back on main closes SebCore menu.

## 1.0.0
- Initial SebCore release.
