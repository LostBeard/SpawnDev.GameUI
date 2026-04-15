using System.Drawing;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// A single crafting recipe.
/// </summary>
public class CraftingRecipe
{
    /// <summary>Unique recipe identifier.</summary>
    public string Id { get; set; } = "";

    /// <summary>Display name of the output item.</summary>
    public string Name { get; set; } = "";

    /// <summary>Category for filtering (e.g., "Weapons", "Tools", "Food", "Building").</summary>
    public string Category { get; set; } = "";

    /// <summary>Required ingredients: (item name, required count).</summary>
    public List<(string Name, int Count)> Ingredients { get; set; } = new();

    /// <summary>Output count.</summary>
    public int OutputCount { get; set; } = 1;

    /// <summary>Crafting time in seconds. 0 = instant.</summary>
    public float CraftTime { get; set; }

    /// <summary>Whether the player currently has all required ingredients.</summary>
    public bool CanCraft { get; set; }

    /// <summary>User data.</summary>
    public object? Tag { get; set; }
}

/// <summary>
/// Crafting recipe browser with category tabs, recipe list, and detail view.
/// Shows available recipes, required ingredients, and a craft button with progress.
///
/// Layout:
///   [Category Tabs: All | Weapons | Tools | Food | Building]
///   [Recipe List (scrollable)]  [Detail Panel          ]
///   [ - Iron Sword             ]  [ Name: Iron Sword    ]
///   [ - Iron Axe (selected)    ]  [ Requires:           ]
///   [ - Wooden Shield          ]  [   2x Iron Bar       ]
///   [                          ]  [   1x Wood Plank     ]
///   [                          ]  [ Time: 3s            ]
///   [                          ]  [ [=====>    ] Craft   ]
///
/// Usage:
///   var crafting = new UICraftingPanel();
///   crafting.AddRecipe(new CraftingRecipe {
///       Id = "iron_sword", Name = "Iron Sword", Category = "Weapons",
///       Ingredients = { ("Iron Bar", 2), ("Wood Plank", 1) },
///       CraftTime = 3f, CanCraft = true,
///   });
///   crafting.OnCraft = (recipe) => StartCrafting(recipe);
/// </summary>
public class UICraftingPanel : UIPanel
{
    private readonly List<CraftingRecipe> _recipes = new();
    private readonly List<string> _categories = new() { "All" };
    private string _activeCategory = "All";
    private int _selectedIndex = -1;
    private int _hoveredIndex = -1;
    private float _scrollOffset;

    // Crafting progress
    private CraftingRecipe? _craftingRecipe;
    private float _craftProgress;
    private bool _isCrafting;

    /// <summary>Called when the player initiates crafting.</summary>
    public Action<CraftingRecipe>? OnCraft { get; set; }

    /// <summary>Called when a recipe is selected (for preview).</summary>
    public Action<CraftingRecipe>? OnSelected { get; set; }

    /// <summary>Currently selected recipe, or null.</summary>
    public CraftingRecipe? SelectedRecipe =>
        _selectedIndex >= 0 && _selectedIndex < FilteredRecipes.Count
            ? FilteredRecipes[_selectedIndex] : null;

    /// <summary>Whether a craft is in progress.</summary>
    public bool IsCrafting => _isCrafting;

    /// <summary>Current craft progress 0-1.</summary>
    public float CraftProgress => _craftProgress;

    // Layout
    /// <summary>Width of the recipe list column.</summary>
    public float ListWidth { get; set; } = 200f;

    /// <summary>Height of each recipe list item.</summary>
    public float ItemHeight { get; set; } = 28f;

    /// <summary>Height of the category tab bar.</summary>
    public float TabHeight { get; set; } = 30f;

    // Colors
    private Color? _itemColor, _itemHoverColor, _itemSelectedColor, _canCraftColor, _cantCraftColor;
    public Color ItemColor { get => _itemColor ?? Color.FromArgb(160, 30, 30, 40); set => _itemColor = value; }
    public Color ItemHoverColor { get => _itemHoverColor ?? Color.FromArgb(200, 50, 50, 65); set => _itemHoverColor = value; }
    public Color ItemSelectedColor { get => _itemSelectedColor ?? Color.FromArgb(200, 108, 92, 231); set => _itemSelectedColor = value; }
    public Color CanCraftColor { get => _canCraftColor ?? Color.FromArgb(200, 50, 200, 50); set => _canCraftColor = value; }
    public Color CantCraftColor { get => _cantCraftColor ?? Color.FromArgb(120, 200, 50, 50); set => _cantCraftColor = value; }

    private List<CraftingRecipe> FilteredRecipes =>
        _activeCategory == "All"
            ? _recipes
            : _recipes.FindAll(r => r.Category == _activeCategory);

    public UICraftingPanel()
    {
        Width = 500;
        Height = 350;
        Padding = 8;
    }

    /// <summary>Add a recipe.</summary>
    public void AddRecipe(CraftingRecipe recipe)
    {
        _recipes.Add(recipe);
        if (!string.IsNullOrEmpty(recipe.Category) && !_categories.Contains(recipe.Category))
            _categories.Add(recipe.Category);
    }

    /// <summary>Remove a recipe by Id.</summary>
    public bool RemoveRecipe(string id)
    {
        int idx = _recipes.FindIndex(r => r.Id == id);
        if (idx < 0) return false;
        _recipes.RemoveAt(idx);
        return true;
    }

    /// <summary>Get a recipe by Id.</summary>
    public CraftingRecipe? GetRecipe(string id) => _recipes.Find(r => r.Id == id);

    /// <summary>Clear all recipes.</summary>
    public void ClearRecipes()
    {
        _recipes.Clear();
        _categories.Clear();
        _categories.Add("All");
        _selectedIndex = -1;
    }

    /// <summary>Number of recipes.</summary>
    public int RecipeCount => _recipes.Count;

    /// <summary>All registered categories.</summary>
    public IReadOnlyList<string> Categories => _categories;

    /// <summary>Active category filter.</summary>
    public string ActiveCategory
    {
        get => _activeCategory;
        set
        {
            if (_categories.Contains(value))
            {
                _activeCategory = value;
                _selectedIndex = -1;
                _scrollOffset = 0;
            }
        }
    }

    /// <summary>Start a crafting operation. Call when craft conditions are met.</summary>
    public void StartCraft(CraftingRecipe recipe)
    {
        if (_isCrafting) return;
        _craftingRecipe = recipe;
        _craftProgress = 0;
        _isCrafting = true;
        if (recipe.CraftTime <= 0)
        {
            // Instant craft
            CompleteCraft();
        }
    }

    /// <summary>Cancel the current crafting operation.</summary>
    public void CancelCraft()
    {
        _isCrafting = false;
        _craftingRecipe = null;
        _craftProgress = 0;
    }

    private void CompleteCraft()
    {
        if (_craftingRecipe != null)
            OnCraft?.Invoke(_craftingRecipe);
        _isCrafting = false;
        _craftingRecipe = null;
        _craftProgress = 0;
    }

    public override void Update(GameInput input, float dt)
    {
        if (!Visible || !Enabled) return;

        // Advance crafting progress
        if (_isCrafting && _craftingRecipe != null && _craftingRecipe.CraftTime > 0)
        {
            _craftProgress += dt / _craftingRecipe.CraftTime;
            if (_craftProgress >= 1f)
            {
                _craftProgress = 1f;
                CompleteCraft();
            }
        }

        // Input handling
        var filtered = FilteredRecipes;
        _hoveredIndex = -1;

        foreach (var pointer in input.Pointers)
        {
            if (!pointer.ScreenPosition.HasValue) continue;
            var mp = pointer.ScreenPosition.Value;
            var bounds = ScreenBounds;

            // Category tab clicks
            float tabX = bounds.X + Padding;
            float tabY = bounds.Y + Padding;
            for (int i = 0; i < _categories.Count; i++)
            {
                float tw = 60f; // fixed tab width
                if (mp.X >= tabX && mp.X < tabX + tw &&
                    mp.Y >= tabY && mp.Y < tabY + TabHeight)
                {
                    if (pointer.WasReleased)
                        ActiveCategory = _categories[i];
                }
                tabX += tw + 4;
            }

            // Recipe list clicks
            float listX = bounds.X + Padding;
            float listY = bounds.Y + Padding + TabHeight + 4;
            float listH = bounds.Height - Padding * 2 - TabHeight - 4;

            for (int i = 0; i < filtered.Count; i++)
            {
                float iy = listY + i * ItemHeight - _scrollOffset;
                if (iy + ItemHeight < listY || iy > listY + listH) continue; // clipped

                if (mp.X >= listX && mp.X < listX + ListWidth &&
                    mp.Y >= iy && mp.Y < iy + ItemHeight)
                {
                    _hoveredIndex = i;
                    if (pointer.WasReleased)
                    {
                        _selectedIndex = i;
                        OnSelected?.Invoke(filtered[i]);
                    }
                }
            }

            // Scroll in list area
            if (mp.X >= listX && mp.X < listX + ListWidth &&
                mp.Y >= listY && mp.Y < listY + listH)
            {
                float maxScroll = MathF.Max(0, filtered.Count * ItemHeight - listH);
                _scrollOffset = MathF.Max(0, MathF.Min(_scrollOffset - pointer.ScrollDelta * 30, maxScroll));
            }
        }

        base.Update(input, dt);
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        var bounds = ScreenBounds;
        var filtered = FilteredRecipes;

        // Panel background
        renderer.DrawRect(bounds.X, bounds.Y, bounds.Width, bounds.Height, BackgroundColor);

        // Category tabs
        float tabX = bounds.X + Padding;
        float tabY = bounds.Y + Padding;
        foreach (var cat in _categories)
        {
            float tw = 60f;
            bool active = cat == _activeCategory;
            var tabBg = active
                ? Color.FromArgb(200, 80, 70, 160)
                : Color.FromArgb(120, 40, 40, 55);
            renderer.DrawRect(tabX, tabY, tw, TabHeight, tabBg);

            float textW = renderer.MeasureText(cat, FontSize.Caption);
            float textX = tabX + (tw - textW) / 2;
            float textY = tabY + (TabHeight - renderer.GetLineHeight(FontSize.Caption)) / 2;
            renderer.DrawText(cat, textX, textY, FontSize.Caption,
                active ? Color.White : Color.FromArgb(180, 180, 180, 180));

            tabX += tw + 4;
        }

        // Recipe list
        float listX = bounds.X + Padding;
        float listY = bounds.Y + Padding + TabHeight + 4;
        float listH = bounds.Height - Padding * 2 - TabHeight - 4;

        // List background
        renderer.DrawRect(listX, listY, ListWidth, listH, Color.FromArgb(80, 0, 0, 0));

        for (int i = 0; i < filtered.Count; i++)
        {
            float iy = listY + i * ItemHeight - _scrollOffset;
            if (iy + ItemHeight < listY || iy > listY + listH) continue;

            var recipe = filtered[i];
            Color bg = i == _selectedIndex ? ItemSelectedColor :
                       i == _hoveredIndex ? ItemHoverColor : ItemColor;
            renderer.DrawRect(listX, iy, ListWidth, ItemHeight - 1, bg);

            // Craftability indicator
            var nameColor = recipe.CanCraft ? CanCraftColor : CantCraftColor;
            renderer.DrawText(recipe.Name, listX + 6, iy + 5, FontSize.Caption, nameColor);
        }

        // Detail panel (right of list)
        float detailX = listX + ListWidth + 8;
        float detailW = bounds.Width - Padding * 2 - ListWidth - 8;
        renderer.DrawRect(detailX, listY, detailW, listH, Color.FromArgb(60, 0, 0, 0));

        var selected = SelectedRecipe;
        if (selected != null)
        {
            float dy = listY + 8;

            // Recipe name
            renderer.DrawText(selected.Name, detailX + 8, dy, FontSize.Heading, Color.White);
            dy += renderer.GetLineHeight(FontSize.Heading) + 8;

            // Output count
            if (selected.OutputCount > 1)
            {
                renderer.DrawText($"Produces: {selected.OutputCount}", detailX + 8, dy, FontSize.Body,
                    Color.FromArgb(200, 200, 200, 200));
                dy += renderer.GetLineHeight(FontSize.Body) + 4;
            }

            // Ingredients header
            renderer.DrawText("Requires:", detailX + 8, dy, FontSize.Body, Color.FromArgb(200, 180, 180, 180));
            dy += renderer.GetLineHeight(FontSize.Body) + 2;

            // Ingredient list
            foreach (var (name, count) in selected.Ingredients)
            {
                string text = count > 1 ? $"  {count}x {name}" : $"  {name}";
                renderer.DrawText(text, detailX + 8, dy, FontSize.Caption,
                    Color.FromArgb(220, 220, 220, 220));
                dy += renderer.GetLineHeight(FontSize.Caption) + 1;
            }

            dy += 8;

            // Craft time
            if (selected.CraftTime > 0)
            {
                string timeStr = selected.CraftTime >= 60
                    ? $"Time: {selected.CraftTime / 60:F0}m {selected.CraftTime % 60:F0}s"
                    : $"Time: {selected.CraftTime:F1}s";
                renderer.DrawText(timeStr, detailX + 8, dy, FontSize.Caption,
                    Color.FromArgb(200, 160, 160, 160));
                dy += renderer.GetLineHeight(FontSize.Caption) + 8;
            }

            // Craft button / progress bar
            float btnW = detailW - 16;
            float btnH = 28;
            float btnX = detailX + 8;
            float btnY = listY + listH - btnH - 8;

            if (_isCrafting && _craftingRecipe?.Id == selected.Id)
            {
                // Progress bar
                renderer.DrawRect(btnX, btnY, btnW, btnH, Color.FromArgb(160, 30, 30, 40));
                renderer.DrawRect(btnX, btnY, btnW * _craftProgress, btnH,
                    Color.FromArgb(200, 80, 180, 80));
                string pctText = $"{(int)(_craftProgress * 100)}%";
                float pctW = renderer.MeasureText(pctText, FontSize.Body);
                renderer.DrawText(pctText, btnX + (btnW - pctW) / 2,
                    btnY + (btnH - renderer.GetLineHeight(FontSize.Body)) / 2,
                    FontSize.Body, Color.White);
            }
            else
            {
                // Craft button
                var btnColor = selected.CanCraft
                    ? Color.FromArgb(200, 40, 120, 40)
                    : Color.FromArgb(120, 60, 60, 60);
                renderer.DrawRect(btnX, btnY, btnW, btnH, btnColor);

                string btnText = selected.CanCraft ? "Craft" : "Missing Materials";
                float btnTextW = renderer.MeasureText(btnText, FontSize.Body);
                renderer.DrawText(btnText, btnX + (btnW - btnTextW) / 2,
                    btnY + (btnH - renderer.GetLineHeight(FontSize.Body)) / 2,
                    FontSize.Body, selected.CanCraft ? Color.White : Color.Gray);
            }
        }
        else
        {
            // No recipe selected
            string hint = filtered.Count > 0 ? "Select a recipe" : "No recipes available";
            float hw = renderer.MeasureText(hint, FontSize.Body);
            renderer.DrawText(hint, detailX + (detailW - hw) / 2, listY + listH / 2,
                FontSize.Body, Color.FromArgb(120, 180, 180, 180));
        }
    }
}
