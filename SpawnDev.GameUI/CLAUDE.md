# SpawnDev.GameUI

GPU-rendered game UI library for Blazor WebAssembly. All UI rendered by WebGPU - no HTML overlays on the canvas. Supports PC, VR, AR, and mobile with unified input handling.

## Build Commands

```bash
dotnet build SpawnDev.GameUI/SpawnDev.GameUI.csproj
```

Target: **net10.0**

## Architecture

### Four Rendering Modes (one library)

| Mode | Use Case | Positioning |
|------|----------|-------------|
| ScreenSpace | PC/tablet HUD overlay | Pixel coordinates, 2D |
| WorldSpace | VR floating panels, menus | Matrix4x4 world transform, 3D |
| ViewAnchored | VR HUD (moves with head) | Offset from camera, fixed distance |
| WorldAnchored | AR labels pinned to real world | XRAnchor position |

### Unified Input (GameInput)

All input sources feed into `GameInput` via `IInputProvider` implementations:
- **MouseKeyboardProvider** - DOM mouse/keyboard/gamepad events
- **XRControllerProvider** - WebXR tracked controllers (trigger, grip, thumbstick)
- **XRHandProvider** - WebXR hand tracking (25 joints per hand, pinch detection)
- **TouchProvider** - DOM touch events

Every input source produces `Pointer` objects with:
- Screen position (2D) or ray origin+direction (3D)
- Primary action (click/trigger/pinch), secondary action (right-click/grip)
- Trigger/grip analog values, scroll delta
- Hand joint positions and radii (for hand tracking)

UI elements consume `GameInput` uniformly - a button doesn't care if it was clicked by mouse, VR controller ray, or hand pinch.

### UI Element Hierarchy

Retained-mode tree structure extracted from SpawnScene's production UI:
- `UIElement` - base class with position, size, children, hit testing (2D + 3D ray)
- `UIPanel` - container with background, border, padding
- `UILabel` - text with font atlas, auto-sizing
- `UIButton` - clickable with hover/press states, works with all pointer types
- `UISlider` - draggable float value, track + thumb
- `UIImage` - GPU texture display

### Dependencies

- `SpawnDev.BlazorJS` - Browser interop, WebGPU/WebXR typed wrappers (66 WebXR classes)

### Origin

Extracted from SpawnScene's production UI system (UIElement, UIRenderer, FontAtlas, InputManager). SpawnScene has a complete, tested WebGPU UI framework. GameUI generalizes it for any SpawnDev game project.

## Consuming Projects

| Project | Needs |
|---------|-------|
| AubsCraft | Admin panel, VR world viewer HUD, settings |
| Lost Spawns | Inventory, crafting, health/stamina, DayZ-style interaction menus, VR/AR |
| SpawnScene | Studio UI (already using the source code we extracted from) |

## Rules

- **NEVER use HTML overlays on the canvas.** All UI rendered by the GPU engine.
- **NEVER use eval(), IJSRuntime for UI.** Use SpawnDev.BlazorJS typed wrappers.
- **SignalR for structured data, JS WebSocket for binary.** No HTTP polling.
- All global rules from `D:\users\tj\Projects\CLAUDE.md` apply.
