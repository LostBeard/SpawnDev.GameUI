# SpawnDev.GameUI - Feature Plan

## Phase 1: Foundation (Building Now)

### Core Architecture
- [x] UIElement base class with 2D + 3D ray hit testing
- [x] Four render modes: ScreenSpace, WorldSpace, ViewAnchored, WorldAnchored
- [x] UITheme system with nullable overrides (per-element or global)
- [x] Pre-built themes: Dark (default), LostSpawns (DayZ gritty), AubsCraft (bright blocky)
- [x] GameInput unified input abstraction
- [x] Pointer abstraction (mouse, controller, hand, touch, gaze)
- [x] IInputProvider plugin interface
- [x] WGSL shaders for screen-space and world-space rendering

### Elements (Extracted from SpawnScene)
- [x] UIPanel - container with background, border, padding, corner radius
- [x] UILabel - text with font sizes, auto-sizing, alignment
- [x] UIButton - clickable with hover/press states, works with all pointer types
- [x] UISlider - horizontal drag, track + thumb + value label
- [x] UIImage - GPU texture display with placeholder

### Input Providers
- [x] MouseKeyboardProvider - DOM events via BlazorJS typed wrappers, gamepad polling
- [x] XRControllerProvider - WebXR tracked controllers (trigger, grip, thumbstick, ray)
- [x] XRHandProvider - WebXR hand tracking (25 joints, pinch detection, point gesture)
- [x] TouchProvider - DOM touch events with multi-touch support (BlazorJS 3.5.1 Touch fix)

### Rendering
- [x] UIShaders (screen-space + world-space WGSL)
- [x] UIRenderer GPU pipeline (vertex buffer, render pass, bind groups, alpha blend)
- [x] FontAtlas OffscreenCanvas generation (4 sizes: 12, 16, 24, 32 px)
- [x] Image rendering with per-texture bind groups
- [ ] World-space panel rendering with MVP matrix

---

## Phase 2: Layout & More Elements

### Layout System
- [ ] FlexLayout - horizontal/vertical stacking with gap, alignment, wrapping
- [ ] GridLayout - rows x columns with cell spanning
- [ ] Absolute positioning (current default)
- [ ] Auto-sizing: fit-content, fill-parent, fixed
- [ ] Margin and padding on all elements

### Additional Elements
- [ ] UICheckbox - toggle with checkmark icon
- [ ] UITextInput - editable text field with cursor, selection, clipboard
- [ ] UIDropdown - select from list, expandable options panel
- [ ] UIScrollView - scrollable container with scrollbar thumb
- [ ] UIProgressBar - horizontal fill bar with label
- [ ] UITooltip - hover popup with delay and arrow
- [ ] UIList - scrollable list with selectable items
- [ ] UIGrid - icon grid (inventory-style)
- [ ] UISeparator - horizontal/vertical divider line
- [ ] UIToggle - on/off switch (iOS-style)
- [ ] UIRadioGroup - mutually exclusive options

### Text Rendering
- [ ] Multi-line text (word wrap, line break)
- [ ] Text overflow: ellipsis, clip, scroll
- [ ] Bold/italic via separate font atlas pages
- [ ] SDF font rendering via ILGPU kernel (resolution-independent, sharp at any zoom)

---

## Phase 3: VR/AR Integration

### VR Controller Input
- [ ] Controller ray visualization (laser pointer line)
- [ ] Ray-panel intersection for UI interaction
- [ ] Trigger = primary action, grip = secondary, thumbstick = scroll
- [ ] Haptic feedback on hover/click via GamepadHapticActuator
- [ ] Controller model rendering (optional, from XR input profiles)

### Hand Tracking Input
- [ ] 25-joint hand skeleton from XRHand/XRJointSpace
- [ ] Pinch detection (thumb-to-index distance threshold)
- [ ] Point gesture for ray direction (index finger extended)
- [ ] Grab gesture for drag operations
- [ ] Hand visualization (optional joint spheres or mesh)
- [ ] Palm-up menu activation gesture

### VR UI Panels
- [ ] World-space floating panels with billboarding option
- [ ] View-anchored HUD at fixed distance from camera
- [ ] Panel grab-and-move with grip button
- [ ] Panel resize handles
- [ ] Panel snap-to-grid in world space
- [ ] Curved panel rendering (cylinder projection for wide menus)

### AR Features
- [ ] World-anchored labels via XRAnchor
- [ ] Hit test placement (tap to place UI at real-world surface)
- [ ] Depth occlusion (UI behind real objects)
- [ ] Light estimation for UI panel ambient lighting

---

## Phase 4: Game-Specific Features

### Inventory System (Lost Spawns)
- [ ] Drag-and-drop between grid slots
- [ ] Item tooltip on hover (name, weight, condition)
- [ ] Stack splitting (shift-click)
- [ ] Container UI (backpack, chest, vehicle storage)
- [ ] Equipment slots (DayZ-style paper doll)
- [ ] Quick-access hotbar

### Crafting UI (Lost Spawns)
- [ ] Recipe browser with category tabs
- [ ] Material requirement display with availability check
- [ ] Craft progress bar
- [ ] Queue system for multiple crafts

### Health/Status (Lost Spawns)
- [ ] Health bar with damage flash
- [ ] Stamina bar with sprint drain
- [ ] Hunger/thirst meters
- [ ] Temperature indicator
- [ ] Status effect icons with duration timers
- [ ] Blood/damage screen overlay effects

### Interaction Menu (Lost Spawns - DayZ style)
- [ ] Radial menu on interact key
- [ ] Context-sensitive options based on target
- [ ] Hold-to-confirm for important actions

### Admin Panel (AubsCraft)
- [ ] Player list with status indicators
- [ ] Server stats dashboard
- [ ] World map overview
- [ ] Plugin management

---

## Phase 5: Polish & Performance

### Performance
- [ ] Quad batching: minimize draw calls (target: 1-2 per frame for 2D)
- [ ] Dirty flag system: only rebuild vertex data when UI changes
- [ ] Texture atlas packing for multiple images
- [ ] GPU text rendering via SDF (eliminate CPU font rasterization)
- [ ] Object pooling for frequent element create/destroy
- [ ] Profiling: quad count, draw call count, vertex upload size per frame

### Animation
- [ ] Tween system for position, size, color, opacity
- [ ] Ease functions (linear, ease-in, ease-out, bounce, elastic)
- [ ] Transition on show/hide (fade, slide, scale)
- [ ] Hover scale effect on buttons

### Accessibility
- [ ] Focus navigation (tab order)
- [ ] Keyboard shortcuts for common actions
- [ ] High contrast theme option
- [ ] Font size scaling preference

### Testing
- [ ] Demo project with all elements in a gallery
- [ ] DemoConsole for desktop testing
- [ ] PlaywrightMultiTest integration
- [ ] Visual regression tests (screenshot comparison)

---

## Dependencies

- `SpawnDev.BlazorJS` - JS interop, WebXR wrappers (66 classes), Gamepad API
- `SpawnDev.ILGPU` - GPU text rendering (SDF), compute kernels (Phase 5)
- No other external dependencies

## Consuming Projects

| Project | Phase Needed | Key Features |
|---------|-------------|--------------|
| Lost Spawns | Phase 1-4 | Inventory, crafting, health, DayZ menus, VR/AR |
| AubsCraft | Phase 1-3 | Admin panel, VR viewer HUD, settings |
| SpawnScene | Phase 1-2 | Studio UI (replace current SpawnScene.UI) |

## Origin

Extracted from SpawnScene's production UI system. SpawnScene has 1252 lines of battle-tested WebGPU UI code. GameUI generalizes it into a reusable library for the SpawnDev ecosystem.
