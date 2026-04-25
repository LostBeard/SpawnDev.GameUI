# Changelog

All notable changes to SpawnDev.GameUI.

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
