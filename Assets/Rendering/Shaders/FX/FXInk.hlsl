// Ink and seal shapes shared by FX/Decal and FX/Alpha_Smoke (Docs/vfx/README.md section 7; palm doc 13.3 Q4-Q5 and
// 13.6). Shapes come from a real brush scan or an authored mask; this file only cuts, draws and lights them.
//
// The mask texture carries two channels: A = shape, R = position along the stroke (0 at the head, 1 at the tail;
// Tools/vfx/cut_brush.py writes it). A seal mask has R = shape, which is harmless because seals are shown fully drawn.
// - Cut: the shape × erosion (_FxAlpha × vertex alpha) is cut at _Cutoff with a _Feather wide soft band, so the core
//   stays at _Opacity and only the edge feathers; erosion removes thin parts first instead of fading to grey.
// - Draw / erase: the stroke appears from the head toward the tail as _FxProgress (+ particle Custom1.x) goes 0 -> 1
//   and leaves from the root as _FxDissolve (+ Custom1.y) does, or by a hard noise step with _NoiseErase = 1 (seals).
// - Gold: a thin gold rim on the head-side outline (_RimColor, the side facing the motion) and at the drawing front,
//   and sparse gold embers inside the ink (_EmberColor; cells of the packed noise's G channel), so a piece of ink is
//   never a plain black hole on the bright track.
#ifndef BUDDAHGO_FX_INK_INCLUDED
#define BUDDAHGO_FX_INK_INCLUDED

#include "FXCommon.hlsl"

TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    half4 _Color;
    half _Opacity;
    half _Cutoff;
    half _Feather;
    half _ClipAt;
    half4 _RimColor;
    half _RimWidth;
    half4 _EdgeColor;
    half _EdgeWidth;
    half4 _EmberColor;
    float _EmberTiling;
    half _EmberAmount;
    float _NoiseTiling;
    half _NoiseErase;
    half _SwapUV;
    float _UseCustomData;
    half _FxAlpha;
    half _FxProgress;
    half _FxDissolve;
    float _DepthOffset;
CBUFFER_END

FxParticleVaryings FxInkVert(FxParticleAttributes input)
{
    FxParticleVaryings output;
    VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = positions.positionCS;
    output.color = input.color;
    float2 uv = _SwapUV > 0.5 ? input.texcoord0.yx : input.texcoord0.xy;
    output.uv = TRANSFORM_TEX(uv, _MainTex);
    output.custom = FxCustomData(input, _UseCustomData);
    output.fogFactor = ComputeFogFactor(positions.positionCS.z);
    return output;
}

half4 FxInkFrag(FxParticleVaryings input) : SV_Target
{
    half4 mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
    half along = mask.r;
    half value = mask.a * _FxAlpha * input.color.a;
    half alpha = smoothstep(_Cutoff - _Feather, _Cutoff + _Feather, value) * _Opacity;

    float draw = saturate(_FxProgress + input.custom.x);
    float erase = saturate(_FxDissolve + input.custom.y);
    clip(draw * (1.0 + _EdgeWidth) - along);
    half front = step(draw * (1.0 + _EdgeWidth) - _EdgeWidth, along) * step(draw, 0.999);

    if (_NoiseErase > 0.5)
    {
        half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv * _NoiseTiling).b;
        float t = erase * (1.0 + _EdgeWidth);
        clip(noise - t);
        front = max(front, step(noise, t + _EdgeWidth) * step(1e-3, erase));
    }
    else
    {
        clip(along - erase);
    }

    // Rim: the outline band just inside the cut, strongest on the head side.
    half rim = (1.0 - smoothstep(_Cutoff, _Cutoff + _RimWidth, value)) * step(_Cutoff - _Feather, value) * (1.0 - along);
    float time = _Time.y + input.custom.z * 10.0;
    half cell = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv * _EmberTiling + float2(0.0, time * 0.15)).g;
    half ember = smoothstep(1.0 - _EmberAmount, 1.0 - _EmberAmount * 0.4, cell) * step(_Cutoff, value);

    half3 color = _Color.rgb * input.color.rgb;
    color = lerp(color, _RimColor.rgb, saturate(rim * _RimColor.a));
    color = lerp(color, _EdgeColor.rgb, saturate(front * _EdgeColor.a));
    color += _EmberColor.rgb * ember;
    clip(alpha - _ClipAt);
    return half4(MixFog(color, input.fogFactor), alpha);
}

#endif
