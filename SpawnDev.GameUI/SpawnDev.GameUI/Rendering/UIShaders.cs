namespace SpawnDev.GameUI.Rendering;

/// <summary>
/// WGSL shaders for the WebGPU UI renderer.
/// Renders batched 2D quads (solid color or font-atlas-textured) as an overlay.
/// For world-space 3D panels, a separate shader with MVP transform is used.
/// </summary>
internal static class UIShaders
{
    /// <summary>
    /// Screen-space UI quad vertex + fragment shader.
    /// Vertex: transforms screen-pixel coords (0,0 = top-left) to NDC.
    /// Fragment: either solid color (UV.x &lt; 0) or atlas-sampled text/image.
    /// </summary>
    public const string ScreenSpaceQuadShader = @"
struct Uniforms {
    viewport : vec2<f32>,
    _pad     : vec2<f32>,
};

@group(0) @binding(0) var<uniform> u : Uniforms;
@group(0) @binding(1) var t_atlas : texture_2d<f32>;
@group(0) @binding(2) var s_atlas : sampler;

struct VertexInput {
    @location(0) pos   : vec2<f32>,  // screen pixels (0,0 = top-left)
    @location(1) uv    : vec2<f32>,  // atlas UV (or -1,-1 for solid color)
    @location(2) color : vec4<f32>,  // RGBA tint / solid color
};

struct VertexOutput {
    @builtin(position) clip_pos : vec4<f32>,
    @location(0) uv    : vec2<f32>,
    @location(1) color : vec4<f32>,
};

@vertex
fn vs_main(input : VertexInput) -> VertexOutput {
    let ndc_x = input.pos.x / u.viewport.x * 2.0 - 1.0;
    let ndc_y = 1.0 - input.pos.y / u.viewport.y * 2.0;

    var out : VertexOutput;
    out.clip_pos = vec4<f32>(ndc_x, ndc_y, 0.0, 1.0);
    out.uv = input.uv;
    out.color = input.color;
    return out;
}

@fragment
fn fs_main(input : VertexOutput) -> @location(0) vec4<f32> {
    let tex = textureSample(t_atlas, s_atlas, max(input.uv, vec2<f32>(0.0)));
    let is_solid = input.uv.x < 0.0;
    let rgb = select(tex.rgb * input.color.rgb, input.color.rgb, is_solid);
    let alpha = select(tex.a * input.color.a, input.color.a, is_solid);
    return vec4<f32>(rgb, alpha);
}
";

    /// <summary>
    /// World-space UI panel vertex + fragment shader.
    /// Same as screen-space but with an MVP matrix instead of viewport-relative transform.
    /// Used for VR floating panels, view-anchored HUDs, and AR labels.
    /// </summary>
    public const string WorldSpaceQuadShader = @"
struct Uniforms {
    mvp : mat4x4<f32>,
};

@group(0) @binding(0) var<uniform> u : Uniforms;
@group(0) @binding(1) var t_atlas : texture_2d<f32>;
@group(0) @binding(2) var s_atlas : sampler;

struct VertexInput {
    @location(0) pos   : vec3<f32>,  // local-space position on the panel
    @location(1) uv    : vec2<f32>,  // atlas UV (or -1,-1 for solid color)
    @location(2) color : vec4<f32>,  // RGBA tint / solid color
};

struct VertexOutput {
    @builtin(position) clip_pos : vec4<f32>,
    @location(0) uv    : vec2<f32>,
    @location(1) color : vec4<f32>,
};

@vertex
fn vs_main(input : VertexInput) -> VertexOutput {
    var out : VertexOutput;
    out.clip_pos = u.mvp * vec4<f32>(input.pos, 1.0);
    out.uv = input.uv;
    out.color = input.color;
    return out;
}

@fragment
fn fs_main(input : VertexOutput) -> @location(0) vec4<f32> {
    let tex = textureSample(t_atlas, s_atlas, max(input.uv, vec2<f32>(0.0)));
    let is_solid = input.uv.x < 0.0;
    let rgb = select(tex.rgb * input.color.rgb, input.color.rgb, is_solid);
    let alpha = select(tex.a * input.color.a, input.color.a, is_solid);
    return vec4<f32>(rgb, alpha);
}
";
}
