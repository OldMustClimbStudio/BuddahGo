// Art-directed sunset sky for URP 14: a zenith-to-horizon gradient whose horizon warms toward the sun's azimuth, a
// glow and halo around the sun, and a sun disk. Unlike Skybox/Procedural it keeps a cool zenith while the horizon
// glows, which gives baked ambient a cool fill against a warm low sun.
//
// The sun direction is _WorldSpaceLightPos0, which Unity sets from Lighting > Environment > Sun Source
// (RenderSettings.sun) for every skybox draw: camera rendering, reflection probe capture and the GI environment bake.
// The colours do not follow the sun's elevation; they are tuned for one time of day. Colour values are linear: the
// properties are [HDR], so Unity passes them to the shader unconverted (unlike RenderSettings.fogColor, which it
// linearises).
Shader "BuddahGo/Skybox/Sunset Sky"
{
    Properties
    {
        [HDR] _ZenithColor("Zenith", Color) = (0.16, 0.22, 0.45, 1)
        [HDR] _HorizonColor("Horizon away from the sun", Color) = (0.85, 0.55, 0.55, 1)
        [HDR] _HorizonSunColor("Horizon toward the sun", Color) = (1.6, 0.75, 0.35, 1)
        _HorizonExponent("Horizon band falloff", Range(0.5, 16)) = 4
        _SunSpread("Warm horizon spread (lower = wider)", Range(0.25, 16)) = 2
        [HDR] _GroundColor("Below the horizon", Color) = (0.30, 0.22, 0.18, 1)
        _GroundBlend("Below-horizon blend sharpness", Range(1, 64)) = 12

        [Header(Sun)]
        [HDR] _SunGlowColor("Glow", Color) = (1.5, 0.7, 0.3, 1)
        _SunGlowPower("Glow tightness", Range(1, 512)) = 24
        _SunHaloPower("Halo tightness", Range(0.5, 32)) = 4
        _SunHaloIntensity("Halo intensity", Range(0, 2)) = 0.4
        [HDR] _SunDiskColor("Disk (replaces the sky behind it)", Color) = (2.2, 0.95, 0.3, 1)
        _SunDiskSize("Disk radius (degrees)", Range(0.1, 5)) = 1.2
        _SunDiskSoftness("Disk edge softness (fraction of radius)", Range(0.01, 1)) = 0.15

        _Exposure("Exposure", Range(0, 8)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Background"
            "Queue" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "SunsetSky"
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex SunsetSkyVertex
            #pragma fragment SunsetSkyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _HorizonSunColor;
                half _HorizonExponent;
                half _SunSpread;
                half4 _GroundColor;
                half _GroundBlend;
                half4 _SunGlowColor;
                half _SunGlowPower;
                half _SunHaloPower;
                half _SunHaloIntensity;
                half4 _SunDiskColor;
                half _SunDiskSize;
                half _SunDiskSoftness;
                half _Exposure;
            CBUFFER_END

            // Built-in skybox constant: direction toward RenderSettings.sun. URP's ShaderLibrary does not declare it.
            float4 _WorldSpaceLightPos0;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 directionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings SunsetSkyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // The skybox mesh is centred on the camera, so its object-space position is the view direction.
                output.directionWS = mul((float3x3)GetObjectToWorldMatrix(), input.positionOS.xyz);
                return output;
            }

            // Full precision: in fp16 (mobile) the disk edge and the tight glow would band.
            float3 SunDirection()
            {
                float3 sun = _WorldSpaceLightPos0.xyz;
                // No sun source assigned: keep a low sun to the south instead of a NaN.
                return dot(sun, sun) > 1e-6 ? normalize(sun) : float3(0.0, 0.2, -0.98);
            }

            half4 SunsetSkyFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 view = normalize(input.directionWS);
                float3 sun = SunDirection();

                // Warmth of the horizon by azimuth: 1 toward the sun, 0 opposite.
                float2 viewFlat = view.xz;
                float2 sunFlat = sun.xz;
                float flatLength = sqrt(max(dot(viewFlat, viewFlat), 1e-8) * max(dot(sunFlat, sunFlat), 1e-8));
                half towardSun = saturate(dot(viewFlat, sunFlat) / flatLength * 0.5 + 0.5);
                half warm = pow(towardSun, _SunSpread);
                half3 horizon = lerp(_HorizonColor.rgb, _HorizonSunColor.rgb, warm);

                // Elevation gradient above the horizon, a short blend into the ground colour below it.
                half up = saturate(view.y);
                half horizonWeight = pow(1.0 - up, _HorizonExponent);
                half3 color = lerp(_ZenithColor.rgb, horizon, horizonWeight);
                half below = saturate(-view.y * _GroundBlend);
                color = lerp(color, _GroundColor.rgb, below);

                // Glow (tight) and halo (wide) around the sun.
                float cosAngle = dot(view, sun);
                float facing = saturate(cosAngle);
                color += _SunGlowColor.rgb * pow(facing, _SunGlowPower);
                color += _SunGlowColor.rgb * (_SunHaloIntensity * pow(facing, _SunHaloPower));

                // Sun disk with a soft edge, hidden below the horizon. It replaces the sky rather than adding to it, so the
                // authored colour survives instead of being pushed toward white by the glow underneath.
                float radius = radians(_SunDiskSize);
                float angle = acos(clamp(cosAngle, -1.0, 1.0));
                half disk = 1.0 - smoothstep(radius * (1.0 - _SunDiskSoftness), radius, angle);
                color = lerp(color, _SunDiskColor.rgb, disk * saturate(view.y * 50.0 + 0.5));

                return half4(color * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
