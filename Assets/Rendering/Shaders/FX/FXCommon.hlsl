// Shared helpers for the FX/ shader family (Docs/vfx/README.md section 7).
//
// Conventions every FX shader follows:
// - Particle data arrives through Custom Vertex Streams in this order: UV (TEXCOORD0.xy), Custom1.xyzw
//   (TEXCOORD0.zw + TEXCOORD1.xy). Custom1.x = progress, .y = dissolve, .z = random phase, .w = reserved.
//   Only particle materials with _UseCustomData = 1 read the stream: a mesh's 2-component UV0 arrives as
//   (u, v, 0, 1), which would read as "fully dissolved". Mesh and quad layers get the same values through the
//   material property block (_FxProgress, _FxDissolve, _FxAlpha) that SkillVfxLayer writes; both sources add up.
// - Packed noise texture T_FX_Noise_Packed (linear): R = tiling noise, G = soft mask, B = dissolve noise,
//   A = gradient.
// - No shader reads the camera depth or opaque texture: the Balanced and Performant pipeline assets have both off.
#ifndef BUDDAHGO_FX_COMMON_INCLUDED
#define BUDDAHGO_FX_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

struct FxParticleAttributes
{
    float4 positionOS : POSITION;
    half4 color : COLOR;
    float4 texcoord0 : TEXCOORD0;
    float2 texcoord1 : TEXCOORD1;
};

struct FxParticleVaryings
{
    float4 positionCS : SV_POSITION;
    half4 color : COLOR;
    float2 uv : TEXCOORD0;
    float4 custom : TEXCOORD1;
    float fogFactor : TEXCOORD2;
};

float4 FxCustomData(FxParticleAttributes input, float useCustomData)
{
    return float4(input.texcoord0.zw, input.texcoord1.xy) * useCustomData;
}

// (radius 0..1 at the quad edge, angle 0..1 around the centre).
float2 FxPolar(float2 uv)
{
    float2 delta = uv - 0.5;
    float radius = length(delta) * 2.0;
    float angle = atan2(delta.x, delta.y) * (1.0 / TWO_PI) + 0.5;
    return float2(radius, angle);
}

// Hard dissolve with a soft band: returns (alpha mask, edge weight). threshold 0 = whole, 1 = gone.
float2 FxDissolve(float noise, float threshold, float edgeWidth)
{
    float t = threshold * (1.0 + edgeWidth);
    float alpha = smoothstep(t - edgeWidth, t, noise);
    float edge = saturate(1.0 - abs(noise - t) / max(edgeWidth, 1e-4)) * step(1e-3, threshold) * alpha;
    return float2(alpha, edge);
}

half FxLuminance(half3 c)
{
    return dot(c, half3(0.2126h, 0.7152h, 0.0722h));
}

#endif
