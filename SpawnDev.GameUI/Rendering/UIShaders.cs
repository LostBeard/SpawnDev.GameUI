namespace SpawnDev.GameUI.Rendering;

/// <summary>
/// WGSL shaders for the WebGPU UI renderer.
/// Supports four rendering modes in one pipeline:
///   1. Solid color quads (UV.x less than 0) - sharp backgrounds, borders
///   2. Bitmap text (flags = 0, UV >= 0) - legacy atlas sampling
///   3. SDF text (flags = 1) - signed distance field with anti-aliasing, outlines
///   4. Rounded solid (flags >= 2) - UV 0..1 local; radiusPx = flags - 2
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
    // Shared fragment body used by both screen and world shaders (after texture samples).
    // Screen and world differ only in vertex transform / uniforms layout.
    private const string FragmentBody = @"
    let safe_uv = max(input.uv, vec2<f32>(0.0));
    let bitmap_sample = textureSample(t_bitmap, s_nearest, safe_uv);
    let sdf_sample = textureSample(t_sdf, s_linear, safe_uv).r;

    let is_solid = input.uv.x < 0.0;
    // flags: 0 = bitmap, 1 = SDF text, >=2 = rounded solid (radiusPx = flags - 2)
    let is_rounded = input.flags >= 1.5;
    let is_sdf = input.flags > 0.5 && input.flags < 1.5;

    // SDF text: distance field -> alpha with anti-aliasing
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

    // Rounded solid: UV is local 0..1; reconstruct pixel size via fwidth
    let radius_px = max(input.flags - 2.0, 0.0);
    let fw = max(fwidth(input.uv), vec2<f32>(1e-5));
    let dims = 1.0 / fw;
    let p = (input.uv - 0.5) * dims;
    let half_size = dims * 0.5;
    let rad = min(radius_px, min(half_size.x, half_size.y));
    let q = abs(p) - half_size + vec2<f32>(rad);
    let dist = length(max(q, vec2<f32>(0.0))) + min(max(q.x, q.y), 0.0) - rad;
    let round_aa = max(fwidth(dist), 0.75);
    let round_alpha = (1.0 - smoothstep(-round_aa, round_aa, dist)) * input.color.a;
    let rounded_result = vec4<f32>(input.color.rgb, round_alpha);

    // Priority: sharp solid > rounded solid > SDF text > bitmap
    let textured_result = select(bitmap_result, sdf_result, is_sdf);
    let after_rounded = select(textured_result, rounded_result, is_rounded);
    return select(after_rounded, solid_result, is_solid);
";

    /// <summary>
    /// Screen-space UI quad vertex + fragment shader with SDF and rounded-rect support.
    /// Vertex: transforms screen-pixel coords (0,0 = top-left) to NDC.
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
@group(0) @binding(3) var s_nearest : sampler;
@group(0) @binding(4) var s_linear  : sampler;

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
" + FragmentBody + @"
}
";

    /// <summary>
    /// World-space UI panel vertex + fragment shader with SDF and rounded-rect support.
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
@group(0) @binding(3) var s_nearest : sampler;
@group(0) @binding(4) var s_linear  : sampler;

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
" + FragmentBody + @"
}
";
}
