using SpawnDev.BlazorJS.JSObjects;
using SpawnDev.GameUI.Elements;
using SpawnDev.GameUI.Rendering;
using System.Drawing;

namespace SpawnDev.GameUI;

/// <summary>
/// SDF text style parameters. Controls outline rendering for SDF text.
/// </summary>
public struct SDFTextStyle
{
    /// <summary>Outline width in SDF units. 0 = no outline, 0.05 = thin, 0.15 = thick.</summary>
    public float OutlineWidth;
    /// <summary>Outline color (only used when OutlineWidth > 0).</summary>
    public Color OutlineColor;
    /// <summary>Extra edge softness. 0 = sharp edges, 0.01 = slightly soft.</summary>
    public float Softness;

    /// <summary>No outline, sharp edges.</summary>
    public static readonly SDFTextStyle Default = new() { OutlineColor = Color.Black };
}

/// <summary>
/// Batched WebGPU quad renderer for UI overlay.
/// All UI elements emit quads via Draw* methods between Begin() and End().
/// End() flushes the batch as a single draw call on top of the 3D scene.
///
/// Supports three rendering modes in one pipeline:
///   1. Solid color rectangles (backgrounds, borders)
///   2. Bitmap font atlas text (legacy, 4 fixed sizes)
///   3. SDF font text (resolution-independent, any scale, outlines)
///
/// Both bitmap and SDF textures are bound simultaneously. A per-vertex
/// flags field selects the rendering path in the fragment shader.
/// Draw order is fully preserved - rects, bitmap text, and SDF text
/// can be freely interleaved without z-ordering issues.
///
/// Vertex format: pos(2) + uv(2) + color(4) + flags(1) = 9 floats = 36 bytes
///
/// Usage per frame:
///   renderer.Begin(viewportW, viewportH);
///   rootElement.Draw(renderer);
///   renderer.End(commandEncoder, targetTextureView);
///
/// Ported from SpawnScene's production UIRenderer.
/// </summary>
public class UIRenderer : IDisposable
{
    private const int MaxQuads = 4096;
    private const int VerticesPerQuad = 6; // 2 triangles
    private const int FloatsPerVertex = 9; // pos(2) + uv(2) + color(4) + flags(1)
    private const int BytesPerVertex = FloatsPerVertex * sizeof(float);

    private GPUDevice? _device;
    private GPUQueue? _queue;
    private FontAtlas? _fontAtlas;
    private SDFFontAtlas? _sdfFontAtlas;

    // GPU resources - screen-space pipeline
    private GPURenderPipeline? _pipeline;
    private GPUBuffer? _vertexBuffer;
    private GPUBuffer? _uniformBuffer; // 32 bytes: viewport(8) + outline(4) + softness(4) + outlineColor(16)
    private GPUBindGroup? _bindGroup;
    private GPUSampler? _sampler;

    // Dummy 1x1 texture for unused texture slots
    private GPUTexture? _dummyTexture;
    private GPUTextureView? _dummyTextureView;
    private GPUTexture? _dummyR8Texture;
    private GPUTextureView? _dummyR8TextureView;

    // GPU resources - world-space pipeline (VR/AR 3D panels)
    private GPURenderPipeline? _worldPipeline;
    private GPUBuffer? _worldVertexBuffer;
    private GPUBuffer? _worldUniformBuffer; // MVP(64) + outline(4) + softness(4) + pad(8) + outlineColor(16) = 96 bytes
    private GPUBindGroup? _worldBindGroup;
    private const int WorldFloatsPerVertex = 10; // pos(3) + uv(2) + color(4) + flags(1)
    private const int WorldBytesPerVertex = WorldFloatsPerVertex * sizeof(float);
    private const int MaxWorldQuads = 1024;
    private readonly float[] _worldVertices = new float[MaxWorldQuads * VerticesPerQuad * WorldFloatsPerVertex];
    private byte[]? _worldVertexBytes;
    private int _worldQuadCount;

    // CPU-side vertex batch
    private readonly float[] _vertices = new float[MaxQuads * VerticesPerQuad * FloatsPerVertex];
    private byte[]? _vertexBytes;
    private int _quadCount;

    // Deferred image draws (separate bind group per texture)
    private readonly List<(GPUTextureView view, float x, float y, float w, float h)> _imageBatch = new();
    private readonly Dictionary<GPUTextureView, GPUBindGroup> _imageBindGroups = new();

    // SDF text style
    private SDFTextStyle _sdfStyle;

    // Per-frame state
    private int _viewportWidth;
    private int _viewportHeight;

    public bool IsReady => _pipeline != null;

    /// <summary>True when SDF font rendering is available.</summary>
    public bool HasSDF => _sdfFontAtlas?.IsReady == true;

    /// <summary>Current SDF text style (outline, softness).</summary>
    public SDFTextStyle TextStyle
    {
        get => _sdfStyle;
        set => _sdfStyle = value;
    }

    /// <summary>
    /// Initialize the UI render pipeline. Call after WebGPU device and FontAtlas are ready.
    /// Optionally provide an SDFFontAtlas for resolution-independent text rendering.
    /// </summary>
    public void Init(GPUDevice device, GPUQueue queue, FontAtlas fontAtlas, string canvasFormat,
        SDFFontAtlas? sdfFontAtlas = null)
    {
        _device = device;
        _queue = queue;
        _fontAtlas = fontAtlas;
        _sdfFontAtlas = sdfFontAtlas;

        // Vertex buffer (dynamic, updated each frame)
        _vertexBuffer = device.CreateBuffer(new GPUBufferDescriptor
        {
            Size = (ulong)(MaxQuads * VerticesPerQuad * BytesPerVertex),
            Usage = GPUBufferUsage.Vertex | GPUBufferUsage.CopyDst,
        });
        _vertexBytes = new byte[_vertices.Length * sizeof(float)];

        // Uniform buffer (32 bytes: viewport + SDF params)
        _uniformBuffer = device.CreateBuffer(new GPUBufferDescriptor
        {
            Size = 32,
            Usage = GPUBufferUsage.Uniform | GPUBufferUsage.CopyDst,
        });

        // Sampler for font atlas (linear filtering works for both bitmap and SDF)
        _sampler = device.CreateSampler(new GPUSamplerDescriptor
        {
            MinFilter = "linear",
            MagFilter = "linear",
        });

        // Create dummy textures for unused slots
        CreateDummyTextures(device, queue);

        // Shader module
        using var shader = device.CreateShaderModule(new GPUShaderModuleDescriptor
        {
            Code = UIShaders.ScreenSpaceQuadShader
        });

        // Render pipeline: alpha blend, no depth, renders on top of scene
        _pipeline = device.CreateRenderPipeline(new GPURenderPipelineDescriptor
        {
            Layout = "auto",
            Vertex = new GPUVertexState
            {
                Module = shader,
                EntryPoint = "vs_main",
                Buffers = new[]
                {
                    new GPUVertexBufferLayout
                    {
                        ArrayStride = (ulong)BytesPerVertex,
                        StepMode = GPUVertexStepMode.Vertex,
                        Attributes = new GPUVertexAttribute[]
                        {
                            new() { ShaderLocation = 0, Offset = 0,  Format = GPUVertexFormat.Float32x2 }, // pos
                            new() { ShaderLocation = 1, Offset = 8,  Format = GPUVertexFormat.Float32x2 }, // uv
                            new() { ShaderLocation = 2, Offset = 16, Format = GPUVertexFormat.Float32x4 }, // color
                            new() { ShaderLocation = 3, Offset = 32, Format = GPUVertexFormat.Float32 },   // flags
                        }
                    }
                }
            },
            Fragment = new GPUFragmentState
            {
                Module = shader,
                EntryPoint = "fs_main",
                Targets = new[]
                {
                    new GPUColorTargetState
                    {
                        Format = canvasFormat,
                        Blend = new GPUBlendState
                        {
                            Color = new GPUBlendComponent
                            {
                                SrcFactor = GPUBlendFactor.SrcAlpha,
                                DstFactor = GPUBlendFactor.OneMinusSrcAlpha,
                                Operation = GPUBlendOperation.Add,
                            },
                            Alpha = new GPUBlendComponent
                            {
                                SrcFactor = GPUBlendFactor.One,
                                DstFactor = GPUBlendFactor.OneMinusSrcAlpha,
                                Operation = GPUBlendOperation.Add,
                            }
                        }
                    }
                }
            },
            Primitive = new GPUPrimitiveState { Topology = GPUPrimitiveTopology.TriangleList },
        });

        // Bind group: uniform + bitmap texture + SDF texture + sampler
        RebuildBindGroup();

        // === World-space pipeline (VR/AR 3D panels) ===
        _worldVertexBuffer = device.CreateBuffer(new GPUBufferDescriptor
        {
            Size = (ulong)(MaxWorldQuads * VerticesPerQuad * WorldBytesPerVertex),
            Usage = GPUBufferUsage.Vertex | GPUBufferUsage.CopyDst,
        });
        _worldVertexBytes = new byte[_worldVertices.Length * sizeof(float)];

        // World uniform buffer: MVP(64) + outline(4) + softness(4) + pad(8) + outlineColor(16) = 96 bytes
        _worldUniformBuffer = device.CreateBuffer(new GPUBufferDescriptor
        {
            Size = 96,
            Usage = GPUBufferUsage.Uniform | GPUBufferUsage.CopyDst,
        });

        using var worldShader = device.CreateShaderModule(new GPUShaderModuleDescriptor
        {
            Code = UIShaders.WorldSpaceQuadShader
        });

        _worldPipeline = device.CreateRenderPipeline(new GPURenderPipelineDescriptor
        {
            Layout = "auto",
            Vertex = new GPUVertexState
            {
                Module = worldShader,
                EntryPoint = "vs_main",
                Buffers = new[]
                {
                    new GPUVertexBufferLayout
                    {
                        ArrayStride = (ulong)WorldBytesPerVertex,
                        StepMode = GPUVertexStepMode.Vertex,
                        Attributes = new GPUVertexAttribute[]
                        {
                            new() { ShaderLocation = 0, Offset = 0,  Format = GPUVertexFormat.Float32x3 }, // pos3D
                            new() { ShaderLocation = 1, Offset = 12, Format = GPUVertexFormat.Float32x2 }, // uv
                            new() { ShaderLocation = 2, Offset = 20, Format = GPUVertexFormat.Float32x4 }, // color
                            new() { ShaderLocation = 3, Offset = 36, Format = GPUVertexFormat.Float32 },   // flags
                        }
                    }
                }
            },
            Fragment = new GPUFragmentState
            {
                Module = worldShader,
                EntryPoint = "fs_main",
                Targets = new[]
                {
                    new GPUColorTargetState
                    {
                        Format = canvasFormat,
                        Blend = new GPUBlendState
                        {
                            Color = new GPUBlendComponent
                            {
                                SrcFactor = GPUBlendFactor.SrcAlpha,
                                DstFactor = GPUBlendFactor.OneMinusSrcAlpha,
                                Operation = GPUBlendOperation.Add,
                            },
                            Alpha = new GPUBlendComponent
                            {
                                SrcFactor = GPUBlendFactor.One,
                                DstFactor = GPUBlendFactor.OneMinusSrcAlpha,
                                Operation = GPUBlendOperation.Add,
                            }
                        }
                    }
                }
            },
            Primitive = new GPUPrimitiveState { Topology = GPUPrimitiveTopology.TriangleList },
            DepthStencil = new GPUDepthStencilState
            {
                Format = "depth24plus",
                DepthWriteEnabled = true,
                DepthCompare = "less",
            },
        });

        RebuildWorldBindGroup();
    }

    private void CreateDummyTextures(GPUDevice device, GPUQueue queue)
    {
        // 1x1 RGBA dummy for bitmap slot when only SDF is used
        _dummyTexture = device.CreateTexture(new GPUTextureDescriptor
        {
            Size = new[] { 1, 1 },
            Format = "rgba8unorm",
            Usage = GPUTextureUsage.TextureBinding | GPUTextureUsage.CopyDst,
        });
        _dummyTextureView = _dummyTexture.CreateView();
        queue.WriteTexture(
            new GPUTexelCopyTextureInfo { Texture = _dummyTexture },
            new byte[] { 0, 0, 0, 0 },
            new GPUTexelCopyBufferLayout { BytesPerRow = 4, RowsPerImage = 1 },
            new uint[] { 1, 1 });

        // 1x1 R8 dummy for SDF slot when SDF is not available
        _dummyR8Texture = device.CreateTexture(new GPUTextureDescriptor
        {
            Size = new[] { 1, 1 },
            Format = "r8unorm",
            Usage = GPUTextureUsage.TextureBinding | GPUTextureUsage.CopyDst,
        });
        _dummyR8TextureView = _dummyR8Texture.CreateView();
        queue.WriteTexture(
            new GPUTexelCopyTextureInfo { Texture = _dummyR8Texture },
            new byte[] { 0 },
            new GPUTexelCopyBufferLayout { BytesPerRow = 1, RowsPerImage = 1 },
            new uint[] { 1, 1 });
    }

    private void RebuildBindGroup()
    {
        if (_pipeline == null || _sampler == null || _uniformBuffer == null) return;
        var bitmapView = _fontAtlas?.View ?? _dummyTextureView;
        var sdfView = _sdfFontAtlas?.View ?? _dummyR8TextureView;
        if (bitmapView == null || sdfView == null) return;

        _bindGroup?.Dispose();
        _bindGroup = _device!.CreateBindGroup(new GPUBindGroupDescriptor
        {
            Layout = _pipeline.GetBindGroupLayout(0),
            Entries = new GPUBindGroupEntry[]
            {
                new() { Binding = 0, Resource = new GPUBufferBinding { Buffer = _uniformBuffer } },
                new() { Binding = 1, Resource = bitmapView },
                new() { Binding = 2, Resource = sdfView },
                new() { Binding = 3, Resource = _sampler },
            }
        });
    }

    private void RebuildWorldBindGroup()
    {
        if (_worldPipeline == null || _sampler == null || _worldUniformBuffer == null) return;
        var bitmapView = _fontAtlas?.View ?? _dummyTextureView;
        var sdfView = _sdfFontAtlas?.View ?? _dummyR8TextureView;
        if (bitmapView == null || sdfView == null) return;

        _worldBindGroup?.Dispose();
        _worldBindGroup = _device!.CreateBindGroup(new GPUBindGroupDescriptor
        {
            Layout = _worldPipeline.GetBindGroupLayout(0),
            Entries = new GPUBindGroupEntry[]
            {
                new() { Binding = 0, Resource = new GPUBufferBinding { Buffer = _worldUniformBuffer } },
                new() { Binding = 1, Resource = bitmapView },
                new() { Binding = 2, Resource = sdfView },
                new() { Binding = 3, Resource = _sampler },
            }
        });
    }

    /// <summary>Start a new UI frame. Clears both screen-space and world-space batches.</summary>
    public void Begin(int viewportWidth, int viewportHeight)
    {
        _viewportWidth = viewportWidth;
        _viewportHeight = viewportHeight;
        _quadCount = 0;
        _worldQuadCount = 0;
        _imageBatch.Clear();
        _sdfStyle = SDFTextStyle.Default;
    }

    /// <summary>
    /// Set the SDF text style for subsequent DrawText calls.
    /// Only affects SDF text rendering (outline, softness).
    /// </summary>
    public void SetTextStyle(SDFTextStyle style) => _sdfStyle = style;

    /// <summary>
    /// Set the SDF text style with individual parameters.
    /// </summary>
    public void SetTextStyle(float outlineWidth = 0, Color? outlineColor = null, float softness = 0)
    {
        _sdfStyle = new SDFTextStyle
        {
            OutlineWidth = outlineWidth,
            OutlineColor = outlineColor ?? Color.Black,
            Softness = softness,
        };
    }

    /// <summary>Reset text style to default (no outline, sharp edges).</summary>
    public void ResetTextStyle() => _sdfStyle = SDFTextStyle.Default;

    /// <summary>Draw a solid-color rectangle.</summary>
    public void DrawRect(float x, float y, float w, float h, Color color)
    {
        if (_quadCount >= MaxQuads) return;
        float r = color.R / 255f, g = color.G / 255f, b = color.B / 255f, a = color.A / 255f;
        AddQuad(x, y, x + w, y + h, -1, -1, -1, -1, r, g, b, a, 0);
    }

    /// <summary>
    /// Draw a text string at the given position.
    /// Automatically uses SDF rendering when available, otherwise falls back to bitmap atlas.
    /// </summary>
    public void DrawText(string text, float x, float y, FontSize size, Color color)
    {
        // SDF path - resolution-independent rendering
        if (_sdfFontAtlas != null && _sdfFontAtlas.IsReady)
        {
            DrawTextSDF(text, x, y, size, color);
            return;
        }

        // Bitmap fallback
        if (_fontAtlas == null || !_fontAtlas.IsReady) return;
        float r = color.R / 255f, g = color.G / 255f, b = color.B / 255f, a = color.A / 255f;
        float cursorX = x;

        foreach (char c in text)
        {
            if (_quadCount >= MaxQuads) break;
            var m = _fontAtlas.GetChar(c, size);
            if (m.Width > 0 && m.Height > 0 && c != ' ')
            {
                AddQuad(cursorX, y, cursorX + m.Width, y + m.Height,
                        m.U0, m.V0, m.U1, m.V1, r, g, b, a, 0);
            }
            cursorX += m.Advance;
        }
    }

    private void DrawTextSDF(string text, float x, float y, FontSize size, Color color)
    {
        float scale = _sdfFontAtlas!.GetScale(size);
        float padding = _sdfFontAtlas.GetScaledPadding(size);
        float r = color.R / 255f, g = color.G / 255f, b = color.B / 255f, a = color.A / 255f;
        float cursorX = x;

        foreach (char c in text)
        {
            if (_quadCount >= MaxQuads) break;
            var m = _sdfFontAtlas.GetChar(c);
            if (m.SDFWidth > 0 && m.SDFHeight > 0 && c != ' ')
            {
                // Scale the SDF region to target size
                float quadW = m.SDFWidth * scale;
                float quadH = m.SDFHeight * scale;

                // Offset by negative padding so the visible glyph aligns with cursorX, y
                float drawX = cursorX - padding;
                float drawY = y - padding;

                AddQuad(drawX, drawY, drawX + quadW, drawY + quadH,
                        m.U0, m.V0, m.U1, m.V1, r, g, b, a, 1); // flags=1 for SDF
            }
            cursorX += m.Advance * scale;
        }
    }

    /// <summary>Draw a textured image quad (rendered as a separate draw call per unique texture).</summary>
    public void DrawImage(GPUTextureView textureView, float x, float y, float w, float h)
    {
        _imageBatch.Add((textureView, x, y, w, h));
    }

    /// <summary>Measure text width without drawing.</summary>
    public float MeasureText(string text, FontSize size)
    {
        if (_sdfFontAtlas?.IsReady == true)
            return _sdfFontAtlas.MeasureString(text, size);
        return _fontAtlas?.MeasureString(text, size) ?? 0;
    }

    /// <summary>Get line height for a font size.</summary>
    public float GetLineHeight(FontSize size)
    {
        if (_sdfFontAtlas?.IsReady == true)
            return _sdfFontAtlas.GetLineHeight(size);
        return _fontAtlas?.GetLineHeight(size) ?? (int)size;
    }

    /// <summary>
    /// Flush the batch: upload vertices and append a render pass to the encoder.
    /// The render pass writes to the given target with LoadOp.Load so the 3D scene is preserved.
    /// </summary>
    public void End(GPUCommandEncoder encoder, GPUTextureView target)
    {
        if (_pipeline == null) return;
        if (_quadCount == 0 && _imageBatch.Count == 0) return;

        // Upload uniform: viewport + SDF outline params
        var uniformData = new float[]
        {
            _viewportWidth, _viewportHeight,         // viewport
            _sdfStyle.OutlineWidth, _sdfStyle.Softness, // SDF params
            _sdfStyle.OutlineColor.R / 255f,         // outline color RGBA
            _sdfStyle.OutlineColor.G / 255f,
            _sdfStyle.OutlineColor.B / 255f,
            _sdfStyle.OutlineColor.A / 255f,
        };
        var uniformBytes = new byte[32];
        Buffer.BlockCopy(uniformData, 0, uniformBytes, 0, 32);
        _queue!.WriteBuffer(_uniformBuffer!, 0, uniformBytes);

        // Append image quads to the vertex array after the main batch
        int imageStartQuad = _quadCount;
        foreach (var (view, ix, iy, iw, ih) in _imageBatch)
        {
            if (imageStartQuad >= MaxQuads) break;
            int offset = imageStartQuad * VerticesPerQuad * FloatsPerVertex;
            SetVertex(offset + 0 * FloatsPerVertex, ix, iy, 0, 0, 1, 1, 1, 1, 0);
            SetVertex(offset + 1 * FloatsPerVertex, ix + iw, iy, 1, 0, 1, 1, 1, 1, 0);
            SetVertex(offset + 2 * FloatsPerVertex, ix, iy + ih, 0, 1, 1, 1, 1, 1, 0);
            SetVertex(offset + 3 * FloatsPerVertex, ix + iw, iy, 1, 0, 1, 1, 1, 1, 0);
            SetVertex(offset + 4 * FloatsPerVertex, ix + iw, iy + ih, 1, 1, 1, 1, 1, 1, 0);
            SetVertex(offset + 5 * FloatsPerVertex, ix, iy + ih, 0, 1, 1, 1, 1, 1, 0);
            imageStartQuad++;
        }

        // Upload entire vertex buffer at once
        int totalQuads = imageStartQuad;
        int totalBytes = totalQuads * VerticesPerQuad * FloatsPerVertex * sizeof(float);
        Buffer.BlockCopy(_vertices, 0, _vertexBytes!, 0, totalBytes);
        _queue.WriteBuffer(_vertexBuffer!, 0, _vertexBytes, 0, (ulong)totalBytes);

        // Render pass: overlay on existing content (LoadOp.Load preserves scene)
        var colorAttach = new GPURenderPassColorAttachment
        {
            View = target,
            LoadOp = GPULoadOp.Load,
            StoreOp = GPUStoreOp.Store,
        };
        using var pass = encoder.BeginRenderPass(new GPURenderPassDescriptor
        {
            ColorAttachments = new[] { colorAttach },
        });
        pass.SetPipeline(_pipeline);
        pass.SetVertexBuffer(0, _vertexBuffer!);

        // Draw main batch (text + solid color quads)
        if (_quadCount > 0 && _bindGroup != null)
        {
            pass.SetBindGroup(0, _bindGroup);
            pass.Draw((uint)(_quadCount * VerticesPerQuad), 1, 0, 0);
        }

        // Draw images - each uses its own bind group at its own offset in the vertex buffer
        int imgQuadIdx = _quadCount;
        foreach (var (view, _, _, _, _) in _imageBatch)
        {
            if (imgQuadIdx >= MaxQuads) break;
            var imgBindGroup = GetOrCreateImageBindGroup(view);
            if (imgBindGroup == null) { imgQuadIdx++; continue; }

            pass.SetBindGroup(0, imgBindGroup);
            pass.Draw(VerticesPerQuad, 1, (uint)(imgQuadIdx * VerticesPerQuad), 0);
            imgQuadIdx++;
        }

        pass.End();
    }

    private GPUBindGroup? GetOrCreateImageBindGroup(GPUTextureView view)
    {
        if (_imageBindGroups.TryGetValue(view, out var existing)) return existing;
        if (_pipeline == null || _sampler == null || _uniformBuffer == null) return null;

        var sdfView = _sdfFontAtlas?.View ?? _dummyR8TextureView;
        if (sdfView == null) return null;

        var bg = _device!.CreateBindGroup(new GPUBindGroupDescriptor
        {
            Layout = _pipeline.GetBindGroupLayout(0),
            Entries = new GPUBindGroupEntry[]
            {
                new() { Binding = 0, Resource = new GPUBufferBinding { Buffer = _uniformBuffer } },
                new() { Binding = 1, Resource = view },
                new() { Binding = 2, Resource = sdfView },
                new() { Binding = 3, Resource = _sampler },
            }
        });
        _imageBindGroups[view] = bg;
        return bg;
    }

    private void AddQuad(float x0, float y0, float x1, float y1,
                         float u0, float v0, float u1, float v1,
                         float r, float g, float b, float a, float flags)
    {
        int offset = _quadCount * VerticesPerQuad * FloatsPerVertex;
        SetVertex(offset + 0 * FloatsPerVertex, x0, y0, u0, v0, r, g, b, a, flags);
        SetVertex(offset + 1 * FloatsPerVertex, x1, y0, u1, v0, r, g, b, a, flags);
        SetVertex(offset + 2 * FloatsPerVertex, x0, y1, u0, v1, r, g, b, a, flags);
        SetVertex(offset + 3 * FloatsPerVertex, x1, y0, u1, v0, r, g, b, a, flags);
        SetVertex(offset + 4 * FloatsPerVertex, x1, y1, u1, v1, r, g, b, a, flags);
        SetVertex(offset + 5 * FloatsPerVertex, x0, y1, u0, v1, r, g, b, a, flags);
        _quadCount++;
    }

    private void SetVertex(int offset, float x, float y, float u, float v,
                           float r, float g, float b, float a, float flags)
    {
        _vertices[offset + 0] = x;
        _vertices[offset + 1] = y;
        _vertices[offset + 2] = u;
        _vertices[offset + 3] = v;
        _vertices[offset + 4] = r;
        _vertices[offset + 5] = g;
        _vertices[offset + 6] = b;
        _vertices[offset + 7] = a;
        _vertices[offset + 8] = flags;
    }

    // =====================================================================
    // World-space 3D rendering (VR/AR floating panels)
    // =====================================================================

    /// <summary>
    /// Draw a solid-color rectangle on a world-space panel.
    /// Coordinates are in panel-local space (0,0 = top-left of panel).
    /// The panel's world transform + camera VP matrix are set via EndWorldSpace().
    /// </summary>
    public void DrawWorldRect(float x, float y, float w, float h, float panelW, float panelH,
        System.Drawing.Color color)
    {
        if (_worldQuadCount >= MaxWorldQuads) return;
        float r = color.R / 255f, g = color.G / 255f, b = color.B / 255f, a = color.A / 255f;
        // Convert panel-local pixel coords to normalized panel coords (-0.5 to 0.5)
        float x0 = (x / panelW) - 0.5f;
        float y0 = 0.5f - (y / panelH); // Y flipped (top = +Y)
        float x1 = ((x + w) / panelW) - 0.5f;
        float y1 = 0.5f - ((y + h) / panelH);
        AddWorldQuad(x0, y0, 0, x1, y0, 0, x0, y1, 0, x1, y1, 0,
                     -1, -1, -1, -1, r, g, b, a, 0);
    }

    /// <summary>
    /// Draw text on a world-space panel.
    /// Coordinates are in panel-local pixel space.
    /// Uses SDF when available for crisp text at any viewing distance.
    /// </summary>
    public void DrawWorldText(string text, float x, float y, float panelW, float panelH,
        Elements.FontSize size, System.Drawing.Color color)
    {
        if (_sdfFontAtlas != null && _sdfFontAtlas.IsReady)
        {
            DrawWorldTextSDF(text, x, y, panelW, panelH, size, color);
            return;
        }

        if (_fontAtlas == null || !_fontAtlas.IsReady) return;
        float r = color.R / 255f, g = color.G / 255f, b = color.B / 255f, a = color.A / 255f;
        float cursorX = x;

        foreach (char c in text)
        {
            if (_worldQuadCount >= MaxWorldQuads) break;
            var m = _fontAtlas.GetChar(c, size);
            if (m.Width > 0 && m.Height > 0 && c != ' ')
            {
                float lx0 = (cursorX / panelW) - 0.5f;
                float ly0 = 0.5f - (y / panelH);
                float lx1 = ((cursorX + m.Width) / panelW) - 0.5f;
                float ly1 = 0.5f - ((y + m.Height) / panelH);
                AddWorldQuad(lx0, ly0, 0, lx1, ly0, 0, lx0, ly1, 0, lx1, ly1, 0,
                             m.U0, m.V0, m.U1, m.V1, r, g, b, a, 0);
            }
            cursorX += m.Advance;
        }
    }

    private void DrawWorldTextSDF(string text, float x, float y, float panelW, float panelH,
        FontSize size, Color color)
    {
        float scale = _sdfFontAtlas!.GetScale(size);
        float padding = _sdfFontAtlas.GetScaledPadding(size);
        float r = color.R / 255f, g = color.G / 255f, b = color.B / 255f, a = color.A / 255f;
        float cursorX = x;

        foreach (char c in text)
        {
            if (_worldQuadCount >= MaxWorldQuads) break;
            var m = _sdfFontAtlas.GetChar(c);
            if (m.SDFWidth > 0 && m.SDFHeight > 0 && c != ' ')
            {
                float quadW = m.SDFWidth * scale;
                float quadH = m.SDFHeight * scale;
                float drawX = cursorX - padding;
                float drawY = y - padding;

                float lx0 = (drawX / panelW) - 0.5f;
                float ly0 = 0.5f - (drawY / panelH);
                float lx1 = ((drawX + quadW) / panelW) - 0.5f;
                float ly1 = 0.5f - ((drawY + quadH) / panelH);
                AddWorldQuad(lx0, ly0, 0, lx1, ly0, 0, lx0, ly1, 0, lx1, ly1, 0,
                             m.U0, m.V0, m.U1, m.V1, r, g, b, a, 1);
            }
            cursorX += m.Advance * scale;
        }
    }

    /// <summary>
    /// Draw a quad with raw world-space vertex positions.
    /// Used for controller rays, laser pointers, and other 3D primitives.
    /// Positions are in world space - use with VP matrix (no model transform).
    /// </summary>
    public void DrawWorldRayQuad(System.Numerics.Vector3 p0, System.Numerics.Vector3 p1,
        System.Numerics.Vector3 p2, System.Numerics.Vector3 p3,
        float r, float g, float b, float a)
    {
        if (_worldQuadCount >= MaxWorldQuads) return;
        AddWorldQuad(p0.X, p0.Y, p0.Z, p1.X, p1.Y, p1.Z,
                     p2.X, p2.Y, p2.Z, p3.X, p3.Y, p3.Z,
                     -1, -1, -1, -1, r, g, b, a, 0);
    }

    /// <summary>
    /// Flush world-space quads with the given MVP matrix.
    /// Call after End() for screen-space UI.
    /// The MVP matrix = Projection * View * Model (panel world transform).
    /// </summary>
    public void EndWorldSpace(GPUCommandEncoder encoder, GPUTextureView colorTarget,
        GPUTextureView depthTarget, System.Numerics.Matrix4x4 mvp)
    {
        if (_worldPipeline == null || _worldQuadCount == 0) return;

        // Upload uniform: MVP + SDF params
        // Layout: MVP(64) + outlineWidth(4) + softness(4) + pad(8) + outlineColor(16) = 96 bytes
        var uniformData = new float[24]; // 96 bytes / 4
        // MVP (column-major for WGSL)
        uniformData[0]  = mvp.M11; uniformData[1]  = mvp.M21; uniformData[2]  = mvp.M31; uniformData[3]  = mvp.M41;
        uniformData[4]  = mvp.M12; uniformData[5]  = mvp.M22; uniformData[6]  = mvp.M32; uniformData[7]  = mvp.M42;
        uniformData[8]  = mvp.M13; uniformData[9]  = mvp.M23; uniformData[10] = mvp.M33; uniformData[11] = mvp.M43;
        uniformData[12] = mvp.M14; uniformData[13] = mvp.M24; uniformData[14] = mvp.M34; uniformData[15] = mvp.M44;
        // SDF params
        uniformData[16] = _sdfStyle.OutlineWidth;
        uniformData[17] = _sdfStyle.Softness;
        uniformData[18] = 0; uniformData[19] = 0; // padding
        uniformData[20] = _sdfStyle.OutlineColor.R / 255f;
        uniformData[21] = _sdfStyle.OutlineColor.G / 255f;
        uniformData[22] = _sdfStyle.OutlineColor.B / 255f;
        uniformData[23] = _sdfStyle.OutlineColor.A / 255f;

        var uniformBytes = new byte[96];
        Buffer.BlockCopy(uniformData, 0, uniformBytes, 0, 96);
        _queue!.WriteBuffer(_worldUniformBuffer!, 0, uniformBytes);

        // Upload vertices
        int totalBytes = _worldQuadCount * VerticesPerQuad * WorldFloatsPerVertex * sizeof(float);
        Buffer.BlockCopy(_worldVertices, 0, _worldVertexBytes!, 0, totalBytes);
        _queue.WriteBuffer(_worldVertexBuffer!, 0, _worldVertexBytes, 0, (ulong)totalBytes);

        // Render pass with depth testing (world-space panels need depth)
        using var pass = encoder.BeginRenderPass(new GPURenderPassDescriptor
        {
            ColorAttachments = new[]
            {
                new GPURenderPassColorAttachment
                {
                    View = colorTarget,
                    LoadOp = GPULoadOp.Load,
                    StoreOp = GPUStoreOp.Store,
                }
            },
            DepthStencilAttachment = new GPURenderPassDepthStencilAttachment
            {
                View = depthTarget,
                DepthLoadOp = "load",
                DepthStoreOp = "store",
            },
        });

        pass.SetPipeline(_worldPipeline);
        pass.SetVertexBuffer(0, _worldVertexBuffer!);

        if (_worldBindGroup != null)
        {
            pass.SetBindGroup(0, _worldBindGroup);
            pass.Draw((uint)(_worldQuadCount * VerticesPerQuad), 1, 0, 0);
        }

        pass.End();
    }

    private void AddWorldQuad(
        float x0, float y0, float z0, float x1, float y1, float z1,
        float x2, float y2, float z2, float x3, float y3, float z3,
        float u0, float v0, float u1, float v1,
        float r, float g, float b, float a, float flags)
    {
        int offset = _worldQuadCount * VerticesPerQuad * WorldFloatsPerVertex;
        // Triangle 1: top-left, top-right, bottom-left
        SetWorldVertex(offset + 0 * WorldFloatsPerVertex, x0, y0, z0, u0, v0, r, g, b, a, flags);
        SetWorldVertex(offset + 1 * WorldFloatsPerVertex, x1, y1, z1, u1, v0, r, g, b, a, flags);
        SetWorldVertex(offset + 2 * WorldFloatsPerVertex, x2, y2, z2, u0, v1, r, g, b, a, flags);
        // Triangle 2: top-right, bottom-right, bottom-left
        SetWorldVertex(offset + 3 * WorldFloatsPerVertex, x1, y1, z1, u1, v0, r, g, b, a, flags);
        SetWorldVertex(offset + 4 * WorldFloatsPerVertex, x3, y3, z3, u1, v1, r, g, b, a, flags);
        SetWorldVertex(offset + 5 * WorldFloatsPerVertex, x2, y2, z2, u0, v1, r, g, b, a, flags);
        _worldQuadCount++;
    }

    private void SetWorldVertex(int offset, float x, float y, float z, float u, float v,
                                float r, float g, float b, float a, float flags)
    {
        _worldVertices[offset + 0] = x;
        _worldVertices[offset + 1] = y;
        _worldVertices[offset + 2] = z;
        _worldVertices[offset + 3] = u;
        _worldVertices[offset + 4] = v;
        _worldVertices[offset + 5] = r;
        _worldVertices[offset + 6] = g;
        _worldVertices[offset + 7] = b;
        _worldVertices[offset + 8] = a;
        _worldVertices[offset + 9] = flags;
    }

    public void Dispose()
    {
        _vertexBuffer?.Destroy();
        _vertexBuffer?.Dispose();
        _uniformBuffer?.Destroy();
        _uniformBuffer?.Dispose();
        _bindGroup?.Dispose();
        _sampler?.Dispose();
        _pipeline?.Dispose();
        foreach (var bg in _imageBindGroups.Values)
            bg.Dispose();
        _imageBindGroups.Clear();

        // Dummy textures
        _dummyTextureView?.Dispose();
        _dummyTexture?.Destroy();
        _dummyTexture?.Dispose();
        _dummyR8TextureView?.Dispose();
        _dummyR8Texture?.Destroy();
        _dummyR8Texture?.Dispose();

        // World-space resources
        _worldVertexBuffer?.Destroy();
        _worldVertexBuffer?.Dispose();
        _worldUniformBuffer?.Destroy();
        _worldUniformBuffer?.Dispose();
        _worldBindGroup?.Dispose();
        _worldPipeline?.Dispose();
    }
}
