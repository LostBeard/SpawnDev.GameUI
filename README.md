# SpawnDev.GameUI

GPU-rendered game UI library for Blazor WebAssembly. All UI rendered by WebGPU - no HTML overlays on the canvas. Supports PC, VR, AR, and mobile with unified input handling.

[![NuGet](https://img.shields.io/nuget/v/SpawnDev.GameUI.svg)](https://www.nuget.org/packages/SpawnDev.GameUI)

## Features

- **36 UI elements** - Full game UI toolkit from settings panels to VR floating menus
- **Unified input** - Mouse, keyboard, gamepad, VR controllers, hand tracking, touch, gaze - all through one `GameInput` abstraction
- **4 render modes** - Screen-space 2D, world-space 3D, view-anchored VR HUD, world-anchored AR
- **Theming** - Game-specific visual styles (dark, military/DayZ, bright/Minecraft)
- **Animation** - Tween system with 10 easing functions, fade/slide/pulse extensions
- **WebGPU rendering** - Batched quad renderer (4096 quads/frame), font atlas, alpha blend
- **VR/AR ready** - 3D ray hit testing, poke interaction, controller ray, adaptive interaction, haptic feedback
- **Game HUD** - Health bars, hotbar, compass, minimap, status effects, crosshair, notifications, interaction prompts
- **Screen management** - Push/pop screen stack with transitions, focus navigation, drag-and-drop
- **111 unit tests** - All passing, real production code paths
- **DI integration** - `builder.Services.AddGameUI()` wires everything up

## Installation

```
dotnet add package SpawnDev.GameUI
```

## Dependencies

- [SpawnDev.BlazorJS](https://github.com/LostBeard/SpawnDev.BlazorJS) (3.5.1+) - Browser interop with 450+ typed wrappers

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

// Touch (mobile)
var touchProvider = new TouchProvider();
touchProvider.Attach(canvasRef);
gameInput.AddProvider(touchProvider);
```

## Themes

```csharp
// Set globally
UITheme.Current = UITheme.LostSpawns;  // DayZ military style
UITheme.Current = UITheme.AubsCraft;   // Bright Minecraft style
UITheme.Current = UITheme.Dark;        // Default dark theme

// Override per-element
button.NormalColor = Color.Red;  // This button is red, others follow theme
```

## License

MIT

## Built With

- [SpawnDev.BlazorJS](https://github.com/LostBeard/SpawnDev.BlazorJS) - Full JS interop for Blazor WebAssembly
- Built by TJ (@LostBeard) and the SpawnDev AI crew
