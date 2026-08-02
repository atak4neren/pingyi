# PingYi Main V2 Override

The classic `MainWindow` remains intact. `MainWindowV2` is the active shell and opens the classic window for complete advanced settings.

## Direction

- Artistic asymmetry: a pinned dark capture stage on the left and a scrollable command surface on the right.
- Palette: ink navy, vivid teal, warm off-white, and restrained burnt orange.
- Typography: Satoshi is the preferred Latin face from the deterministic design selection. It is not redistributed because its Fontshare license is not compatible with the MIT source bundle; the application uses the native `Segoe UI Variable Text` fallback and system CJK fallback.
- Motion translation: web-only GSAP patterns are expressed with a pinned native layout, short state transitions, and a low-motion status ticker.

## Bento Math

The engine area is a 12-column by 2-row explicit grid. Provider card `7x2`, model card `5x1`, privacy card `5x1`; `14 + 5 + 5 = 24`, exactly filling all `12x2` cells.

## Accessibility

- State is always expressed by text plus icon and color.
- Every icon-only action has an automation name.
- Buttons remain at least 40 pixels high and keyboard focus uses the shared focus ring.
- No continuously moving decorative imagery is used.
