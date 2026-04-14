using SpawnDev.BlazorJS.JSObjects;
using SpawnDev.GameUI.Elements;
using SpawnDev.GameUI.Rendering;
using System.Drawing;

namespace SpawnDev.GameUI;

/// <summary>
/// Batched WebGPU quad renderer for UI overlay.
/// All UI elements emit quads via Draw* methods between Begin() and End().
/// End() flushes the batch as a single draw call on top of the 3D scene.
///
/// Supports up to 4096 quads per frame: solid rectangles, font-atlas text, and GPU texture images.
/// Alpha-blended, no depth test, renders with LoadOp.Load to preserve the scene underneath.
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
    private const int FloatsPerVertex = 8; // pos(2) + uv(2) + color(4)
    private const int BytesPerVertex = FloatsPerVertex * sizeof(float);

    private GPUDevice? _device;
    private GPUQueue? _queue;
    private FontAtlas? _fontAtlas;

    // GPU resources - screen-space pipeline
    private GPURenderPipeline? _pipeline;
    private GPUBuffer? _vertexBuffer;
    private GPUBuffer? _uniformBuffer;
    private GPUBindGroup? _bindGroup;
    private GPUSampler? _sampler;

    // GPU resources - world-space pipeline (VR/AR 3D panels)
    private GPURenderPipeline? _worldPipeline;
    private GPUBuffer? _worldVertexBuffer;
    private GPUBuffer? _worldUniformBuffer; // MVP matrix (64 bytes)
    private GPUBindGroup? _worldBindGroup;
    private const int WorldFloatsPerVertex = 9; // pos(3) + uv(2) + color(4)
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

    // Per-frame state
    private int _viewportWidth;
    private int _viewportHeight;

    public bool IsReady => _pipeline != null;

    /// <summary>
    /// Initialize the UI render pipeline. Call after WebGPU device and FontAtlas are ready.
    /// </summary>
    public void Init(GPUDevice device, GPUQueue queue, FontAtlas fontAtlas, string canvasFormat)
    {
        _device = device;
        _queue = queue;
        _fontAtlas = fontAtlas;

        // Vertex buffer (dynamic, updated each frame)
        _vertexBuffer = device.CreateBuffer(new GPUBufferDescriptor
        {
            Size = (ulong)(MaxQuads * VerticesPerQuad * BytesPerVertex),
            Usage = GPUBufferUsage.Vertex | GPUBufferUsage.CopyDst,
        });
        _vertexBytes = new byte[_vertices.Length * sizeof(float)];

        // Uniform buffer (viewport size, 16 bytes aligned)
        _uniformBuffer = device.CreateBuffer(new GPUBufferDescriptor
        {
            Size = 16,
            Usage = GPUBufferUsage.Uniform | GPUBufferUsage.CopyDst,
        });

        // Sampler for font atlas
        _sampler = device.CreateSampler(new GPUSamplerDescriptor
        {
            MinFilter = "linear",
            MagFilter = "linear",
        });

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

        // Bind group: uniform + atlas texture + sampler
        RebuildBindGroup();

        // === World-space pipeline (VR/AR 3D panels) ===
        _worldVertexBuffer = device.CreateBuffer(new GPUBufferDescriptor
        {
            Size = (ulong)(MaxWorldQuads * VerticesPerQuad * WorldBytesPerVertex),
            Usage = GPUBufferUsage.Vertex | GPUBufferUsage.CopyDst,
        });
        _worldVertexBytes = new byte[_worldVertices.Length * sizeof(float)];

        // MVP uniform buffer (mat4x4 = 64 bytes)
        _worldUniformBuffer = device.CreateBuffer(new GPUBufferDescriptor
        {
            Size = 64,
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

    private void RebuildBindGroup()
    {
        if (_pipeline == null || _fontAtlas?.View == null || _sampler == null || _uniformBuffer == null) return;
        _bindGroup?.Dispose();
        _bindGroup = _device!.CreateBindGroup(new GPUBindGroupDescriptor
        {
            Layout = _pipeline.GetBindGroupLayout(0),
            Entries = new GPUBindGroupEntry[]
            {
                new() { Binding = 0, Resource = new GPUBufferBinding { Buffer = _uniformBuffer } },
                new() { Binding = 1, Resource = _fontAtlas.View },
                new() { Binding = 2, Resource = _sampler },
            }
        });
    }

    private void RebuildWorldBindGroup()
    {
        if (_worldPipeline == null || _fontAtlas?.View == null || _sampler == null || _worldUniformBuffer == null) return;
        _worldBindGroup?.Dispose();
        _worldBindGroup = _device!.CreateBindGroup(new GPUBindGroupDescriptor
        {
            Layout = _worldPipeline.GetBindGroupLayout(0),
            Entries = new GPUBindGroupEntry[]
            {
                new() { Binding = 0, Resource = new GPUBufferBinding { Buffer = _worldUniformBuffer } },
                new() { Binding = 1, Resource = _fontAtlas.View },
                new() { Binding = 2, Resource = _sampler },
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
    }

    /// <summary>Draw a solid-color rectangle.</summary>
    public void DrawRect(float x, float y, float w, float h, Color color)
    {
        if (_quadCount >= MaxQuads) return;
        float r = color.R / 255f, g = color.G / 255f, b = color.B / 255f, a = color.A / 255f;
        AddQuad(x, y, x + w, y + h, -1, -1, -1, -1, r, g, b, a);
    }

    /// <summary>Draw a text string at the given position.</summary>
    public void DrawText(string text, float x, float y, FontSize size, Color color)
    {
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
                        m.U0, m.V0, m.U1, m.V1, r, g, b, a);
            }
            cursorX += m.Advance;
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
        return _fontAtlas?.MeasureString(text, size) ?? 0;
    }

    /// <summary>Get line height for a font size.</summary>
    public float GetLineHeight(FontSize size)
    {
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

        // Upload viewport uniform
        var uniformData = new float[] { _viewportWidth, _viewportHeight, 0, 0 };
        var uniformBytes = new byte[16];
        Buffer.BlockCopy(uniformData, 0, uniformBytes, 0, 16);
        _queue!.WriteBuffer(_uniformBuffer!, 0, uniformBytes);

        // Append image quads to the vertex array after the main batch
        int imageStartQuad = _quadCount;
        foreach (var (view, ix, iy, iw, ih) in _imageBatch)
        {
            if (imageStartQuad >= MaxQuads) break;
            int offset = imageStartQuad * VerticesPerQuad * FloatsPerVertex;
            SetVertex(offset + 0 * FloatsPerVertex, ix, iy, 0, 0, 1, 1, 1, 1);
            SetVertex(offset + 1 * FloatsPerVertex, ix + iw, iy, 1, 0, 1, 1, 1, 1);
            SetVertex(offset + 2 * FloatsPerVertex, ix, iy + ih, 0, 1, 1, 1, 1, 1);
            SetVertex(offset + 3 * FloatsPerVertex, ix + iw, iy, 1, 0, 1, 1, 1, 1);
            SetVertex(offset + 4 * FloatsPerVertex, ix + iw, iy + ih, 1, 1, 1, 1, 1, 1);
            SetVertex(offset + 5 * FloatsPerVertex, ix, iy + ih, 0, 1, 1, 1, 1, 1);
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

        var bg = _device!.CreateBindGroup(new GPUBindGroupDescriptor
        {
            Layout = _pipeline.GetBindGroupLayout(0),
            Entries = new GPUBindGroupEntry[]
            {
                new() { Binding = 0, Resource = new GPUBufferBinding { Buffer = _uniformBuffer } },
                new() { Binding = 1, Resource = view },
                new() { Binding = 2, Resource = _sampler },
            }
        });
        _imageBindGroups[view] = bg;
        return bg;
    }

    private void AddQuad(float x0, float y0, float x1, float y1,
                         float u0, float v0, float u1, float v1,
                         float r, float g, float b, float a)
    {
        int offset = _quadCount * VerticesPerQuad * FloatsPerVertex;
        SetVertex(offset + 0 * FloatsPerVertex, x0, y0, u0, v0, r, g, b, a);
        SetVertex(offset + 1 * FloatsPerVertex, x1, y0, u1, v0, r, g, b, a);
        SetVertex(offset + 2 * FloatsPerVertex, x0, y1, u0, v1, r, g, b, a);
        SetVertex(offset + 3 * FloatsPerVertex, x1, y0, u1, v0, r, g, b, a);
        SetVertex(offset + 4 * FloatsPerVertex, x1, y1, u1, v1, r, g, b, a);
        SetVertex(offset + 5 * FloatsPerVertex, x0, y1, u0, v1, r, g, b, a);
        _quadCount++;
    }

    private void SetVertex(int offset, float x, float y, float u, float v,
                           float r, float g, float b, float a)
    {
        _vertices[offset + 0] = x;
        _vertices[offset + 1] = y;
        _vertices[offset + 2] = u;
        _vertices[offset + 3] = v;
        _vertices[offset + 4] = r;
        _vertices[offset + 5] = g;
        _vertices[offset + 6] = b;
        _vertices[offset + 7] = a;
    }

    // =====================================================================
    // World-space 3D rendering (VR/AR floating panels)
    // =====================================================================

    /// <summary>
    /// Draw a solid-color rectangle on a world-space panel.
    /// Coordinates are in panel-local space (0,0 = top-left of panel).
    /// The panel's world transform + camera VP matrix are set via SetWorldMVP().
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
                     -1, -1, -1, -1, r, g, b, a);
    }

    /// <summary>
    /// Draw text on a world-space panel.
    /// Coordinates are in panel-local pixel space.
    /// </summary>
    public void DrawWorldText(string text, float x, float y, float panelW, float panelH,
        Elements.FontSize size, System.Drawing.Color color)
    {
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
                             m.U0, m.V0, m.U1, m.V1, r, g, b, a);
            }
            cursorX += m.Advance;
        }
    }

    /// <summary>
    /// Flush world-space quads with the given MVP matrix.
    /// Call after End() for screen-space UI, in the same render pass or a new one.
    /// The MVP matrix = Projection * View * Model (panel world transform).
    /// </summary>
    public void EndWorldSpace(GPUCommandEncoder encoder, GPUTextureView colorTarget,
        GPUTextureView depthTarget, System.Numerics.Matrix4x4 mvp)
    {
        if (_worldPipeline == null || _worldQuadCount == 0) return;

        // Upload MVP matrix (column-major for WGSL)
        var mvpData = new float[16];
        // System.Numerics.Matrix4x4 is row-major, WGSL expects column-major
        mvpData[0]  = mvp.M11; mvpData[1]  = mvp.M21; mvpData[2]  = mvp.M31; mvpData[3]  = mvp.M41;
        mvpData[4]  = mvp.M12; mvpData[5]  = mvp.M22; mvpData[6]  = mvp.M32; mvpData[7]  = mvp.M42;
        mvpData[8]  = mvp.M13; mvpData[9]  = mvp.M23; mvpData[10] = mvp.M33; mvpData[11] = mvp.M43;
        mvpData[12] = mvp.M14; mvpData[13] = mvp.M24; mvpData[14] = mvp.M34; mvpData[15] = mvp.M44;

        var mvpBytes = new byte[64];
        Buffer.BlockCopy(mvpData, 0, mvpBytes, 0, 64);
        _queue!.WriteBuffer(_worldUniformBuffer!, 0, mvpBytes);

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
        float r, float g, float b, float a)
    {
        int offset = _worldQuadCount * VerticesPerQuad * WorldFloatsPerVertex;
        // Triangle 1: top-left, top-right, bottom-left
        SetWorldVertex(offset + 0 * WorldFloatsPerVertex, x0, y0, z0, u0, v0, r, g, b, a);
        SetWorldVertex(offset + 1 * WorldFloatsPerVertex, x1, y1, z1, u1, v0, r, g, b, a);
        SetWorldVertex(offset + 2 * WorldFloatsPerVertex, x2, y2, z2, u0, v1, r, g, b, a);
        // Triangle 2: top-right, bottom-right, bottom-left
        SetWorldVertex(offset + 3 * WorldFloatsPerVertex, x1, y1, z1, u1, v0, r, g, b, a);
        SetWorldVertex(offset + 4 * WorldFloatsPerVertex, x3, y3, z3, u1, v1, r, g, b, a);
        SetWorldVertex(offset + 5 * WorldFloatsPerVertex, x2, y2, z2, u0, v1, r, g, b, a);
        _worldQuadCount++;
    }

    private void SetWorldVertex(int offset, float x, float y, float z, float u, float v,
                                float r, float g, float b, float a)
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

        // World-space resources
        _worldVertexBuffer?.Destroy();
        _worldVertexBuffer?.Dispose();
        _worldUniformBuffer?.Destroy();
        _worldUniformBuffer?.Dispose();
        _worldBindGroup?.Dispose();
        _worldPipeline?.Dispose();
    }
}
