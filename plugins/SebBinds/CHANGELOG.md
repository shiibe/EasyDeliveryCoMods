## 1.0.2
- Align Map/Items/Jobs semantics with vanilla: `MapItems` drives inventory (hold); `Jobs` toggles jobs/map menu (press).
- Expose proper handbrake vs brake/reverse behavior: `Back/Handbrake` (hold) is the handbrake while driving; `Brake/Reverse` affects vehicle brake/reverse input.
- Update defaults: keyboard `Brake/Reverse = S`, `Back/Handbrake = Space`, `MapItems = Shift (hold)`, `Jobs = Tab`; controller seeds `MapItems` from `Inventory` and `Jobs` from `Map`.
- Allow common keyboard reuse without conflict prompts (WASD for on-foot + vehicle).
- Improve duplicate-bind UX: show wrapped comma-separated conflicts (28 chars/line) and allow `Keep Both`.
- Fix bind capture UI: conflict prompt no longer captures inputs; add shortcuts `Enter=Keep Both`, `Esc=Try Another`.

## 1.0.1
- Scheme select: keep options vertically centered when Wheel is installed.

## 1.0.0
- Initial SebBinds release.
