# SpawnDev.GameUI - Feature Plan

**Current: 40+ elements, 50+ source files, ~12000 lines. All phases shipping.**

## Phase 1: Foundation - COMPLETE

### Core Architecture - ALL DONE
- [x] UIElement base class with 2D + 3D ray hit testing, Opacity, Margin
- [x] Four render modes: ScreenSpace, WorldSpace, ViewAnchored, WorldAnchored
- [x] UITheme system with nullable overrides + 3 presets (Dark, LostSpawns, AubsCraft)
- [x] GameInput unified input abstraction with Pointer
- [x] IInputProvider plugin interface
- [x] WGSL shaders for screen-space and world-space rendering
- [x] UIScreenManager - push/pop/toggle screen stack with transitions

### Elements (5 from SpawnScene) - ALL DONE
- [x] UIPanel, UILabel, UIButton, UISlider, UIImage

### Input Providers (4) - ALL DONE
- [x] MouseKeyboardProvider, XRControllerProvider, XRHandProvider, TouchProvider

### Rendering - 3/4 DONE
- [x] UIRenderer GPU pipeline (4096 quad batch, alpha blend)
- [x] FontAtlas OffscreenCanvas (4 sizes)
- [x] Image rendering with per-texture bind groups
- [ ] **World-space panel rendering with MVP matrix** (shader written, pipeline not wired)

---

## Phase 2: Layout & More Elements - MOSTLY DONE

### Layout System - 4/5 DONE
- [x] FlexLayout (UIFlexPanel) - rows/columns, gap, alignment, auto-size
- [x] Anchor positioning (UIAnchorPanel) - 9 anchor points for HUD layout
- [x] Universal Margin on UIElement (MarginTop/Bottom/Left/Right)
- [x] Percentage sizing (WidthPercent/HeightPercent/XPercent/YPercent on UIElement)
- [ ] Grid layout with cell spanning (UIGrid has fixed cells but no spanning)

### Elements (15 additional) - ALL DONE
- [x] UICheckbox, UIToggle, UISlider, UIDropdown, UIRadioGroup
- [x] UITextInput (cursor, keyboard, placeholder, submit)
- [x] UITextBlock (multi-line word wrap, overflow ellipsis, alignment)
- [x] UIScrollView (wheel, thumbstick, scrollbar drag)
- [x] UIList (selectable items), UIGrid (inventory cells)
- [x] UIProgressBar (threshold colors), UISeparator
- [x] UITooltip (multi-line, static Show/Hide)
- [x] UITabPanel (tabbed container with header switching)
- [x] UIColorPicker (RGB sliders + preset swatches)
- [x] UIKeyBindDisplay (action-to-key mapping, click-to-rebind)

### Text Rendering - 2/4 DONE
- [x] Multi-line text with word wrap (UITextBlock)
- [x] SDF font rendering (SDFFontAtlas + Chamfer distance transform, R8Unorm, outline support)
- [ ] Text overflow: ellipsis (done), clip (done), scroll (TODO)
- [ ] Bold/italic via separate font atlas pages

---

## Phase 2b: Game Elements - DONE

- [x] UIHotbar - quick-access bar (1-9 keys, scroll, right-click context)
- [x] UIStatusHUD - health/stamina/hunger/thirst/temperature with threshold colors
- [x] UIRadialMenu - DayZ-style context menu with hold-to-confirm
- [x] UINotificationStack - animated toasts (Info/Success/Warning/Damage/Achievement)
- [x] UICrosshair - 4 styles (Dot/Cross/Plus/Brackets), target-aware color
- [x] UIContextMenu - right-click menu with separators, shortcuts, keyboard nav
- [x] UIMinimapFrame - compass bearing, coordinates, zoom, cardinal markers
- [x] UIDebugOverlay - immediate-mode F3 debug display

---

## Phase 2c: Systems - DONE

- [x] UIScreenManager - screen stack with push/pop/toggle, transitions, dim
- [x] UIFocusManager - Tab/arrow/D-pad navigation, directional focus
- [x] Animation: Tween (struct), TweenManager (256 pool), 10 easings
- [x] Animation: FadeIn/Out, SlideIn/Out, Pulse extensions

---

## Phase 3: VR/AR Integration - NOT STARTED (foundations built)

### VR Controller Input
- [ ] Controller ray visualization (laser pointer)
- [ ] Haptic feedback on hover/click
- [ ] Controller model rendering

### Hand Tracking
- [ ] Poke interaction (finger tip intersects panel)
- [ ] Grab gesture for panel drag
- [ ] Palm-up menu activation
- [ ] Distance-adaptive interaction (ray vs poke)

### VR UI Panels
- [ ] World-space floating panels with billboarding
- [ ] View-anchored HUD at fixed distance
- [ ] Panel grab-and-move with grip
- [ ] Curved panel rendering

### AR Features
- [ ] World-anchored labels via XRAnchor
- [ ] Hit test placement
- [ ] Depth occlusion

---

## Phase 4: Game-Specific Features - PARTIAL

### Done
- [x] Inventory grid (UIGrid)
- [x] Hotbar with number keys
- [x] Health/stamina/hunger/thirst/temperature (UIStatusHUD)
- [x] Radial interaction menu with hold-to-confirm
- [x] Context menu
- [x] Toast notifications

### TODO
- [x] Drag-and-drop between grid slots (UIGrid.EnableDragDrop, SwapCells, MoveCell, GetCellAtPosition)
- [x] Equipment slots (UIEquipmentPanel: 10 typed slots, paper doll layout, type filtering)
- [x] Crafting recipe browser + progress bar (UICraftingPanel: categories, ingredients, craft progress)
- [x] Status effect icons with duration timers (UIStatusEffects: buff/debuff icons, auto-expire, stacks)
- [x] Screen overlay effects (UIScreenOverlay: Flash, FadeIn/Out, persistent overlays)

---

## Phase 5: Polish & Performance - PARTIAL

### Done
- [x] Animation system (10 easings, extensions)
- [x] Focus navigation (Tab, arrows, D-pad)
- [x] Quad batching (UIRenderer)
- [x] Debug overlay

### TODO
- [x] SDF fonts (SDFFontAtlas: Chamfer DT, R8Unorm, outline/glow via WGSL, auto-enabled)
- [x] Nine-slice rendering (DrawNineSlice on UIRenderer, NineSliceBorder, UIPanel texture support)
- [ ] Texture atlas consolidation (one draw call for all images)
- [ ] Compressed vertex format (fp16 + unorm8 = 12 bytes)
- [ ] Dirty flag system (universal)
- [x] High contrast / colorblind themes (HighContrast, ColorblindSafe, TritanopiaSafe presets)
- [x] Font size scaling (UITheme.FontScale, DrawText float overload, auto-applied)
- [ ] Particle effects on UI
- [ ] UI serialization (JSON layout definitions)

---

## Dependencies

- `SpawnDev.BlazorJS` 3.5.1+ (browser interop, WebXR, Gamepad, Touch)
- `SpawnDev.ILGPU` (future: SDF fonts, GPU layout, texture atlas packing)

## Consuming Projects

| Project | Key Features Needed |
|---------|-------------------|
| Lost Spawns | StatusHUD, Hotbar, Inventory Grid, RadialMenu, Crosshair, Minimap, P2P Reputation UI |
| AubsCraft | TabPanel settings, VR viewer HUD, admin panels |
| SpawnScene | Studio UI (replace current SpawnScene.UI) |
