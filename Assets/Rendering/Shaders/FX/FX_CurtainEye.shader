// FX/CurtainEye: the 一念光明 black curtain, H1 墨眼 (Docs/vfx/blackcurtain-opening-research.md section 7).
// Full-screen pass drawn by BlackCurtainRenderFeature: the black hole bends the scene (lensing, twirl, radial smear),
// opens an ink event horizon ringed by gold light inside an ink shell, then the horizon swallows the screen; the hold
// and the fade-out are the same pass at _Black. Every parameter below is written per frame by BlackCurtainScreenEffect.
Shader "FX/CurtainEye"
{
    Properties
    {
        _InkColor ("Ink", Color) = (0.067, 0.102, 0.098, 1)
        _GoldColor ("Gold (light inside the ink)", Color) = (1, 0.737, 0.42, 1)
        _GoldIntensity ("Gold intensity", Float) = 1.6
        _FlashCap ("Peak gold (caps the collapse flash)", Float) = 1.6
        _HighlightColor ("Wet ink highlight", Color) = (0.85, 0.85, 0.8, 1)
        _AntiMode ("Anti palette (red, not light)", Float) = 0
        [Header(Anti palette (Docs vfx anti palette md section 5))]
        _AntiInkColor ("Anti ink", Color) = (0, 0, 0, 1)
        _AntiMistColor ("Red mist (brightest gold)", Color) = (1, 0.063, 0.063, 1)
        _AntiSmokeColor ("Blood red smoke (gold glow)", Color) = (0.584, 0, 0, 1)
        _AntiDarkColor ("Dark blood red (mid gold)", Color) = (0.435, 0, 0, 1)
        [Header(Flying specks in the black (same math as the retired SG_BlackCurtain FlowOffset))]
        _SpeckColor ("Speck colour", Color) = (0.349, 0.349, 0.349, 1)
        _FlowScale ("Flow scale", Float) = 9.4
        _FlowSpeed ("Flow speed (x, y)", Vector) = (10, -10, 0, 0)
        _FlowNoiseScale ("Gradient noise scale", Float) = 10
        _SpeckStrength ("Speck strength", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "CurtainEye"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _InkColor, _GoldColor, _HighlightColor, _SpeckColor, _FlowSpeed;
            float4 _AntiInkColor, _AntiMistColor, _AntiSmokeColor, _AntiDarkColor;
            float _GoldIntensity, _FlashCap, _AntiMode, _FlowScale, _FlowNoiseScale, _SpeckStrength;

            float4 _EyeCenter;      // xy: caster in viewport space
            float _EyeAspect;       // width / height
            float _EyeTime;         // seconds, animates the noise
            float _HoleRadius;      // event horizon, in screen heights
            float _Lens;            // lensing strength
            float _Twirl;           // radians at the hole
            float _RadialBlur;      // smear along the line to the hole
            float _Bulge;           // anticipation: the frame bulges out before the pull
            float _Ring;            // photon ring brightness
            float _RingPhase;       // the lopsided ring turns
            float _Spiral;          // ink shell: spiral arms outside the ring
            float _Dispersion;      // two-tone ghost (gold / ink), never RGB fringes
            float _Edge;            // how ragged the horizon is (wet ink, not a circle)
            float _CoreFlash;       // collapse flash at the caster
            float _Black;           // hold and fade-out: 1 = full curtain
            float _MaxOpacity;      // table maxOpacity: how much of the curtain covers the scene

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash(i), b = Hash(i + float2(1, 0)), c = Hash(i + float2(0, 1)), d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                return ValueNoise(p) * 0.5 + ValueNoise(p * 2.03 + 17.1) * 0.3 + ValueNoise(p * 4.1 + 5.3) * 0.2;
            }

            // Shader Graph's Gradient Noise node (Unity_GradientNoise_float), so the specks match the retired curtain.
            float2 GradientNoiseDir(float2 p)
            {
                p = p % 289.0;
                float x = (34.0 * p.x + 1.0) * p.x % 289.0 + p.y;
                x = (34.0 * x + 1.0) * x % 289.0;
                x = frac(x / 41.0) * 2.0 - 1.0;
                return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
            }

            float GradientNoise(float2 p)
            {
                float2 ip = floor(p);
                float2 fp = frac(p);
                float d00 = dot(GradientNoiseDir(ip), fp);
                float d01 = dot(GradientNoiseDir(ip + float2(0, 1)), fp - float2(0, 1));
                float d10 = dot(GradientNoiseDir(ip + float2(1, 0)), fp - float2(1, 0));
                float d11 = dot(GradientNoiseDir(ip + float2(1, 1)), fp - float2(1, 1));
                fp = fp * fp * fp * (fp * (fp * 6.0 - 15.0) + 10.0);
                return lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x) + 0.5;
            }

            float3 Scene(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv)).rgb;
            }

            // Off-screen samples fade into ink: the void the hole has emptied, never stretched edge pixels and never a
            // hard line where the screen ends.
            float3 SampleScene(float2 uv, float3 inkColor)
            {
                float2 outside = max(-uv, uv - 1.0);
                float beyond = max(max(outside.x, outside.y), 0.0);
                return lerp(Scene(uv), inkColor, smoothstep(0.0, 0.08, beyond));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 aspect = float2(_EyeAspect, 1.0);
                bool anti = _AntiMode > 0.5;
                float3 inkColor = anti ? _AntiInkColor.rgb : _InkColor.rgb;

                float2 d = (uv - _EyeCenter.xy) * aspect;
                float r = length(d) + 1e-5;
                float th = atan2(d.y, d.x);
                float2 cs = float2(cos(th), sin(th));
                float rs = max(_HoleRadius, 0.0);

                float ink = 0.0;
                float light = 0.0;      // ring core and collapse: the brightest gold
                float ghost = 0.0;      // two-tone ghost: mid gold
                float gloss = 0.0;
                float glow = 0.0;       // the wider halo around the ring and the flash

                // Event horizon: a drop of wet ink. The rim swells slowly (low-frequency noise drifting in time), its
                // feather widens as the hole grows, and a bleed halo darkens outward from it like ink soaking into paper.
                if (rs > 0.0)
                {
                    float wobble = Fbm(cs * 1.2 + float2(_EyeTime * 0.35, -_EyeTime * 0.25)) - 0.5;
                    float rn = rs * (1.0 + wobble * 0.32 * _Edge);
                    float feather = 0.012 + 0.10 * rs;
                    ink = max(ink, smoothstep(rn + feather, rn - feather * 0.6, r));
                    float bleed = max(r - rn, 0.0) / (feather * 2.5);
                    ink = max(ink, 0.45 * exp(-bleed * bleed));
                }

                // Where this pixel looks: bulge, twirl, then lensing. r * exp(-a / r^2) bends like Rs^2 / r far from the
                // hole and folds the image back smoothly near it, without the seam a hard mirror leaves.
                float rsrc = r * (1.0 - _Bulge * exp(-r * 2.5));
                float ths = th + _Twirl * exp(-r * 2.2);
                if (_Lens > 0.0 && rs > 0.0)
                    rsrc *= exp(-_Lens * rs * rs * 2.2 / (r * r + 1e-4));
                float2 dir = float2(cos(ths), sin(ths)) / aspect;

                // Radial smear toward the hole (GPU Gems 3 ch. 13 structure), 8 taps, dithered per pixel so the taps
                // read as motion instead of stepped copies.
                float jitter = frac(52.9829189 * frac(dot(input.positionCS.xy, float2(0.06711056, 0.00583715))));
                float3 scene = 0.0;
                [unroll]
                for (int j = 0; j < 8; j++)
                {
                    float f = 1.0 + (j + jitter) * 0.016 * _RadialBlur;
                    scene += SampleScene(_EyeCenter.xy + dir * rsrc * f, inkColor);
                }
                scene *= 0.125;

                if (rs > 0.0)
                {
                    // Light is being swallowed near the hole.
                    scene *= 0.3 + 0.7 * smoothstep(rs, rs * 2.8, r);

                    // Two-tone dispersion: an outer ghost carried by gold (dark red on anti), no RGB fringes.
                    if (_Dispersion > 0.0)
                    {
                        float lum = dot(SampleScene(_EyeCenter.xy + dir * rsrc * 1.07, inkColor), float3(0.3, 0.59, 0.11));
                        ghost += lum * 0.35 * _Dispersion * (1.0 - smoothstep(rs * 1.5, rs * 6.0, r));
                    }

                    // Ink shell: spiral arms outside the ring, with a wet highlight along each arm.
                    if (_Spiral > 0.0)
                    {
                        float a = th * 2.0 + log(r / rs) * 5.0 - _EyeTime * 4.0;
                        float arm = 0.5 + 0.5 * sin(a + 1.5 * Fbm(cs * 3.0));
                        float band = smoothstep(rs * 1.2, rs * 1.8, r) * (1.0 - smoothstep(rs * 2.0, rs * 4.0, r));
                        ink = max(ink, band * smoothstep(0.45, 0.95, arm) * 0.8 * _Spiral);
                        float hl = (arm - 0.6) / 0.05;
                        gloss += band * exp(-hl * hl) * 0.12 * _Spiral;
                    }

                    // Photon ring: light inside the ink shell (ink -> gold -> black core), lopsided and turning.
                    if (_Ring > 0.0)
                    {
                        float b = (r - rs * 1.17) / (rs * 0.13 + 0.003);
                        float asym = 0.5 + 0.5 * cos(th - _RingPhase);
                        light += exp(-b * b) * _Ring * (0.3 + 0.7 * asym);
                        glow = max(glow, exp(-b * b * 0.25) * 0.4 * saturate(_Ring));
                    }
                }

                // Collapse flash on the caster.
                if (_CoreFlash > 0.0)
                {
                    float dd = length((uv - _EyeCenter.xy) * aspect) / 0.05;
                    light += _CoreFlash * 1.7 * exp(-dd * dd);
                    glow = max(glow, _CoreFlash * 0.55 * exp(-dd * dd * 0.3));
                }

                float3 col = scene;
                if (anti)
                {
                    // Anti (Docs/vfx/anti-palette.md): the gold is translated into the reaction reds by depth, all
                    // alpha-blended, so the red stains instead of glowing: the ghost (mid gold) -> dark blood red, the
                    // halo around the ring and the flash (gold glow) -> blood red smoke, the ring core and the collapse
                    // (the brightest gold) -> red mist at alpha <= 0.55; ink is pure black, white only in the small
                    // wet highlights.
                    col = lerp(col, inkColor, ink);
                    col = lerp(col, _AntiDarkColor.rgb, min(0.6, ghost * 2.0));
                    col = lerp(col, _AntiSmokeColor.rgb, min(0.8, glow * 1.5));
                    col = lerp(col, _AntiMistColor.rgb, min(0.55, light * 0.6));
                    col += gloss * 0.6 * _HighlightColor.rgb;
                }
                else
                {
                    col = lerp(col, inkColor, ink);
                    col += _GoldColor.rgb * min((light + ghost) * _GoldIntensity, _FlashCap);
                    col += gloss * _HighlightColor.rgb;
                }

                col = lerp(col, inkColor, saturate(_Black));

                // The debris flying in the black (on purpose in the retired SG_BlackCurtain): its FlowOffset group
                // shows _SpeckColor wherever its fast-moving gradient noise reaches 0. Same coordinates, same test,
                // wherever the screen is ink.
                float coverage = saturate(max(ink, _Black));
                float flow = GradientNoise((uv * _FlowScale + _Time.y * _FlowSpeed.xy) * _FlowNoiseScale);
                float speck = flow <= 0.0 ? 1.0 : 0.0;
                col = lerp(col, _SpeckColor.rgb, speck * coverage * _SpeckStrength);

                col = lerp(Scene(uv), col, saturate(_MaxOpacity));
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
