using SpawnDev.BlazorJS.JSObjects;
using SpawnDev.GameUI.Elements;

namespace SpawnDev.GameUI.Rendering;

/// <summary>Metrics for a single character in the SDF atlas, measured at the base font size.</summary>
public struct SDFCharMetrics
{
    /// <summary>UV coordinates of the SDF region in the atlas (normalized 0-1). Includes padding.</summary>
    public float U0, V0, U1, V1;
    /// <summary>Character advance width at base size (pixels).</summary>
    public float Advance;
    /// <summary>Full SDF region size including padding (pixels at base size).</summary>
    public float SDFWidth, SDFHeight;
    /// <summary>Glyph size without SDF padding (pixels at base size).</summary>
    public float GlyphWidth, GlyphHeight;
    /// <summary>Vertical offset from baseline to top of glyph at base size.</summary>
    public float BearingY;
}

/// <summary>
/// Signed Distance Field font atlas generator.
/// Rasterizes glyphs at a single large base size (48px), computes a distance field
/// around each glyph, and stores it in an R8Unorm GPU texture.
///
/// SDF rendering gives resolution-independent text - crisp at any zoom level,
/// any viewing distance. Critical for VR/AR where text is viewed at varying distances.
/// Also enables GPU-cheap outline, glow, and drop shadow effects.
///
/// Generation:
///   1. Render all glyphs onto OffscreenCanvas at 48px with SDF padding
///   2. Read pixel data once (single JS-to-.NET transfer)
///   3. Compute signed distance field per glyph via Chamfer distance transform
///   4. Upload as R8Unorm texture (4x smaller than RGBA)
///
/// At render time, the fragment shader uses smoothstep + fwidth() for
/// pixel-perfect anti-aliasing at any scale.
/// </summary>
public class SDFFontAtlas : IDisposable
{
    /// <summary>Atlas texture dimensions.</summary>
    public const int AtlasSize = 1024;

    /// <summary>Base font size for glyph rasterization. All metrics are at this size.</summary>
    public const int BaseFontSize = 48;

    /// <summary>
    /// SDF spread in pixels - how far the distance field extends beyond the glyph edge.
    /// Higher spread = more room for outlines and effects, but less atlas space per glyph.
    /// 6px at 48px base = 12.5% of glyph height, enough for thick outlines.
    /// </summary>
    public const int Spread = 6;

    /// <summary>Padding around each glyph in the atlas (spread + 1 for bilinear safety).</summary>
    public const int GlyphPadding = Spread + 1;

    private const string FontFamily = "Inter, system-ui, -apple-system, sans-serif";
    private const int FirstChar = 32;  // space
    private const int LastChar = 126;  // tilde

    private readonly Dictionary<char, SDFCharMetrics> _metrics = new();

    /// <summary>The R8Unorm GPU texture containing the SDF atlas.</summary>
    public GPUTexture? Texture { get; private set; }

    /// <summary>Texture view for binding to shaders.</summary>
    public GPUTextureView? View { get; private set; }

    /// <summary>True when the atlas has been generated and uploaded to GPU.</summary>
    public bool IsReady => View != null;

    /// <summary>Font ascent at base size (pixels from baseline to top).</summary>
    public float BaseAscent { get; private set; }

    /// <summary>Line height at base size (ascent + descent).</summary>
    public float BaseLineHeight { get; private set; }

    /// <summary>
    /// Generate the SDF font atlas and upload to GPU.
    /// Must be called after WebGPU device is available.
    /// </summary>
    public void Init(GPUDevice device, GPUQueue queue)
    {
        using var canvas = new OffscreenCanvas(AtlasSize, AtlasSize);
        using var ctx = canvas.Get2DContext();

        ctx.ClearRect(0, 0, AtlasSize, AtlasSize);
        ctx.FillStyle = "white";
        ctx.TextBaseline = "top";
        ctx.Font = $"{BaseFontSize}px {FontFamily}";

        // Measure font metrics from reference character
        using var refMetrics = ctx.MeasureText("M");
        BaseAscent = (float)refMetrics.FontBoundingBoxAscent;
        float descent = (float)refMetrics.FontBoundingBoxDescent;
        BaseLineHeight = BaseAscent + descent;

        // Plan glyph layout - measure all characters, assign atlas positions
        int cursorX = 1, cursorY = 1, rowHeight = 0;
        var glyphLayout = new List<GlyphInfo>();

        for (int c = FirstChar; c <= LastChar; c++)
        {
            char ch = (char)c;
            using var tm = ctx.MeasureText(ch.ToString());
            float advance = (float)tm.Width;
            int glyphW = (int)Math.Ceiling(advance) + 2;
            int glyphH = (int)Math.Ceiling(BaseLineHeight) + 2;
            int sdfW = glyphW + 2 * GlyphPadding;
            int sdfH = glyphH + 2 * GlyphPadding;

            // Wrap to next row
            if (cursorX + sdfW + 1 >= AtlasSize)
            {
                cursorX = 1;
                cursorY += rowHeight + 1;
                rowHeight = 0;
            }

            if (cursorY + sdfH + 1 >= AtlasSize)
                break; // Atlas full

            glyphLayout.Add(new GlyphInfo
            {
                Char = ch,
                AtlasX = cursorX,
                AtlasY = cursorY,
                SDFWidth = sdfW,
                SDFHeight = sdfH,
                GlyphWidth = glyphW,
                GlyphHeight = glyphH,
                Advance = advance,
            });

            cursorX += sdfW + 1;
            rowHeight = Math.Max(rowHeight, sdfH);
        }

        // Render all glyphs at their atlas positions (offset by padding so SDF region surrounds them)
        foreach (var g in glyphLayout)
        {
            if (g.Char != ' ')
                ctx.FillText(g.Char.ToString(), g.AtlasX + GlyphPadding, g.AtlasY + GlyphPadding + 1);
        }

        // Read all pixels once (single JS-to-.NET transfer)
        using var imageData = ctx.GetImageData(0, 0, AtlasSize, AtlasSize);
        using var dataArray = imageData.Data;
        var allPixels = dataArray.ReadBytes();

        // Build SDF atlas
        var atlas = new byte[AtlasSize * AtlasSize];

        foreach (var g in glyphLayout)
        {
            // Extract alpha channel for this glyph's SDF region
            var region = new byte[g.SDFWidth * g.SDFHeight];
            for (int y = 0; y < g.SDFHeight; y++)
            {
                for (int x = 0; x < g.SDFWidth; x++)
                {
                    int srcIdx = ((g.AtlasY + y) * AtlasSize + (g.AtlasX + x)) * 4 + 3; // alpha channel
                    region[y * g.SDFWidth + x] = allPixels[srcIdx];
                }
            }

            // Compute SDF for this glyph
            var sdf = ComputeSDF(region, g.SDFWidth, g.SDFHeight, Spread);

            // Copy into atlas
            for (int y = 0; y < g.SDFHeight; y++)
                for (int x = 0; x < g.SDFWidth; x++)
                    atlas[(g.AtlasY + y) * AtlasSize + (g.AtlasX + x)] = sdf[y * g.SDFWidth + x];

            // Store metrics
            _metrics[g.Char] = new SDFCharMetrics
            {
                U0 = (float)g.AtlasX / AtlasSize,
                V0 = (float)g.AtlasY / AtlasSize,
                U1 = (float)(g.AtlasX + g.SDFWidth) / AtlasSize,
                V1 = (float)(g.AtlasY + g.SDFHeight) / AtlasSize,
                Advance = g.Advance,
                SDFWidth = g.SDFWidth,
                SDFHeight = g.SDFHeight,
                GlyphWidth = g.GlyphWidth,
                GlyphHeight = g.GlyphHeight,
                BearingY = BaseAscent + 1,
            };
        }

        // Upload to GPU as R8Unorm (single channel - 4x smaller than RGBA)
        Texture = device.CreateTexture(new GPUTextureDescriptor
        {
            Size = new[] { AtlasSize, AtlasSize },
            Format = "r8unorm",
            Usage = GPUTextureUsage.TextureBinding | GPUTextureUsage.CopyDst,
        });
        View = Texture.CreateView();

        queue.WriteTexture(
            new GPUTexelCopyTextureInfo { Texture = Texture },
            atlas,
            new GPUTexelCopyBufferLayout
            {
                Offset = 0,
                BytesPerRow = (uint)AtlasSize,
                RowsPerImage = (uint)AtlasSize,
            },
            new uint[] { (uint)AtlasSize, (uint)AtlasSize }
        );
    }

    /// <summary>Get the scale factor to render at a given FontSize.</summary>
    public float GetScale(FontSize size) => (int)size / (float)BaseFontSize;

    /// <summary>Get metrics for a character at base size.</summary>
    public SDFCharMetrics GetChar(char c)
    {
        if (_metrics.TryGetValue(c, out var m)) return m;
        if (_metrics.TryGetValue(' ', out var sp)) return sp;
        return default;
    }

    /// <summary>Measure the width of a string at the given font size.</summary>
    public float MeasureString(string text, FontSize size)
    {
        float scale = GetScale(size);
        float width = 0;
        foreach (char c in text)
            width += GetChar(c).Advance * scale;
        return width;
    }

    /// <summary>Get the line height at the given font size.</summary>
    public float GetLineHeight(FontSize size)
    {
        float scale = GetScale(size);
        return BaseLineHeight * scale;
    }

    /// <summary>Get the scaled glyph padding for a font size.</summary>
    public float GetScaledPadding(FontSize size) => GlyphPadding * GetScale(size);

    /// <summary>
    /// Compute the signed distance field from a bitmap alpha channel.
    /// Uses Chamfer (8-connected) distance transform: two-pass sweep with
    /// D1=1 (cardinal) and D2=sqrt(2) (diagonal) step costs.
    /// Output: 0.5 at edge, >0.5 inside glyph, less than 0.5 outside.
    /// </summary>
    public static byte[] ComputeSDF(byte[] alpha, int width, int height, int spread)
    {
        int size = width * height;
        var distOut = new float[size]; // distance from outside pixels to nearest glyph pixel
        var distIn = new float[size];  // distance from inside pixels to nearest edge

        const float INF = 1e6f;
        const float D1 = 1.0f;       // cardinal step
        const float D2 = 1.4142135f;  // diagonal step
        const byte THRESHOLD = 128;

        // Initialize: inside pixels start with distOut=0, outside start with distIn=0
        for (int i = 0; i < size; i++)
        {
            if (alpha[i] >= THRESHOLD)
            {
                distOut[i] = 0;
                distIn[i] = INF;
            }
            else
            {
                distOut[i] = INF;
                distIn[i] = 0;
            }
        }

        // Forward pass: top-left to bottom-right
        // Check left, top, top-left, top-right neighbors
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (x > 0)
                {
                    distOut[i] = MathF.Min(distOut[i], distOut[i - 1] + D1);
                    distIn[i] = MathF.Min(distIn[i], distIn[i - 1] + D1);
                }
                if (y > 0)
                {
                    distOut[i] = MathF.Min(distOut[i], distOut[i - width] + D1);
                    distIn[i] = MathF.Min(distIn[i], distIn[i - width] + D1);
                }
                if (x > 0 && y > 0)
                {
                    distOut[i] = MathF.Min(distOut[i], distOut[i - width - 1] + D2);
                    distIn[i] = MathF.Min(distIn[i], distIn[i - width - 1] + D2);
                }
                if (x < width - 1 && y > 0)
                {
                    distOut[i] = MathF.Min(distOut[i], distOut[i - width + 1] + D2);
                    distIn[i] = MathF.Min(distIn[i], distIn[i - width + 1] + D2);
                }
            }
        }

        // Backward pass: bottom-right to top-left
        // Check right, bottom, bottom-right, bottom-left neighbors
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = width - 1; x >= 0; x--)
            {
                int i = y * width + x;
                if (x < width - 1)
                {
                    distOut[i] = MathF.Min(distOut[i], distOut[i + 1] + D1);
                    distIn[i] = MathF.Min(distIn[i], distIn[i + 1] + D1);
                }
                if (y < height - 1)
                {
                    distOut[i] = MathF.Min(distOut[i], distOut[i + width] + D1);
                    distIn[i] = MathF.Min(distIn[i], distIn[i + width] + D1);
                }
                if (x < width - 1 && y < height - 1)
                {
                    distOut[i] = MathF.Min(distOut[i], distOut[i + width + 1] + D2);
                    distIn[i] = MathF.Min(distIn[i], distIn[i + width + 1] + D2);
                }
                if (x > 0 && y < height - 1)
                {
                    distOut[i] = MathF.Min(distOut[i], distOut[i + width - 1] + D2);
                    distIn[i] = MathF.Min(distIn[i], distIn[i + width - 1] + D2);
                }
            }
        }

        // Combine: signed distance = outside_dist - inside_dist
        // Normalize to 0-1 range: 0.5 = edge, >0.5 = inside, <0.5 = outside
        var result = new byte[size];
        float invSpread = 1.0f / (2.0f * spread);
        for (int i = 0; i < size; i++)
        {
            float sd = distOut[i] - distIn[i];
            float normalized = 0.5f - sd * invSpread;
            result[i] = (byte)(Math.Clamp(normalized, 0f, 1f) * 255);
        }

        return result;
    }

    public void Dispose()
    {
        View?.Dispose();
        Texture?.Destroy();
        Texture?.Dispose();
    }

    private struct GlyphInfo
    {
        public char Char;
        public int AtlasX, AtlasY;
        public int SDFWidth, SDFHeight;
        public int GlyphWidth, GlyphHeight;
        public float Advance;
    }
}
