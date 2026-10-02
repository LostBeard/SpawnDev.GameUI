# Changelog

All notable changes to SpawnDev.GameUI.

## [0.1.0-rc.6] - unreleased

### Fixed
- **Non-ASCII text drew as nothing.** Both font atlases (`SDFFontAtlas`, `FontAtlas`) built glyphs for ASCII 32-126 only,
  and a missing glyph draws as a space, so a middle dot, an em dash, a degree sign or an accented letter vanished from
  every label. The atlases now cover `GlyphSet.Characters`: printable ASCII, Latin-1 Supplement (U+00A0-U+00FF) and
  common UI punctuation and symbols (dashes, curly quotes, bullet, ellipsis, arrows, check mark, triangles). Found in
  SpawnScene, whose project header separators were missing.
- A full atlas now logs how many glyphs it placed and the first one left out, instead of stopping silently.
- **A translucent bordered button or panel showed its border colour across the whole face.**
  `DrawBorderedRoundedRect` drew a full rect in the border colour and the fill over its inset, so anything less than
  opaque let the border colour through (a transparent button over an image washed it cyan). A translucent fill now gets
  a true border ring (`DrawRoundedRing`, a new shader path); an opaque fill keeps the layered draw. Same for the
  world-space variant.

- **Small text lost thin strokes.** At 12 px the SDF glyphs (minified ~4x from the 48 px field) left strokes about a pixel
  thick at partial coverage, and each glyph instance sat on a different sub-pixel phase (fractional advances and padding):
  hyphens, the bars of `=` and `+`, the foot of `2` came out faint, as dots, or missing, differently on every line.
  Text at or below `BitmapTextMaxPixels` (14) now draws from the bitmap atlas (rasterised by the browser at that size),
  larger text from the SDF; SDF and bitmap glyph quads are snapped to whole pixels (spacing still follows the
  fractional advances). Measured in SpawnScene's `?autotest=textlab`, which draws the same strings in each mode.

- **`UITabPanel` took the arrow keys from anywhere on the page.** Left/Right now switch tabs only while the pointer is over
  the panel.

### Added
- `UITabPanel.TabWidth`: fixed-width, left-aligned tab headers (0 = the old equal split), and a rule under the tab row.
- `UIRenderer.TextMode` (Auto / Sdf / Bitmap) and `BitmapTextMaxPixels`. Drawing, measuring and line height follow the
  same choice, so layout matches rendering.
- `UIRenderer.DrawRoundedRing` / `DrawWorldRoundedRing`, and `TryGetQuadFlags` / `TryGetQuadColor` for CPU-side tests.
- `UITextBlock.MeasureHeight(renderer)`: the wrapped height at the current width, exactly as `Draw` will size it, for
  layout before the first draw (a layout had to guess and a guess overlapped the next element).

## [0.1.0-rc.4] - 2026-09-24

### Fixed
- `UIScrollView` Update and HitTest now apply the same `ScrollOffset` transform as Draw.
  Scrolled buttons were painted correctly but hit at content-space Y, so clicks missed
  after scrolling. `UIElement.HitTest` is virtual so the scroll view can override it.

## [0.1.0-rc.1] - 2026-04-25

First release candidate. Lost Spawns is the active consumer driving the
shape of the API; bumping to RC so it can build via package-ref instead
of project-ref for GitHub Actions deploy.

### Fixed
- `UILabel` now honors the `Align` property (was declared but never read).
  `TextAlign.Center` and `TextAlign.Right` correctly shift text within the
  label's bounds.
- `UILabel` no longer overwrites an explicit `Width` with measured text
  width on first draw. Auto-size only fills in zero defaults; explicit
  values stick. Fixes loading-screen / pause-menu titles getting clobbered
  to text-width when consumers set them to panel-width.

### Library scope
- ScreenSpace, WorldSpace, ViewAnchored, WorldAnchored render modes
- Unified `GameInput` for mouse/keyboard, gamepad, XR controllers, hand tracking, touch
- `UIPanel`, `UILabel`, `UIButton`, `UISlider`, `UIImage`, `UIProgressBar`, `UIGrid`, `UIMapPanel`, `UIHotbar`, `UIStatusHUD`, `UICrosshair`, `UIScreenOverlay`, `UINotificationStack`, `UIDropdown`, `UIRadialMenu`, `UIChatBox`, `UILoadingScreen`, `UIAnchorPanel`
- SDF font atlas with outline support, automatic bitmap fallback
