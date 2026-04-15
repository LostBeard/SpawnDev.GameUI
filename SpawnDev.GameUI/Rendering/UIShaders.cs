namespace SpawnDev.GameUI.Rendering;

/// <summary>
/// WGSL shaders for the WebGPU UI renderer.
/// Supports three rendering modes in one pipeline:
///   1. Solid color quads (UV.x less than 0) - backgrounds, borders
///   2. Bitmap text (flags = 0, UV >= 0) - legacy atlas sampling
///   3. SDF text (flags = 1) - signed distance field with anti-aliasing, outlines
///
/// Both bitmap and SDF textures are bound simultaneously.
/// The per-vertex flags field selects the rendering path.
///
/// IMPORTANT: All textureSample calls are unconditional (uniform control flow).
/// WebGPU WGSL validation rejects textureSample inside non-uniform branches.
/// Results are combined using select() instead of if/return.
/// </summary>
internal static class UIShaders
{
    /// <summary>
    /// Screen-space UI quad vertex + fragment shader with SDF support.
    /// Vertex: transforms screen-pixel coords (0,0 = top-left) to NDC.
    /// Fragment: solid color, bitmap atlas text, or SDF text with outline.
    /// </summary>
    public const string ScreenSpaceQuadShader = @"
struct Uniforms {
    viewport     : vec2<f32>,
    outlineWidth : f32,
    softness     : f32,
    outlineColor : vec4<f32>,
};

@group(0) @binding(0) var<uniform> u : Uniforms;
@group(0) @binding(1) var t_bitmap : texture_2d<f32>;
@group(0) @binding(2) var t_sdf    : texture_2d<f32>;
@group(0) @binding(3) var s_atlas  : sampler;

struct VertexInput {
    @location(0) pos   : vec2<f32>,
    @location(1) uv    : vec2<f32>,
    @location(2) color : vec4<f32>,
    @location(3) flags : f32,
};

struct VertexOutput {
    @builtin(position) clip_pos : vec4<f32>,
    @location(0) uv    : vec2<f32>,
    @location(1) color : vec4<f32>,
    @location(2) flags : f32,
};

@vertex
fn vs_main(input : VertexInput) -> VertexOutput {
    let ndc_x = input.pos.x / u.viewport.x * 2.0 - 1.0;
    let ndc_y = 1.0 - input.pos.y / u.viewport.y * 2.0;

    var out : VertexOutput;
    out.clip_pos = vec4<f32>(ndc_x, ndc_y, 0.0, 1.0);
    out.uv = input.uv;
    out.color = input.color;
    out.flags = input.flags;
    return out;
}

@fragment
fn fs_main(input : VertexOutput) -> @location(0) vec4<f32> {
    // Sample BOTH textures unconditionally (WebGPU requires uniform control flow for textureSample)
    let safe_uv = max(input.uv, vec2<f32>(0.0));
    let bitmap_sample = textureSample(t_bitmap, s_atlas, safe_uv);
    let sdf_sample = textureSample(t_sdf, s_atlas, safe_uv).r;

    let is_solid = input.uv.x < 0.0;
    let is_sdf = input.flags > 0.5;

    // SDF text: distance field -> alpha with anti-aliasing
    let edge = 0.5;
    let aa = fwidth(sdf_sample) * 0.75 + u.softness;
    let fill_alpha = smoothstep(edge - aa, edge + aa, sdf_sample);
    let outline_edge = edge - u.outlineWidth;
    let outline_alpha = smoothstep(outline_edge - aa, outline_edge + aa, sdf_sample);
    let has_outline = u.outlineWidth > 0.001;

    // SDF result (with or without outline)
    let sdf_color = select(input.color.rgb, mix(u.outlineColor.rgb, input.color.rgb, fill_alpha), has_outline);
    let sdf_alpha = select(fill_alpha, outline_alpha, has_outline) * input.color.a;
    let sdf_result = vec4<f32>(sdf_color, sdf_alpha);

    // Bitmap text result
    let bitmap_result = vec4<f32>(bitmap_sample.rgb * input.color.rgb, bitmap_sample.a * input.color.a);

    // Solid color result
    let solid_result = input.color;

    // Select final output: solid > SDF > bitmap (priority order)
    let textured_result = select(bitmap_result, sdf_result, is_sdf);
    return select(textured_result, solid_result, is_solid);
}
";

    /// <summary>
    /// World-space UI panel vertex + fragment shader with SDF support.
    /// Same fragment logic as screen-space but with MVP matrix vertex transform.
    /// Used for VR floating panels, view-anchored HUDs, and AR labels.
    /// </summary>
    public const string WorldSpaceQuadShader = @"
struct Uniforms {
    mvp          : mat4x4<f32>,
    outlineWidth : f32,
    softness     : f32,
    _pad         : vec2<f32>,
    outlineColor : vec4<f32>,
};

@group(0) @binding(0) var<uniform> u : Uniforms;
@group(0) @binding(1) var t_bitmap : texture_2d<f32>;
@group(0) @binding(2) var t_sdf    : texture_2d<f32>;
@group(0) @binding(3) var s_atlas  : sampler;

struct VertexInput {
    @location(0) pos   : vec3<f32>,
    @location(1) uv    : vec2<f32>,
    @location(2) color : vec4<f32>,
    @location(3) flags : f32,
};

struct VertexOutput {
    @builtin(position) clip_pos : vec4<f32>,
    @location(0) uv    : vec2<f32>,
    @location(1) color : vec4<f32>,
    @location(2) flags : f32,
};

@vertex
fn vs_main(input : VertexInput) -> VertexOutput {
    var out : VertexOutput;
    out.clip_pos = u.mvp * vec4<f32>(input.pos, 1.0);
    out.uv = input.uv;
    out.color = input.color;
    out.flags = input.flags;
    return out;
}

@fragment
fn fs_main(input : VertexOutput) -> @location(0) vec4<f32> {
    // Sample BOTH textures unconditionally (uniform control flow required)
    let safe_uv = max(input.uv, vec2<f32>(0.0));
    let bitmap_sample = textureSample(t_bitmap, s_atlas, safe_uv);
    let sdf_sample = textureSample(t_sdf, s_atlas, safe_uv).r;

    let is_solid = input.uv.x < 0.0;
    let is_sdf = input.flags > 0.5;

    let edge = 0.5;
    let aa = fwidth(sdf_sample) * 0.75 + u.softness;
    let fill_alpha = smoothstep(edge - aa, edge + aa, sdf_sample);
    let outline_edge = edge - u.outlineWidth;
    let outline_alpha = smoothstep(outline_edge - aa, outline_edge + aa, sdf_sample);
    let has_outline = u.outlineWidth > 0.001;

    let sdf_color = select(input.color.rgb, mix(u.outlineColor.rgb, input.color.rgb, fill_alpha), has_outline);
    let sdf_alpha = select(fill_alpha, outline_alpha, has_outline) * input.color.a;
    let sdf_result = vec4<f32>(sdf_color, sdf_alpha);

    let bitmap_result = vec4<f32>(bitmap_sample.rgb * input.color.rgb, bitmap_sample.a * input.color.a);
    let solid_result = input.color;

    let textured_result = select(bitmap_result, sdf_result, is_sdf);
    return select(textured_result, solid_result, is_solid);
}
";
}
