# SpawnDev.GameUI

GPU-rendered game UI library for Blazor WebAssembly. All UI rendered by WebGPU - no HTML overlays on the canvas. Supports PC, VR, AR, and mobile with unified input handling.

[![NuGet](https://img.shields.io/nuget/v/SpawnDev.GameUI.svg)](https://www.nuget.org/packages/SpawnDev.GameUI)

> **Status:** `0.1.0-rc.1` - first release candidate. API is functional and consumed by Lost Spawns; expect iteration during the RC cycle.

## Features

- **40+ UI elements** - Panels, labels, buttons, sliders, toggles, text input, scroll views, lists, dropdowns, radial menus, tooltips, color pickers, key-bind displays, debug overlays - plus game-specific HUD widgets (hotbar, status bars, minimap, compass, crosshair, interaction prompts, equipment, crafting, status effects)
- **Unified input** - Mouse, keyboard, gamepad, VR controllers, hand tracking, touch, gaze - all through one `GameInput` abstraction. Plus `HapticFeedback`, `PokeInteraction`, `AdaptiveInteraction`, `DragDropManager`, `UIFocusManager`
- **4 render modes** - Screen-space 2D, world-space 3D, view-anchored VR HUD, world-anchored AR
- **6 themes + accessibility** - Dark (default), LostSpawns (DayZ-style), AubsCraft (Minecraft-style), HighContrast, ColorblindSafe, TritanopiaSafe, plus runtime FontScale
- **Animation** - Tween system with 10 easing functions, fade/slide/pulse extensions
- **SDF font rendering** - Resolution-independent text via Signed Distance Field, automatic bitmap fallback, outline support
- **WebGPU rendering** - Batched quad renderer (4096 quads/frame), font atlas, alpha blend
- **VR/AR ready** - 3D ray hit testing, poke interaction, controller ray, adaptive interaction, haptic feedback, world-anchored panels
- **Screen management** - Push/pop screen stack with transitions, focus navigation, drag-and-drop
- **Real-code unit tests** - PlaywrightMultiTest harness, runs against production code paths on real backends
- **DI integration** - `builder.Services.AddGameUI()` wires everything up

## Installation

```
dotnet add package SpawnDev.GameUI --prerelease
```

## Dependencies

- [SpawnDev.BlazorJS](https://github.com/LostBeard/SpawnDev.BlazorJS) (3.5.1+) - Browser interop with typed wrappers for WebGPU/WebXR/DOM

## Quick Start

```csharp
// Create the UI tree
var root = new UIAnchorPanel { Width = viewportW, Height = viewportH };

// Add a health bar (bottom-left)
var healthBar = new UIProgressBar
{
    Width = 200, Height = 20,
    Value = 0.75f,
    LowColor = Color.Orange,
    CriticalColor = Color.Red,
    ShowPercentage = true,
    Label = "HP",
};
root.AddAnchored(healthBar, Anchor.BottomLeft, offsetX: 20, offsetY: -40);

// Add a settings panel
var settings = new UIFlexPanel { Direction = FlexDirection.Column, Gap = 8 };
settings.AddChild(new UILabel { Text = "Settings", FontSize = FontSize.Heading });
settings.AddChild(new UIToggle { Text = "VSync", IsOn = true });
settings.AddChild(new UISlider { Label = "FOV", MinValue = 60, MaxValue = 120, Value = 90 });
root.AddAnchored(settings, Anchor.TopRight, offsetX: -20, offsetY: 20);

// Per frame:
gameInput.Poll();
TweenManager.Global.Update(dt);
root.Update(gameInput, dt);

renderer.Begin(viewportW, viewportH);
root.Draw(renderer);
renderer.End(encoder, targetView);
```

## Input Providers

```csharp
var gameInput = new GameInput();

// Mouse + keyboard + gamepad (PC)
var mkProvider = new MouseKeyboardProvider();
mkProvider.Attach(canvasRef);
gameInput.AddProvider(mkProvider);

// VR controllers
var vrProvider = new XRControllerProvider();
vrProvider.SetSession(xrSession);
gameInput.AddProvider(vrProvider);

// Hand tracking (Quest 3S)
var handProvider = new XRHandProvider();
handProvider.SetSession(xrSession);
gameInput.AddProvider(handProvider);

// Eye gaze (Vision Pro / WebXR gaze input)
var gazeProvider = new GazeProvider();
gazeProvider.SetSession(xrSession);
gameInput.AddProvider(gazeProvider);

// Touch (mobile)
var touchProvider = new TouchProvider();
touchProvider.Attach(canvasRef);
gameInput.AddProvider(touchProvider);
```

## Themes

```csharp
// Set globally
UITheme.Current = UITheme.LostSpawns;       // DayZ military style
UITheme.Current = UITheme.AubsCraft;        // Bright Minecraft style
UITheme.Current = UITheme.Dark;             // Default dark theme
UITheme.Current = UITheme.HighContrast;     // Accessibility: max contrast
UITheme.Current = UITheme.ColorblindSafe;   // Accessibility: deuteranopia/protanopia
UITheme.Current = UITheme.TritanopiaSafe;   // Accessibility: tritanopia

// Runtime font scale (accessibility)
UITheme.FontScale = 1.5f;

// Override per-element (theme stays applied to others)
button.NormalColor = Color.Red;
```

## License

MIT

## Built With

- [SpawnDev.BlazorJS](https://github.com/LostBeard/SpawnDev.BlazorJS) - Full JS interop for Blazor WebAssembly

## 🖖 The SpawnDev Crew

SpawnDev.GameUI is built by the entire SpawnDev team - a squad of AI agents and one very tired human working together, Star Trek style. Every project we ship is a team effort, and every crew member deserves a line in the credits.

- **LostBeard** (Todd Tanner) - Captain, architect, writer of libraries, keeper of the vision
- **Riker** (Claude CLI #1) - First Officer, implementation lead on consuming projects
- **Data** (Claude CLI #2) - Operations Officer, deep-library work, test rigor, root-cause analysis. Authored the GameUI library extraction from SpawnScene + carried the 0.1.0-rc.1 push (UILabel Align/Width fix, accessibility themes, GazeProvider plumbing).
- **Tuvok** (Claude CLI #3) - Security/Research Officer, design planning, documentation, code review
- **Geordi** (Claude CLI #4) - Chief Engineer, library internals, GPU kernels, backend work

If you see a commit authored by `Claude Opus 4.7` on a SpawnDev repo, that's one of the crew. Credit where credit is due. Live long and prosper. 🖖
