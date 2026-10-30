// FX/InkGlass: the "ink in glass" body for the 如来神掌 Tripo hand (Docs/vfx/palm-mesh-tripo.md section 8). Same picture
// language as the Piloto Realistic Ying Yang pack: only white (light) and black (wet ink) with grey in the transitions,
// a three-colour ramp on a cloud texture, no colour anywhere.
// - Glass: a white translucent body (Alpha blend, no ZWrite), drawn twice: back faces first (fainter), then front faces,
//   so the hand reads as a solid clear object without the depth or opaque textures (Balanced / Performant have none).
// - Ink: the pack's cloud noise sampled triplanar in object space, two scales, drifting slowly; the sample position is
//   pushed along the view direction (_InkDepth) so the blots sit inside the glass and shift as the camera moves. The
//   cloud value goes through a white / grey / black ramp (_InkCoverage, _InkSoftness, _InkMidWidth), like the pack's
//   Color Ramping, so blots have a soft grey wash around a black core instead of a flat stain.
// - Edges: a view Fresnel pulls the body toward ink and raises opacity at the silhouette, which is where ink collects in
//   a glass cast; creases come from the mesh itself.
// - Light: a soft wrapped diffuse from the main light for the form and a tight Blinn-Phong highlight (white, HDR) for
//   the glossy surface; the highlight also raises alpha so it shows on the clear parts.
// - Driven: _FxAlpha fades the whole hand; _FxDissolve eats it from the cloud noise with an ink edge (_DissolveEdge).
Shader "FX/InkGlass"
{
    Properties
    {
        [Header(Glass body)]
        _BodyColor("Body (glass)", Color) = (1, 1, 1, 0.7)
        _BackAlpha("Back faces alpha factor", Range(0, 1)) = 0.45
        _Shading("Form shading", Range(0, 1)) = 0.4
        [HDR] _SpecColor("Highlight", Color) = (1.8, 1.8, 1.8, 1)
        _Gloss("Highlight tightness", Range(8, 512)) = 120

        [Header(Ink)]
        _InkColor("Ink", Color) = (0.02, 0.02, 0.025, 1)
        _MidColor("Ink wash (mid-tone)", Color) = (0.52, 0.52, 0.55, 1)
        [NoScaleOffset] _InkTex("Ink cloud texture (R)", 2D) = "gray" {}
        _InkTiling("Ink tiling (per object unit)", Float) = 0.012
        _InkDetailScale("Detail layer scale", Float) = 2.6
        _InkDetailWeight("Detail layer weight", Range(0, 1)) = 0.4
        _InkCoverage("Ink coverage", Range(0, 1)) = 0.42
        _InkSoftness("Ink edge softness", Range(0.01, 1)) = 0.22
        _InkMidWidth("Wash width", Range(0, 1)) = 0.35
        _InkDepth("Ink depth inside the glass (object units)", Range(0, 30)) = 8
        _InkFlow("Ink drift speed", Float) = 0.015
        _InkOpacity("Opacity of the ink core", Range(0, 1)) = 0.9
        _WashOpacity("Opacity of the wash", Range(0, 1)) = 0.35
        _EdgeDarken("Dried edge around blots", Range(0, 1)) = 0.6

        [Header(Edges)]
        _RimPower("Edge ink falloff", Range(0.5, 8)) = 2.2
        _RimStrength("Edge ink", Range(0, 1)) = 0.85
        _RimAlpha("Edge opacity boost", Range(0, 1)) = 0.45

        [Header(Driven)]
        _FxAlpha("Alpha (driven)", Range(0, 1)) = 1
        _FxDissolve("Dissolve (driven)", Range(0, 1)) = 0
        _DissolveEdge("Dissolve edge width", Range(0.005, 0.4)) = 0.08
    }

    HLSLINCLUDE
    #include "FXCommon.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

    TEXTURE2D(_InkTex); SAMPLER(sampler_InkTex);

    CBUFFER_START(UnityPerMaterial)
        half4 _BodyColor;
        half _BackAlpha;
        half _Shading;
        half4 _SpecColor;
        half _Gloss;
        half4 _InkColor;
        half4 _MidColor;
        float _InkTiling;
        float _InkDetailScale;
        half _InkDetailWeight;
        half _InkCoverage;
        half _InkSoftness;
        half _InkMidWidth;
        float _InkDepth;
        float _InkFlow;
        half _InkOpacity;
        half _WashOpacity;
        half _EdgeDarken;
        half _RimPower;
        half _RimStrength;
        half _RimAlpha;
        half _FxAlpha;
        half _FxDissolve;
        half _DissolveEdge;
    CBUFFER_END

    struct Attributes
    {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float3 positionOS : TEXCOORD0;
        float3 normalOS : TEXCOORD1;
        float3 normalWS : TEXCOORD2;
        float3 viewDirWS : TEXCOORD3;
        float fogFactor : TEXCOORD4;
    };

    Varyings InkGlassVert(Attributes input)
    {
        Varyings output;
        VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
        output.positionCS = positions.positionCS;
        output.positionOS = input.positionOS.xyz;
        output.normalOS = input.normalOS;
        output.normalWS = TransformObjectToWorldNormal(input.normalOS);
        output.viewDirWS = GetWorldSpaceNormalizeViewDir(positions.positionWS);
        output.fogFactor = ComputeFogFactor(positions.positionCS.z);
        return output;
    }

    // Triplanar cloud sample in object space (R channel), weights from the object normal.
    half SampleCloud(float3 p, float3 weights, float2 drift)
    {
        half x = SAMPLE_TEXTURE2D(_InkTex, sampler_InkTex, p.zy + drift).r;
        half y = SAMPLE_TEXTURE2D(_InkTex, sampler_InkTex, p.xz + drift.yx).r;
        half z = SAMPLE_TEXTURE2D(_InkTex, sampler_InkTex, p.xy - drift).r;
        return x * weights.x + y * weights.y + z * weights.z;
    }

    half4 InkGlassFrag(Varyings input, bool frontFace, half passAlpha)
    {
        float3 normalWS = normalize(input.normalWS) * (frontFace ? 1.0 : -1.0);
        float3 normalOS = normalize(input.normalOS) * (frontFace ? 1.0 : -1.0);
        float3 viewDirWS = normalize(input.viewDirWS);
        float3 viewDirOS = normalize(mul((float3x3)unity_WorldToObject, viewDirWS));

        // Ink clouds: sampled a little behind the surface along the view ray so they read as inside the glass.
        float3 p = (input.positionOS - viewDirOS * _InkDepth) * _InkTiling;
        float3 weights = pow(abs(normalOS), 4.0);
        weights /= max(weights.x + weights.y + weights.z, 1e-4);
        float2 drift = _Time.y * _InkFlow * float2(1.0, 0.6);
        half cloud = SampleCloud(p, weights, drift);
        half detail = SampleCloud(p * _InkDetailScale + 0.37, weights, -drift * 1.7);
        half value = lerp(cloud, max(cloud, detail), _InkDetailWeight);

        // Three-colour ramp: body -> wash -> ink, the pack's Color Ramping with white / grey / black.
        half threshold = 1.0 - _InkCoverage;
        half ink = smoothstep(threshold - _InkSoftness * 0.5, threshold + _InkSoftness * 0.5, value);
        half wash = smoothstep(threshold - _InkSoftness * 0.5 - _InkMidWidth, threshold - _InkSoftness * 0.5, value);

        // Light: wrapped diffuse for the form, tight highlight for the gloss.
        Light mainLight = GetMainLight();
        half ndl = saturate(dot(normalWS, mainLight.direction) * 0.5 + 0.5);
        half shade = lerp(1.0, ndl, _Shading);
        half3 ambient = SampleSH(normalWS);
        half3 body = _BodyColor.rgb * lerp(1.0, saturate(FxLuminance(mainLight.color) * shade + FxLuminance(ambient) * 0.3), _Shading);
        half3 halfDir = normalize(mainLight.direction + viewDirWS);
        half spec = pow(saturate(dot(normalWS, halfDir)), _Gloss);

        // Dried edge: a darker ring where a blot meets the wash, like ink drying from the rim inward.
        half ring = smoothstep(threshold - _InkSoftness, threshold, value) * (1.0 - smoothstep(threshold, threshold + _InkSoftness * 1.5, value));
        ring *= _EdgeDarken;

        half3 color = lerp(body, _MidColor.rgb, wash);
        color = lerp(color, _InkColor.rgb, max(ink, ring));

        // Edges: ink collects at the silhouette and the glass gets more opaque there. Seen through the front, the
        // back faces' silhouette is kept fainter so it reads as the far side, not a crack.
        half rimScale = frontFace ? 1.0 : 0.15;
        half fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);
        color = lerp(color, _InkColor.rgb, fresnel * _RimStrength * rimScale);
        half alpha = _BodyColor.a;
        alpha = lerp(alpha, _WashOpacity, wash);
        alpha = lerp(alpha, _InkOpacity, max(ink, ring));
        alpha = saturate(alpha + fresnel * _RimAlpha * rimScale);

        half3 highlight = _SpecColor.rgb * mainLight.color * spec * (frontFace ? 1.0 : 0.35);
        color += highlight;
        alpha = saturate(alpha + FxLuminance(highlight) * 0.5);

        // Driven dissolve: eaten from the cloud with an ink edge.
        float2 dissolve = FxDissolve(value, _FxDissolve, _DissolveEdge);
        color = lerp(color, _InkColor.rgb, dissolve.y);
        alpha *= dissolve.x * _FxAlpha * passAlpha;

        return half4(MixFog(color, input.fogFactor), alpha);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        // Back faces first (SRPDefaultUnlit draws before UniversalForward for the same renderer).
        Pass
        {
            Name "InkGlassBack"
            Cull Front
            HLSLPROGRAM
            #pragma vertex InkGlassVert
            #pragma fragment FragBack
            #pragma multi_compile_fog
            half4 FragBack(Varyings input) : SV_Target { return InkGlassFrag(input, false, _BackAlpha); }
            ENDHLSL
        }

        Pass
        {
            Name "InkGlassFront"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex InkGlassVert
            #pragma fragment FragFront
            #pragma multi_compile_fog
            half4 FragFront(Varyings input) : SV_Target { return InkGlassFrag(input, true, 1.0); }
            ENDHLSL
        }
    }
    Fallback Off
}
