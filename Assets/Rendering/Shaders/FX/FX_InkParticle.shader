// FX/InkParticle: draws the InkParticles buffer (Docs/vfx/palm-mesh-tripo.md section 18) as tens of thousands of tiny
// soft discs. Each disc is faint on its own (_Opacity); where the flow packs them together they add up to solid ink,
// where they thin out they read as wash. Procedural quads from SV_VertexID (6 vertices per particle), camera-facing,
// soft against scene depth. _FxAlpha / _FxDissolve are for the timeline (whole-layer fade).
Shader "FX/InkParticle"
{
    Properties
    {
        [NoScaleOffset] _ColorRamp("Colour over life (InkParticles bakes its gradient here)", 2D) = "black" {}
        _Opacity("Opacity per particle", Range(0, 1)) = 0.12
        _Size("Size at birth (world units)", Float) = 2.0
        _SizeGrow("Size growth over life (x)", Float) = 2.5
        _Core("Disc hardness", Range(0.5, 4)) = 1.4
        _FadeIn("Fade in (fraction of life)", Range(0.01, 0.5)) = 0.08
        _FadeOutStart("Fade out starts (fraction of life)", Range(0, 0.95)) = 0.5
        _SoftDistance("Soft particle distance (world units)", Float) = 4
        _FxAlpha("Layer alpha (timeline)", Range(0, 1)) = 1
        _FxDissolve("Layer dissolve (timeline)", Range(0, 1)) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

    struct Particle
    {
        float3 pos;
        float age;
        float3 vel;
        float life;
        float seed;
        float3 pad;
    };

    StructuredBuffer<Particle> _Particles;
    TEXTURE2D(_ColorRamp); SAMPLER(sampler_ColorRamp);

    CBUFFER_START(UnityPerMaterial)
        half _Opacity;
        float _Size;
        float _SizeGrow;
        half _Core;
        half _FadeIn;
        half _FadeOutStart;
        float _SoftDistance;
        half _FxAlpha;
        half _FxDissolve;
    CBUFFER_END

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;          // -1..1 across the disc
        half4 color : COLOR;            // rgb tone, a = alpha envelope
        float fogFactor : TEXCOORD1;
    };

    static const float2 Corners[6] = { float2(-1, -1), float2(1, -1), float2(1, 1), float2(-1, -1), float2(1, 1), float2(-1, 1) };

    Varyings Vert(uint vid : SV_VertexID)
    {
        Varyings o = (Varyings)0;
        uint pid = vid / 6;
        Particle p = _Particles[pid];
        float t = p.age / max(p.life, 1e-3);
        if (p.age >= p.life)
        {
            o.positionCS = float4(0, 0, 0, 0);   // dead: zero-area, clipped
            return o;
        }
        float2 c = Corners[vid % 6];
        float size = _Size * (1.0 + _SizeGrow * t) * (0.7 + 0.6 * p.seed);
        float3 right = UNITY_MATRIX_V[0].xyz;
        float3 up = UNITY_MATRIX_V[1].xyz;
        float3 world = p.pos + (right * c.x + up * c.y) * size;
        o.positionCS = TransformWorldToHClip(world);
        o.uv = c;
        half env = smoothstep(0.0, _FadeIn, t) * (1.0 - smoothstep(_FadeOutStart, 1.0, t));
        half4 rampCol = SAMPLE_TEXTURE2D_LOD(_ColorRamp, sampler_ColorRamp, float2(t, 0.5), 0);
        o.color = half4(rampCol.rgb, env * rampCol.a);
        o.fogFactor = ComputeFogFactor(o.positionCS.z);
        return o;
    }

    half4 Frag(Varyings i) : SV_Target
    {
        half d = length(i.uv);
        half disc = pow(saturate(1.0 - d), _Core);
        half alpha = disc * i.color.a * _Opacity * _FxAlpha * (1.0 - _FxDissolve);
        float2 depthUV = GetNormalizedScreenSpaceUV(i.positionCS);
        float sceneEye = LinearEyeDepth(SampleSceneDepth(depthUV), _ZBufferParams);
        float fragEye = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
        alpha *= saturate((sceneEye - fragEye) / max(_SoftDistance, 1e-3));
        return half4(MixFog(i.color.rgb, i.fogFactor), alpha);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "InkParticle"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            ENDHLSL
        }
    }
    Fallback Off
}
