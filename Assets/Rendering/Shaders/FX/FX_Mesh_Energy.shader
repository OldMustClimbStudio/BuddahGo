// FX/Mesh_Energy: the mesh body functional layer (Docs/vfx/README.md 3.2 and 7). After the second review (palm doc
// 13.3 Q2) a skill's signature mesh is a self-lit gold energy form, never a dark or metal surface:
// - Body: dark gold in the middle (#8A5A14), gold at the silhouette and the fingertips (#F2C14E), with thin filaments
//   of light flowing toward the tips (iso-lines of scrolling noise). HDR, so the bright parts feed the scene bloom.
// - Charge: the reveal front sweeps from the tip toward the base with a gold edge, and the glow rises from dark gold
//   to full with _FxProgress; at no point is the body black.
// - Leading edge: the part furthest along the flight direction (object +Z) goes warm white (#FFF3C4); a position mask,
//   not a view Fresnel.
// - Exit: the glow drops to dark gold, then a hard noise dissolve with a gold edge (_FxDissolve).
// - Pulse mode (_PulseOnly = 1, additive blending set by the material): the same mesh drawn once more, adding the body
//   colour × _PulseColor × _FxAlpha, for the Imp flash's 2-4 warm-white frames; it keeps the body's light structure.
// The mesh's object-space height range comes from _HeightRange and its depth range from _DepthRange (set by the builder
// from the mesh bounds); its +Z is the palm's front. Noise is projected on object XY, so no UVs are needed.
Shader "FX/Mesh_Energy"
{
    Properties
    {
        [HDR] _CoreColor("Core (middle of the body)", Color) = (0.25, 0.10, 0.007, 1)
        [HDR] _RimColor("Rim and tips", Color) = (1.8, 1.05, 0.14, 1)
        _RimPower("Rim falloff", Range(0.5, 6)) = 1.6
        _TipStart("Tips start (fraction of height)", Range(0, 1)) = 0.7
        _ChargeStart("Glow at the start of the charge", Range(0, 1)) = 0.45

        [Header(Filaments)]
        _FilamentTiling("Filament tiling (object units)", Float) = 0.035
        _FilamentBands("Filament count", Float) = 6
        _FilamentWidth("Filament width", Range(0.01, 0.5)) = 0.12
        _FilamentSpeed("Filament flow toward the tips", Float) = 0.25
        _FilamentAmount("Filament brightness", Range(0, 1)) = 0.55

        [Header(Leading edge)]
        [HDR] _LeadColor("Leading edge", Color) = (2.2, 2.0, 1.4, 1)
        _DepthRange("Object-space depth range (min z, max z)", Vector) = (-0.5, 0.5, 0, 0)
        _LeadStart("Leading edge start (fraction of depth)", Range(0, 1)) = 0.75
        _LeadFacing("Leading edge facing sharpness", Range(0, 4)) = 2

        [Header(Reveal and dissolve)]
        _HeightRange("Object-space height range (min y, max y)", Vector) = (-0.5, 0.5, 0, 0)
        [NoScaleOffset] _NoiseTex("Packed noise (R noise, B dissolve)", 2D) = "gray" {}
        _NoiseTiling("Noise tiling (object units)", Float) = 0.03
        [HDR] _EdgeColor("Dissolve / reveal edge", Color) = (1.8, 1.05, 0.14, 1)
        _EdgeWidth("Edge width", Range(0.005, 0.4)) = 0.02

        [Header(Pulse)]
        [MaterialToggle] _PulseOnly("Pulse only (additive copy)", Float) = 0
        [HDR] _PulseColor("Pulse", Color) = (1.6, 1.5, 1.2, 1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst blend", Float) = 0
        [MaterialToggle] _ZWrite("ZWrite", Float) = 1

        [Header(Driven)]
        _FxAlpha("Alpha (driven)", Range(0, 1)) = 1
        _FxProgress("Charge progress (driven)", Range(0, 1)) = 1
        _FxDissolve("Dissolve (driven)", Range(0, 1)) = 0
    }

    HLSLINCLUDE
    #include "FXCommon.hlsl"

    TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

    CBUFFER_START(UnityPerMaterial)
        half4 _CoreColor;
        half4 _RimColor;
        half _RimPower;
        half _TipStart;
        half _ChargeStart;
        float _FilamentTiling;
        float _FilamentBands;
        half _FilamentWidth;
        float _FilamentSpeed;
        half _FilamentAmount;
        half4 _LeadColor;
        float4 _DepthRange;
        half _LeadStart;
        half _LeadFacing;
        float4 _HeightRange;
        float _NoiseTiling;
        half4 _EdgeColor;
        half _EdgeWidth;
        half _PulseOnly;
        half4 _PulseColor;
        half _SrcBlend;
        half _DstBlend;
        half _ZWrite;
        half _FxAlpha;
        half _FxProgress;
        half _FxDissolve;
    CBUFFER_END

    struct MeshAttributes
    {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
    };

    struct MeshVaryings
    {
        float4 positionCS : SV_POSITION;
        float3 positionWS : TEXCOORD0;
        float3 normalWS : TEXCOORD1;
        float3 positionOS : TEXCOORD2;
        float fogFactor : TEXCOORD3;
    };

    MeshVaryings MeshVert(MeshAttributes input)
    {
        MeshVaryings output;
        VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
        output.positionCS = positions.positionCS;
        output.positionWS = positions.positionWS;
        output.normalWS = TransformObjectToWorldNormal(input.normalOS);
        output.positionOS = input.positionOS.xyz;
        output.fogFactor = ComputeFogFactor(positions.positionCS.z);
        return output;
    }

    // x = visible (0 or 1), y = gold edge weight, z = normalised height.
    float3 MeshMasks(MeshVaryings input)
    {
        float height = saturate((input.positionOS.y - _HeightRange.x) / max(_HeightRange.y - _HeightRange.x, 1e-4));
        half4 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.positionOS.xy * _NoiseTiling);

        // Entry: the tip (height 1) appears first; the hard-edged front sweeps down over the first 80 % of the charge.
        float reveal = saturate(_FxProgress * 1.25);
        float front = 1.0 - reveal * (1.0 + _EdgeWidth * 2.0) + (noise.r - 0.5) * _EdgeWidth * 2.0;
        float revealed = step(front, height);
        float revealEdge = step(height, front + _EdgeWidth) * step(reveal, 0.999);

        // The exit dims first (see BodyColor), then dissolves over the last 60 %.
        float2 dissolve = FxDissolve(noise.b, saturate(_FxDissolve * 1.6 - 0.6), _EdgeWidth);
        float visible = revealed * step(0.5, dissolve.x);
        return float3(visible, max(revealEdge, dissolve.y), height);
    }

    half3 BodyColor(MeshVaryings input, float3 masks)
    {
        float3 normalWS = normalize(input.normalWS);
        float3 viewWS = normalize(GetWorldSpaceViewDir(input.positionWS));
        half rim = pow(1.0 - saturate(abs(dot(normalWS, viewWS))), _RimPower);
        half tip = smoothstep(_TipStart, 1.0, masks.z);

        // Filaments: thin bright iso-lines of a slowly rising noise field.
        float2 filamentUV = input.positionOS.xy * _FilamentTiling * float2(1.0, 0.35) - float2(0.0, _FilamentSpeed * _Time.y);
        half field = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, filamentUV).r * _FilamentBands;
        half band = abs(frac(field) - 0.5) * 2.0;
        half filament = smoothstep(1.0 - _FilamentWidth, 1.0, band);

        half glow = saturate(rim + tip * 0.6 + filament * _FilamentAmount);
        half3 color = lerp(_CoreColor.rgb, _RimColor.rgb, glow);

        float depth = saturate((input.positionOS.z - _DepthRange.x) / max(_DepthRange.y - _DepthRange.x, 1e-4));
        float3 flightWS = normalize(UNITY_MATRIX_M._m02_m12_m22);
        half facing = pow(saturate(dot(normalWS, flightWS) * 0.5 + 0.5), _LeadFacing);
        half lead = smoothstep(_LeadStart, 1.0, depth) * facing * step(0.999, _FxProgress);
        color = lerp(color, _LeadColor.rgb, saturate(lead));

        // Charge brings the glow up from dark gold; the exit brings it back down before the dissolve eats it.
        half level = lerp(_ChargeStart, 1.0, saturate(_FxProgress * 1.25)) * lerp(1.0, 0.45, saturate(_FxDissolve * 2.5));
        color *= level;
        return lerp(color, _EdgeColor.rgb, saturate(masks.y));
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "FXMeshEnergy"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex MeshVert
            #pragma fragment Frag
            #pragma multi_compile_fog

            half4 Frag(MeshVaryings input) : SV_Target
            {
                float3 masks = MeshMasks(input);
                clip(masks.x - 0.5);
                half3 body = BodyColor(input, masks);
                if (_PulseOnly > 0.5)
                    return half4(body * _PulseColor.rgb * _FxAlpha, 1.0);
                // The body is opaque light: _FxAlpha only cuts it away entirely (0 hides the layer).
                clip(_FxAlpha - 0.01);
                return half4(MixFog(body, input.fogFactor), 1.0);
            }
            ENDHLSL
        }
    }
}
