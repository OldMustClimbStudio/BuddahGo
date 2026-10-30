// FX/Decal: the decal functional layer as a flat quad (Docs/vfx/README.md section 7: no URP Decal renderer feature):
// a hard-edged seal or mark, alpha cutout (_ClipAt 0.5, no feather) with a depth offset so a ground quad sits on the
// track without z-fighting; it reads no depth texture. Cutting, drawing and the gold front are in FXInk.hlsl. Draws the
// cinnabar seal in the air and the buff confirmation circle.
Shader "FX/Decal"
{
    Properties
    {
        _MainTex("Mask (A shape, R along the stroke)", 2D) = "white" {}
        _Color("Ink", Color) = (0.082, 0.082, 0.102, 1)
        _Opacity("Core opacity", Range(0, 1)) = 1
        _Cutoff("Cutoff", Range(0.05, 0.95)) = 0.5
        _Feather("Edge feather", Range(0, 0.5)) = 0
        _ClipAt("Discard below alpha", Range(0, 1)) = 0.5
        [HDR] _RimColor("Gold rim on the head side (alpha = amount)", Color) = (0, 0, 0, 0)
        _RimWidth("Rim width (in mask value)", Range(0.01, 0.5)) = 0.12
        [HDR] _EdgeColor("Drawing / erasing front (alpha = amount)", Color) = (0, 0, 0, 0)
        _EdgeWidth("Front width (fraction of the stroke)", Range(0.005, 0.2)) = 0.04
        [HDR] _EmberColor("Embers in the ink", Color) = (0, 0, 0, 0)
        _EmberTiling("Ember tiling", Float) = 3
        _EmberAmount("Ember amount", Range(0, 0.3)) = 0.06

        [Header(Erase)]
        [MaterialToggle] _NoiseErase("Erase by noise instead of from the root", Float) = 0
        [NoScaleOffset] _NoiseTex("Packed noise (G cells, B dissolve)", 2D) = "gray" {}
        _NoiseTiling("Noise tiling", Float) = 2

        [MaterialToggle] _SwapUV("Swap U and V (stretched particles)", Float) = 0
        [Toggle] _UseCustomData("Read particle Custom1 stream", Float) = 0

        [Header(Driven)]
        _FxAlpha("Erosion (driven, 1 = whole)", Range(0, 1)) = 1
        _FxProgress("Draw (driven)", Range(0, 1)) = 1
        _FxDissolve("Erase (driven)", Range(0, 1)) = 0
        _DepthOffset("Depth offset", Float) = -1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest+10"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "FXDecal"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Off
            Offset [_DepthOffset], [_DepthOffset]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex FxInkVert
            #pragma fragment FxInkFrag
            #pragma multi_compile_fog
            #include "FXInk.hlsl"
            ENDHLSL
        }
    }
}
