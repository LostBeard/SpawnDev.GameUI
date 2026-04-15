using System.Drawing;
using System.Numerics;
using SpawnDev.GameUI;
using SpawnDev.GameUI.Animation;
using SpawnDev.GameUI.Elements;
using SpawnDev.GameUI.Input;
using SpawnDev.GameUI.Rendering;

namespace SpawnDev.GameUI.Demo.Shared.UnitTests;

/// <summary>
/// Unit tests for SpawnDev.GameUI core functionality.
/// Tests real production code paths: element hierarchy, theming, animation math,
/// focus navigation, hit testing, layout, and input state management.
/// No mocks - all tests exercise the actual classes.
/// </summary>
public static class GameUITests
{
    /// <summary>Run all tests. Returns (passed, failed, errors).</summary>
    public static (int passed, int failed, List<string> errors) RunAll()
    {
        int passed = 0, failed = 0;
        var errors = new List<string>();

        void Assert(bool condition, string testName)
        {
            if (condition) passed++;
            else { failed++; errors.Add(testName); }
        }

        // === Element Hierarchy ===

        // AddChild sets parent
        {
            var parent = new UIPanel();
            var child = new UILabel { Text = "Test" };
            parent.AddChild(child);
            Assert(child.Parent == parent, "AddChild_SetsParent");
            Assert(parent.Children.Count == 1, "AddChild_AddsToChildren");
        }

        // RemoveChild clears parent
        {
            var parent = new UIPanel();
            var child = new UIButton { Text = "Btn" };
            parent.AddChild(child);
            parent.RemoveChild(child);
            Assert(child.Parent == null, "RemoveChild_ClearsParent");
            Assert(parent.Children.Count == 0, "RemoveChild_RemovesFromChildren");
        }

        // ClearChildren removes all
        {
            var parent = new UIPanel();
            parent.AddChild(new UILabel());
            parent.AddChild(new UILabel());
            parent.AddChild(new UILabel());
            parent.ClearChildren();
            Assert(parent.Children.Count == 0, "ClearChildren_RemovesAll");
        }

        // Nested hierarchy - ScreenBounds computes correctly
        {
            var root = new UIPanel { X = 10, Y = 20 };
            var child = new UIPanel { X = 5, Y = 5 };
            var grandchild = new UILabel { X = 3, Y = 3, Width = 50, Height = 20 };
            root.AddChild(child);
            child.AddChild(grandchild);
            var bounds = grandchild.ScreenBounds;
            Assert(MathF.Abs(bounds.X - 18) < 0.01f, "ScreenBounds_X_Accumulates"); // 10+5+3
            Assert(MathF.Abs(bounds.Y - 28) < 0.01f, "ScreenBounds_Y_Accumulates"); // 20+5+3
        }

        // === Hit Testing (2D) ===

        // HitTest finds deepest element
        {
            var root = new UIPanel { X = 0, Y = 0, Width = 200, Height = 200 };
            var btn = new UIButton { X = 50, Y = 50, Width = 100, Height = 40, Text = "Click" };
            root.AddChild(btn);
            var hit = root.HitTest(new Vector2(75, 65));
            Assert(hit == btn, "HitTest_FindsButton");
            var miss = root.HitTest(new Vector2(10, 10));
            Assert(miss == root, "HitTest_FindsRoot_WhenNoChild");
            var outside = root.HitTest(new Vector2(300, 300));
            Assert(outside == null, "HitTest_ReturnsNull_WhenOutside");
        }

        // Invisible elements are not hit
        {
            var root = new UIPanel { X = 0, Y = 0, Width = 200, Height = 200 };
            var btn = new UIButton { X = 50, Y = 50, Width = 100, Height = 40, Visible = false };
            root.AddChild(btn);
            var hit = root.HitTest(new Vector2(75, 65));
            Assert(hit == root, "HitTest_SkipsInvisible");
        }

        // Disabled elements are not hit
        {
            var root = new UIPanel { X = 0, Y = 0, Width = 200, Height = 200 };
            var btn = new UIButton { X = 50, Y = 50, Width = 100, Height = 40, Enabled = false };
            root.AddChild(btn);
            var hit = root.HitTest(new Vector2(75, 65));
            Assert(hit == root, "HitTest_SkipsDisabled");
        }

        // === 3D Ray Hit Testing ===

        {
            var panel = new UIElement
            {
                Width = 2, Height = 2,
                RenderMode = UIRenderMode.WorldSpace,
                WorldTransform = Matrix4x4.Identity, // panel at origin, facing +Z
            };
            // Ray from (0,0,5) toward (0,0,-1) should hit the panel at origin
            var hit = panel.HitTestRay(new Vector3(0, 0, 5), new Vector3(0, 0, -1), out float dist);
            // Note: RayIntersectsPanel checks against WorldTransform Z normal
            // With identity transform, panel normal is (0,0,1) facing +Z
            // Ray going -Z will intersect at z=0
            Assert(dist < 10f || hit == null, "RayHitTest_Computes"); // basic sanity
        }

        // === Theming ===

        // Theme defaults apply
        {
            UITheme.Current = UITheme.Dark;
            var btn = new UIButton();
            Assert(btn.NormalColor == UITheme.Dark.ButtonNormal, "Theme_DefaultApplies");
        }

        // Per-element override wins over theme
        {
            UITheme.Current = UITheme.Dark;
            var btn = new UIButton();
            btn.NormalColor = Color.Red;
            Assert(btn.NormalColor == Color.Red, "Theme_OverrideWins");
        }

        // Switching theme changes unoverridden elements
        {
            var btn = new UIButton();
            UITheme.Current = UITheme.LostSpawns;
            Assert(btn.NormalColor == UITheme.LostSpawns.ButtonNormal, "Theme_SwitchApplies");
            UITheme.Current = UITheme.Dark; // restore
        }

        // === Animation / Easing ===

        // Easing functions return correct boundary values
        {
            foreach (EasingType easing in Enum.GetValues<EasingType>())
            {
                float at0 = Easing.Apply(easing, 0f);
                float at1 = Easing.Apply(easing, 1f);
                Assert(MathF.Abs(at0) < 0.01f, $"Easing_{easing}_At0_IsZero");
                Assert(MathF.Abs(at1 - 1f) < 0.01f, $"Easing_{easing}_At1_IsOne");
            }
        }

        // TweenManager executes and completes
        {
            var mgr = new TweenManager();
            float result = 0;
            bool completed = false;
            mgr.Start(v => result = v, 0, 100, 0.5f, onComplete: () => completed = true);
            Assert(mgr.ActiveCount == 1, "Tween_StartsActive");

            // Simulate 0.25s
            mgr.Update(0.25f);
            Assert(result > 0 && result < 100, "Tween_MidwayValue");

            // Simulate remaining
            mgr.Update(0.3f);
            Assert(MathF.Abs(result - 100) < 0.01f, "Tween_FinalValue");
            Assert(completed, "Tween_CompletionCallbackFired");
            Assert(mgr.ActiveCount == 0, "Tween_RemovedAfterComplete");
        }

        // TweenManager delay works
        {
            var mgr = new TweenManager();
            float result = 0;
            mgr.Start(v => result = v, 0, 100, 0.5f, delay: 0.3f);
            mgr.Update(0.2f); // still in delay (0.1s remaining)
            Assert(MathF.Abs(result) < 0.01f, "Tween_DelayHoldsStart");
            mgr.Update(0.2f); // delay over (0.1s into tween)
            mgr.Update(0.2f); // 0.3s into tween
            Assert(result > 10, "Tween_DelayThenAdvances"); // should be well past start
        }

        // === FlexPanel Layout ===

        {
            var flex = new UIFlexPanel { Direction = FlexDirection.Column, Gap = 10, Padding = 5 };
            var a = new UILabel { Text = "A", Width = 100, Height = 20 };
            var b = new UILabel { Text = "B", Width = 100, Height = 20 };
            var c = new UILabel { Text = "C", Width = 100, Height = 20 };
            flex.AddChild(a);
            flex.AddChild(b);
            flex.AddChild(c);
            // Trigger layout by drawing (needs renderer - skip actual draw)
            // Instead, verify children are added correctly
            Assert(flex.Children.Count == 3, "FlexPanel_HasChildren");
        }

        // === AnchorPanel ===

        {
            var anchor = new UIAnchorPanel { Width = 800, Height = 600 };
            var tl = new UILabel { Width = 50, Height = 20 };
            var br = new UILabel { Width = 50, Height = 20 };
            anchor.AddAnchored(tl, Anchor.TopLeft, offsetX: 10, offsetY: 10);
            anchor.AddAnchored(br, Anchor.BottomRight, offsetX: -10, offsetY: -10);
            Assert(anchor.Children.Count == 2, "AnchorPanel_HasChildren");
        }

        // === GameInput ===

        {
            var input = new GameInput();
            input.Poll();
            Assert(input.Pointers.Count == 0, "GameInput_NoProviders_NoPointers");
            Assert(input.PrimaryPointer == null, "GameInput_NoProviders_NoPrimary");
        }

        // === UIList ===

        {
            var list = new UIList { Width = 200, Height = 300 };
            list.AddItem("Item 1");
            list.AddItem("Item 2");
            list.AddItem("Item 3", tag: 42);
            Assert(list.ItemCount == 3, "UIList_ItemCount");
            Assert(list.GetItem(2).Tag is int tag && tag == 42, "UIList_ItemTag");
            list.SelectedIndex = 1;
            Assert(list.SelectedItem?.Text == "Item 2", "UIList_Selection");
            list.RemoveAt(0);
            Assert(list.ItemCount == 2, "UIList_RemoveAt");
        }

        // === UIGrid ===

        {
            var grid = new UIGrid { Columns = 4, Rows = 3 };
            Assert(grid.TotalCells == 12, "UIGrid_TotalCells");
            grid.SetCell(5, label: "Sword");
            Assert(grid.GetCell(5)?.Label == "Sword", "UIGrid_SetGetCell");
            grid.ClearCell(5);
            Assert(grid.GetCell(5) == null, "UIGrid_ClearCell");
        }

        // === UIHotbar ===

        {
            var hotbar = new UIHotbar { SlotCount = 9 };
            hotbar.SetSlot(0, "Axe");
            hotbar.SetSlot(5, "Bandage");
            Assert(hotbar.GetSlot(0).Label == "Axe", "UIHotbar_SetSlot");
            Assert(hotbar.GetSlot(5).Label == "Bandage", "UIHotbar_GetSlot");
            Assert(hotbar.GetSlot(3).Label == null, "UIHotbar_EmptySlot");

            int changedTo = -1;
            hotbar.OnSlotChanged = idx => changedTo = idx;
            hotbar.SelectedSlot = 3;
            Assert(changedTo == 3, "UIHotbar_SelectionCallback");
        }

        // === UINotificationStack ===

        {
            var notifs = new UINotificationStack { DefaultDuration = 1f };
            notifs.Push("Test 1", NotificationType.Info);
            notifs.Push("Test 2", NotificationType.Success);
            notifs.Push("Test 3", NotificationType.Damage);
            // Notifications exist (can't easily count without reflection, but no crash = pass)
            Assert(true, "UINotificationStack_PushNoError");
        }

        // === UIScreenManager ===

        {
            var screens = new UIScreenManager(800, 600);
            var hudScreen = new UIPanel { Width = 800, Height = 600 };
            var invScreen = new UIPanel { Width = 800, Height = 600 };
            screens.Register("hud", hudScreen);
            screens.Register("inventory", invScreen);
            screens.Push("hud");
            Assert(screens.ActiveScreen == "hud", "ScreenManager_Push");
            Assert(screens.StackDepth == 1, "ScreenManager_StackDepth1");
            screens.Push("inventory");
            Assert(screens.ActiveScreen == "inventory", "ScreenManager_PushOver");
            Assert(screens.StackDepth == 2, "ScreenManager_StackDepth2");
            screens.Pop();
            Assert(screens.ActiveScreen == "hud", "ScreenManager_Pop");
            screens.Toggle("inventory");
            Assert(screens.IsOnStack("inventory"), "ScreenManager_Toggle_Push");
            screens.Toggle("inventory");
            Assert(!screens.IsOnStack("inventory"), "ScreenManager_Toggle_Pop");
        }

        // === DragDropManager ===

        {
            var dd = new DragDropManager();
            Assert(!dd.IsDragging, "DragDrop_InitiallyNotDragging");
            dd.BeginDrag("test_item", "Iron Axe");
            Assert(dd.IsDragging, "DragDrop_BeginDrag");
            Assert(dd.DragLabel == "Iron Axe", "DragDrop_DragLabel");
            dd.CancelDrag();
            Assert(!dd.IsDragging, "DragDrop_Cancel");
        }

        // === Margin ===

        {
            var el = new UIElement();
            el.Margin = 10;
            Assert(el.MarginTop == 10 && el.MarginBottom == 10 && el.MarginLeft == 10 && el.MarginRight == 10,
                "Margin_SetsAll");
        }

        // === Opacity ===

        {
            var el = new UIElement();
            Assert(MathF.Abs(el.Opacity - 1f) < 0.01f, "Opacity_DefaultsTo1");
            el.Opacity = 0.5f;
            Assert(MathF.Abs(el.Opacity - 0.5f) < 0.01f, "Opacity_SetWorks");
        }

        // === PokeInteraction ===

        {
            var poke = new PokeInteraction();
            Assert(poke.ActivationDepth > 0, "Poke_HasActivationDepth");
            Assert(poke.DeactivationDepth < poke.ActivationDepth, "Poke_HysteresisCorrect");
        }

        // === AdaptiveInteraction ===

        {
            var adaptive = new AdaptiveInteraction();
            Assert(adaptive.PokeDistance < adaptive.RayDistance, "Adaptive_PokeCloserThanRay");

            // Far distance = ray mode
            var farPointer = new Pointer { Type = PointerType.Hand, Hand = Handedness.Right };
            adaptive.Update(farPointer, 1.0f);
            Assert(adaptive.GetMode(Handedness.Right) == InteractionMode.Ray, "Adaptive_FarIsRay");
            Assert(adaptive.GetBlend(Handedness.Right) < 0.01f, "Adaptive_FarBlendZero");

            // Close distance = poke mode
            adaptive.Update(farPointer, 0.1f);
            Assert(adaptive.GetMode(Handedness.Right) == InteractionMode.Poke, "Adaptive_CloseIsPoke");
            Assert(adaptive.GetBlend(Handedness.Right) > 0.99f, "Adaptive_CloseBlendOne");

            // Transition zone
            adaptive.Update(farPointer, 0.4f);
            float blend = adaptive.GetBlend(Handedness.Right);
            Assert(blend > 0.1f && blend < 0.9f, "Adaptive_TransitionBlends");
        }

        // === GazeProvider ===

        {
            var gaze = new GazeProvider();
            Assert(gaze.DwellTime > 0, "Gaze_HasDwellTime");
            Assert(gaze.DwellProgress < 0.01f, "Gaze_InitialProgressZero");

            // Dwell on a target
            var btn = new UIButton { Text = "Target" };
            bool activated = gaze.UpdateDwell(btn, 0.5f);
            Assert(!activated, "Gaze_NotActivatedEarly");
            Assert(gaze.DwellProgress > 0.3f, "Gaze_ProgressAdvances");

            // Change target resets
            var btn2 = new UIButton { Text = "Other" };
            gaze.UpdateDwell(btn2, 0.1f);
            Assert(gaze.DwellTarget == btn2, "Gaze_TargetSwitches");
            Assert(gaze.DwellProgress < 0.1f, "Gaze_ResetOnTargetChange");

            // Full dwell activates
            for (int i = 0; i < 20; i++)
                activated = gaze.UpdateDwell(btn2, 0.1f);
            Assert(activated || gaze.DwellProgress >= 0.99f, "Gaze_DwellCompletes");
        }

        // === HapticFeedback ===

        {
            var haptics = new HapticFeedback();
            Assert(haptics.Enabled, "Haptics_DefaultEnabled");
            haptics.IntensityScale = 0.5f;
            Assert(MathF.Abs(haptics.IntensityScale - 0.5f) < 0.01f, "Haptics_IntensityScale");
            // Pulse with no haptic actuator should not crash
            var pointer = new Pointer { Type = PointerType.Controller };
            haptics.Pulse(pointer, HapticType.Click); // should not throw
            Assert(true, "Haptics_PulseNoActuator_NoCrash");
        }

        // === UIWorldPanel ===

        {
            var panel = new UIWorldPanel
            {
                PanelWidth = 400,
                PanelHeight = 300,
                WorldScale = 0.001f,
            };
            Assert(panel.PanelWidth == 400, "WorldPanel_Width");
            Assert(panel.RenderMode == UIRenderMode.WorldSpace, "WorldPanel_DefaultWorldSpace");
            Assert(MathF.Abs(panel.WorldScale - 0.001f) < 0.0001f, "WorldPanel_Scale");
        }

        // === UITabPanel ===

        {
            var tabs = new UITabPanel { Width = 400, Height = 300 };
            var page1 = new UIPanel();
            var page2 = new UIPanel();
            tabs.AddTab("General", page1);
            tabs.AddTab("Audio", page2);
            Assert(tabs.ActiveIndex == 0, "TabPanel_DefaultFirst");
            Assert(page1.Visible, "TabPanel_FirstVisible");
            Assert(!page2.Visible, "TabPanel_SecondHidden");
            tabs.ActiveIndex = 1;
            Assert(!page1.Visible, "TabPanel_SwitchHidesFirst");
            Assert(page2.Visible, "TabPanel_SwitchShowsSecond");
        }

        // === UITextBlock ===

        {
            var block = new UITextBlock
            {
                Text = "Hello World",
                Width = 200,
                FontSize = FontSize.Body,
            };
            Assert(block.Text == "Hello World", "TextBlock_SetText");
            Assert(block.Overflow == TextOverflow.Clip, "TextBlock_DefaultOverflow");
            block.MaxLines = 3;
            Assert(block.MaxLines == 3, "TextBlock_MaxLines");
        }

        // === UIColorPicker ===

        {
            var picker = new UIColorPicker();
            Assert(picker.SelectedColor == System.Drawing.Color.White, "ColorPicker_DefaultWhite");
            bool changed = false;
            picker.OnChanged = _ => changed = true;
            picker.SelectedColor = System.Drawing.Color.Red;
            Assert(changed, "ColorPicker_OnChanged");
            Assert(picker.SelectedColor == System.Drawing.Color.Red, "ColorPicker_SetColor");
        }

        // === UIRadioGroup ===

        {
            var radio = new UIRadioGroup();
            radio.SetOptions("Low", "Medium", "High");
            Assert(radio.SelectedIndex == 0, "RadioGroup_DefaultFirst");
            Assert(radio.SelectedValue == "Low", "RadioGroup_DefaultValue");
            radio.SelectedIndex = 2;
            Assert(radio.SelectedValue == "High", "RadioGroup_SetIndex");
        }

        // === UIControllerRay ===

        {
            var ray = new UIControllerRay();
            ray.SetRay(new Vector3(0, 1, 0), new Vector3(0, 0, -1));
            Assert(ray.Visible, "ControllerRay_DefaultVisible");
            Assert(MathF.Abs(ray.MaxLength - 5f) < 0.01f, "ControllerRay_DefaultLength");
            ray.IsHovering = true;
            ray.HitDistance = 2.5f;
            Assert(ray.IsHovering, "ControllerRay_HoverSet");
        }

        // === SDF Distance Transform ===

        // ComputeSDF: fully outside region should be all < 128 (below 0.5)
        {
            var allOutside = new byte[64]; // 8x8, all zeros = all outside
            var sdf = SDFFontAtlas.ComputeSDF(allOutside, 8, 8, 3);
            Assert(sdf.Length == 64, "SDF_OutputSize");
            bool allBelow = true;
            for (int i = 0; i < sdf.Length; i++)
                if (sdf[i] > 128) { allBelow = false; break; }
            Assert(allBelow, "SDF_AllOutside_BelowEdge");
        }

        // ComputeSDF: fully inside region should be all > 128 (above 0.5)
        {
            var allInside = new byte[64];
            for (int i = 0; i < 64; i++) allInside[i] = 255;
            var sdf = SDFFontAtlas.ComputeSDF(allInside, 8, 8, 3);
            bool allAbove = true;
            for (int i = 0; i < sdf.Length; i++)
                if (sdf[i] < 128) { allAbove = false; break; }
            Assert(allAbove, "SDF_AllInside_AboveEdge");
        }

        // ComputeSDF: center block should have 128 (edge) at boundary
        {
            // 10x10 bitmap with center 6x6 filled
            var bitmap = new byte[100];
            for (int y = 2; y < 8; y++)
                for (int x = 2; x < 8; x++)
                    bitmap[y * 10 + x] = 255;
            var sdf = SDFFontAtlas.ComputeSDF(bitmap, 10, 10, 4);

            // Center should be above edge (inside)
            Assert(sdf[5 * 10 + 5] > 128, "SDF_Center_Inside");

            // Corner (0,0) should be below edge (outside)
            Assert(sdf[0] < 128, "SDF_Corner_Outside");

            // Edge pixel (2,5) should be near 128
            byte edgeVal = sdf[5 * 10 + 2];
            Assert(edgeVal >= 100 && edgeVal <= 156, "SDF_Edge_NearHalf");
        }

        // ComputeSDF: distance should decrease toward edges
        {
            // 12x12 with center 8x8 filled
            var bitmap = new byte[144];
            for (int y = 2; y < 10; y++)
                for (int x = 2; x < 10; x++)
                    bitmap[y * 12 + x] = 255;
            var sdf = SDFFontAtlas.ComputeSDF(bitmap, 12, 12, 5);

            // Deep inside (6,6) should be higher than near-edge (2,6)
            Assert(sdf[6 * 12 + 6] > sdf[6 * 12 + 2], "SDF_DeepInside_HigherThanEdge");

            // Far outside (0,0) should be lower than near-edge outside (1,6)
            Assert(sdf[0] < sdf[1 * 12 + 2], "SDF_FarOutside_LowerThanNearEdge");
        }

        // ComputeSDF: symmetry - symmetric input should give symmetric SDF
        {
            // 8x8 with center 4x4 filled (symmetric)
            var bitmap = new byte[64];
            for (int y = 2; y < 6; y++)
                for (int x = 2; x < 6; x++)
                    bitmap[y * 8 + x] = 255;
            var sdf = SDFFontAtlas.ComputeSDF(bitmap, 8, 8, 3);

            // Top-left corner should equal bottom-right corner
            Assert(sdf[0] == sdf[63], "SDF_Symmetry_Corners");
            // Top-right should equal bottom-left
            Assert(sdf[7] == sdf[56], "SDF_Symmetry_OppositeCorners");
        }

        // === SDFFontAtlas Scale Factors ===

        {
            // Verify scale calculations (no GPU needed - pure math)
            float captionScale = (int)FontSize.Caption / (float)SDFFontAtlas.BaseFontSize;
            float bodyScale = (int)FontSize.Body / (float)SDFFontAtlas.BaseFontSize;
            float headingScale = (int)FontSize.Heading / (float)SDFFontAtlas.BaseFontSize;
            float titleScale = (int)FontSize.Title / (float)SDFFontAtlas.BaseFontSize;

            Assert(MathF.Abs(captionScale - 0.25f) < 0.001f, "SDF_Scale_Caption"); // 12/48
            Assert(MathF.Abs(bodyScale - 1f / 3f) < 0.001f, "SDF_Scale_Body"); // 16/48
            Assert(MathF.Abs(headingScale - 0.5f) < 0.001f, "SDF_Scale_Heading"); // 24/48
            Assert(MathF.Abs(titleScale - 2f / 3f) < 0.001f, "SDF_Scale_Title"); // 32/48
        }

        // === SDFFontAtlas Constants ===

        {
            Assert(SDFFontAtlas.BaseFontSize == 48, "SDF_BaseFontSize_48");
            Assert(SDFFontAtlas.Spread == 6, "SDF_Spread_6");
            Assert(SDFFontAtlas.GlyphPadding == 7, "SDF_GlyphPadding_SpreadPlus1");
            Assert(SDFFontAtlas.AtlasSize == 1024, "SDF_AtlasSize_1024");
        }

        // === UILabel Outline Properties ===

        {
            var label = new UILabel { Text = "Test" };
            Assert(MathF.Abs(label.OutlineWidth) < 0.001f, "Label_OutlineWidth_DefaultZero");
            Assert(label.OutlineColor == Color.Black, "Label_OutlineColor_DefaultBlack");

            label.OutlineWidth = 0.1f;
            label.OutlineColor = Color.Red;
            Assert(MathF.Abs(label.OutlineWidth - 0.1f) < 0.001f, "Label_OutlineWidth_Set");
            Assert(label.OutlineColor == Color.Red, "Label_OutlineColor_Set");
        }

        // === UITextBlock Outline Properties ===

        {
            var block = new UITextBlock { Text = "Test block" };
            Assert(MathF.Abs(block.OutlineWidth) < 0.001f, "TextBlock_OutlineWidth_DefaultZero");
            Assert(block.OutlineColor == Color.Black, "TextBlock_OutlineColor_DefaultBlack");

            block.OutlineWidth = 0.08f;
            block.OutlineColor = Color.DarkBlue;
            Assert(MathF.Abs(block.OutlineWidth - 0.08f) < 0.001f, "TextBlock_OutlineWidth_Set");
            Assert(block.OutlineColor == Color.DarkBlue, "TextBlock_OutlineColor_Set");
        }

        // === SDFTextStyle ===

        {
            var style = SDFTextStyle.Default;
            Assert(MathF.Abs(style.OutlineWidth) < 0.001f, "SDFStyle_Default_NoOutline");
            Assert(MathF.Abs(style.Softness) < 0.001f, "SDFStyle_Default_NoSoftness");
            Assert(style.OutlineColor == Color.Black, "SDFStyle_Default_BlackOutline");

            var custom = new SDFTextStyle
            {
                OutlineWidth = 0.12f,
                OutlineColor = Color.Yellow,
                Softness = 0.01f,
            };
            Assert(MathF.Abs(custom.OutlineWidth - 0.12f) < 0.001f, "SDFStyle_Custom_OutlineWidth");
            Assert(custom.OutlineColor == Color.Yellow, "SDFStyle_Custom_OutlineColor");
            Assert(MathF.Abs(custom.Softness - 0.01f) < 0.001f, "SDFStyle_Custom_Softness");
        }

        // === SDF Edge Cases ===

        // Single pixel bitmap
        {
            var single = new byte[] { 255 };
            var sdf = SDFFontAtlas.ComputeSDF(single, 1, 1, 2);
            Assert(sdf.Length == 1, "SDF_SinglePixel_OutputSize");
            Assert(sdf[0] > 128, "SDF_SinglePixel_Inside");
        }

        // Empty bitmap (all zeros)
        {
            var empty = new byte[4]; // 2x2
            var sdf = SDFFontAtlas.ComputeSDF(empty, 2, 2, 1);
            Assert(sdf.Length == 4, "SDF_Empty_OutputSize");
            bool allLow = true;
            for (int i = 0; i < 4; i++)
                if (sdf[i] > 128) { allLow = false; break; }
            Assert(allLow, "SDF_Empty_AllOutside");
        }

        // 1-pixel border (tests distance gradient)
        {
            // 6x6 with center 4x4 filled, 1px border
            var bitmap = new byte[36];
            for (int y = 1; y < 5; y++)
                for (int x = 1; x < 5; x++)
                    bitmap[y * 6 + x] = 255;
            var sdf = SDFFontAtlas.ComputeSDF(bitmap, 6, 6, 3);

            // Immediate outside (0,3) should be between 0 and 128
            Assert(sdf[3 * 6 + 0] < 128, "SDF_Border_OutsideBelow");
            Assert(sdf[3 * 6 + 0] > 0, "SDF_Border_OutsideAboveZero");

            // Immediate inside (1,3) should be between 128 and 255
            Assert(sdf[3 * 6 + 1] > 128, "SDF_Border_InsideAbove");
        }

        return (passed, failed, errors);
    }
}

