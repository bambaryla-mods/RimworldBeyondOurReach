#version 450

// The dark mirror's portal (CompMirrorPortal): _OnTex phases in over _MainTex through value noise, with a glowing
// edge where the two meet. _Phase is x phase (0 all off, 1 all on), y edge width, z noise cells across the texture.
// The noise lives in texture space, so the pattern stays put on the portal as it bobs.

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _MainTex_ST; // @default = vec4(0.0, 0.0, 1.0, 1.0)
    vec4 _Phase;      // @default = vec4(0.0, 0.08, 24.0, 0.0)
    vec4 _EdgeColor;  // @color @default = vec4(1.0, 0.25, 0.2, 1.0)
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;
layout(set = 2, binding = 2) uniform sampler2D _OnTex;

float hash(vec2 cell)
{
    return fract(sin(dot(cell, vec2(127.1, 311.7))) * 43758.5453);
}

float valueNoise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

void main()
{
    vec2 uv = vUv * u._MainTex_ST.zw + u._MainTex_ST.xy;
    vec4 off = texture(_MainTex, uv);
    vec4 on = texture(_OnTex, uv);

    float w = u._Phase.y;
    float p = u._Phase.x * (1.0 + w);
    float n = valueNoise(vUv * u._Phase.z);

    vec4 color;
    if (n < p - w)
    {
        color = on;
    }
    else if (n < p)
    {
        color = vec4(mix(on.rgb, u._EdgeColor.rgb, u._EdgeColor.a), max(on.a, off.a));
    }
    else
    {
        color = off;
    }
    color *= u._Color * vColor;
    if (color.a <= 0.001)
        discard;

    outColor = color;
}
