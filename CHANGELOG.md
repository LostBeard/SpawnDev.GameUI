# Changelog

All notable changes to SpawnDev.GameUI.

## [1.1.0-local.2] - 2026-10-14 (local feed only, not released)

### Added
- **`UIChatBox` as a game chat feed** (AubsCraft's viewer chat): `ShowChannelTabs` (default true; false hides the tab bar and gives its
  space to the messages - the default channels stay and "All" stays active, nothing is filtered), `InputOnlyWhenFocused` (the input
  field is drawn and clickable only while focused: a feed until the player opens it with `FocusInput()`), `BottomAlign` (the newest
  message sits at the bottom of the area; `MessageOffsetY`) and `GetMessageOpacity(age)` - messages fade after `FadeStart` as before
  but never while the input is focused (the whole history is readable while typing). Defaults are unchanged for existing users.
  Tests: tab area, input visibility, alignment offsets, fade curve and the focus exception (a mutation of the focus rule is caught).
## [1.1.0-local.1] - 2026-10-14 (local feed only, not released)

### Added
- **`GameUIService.Init(device, queue, canvasFormat, viewportWidth, viewportHeight)`: GameUI where there is no DOM canvas.**
  A render loop in a Web Worker (the main thread owns the canvas's events), a headless host, a test. It creates the renderer,
  the bitmap and SDF font atlases (rasterized with an `OffscreenCanvas`) and the screen stack, and touches no `document`,
  `window` or `navigator`. The existing `Init(..., ElementReference, ...)` is unchanged and shares the same setup.
- **`PushedInputProvider`: input fed by the host.** `MouseMove` / `MouseDown` / `MouseUp` / `Wheel` / `KeyDown` / `KeyUp`
  push events as they happen; `Poll` hands the frame to `GameInput`. `GameUIService.Pushed` is the instance a DOM-less `Init`
  creates. `PointerActive = false` reports no pointer while something else owns the mouse (a camera holding pointer lock),
  so nothing hovers or clicks under a cursor that is not on screen. `ReleaseAll()` lets go of held keys when focus is lost
  and cancels a held mouse button without raising a release, so the button under the cursor is not counted as clicked.

### Changed
- `MouseKeyboardProvider` buffers its DOM events in a `PushedInputProvider` instead of its own copy of the same logic.
  Same pointer and key behaviour; gamepad polling stays in `MouseKeyboardProvider`.

## [1.0.0] - 2026-10-10

First stable release. The API is the 0.1.0 release-candidate API plus the click routing below.

### Changed
- **Only the topmost element under a pointer gets its click.** Every element under the pointer used to fire on release,
  so a button on top of a clickable card was also a click on the card (SpawnScene: Delete on a project card opened the
  project, and the card's handler then cancelled "Confirm delete"). Click targets now claim the release
  (`GameInput.ClaimClick`); elements update in draw order, so the last claim is the one on top, and the winners run when
  the root's `Update` ends. Converted: `UIButton`, `UICheckbox`, `UIToggle`, `UIRadioGroup`, `UITabPanel` headers,
  `UIList`, `UIVirtualList`, `UIDropdown`, `UIContextMenu` (a release anywhere on an open menu is the menu's). Labels,
  panels and images do not claim, so a click on a label goes to the button under it.
  - Clicks run when the root's `Update` returns instead of mid-traversal. A standalone element is its own root, so
    `element.Update(input, dt)` still delivers its click before it returns.
  - Custom elements: claim with `input.ClaimClick(pointer, this, action)` instead of acting on `WasReleased` directly.
    An `Update` override that does not call `base.Update` calls `EndUpdate(input)` last. Claims a root never delivered
    run at the next `GameInput.Poll()`, late but not lost.

### Fixed
- **A translucent button turned solid on hover.** `UIButton` drew its hover/focus ring as a filled rect under the button,
  covered only by an opaque body; a transparent button over an image (a card's hover tint) showed the focus colour
  across its whole face and hid the image. The ring is now drawn as a ring (`DrawRoundedRing`). Opaque buttons look the
  same.
- Package project URL pointed at another repository.

## [0.1.0-rc.6] - 2026-10-06

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
