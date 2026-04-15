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
    viewport     : vec2<f32>,  // screen size in pixels
    outlineWidth : f32,        // SDF outline width (0 = none, 0.05-0.15 = typical)
    softness     : f32,        // extra edge softness (0 = sharp)
    outlineColor : vec4<f32>,  // outline RGBA
};

@group(0) @binding(0) var<uniform> u : Uniforms;
@group(0) @binding(1) var t_bitmap : texture_2d<f32>;
@group(0) @binding(2) var t_sdf    : texture_2d<f32>;
@group(0) @binding(3) var s_atlas  : sampler;

struct VertexInput {
    @location(0) pos   : vec2<f32>,  // screen pixels (0,0 = top-left)
    @location(1) uv    : vec2<f32>,  // atlas UV (or -1,-1 for solid color)
    @location(2) color : vec4<f32>,  // RGBA tint / solid color
    @location(3) flags : f32,        // 0 = solid/bitmap, 1 = SDF
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
    // Solid color quad (no texture sampling)
    if (input.uv.x < 0.0) {
        return input.color;
    }

    // SDF text rendering
    if (input.flags > 0.5) {
        let dist = textureSample(t_sdf, s_atlas, input.uv).r;
        let edge = 0.5;
        let aa = fwidth(dist) * 0.75 + u.softness;

        // Fill
        let fill_alpha = smoothstep(edge - aa, edge + aa, dist);

        // Outline (when outlineWidth > 0)
        if (u.outlineWidth > 0.001) {
            let outline_edge = edge - u.outlineWidth;
            let outline_alpha = smoothstep(outline_edge - aa, outline_edge + aa, dist);
            let blended_color = mix(u.outlineColor.rgb, input.color.rgb, fill_alpha);
            return vec4<f32>(blended_color, outline_alpha * input.color.a);
        }

        return vec4<f32>(input.color.rgb, fill_alpha * input.color.a);
    }

    // Bitmap text rendering
    let tex = textureSample(t_bitmap, s_atlas, input.uv);
    return vec4<f32>(tex.rgb * input.color.rgb, tex.a * input.color.a);
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
    @location(0) pos   : vec3<f32>,  // local-space position on the panel
    @location(1) uv    : vec2<f32>,  // atlas UV (or -1,-1 for solid color)
    @location(2) color : vec4<f32>,  // RGBA tint / solid color
    @location(3) flags : f32,        // 0 = solid/bitmap, 1 = SDF
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
    if (input.uv.x < 0.0) {
        return input.color;
    }

    if (input.flags > 0.5) {
        let dist = textureSample(t_sdf, s_atlas, input.uv).r;
        let edge = 0.5;
        let aa = fwidth(dist) * 0.75 + u.softness;
        let fill_alpha = smoothstep(edge - aa, edge + aa, dist);

        if (u.outlineWidth > 0.001) {
            let outline_edge = edge - u.outlineWidth;
            let outline_alpha = smoothstep(outline_edge - aa, outline_edge + aa, dist);
            let blended_color = mix(u.outlineColor.rgb, input.color.rgb, fill_alpha);
            return vec4<f32>(blended_color, outline_alpha * input.color.a);
        }

        return vec4<f32>(input.color.rgb, fill_alpha * input.color.a);
    }

    let tex = textureSample(t_bitmap, s_atlas, input.uv);
    return vec4<f32>(tex.rgb * input.color.rgb, tex.a * input.color.a);
}
";
}
