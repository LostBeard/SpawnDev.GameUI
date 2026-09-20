using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.GameUI.Elements;

namespace SpawnDev.GameUI.Rendering;

/// <summary>Metrics for a single character in the font atlas.</summary>
public struct CharMetrics
{
    /// <summary>UV coordinates in the atlas texture (normalized 0-1).</summary>
    public float U0, V0, U1, V1;
    /// <summary>Character advance width in pixels.</summary>
    public float Advance;
    /// <summary>Character glyph width and height in pixels.</summary>
    public float Width, Height;
    /// <summary>Distance from the top of the glyph cell to the alphabetic baseline.</summary>
    public float BearingY;
}

/// <summary>
/// Generates a bitmap font atlas at runtime using OffscreenCanvas,
/// then uploads it to a WebGPU texture for UI text rendering.
/// White glyphs on transparent background - tinted by vertex color at render time.
/// 1024x1024 RGBA, 4 font sizes, ASCII 32-126.
///
/// All JS interop via SpawnDev.SpawnJS typed wrappers (OffscreenCanvas, CanvasRenderingContext2D).
/// Ported from SpawnScene's production FontAtlas.
/// </summary>
public class FontAtlas : IDisposable
{
    private const int AtlasSize = 1024;
    private const string FontFamily = "Inter, system-ui, -apple-system, sans-serif";
    private const int FirstChar = 32;  // space
    private const int LastChar = 126;  // tilde
    private const int CellPad = 1;

    private readonly Dictionary<FontSize, Dictionary<char, CharMetrics>> _metrics = new();

    public GPUTexture? Texture { get; private set; }
    public GPUTextureView? View { get; private set; }
    public bool IsReady => View != null;

    /// <summary>
    /// Generate the font atlas and upload to GPU.
    /// Must be called after WebGPU device is available.
    /// Uses OffscreenCanvas 2D context for glyph rasterization.
    /// </summary>
    public void Init(GPUDevice device, GPUQueue queue)
    {
        using var canvas = new OffscreenCanvas(AtlasSize, AtlasSize);
        using var ctx = canvas.Get2DContext();

        // Clear to transparent
        ctx.ClearRect(0, 0, AtlasSize, AtlasSize);

        // White text on transparent background (tinted by vertex color at render time).
        // Alphabetic baseline: FontBoundingBoxAscent/Descent are defined relative to it.
        // TextBaseline=top + those metrics left empty space under the ink, so centered UI
        // text sat high in its cell (and bilinear sampling of misaligned quads looked
        // thin/thick like a DX9 half-pixel miss).
        ctx.FillStyle = "white";
        ctx.TextBaseline = "alphabetic";

        int cursorX = 1;
        int cursorY = 1;
        int rowHeight = 0;

        // Generate glyphs for each font size
        foreach (var size in new[] { FontSize.Caption, FontSize.Body, FontSize.Heading, FontSize.Title })
        {
            int px = (int)size;
            ctx.Font = $"{px}px {FontFamily}";
            var charMap = new Dictionary<char, CharMetrics>();

            // Em-box metrics for this size (use 'M' as reference)
            using var mMetrics = ctx.MeasureText("M");
            float ascent = (float)mMetrics.FontBoundingBoxAscent;
            float descent = (float)mMetrics.FontBoundingBoxDescent;
            float lineHeight = ascent + descent;
            int glyphHeight = (int)Math.Ceiling(lineHeight) + CellPad * 2;

            for (int c = FirstChar; c <= LastChar; c++)
            {
                char ch = (char)c;
                string s = ch.ToString();

                using var tm = ctx.MeasureText(s);
                float advance = (float)tm.Width;
                int glyphWidth = (int)Math.Ceiling(advance) + CellPad * 2;

                // Wrap to next row if needed
                if (cursorX + glyphWidth + 1 >= AtlasSize)
                {
                    cursorX = 1;
                    cursorY += rowHeight + 1;
                    rowHeight = 0;
                }

                if (cursorY + glyphHeight + 1 >= AtlasSize)
                    break; // Atlas full

                // Baseline sits CellPad + ascent below the cell top so ink fills the cell.
                float baselineY = cursorY + CellPad + ascent;
                ctx.FillText(s, cursorX + CellPad, baselineY);

                charMap[ch] = new CharMetrics
                {
                    // Half-texel inset: with linear sampling, edge UVs sit on texel
                    // boundaries and bleed into neighbors (uneven stem weight). Inset
                    // keeps the filter kernel inside the glyph's padded cell.
                    U0 = (cursorX + 0.5f) / AtlasSize,
                    V0 = (cursorY + 0.5f) / AtlasSize,
                    U1 = (cursorX + glyphWidth - 0.5f) / AtlasSize,
                    V1 = (cursorY + glyphHeight - 0.5f) / AtlasSize,
                    Advance = advance,
                    Width = glyphWidth,
                    Height = glyphHeight,
                    BearingY = CellPad + ascent,
                };

                cursorX += glyphWidth + 1;
                rowHeight = Math.Max(rowHeight, glyphHeight);
            }

            _metrics[size] = charMap;
        }

        // Upload atlas pixels JS → GPU. ImageData.Data is already a Uint8ClampedArray;
        // do NOT ReadBytes() into the .NET/WASM heap just to hand the same bytes to writeTexture.
        using var imageData = ctx.GetImageData(0, 0, AtlasSize, AtlasSize);
        using var dataArray = imageData.Data;

        Texture = device.CreateTexture(new GPUTextureDescriptor
        {
            Size = new[] { AtlasSize, AtlasSize },
            Format = "rgba8unorm",
            Usage = GPUTextureUsage.TextureBinding | GPUTextureUsage.CopyDst,
        });
        View = Texture.CreateView();

        queue.WriteTexture(
            new GPUTexelCopyTextureInfo { Texture = Texture },
            dataArray,
            new GPUTexelCopyBufferLayout
            {
                Offset = 0,
                BytesPerRow = (uint)(AtlasSize * 4),
                RowsPerImage = (uint)AtlasSize,
            },
            new uint[] { AtlasSize, AtlasSize }
        );
    }

    /// <summary>Get metrics for a character at the given font size.</summary>
    public CharMetrics GetChar(char c, FontSize size)
    {
        if (_metrics.TryGetValue(size, out var map) && map.TryGetValue(c, out var m))
            return m;
        if (_metrics.TryGetValue(size, out var fallback) && fallback.TryGetValue(' ', out var sp))
            return sp;
        return default;
    }

    /// <summary>Measure the width of a string in pixels at the given font size.</summary>
    public float MeasureString(string text, FontSize size)
    {
        float width = 0;
        foreach (char c in text)
            width += GetChar(c, size).Advance;
        return width;
    }

    /// <summary>Get the line height in pixels for the given font size.</summary>
    public float GetLineHeight(FontSize size)
    {
        var m = GetChar('M', size);
        return m.Height;
    }

    public void Dispose()
    {
        View?.Dispose();
        Texture?.Destroy();
        Texture?.Dispose();
    }
}
