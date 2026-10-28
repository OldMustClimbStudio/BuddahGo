// Hover outline for interactive menu-environment objects: an inverted hull pushed out a fixed number of
// screen pixels, drawn additively so it glows (and blooms) around the object's silhouette only.
Shader "BuddahGo/HoverOutline"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (2.4, 1.7, 0.8, 1)
        _Width ("Width (pixels)", Float) = 3
        _Radial ("Radial extrusion (hard-edged meshes)", Range(0, 1)) = 0
        _CenterOS ("Centre (object space)", Vector) = (0, 0, 0, 0)
        _Fade ("Fade", Range(0, 1)) = 1
        _Pulse ("Pulse", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "HoverOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Width;
                float _Radial;
                float4 _CenterOS;
                float _Fade;
                float _Pulse;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 radial = input.positionOS.xyz - _CenterOS.xyz;
                float3 dir = normalize(lerp(input.normalOS, radial / max(length(radial), 1e-5), _Radial));
                float4 cs = TransformObjectToHClip(input.positionOS.xyz);
                float3 dirCS = mul((float3x3)UNITY_MATRIX_VP, TransformObjectToWorldDir(dir));
                float2 screenDir = dirCS.xy / max(length(dirCS.xy), 1e-5);
                cs.xy += screenDir * (_Width * 2.0 / _ScreenParams.xy) * cs.w;
                o.positionCS = cs;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half pulse = 1.0 - _Pulse * 0.5 * (1.0 + sin(_Time.y * 3.0));
                return half4(_Color.rgb * pulse, saturate(_Color.a * _Fade));
            }
            ENDHLSL
        }
    }
}
