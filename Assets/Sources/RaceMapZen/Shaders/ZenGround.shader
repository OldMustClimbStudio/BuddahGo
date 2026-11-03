// RaceMap zen ground: raked karesansui sand in three large colour environments, ported from the Blender material Zen_R_Gravel.
// _Field (float, 1 m per texel): R = distance used for the rake rings (f_R), G = distance to the track edge,
// B = black-sand weight, A = yellow-sand weight; white = 1 - B - A (round 24: 白川砂 / 黑砂 / 黄土砂 with soft 30-40 m blends).
// Within 5.0..5.6 m of the track edge the ground fades into a dark pebble band.
// Texture space is the Blender ground space in metres: X = -worldX - 153, Y = -worldZ - 293 (see RaceMapZen notes).
Shader "BuddahGo/ZenGround"
{
    Properties
    {
        _Field ("Field (R f_R, G dtrack, B garden)", 2D) = "black" {}
        _FieldParams ("Field origin X, Y (m), 1/width, 1/height (m)", Vector) = (0, 0, 0.001, 0.001)
        _SandMap ("Sand base colour", 2D) = "white" {}
        _SandRough ("Sand roughness", 2D) = "white" {}
        _SandHeight ("Sand height", 2D) = "black" {}
        _SandTiling ("Sand tiling (per metre)", Float) = 0.25
        _GrooveFreq ("Rake groove frequency (rad per metre)", Float) = 4.833
        _GrooveNormal ("Rake groove normal strength", Float) = 0.35
        _SandNormal ("Sand grain normal strength", Float) = 0.15
        _RustTint ("Dry-waterfall sand tint", Color) = (0.95, 0.82, 0.64, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_Field); SAMPLER(sampler_Field);
            TEXTURE2D(_SandMap); SAMPLER(sampler_SandMap);
            TEXTURE2D(_SandRough); SAMPLER(sampler_SandRough);
            TEXTURE2D(_SandHeight); SAMPLER(sampler_SandHeight);
            CBUFFER_START(UnityPerMaterial)
                float4 _FieldParams;
                float4 _Field_ST, _SandMap_ST, _SandRough_ST, _SandHeight_ST;
                float _SandTiling, _GrooveFreq, _GrooveNormal, _SandNormal;
                half4 _RustTint;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float fogFactor : TEXCOORD1; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // --- value-gradient noise, close enough to Blender's Noise Texture "Fac" (0..1, centred on 0.5)
            float2 hash2 (float2 p) { p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3))); return -1.0 + 2.0 * frac(sin(p) * 43758.5453); }
            float gnoise (float2 p)
            {
                float2 i = floor(p), f = frac(p); float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(dot(hash2(i), f), dot(hash2(i + float2(1, 0)), f - float2(1, 0)), u.x),
                            lerp(dot(hash2(i + float2(0, 1)), f - float2(0, 1)), dot(hash2(i + float2(1, 1)), f - float2(1, 1)), u.x), u.y);
            }
            float fbm (float2 p, int octaves)
            {
                float a = 0.5, s = 0.0, n = 0.0;
                for (int k = 0; k < octaves; k++) { s += a * gnoise(p); n += a; p *= 2.0; a *= 0.5; }
                return saturate(0.5 + 0.75 * s / n);
            }
            float voronoiF1 (float2 p)
            {
                float2 i = floor(p), f = frac(p); float d = 8.0;
                for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
                {
                    float2 g = float2(x, y); float2 o = 0.5 + 0.5 * hash2(i + g);
                    d = min(d, length(g + o - f));
                }
                return d;
            }

            float3 field (float2 bm)
            {
                float2 uv = (bm - _FieldParams.xy) * _FieldParams.zw + 0.5 * _FieldParams.zw;
                if (any(uv < 0.0) || any(uv > 1.0)) return float3(1e4, 1e4, 0);      // outside: plain sand, no rake
                return SAMPLE_TEXTURE2D_LOD(_Field, sampler_Field, uv, 0).rgb;
            }
            // rake ring distance with the uv clamped to the field, for the finite differences at the field border
            float fieldR (float2 bm)
            {
                float2 uv = (bm - _FieldParams.xy) * _FieldParams.zw + 0.5 * _FieldParams.zw;
                return SAMPLE_TEXTURE2D_LOD(_Field, sampler_Field, saturate(uv), 0).r;
            }
            // sand colour weights (B black, A yellow; white is the rest), bilinear so the colours blend softly
            float2 sandWeights (float2 bm)
            {
                float2 uv = (bm - _FieldParams.xy) * _FieldParams.zw + 0.5 * _FieldParams.zw;
                if (any(uv < 0.0) || any(uv > 1.0)) return float2(0, 0);
                return saturate(SAMPLE_TEXTURE2D_LOD(_Field, sampler_Field, uv, 0).ba);
            }
            float groove (float fR) { return pow(saturate(0.5 * cos(fR * _GrooveFreq) + 0.5), 1.8); }

            half4 frag (Varyings i) : SV_Target
            {
                float2 bm = float2(-i.positionWS.x - 153.0, -i.positionWS.z - 293.0);   // Blender ground metres
                float3 F = field(bm);
                float2 sw = sandWeights(bm);
                float moss = 0.0;                       // round 17: no lawn or moss ground any more
                float rake = (F.r < 5e3) ? 1.0 : 0.0;
                float g = rake > 0 ? groove(F.r) : 0.6;
                float2 suv = bm * _SandTiling;
                half3 sand = SAMPLE_TEXTURE2D(_SandMap, sampler_SandMap, suv).rgb;
                half rough = SAMPLE_TEXTURE2D(_SandRough, sampler_SandRough, suv).r;
                float tone = lerp(0.93, 1.03, fbm(bm * 0.062, 2));
                // three sand colours blended by weight (Blender Zen_R_Gravel zen_sand3_* tints, same values)
                float wWhite = saturate(1.0 - sw.x - sw.y);
                half3 tint = wWhite * half3(1, 1, 1) + sw.x * half3(0.20, 0.20, 0.22) + sw.y * half3(0.96, 0.82, 0.58);
                half3 col = sand * lerp(0.8, 1.0, g) * tone * tint;
                // dark pebble band along the track
                half3 peb = lerp(half3(0.01, 0.01, 0.012), half3(0.09, 0.09, 0.1), saturate(voronoiF1(bm * 5.0) / 0.12));
                float band = saturate((F.g - 5.0) / 0.6);
                col = lerp(peb, col, band);

                // normal: rake grooves (finite differences of the groove height) + sand grain height
                float3 n = float3(0, 1, 0);
                if (rake > 0 && moss < 0.5)
                {
                    const float e = 0.15;
                    float rxp = fieldR(bm + float2(e, 0)), rxn = fieldR(bm - float2(e, 0));
                    float ryp = fieldR(bm + float2(0, e)), ryn = fieldR(bm - float2(0, e));
                    float rakeAll = step(max(max(rxp, rxn), max(ryp, ryn)), 5e3);   // no groove slope next to unraked texels
                    float gx = (groove(rxp) - groove(rxn)) * rakeAll;
                    float gy = (groove(ryp) - groove(ryn)) * rakeAll;
                    float hx = SAMPLE_TEXTURE2D(_SandHeight, sampler_SandHeight, (bm + float2(e, 0)) * _SandTiling).r - SAMPLE_TEXTURE2D(_SandHeight, sampler_SandHeight, (bm - float2(e, 0)) * _SandTiling).r;
                    float hy = SAMPLE_TEXTURE2D(_SandHeight, sampler_SandHeight, (bm + float2(0, e)) * _SandTiling).r - SAMPLE_TEXTURE2D(_SandHeight, sampler_SandHeight, (bm - float2(0, e)) * _SandTiling).r;
                    float dX = (gx * _GrooveNormal + hx * _SandNormal) / (2 * e);
                    float dY = (gy * _GrooveNormal + hy * _SandNormal) / (2 * e) ;
                    // Blender (X, Y) = (-world x, -world z): d/dworldx = -d/dX, d/dworldz = -d/dY
                    n = normalize(float3(dX, 1.0, dY));
                }

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = i.fogFactor;
                inputData.bakedGI = SampleSH(n);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = col; s.metallic = 0; s.specular = 0; s.smoothness = 1.0 - rough;
                s.occlusion = 1; s.alpha = 1; s.normalTS = half3(0, 0, 1);
                half4 c = UniversalFragmentPBR(inputData, s);
                c.rgb = MixFog(c.rgb, i.fogFactor);
                return c;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
