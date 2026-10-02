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
