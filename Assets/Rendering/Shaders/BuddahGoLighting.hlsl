#ifndef BUDDAHGO_LIGHTING_INCLUDED
#define BUDDAHGO_LIGHTING_INCLUDED

// Lighting for BuddahGo/Lit. Direct lighting, mixed lighting, light layers, Forward+ and the final colour assembly
// mirror URP 14's UniversalFragmentPBR (ShaderLibrary/Lighting.hlsl, clear coat removed). Only the indirect term
// differs, and each difference has a material weight where 0 reproduces stock URP Lit:
//   * _MultiBounceAO: albedo-aware multi-bounce diffuse occlusion (Jimenez et al., "Practical Realtime Strategies for
//     Accurate Indirect Occlusion", 2016), so occluded creases keep some bounced colour instead of going grey;
//   * _SpecularOcclusion: specular occlusion after Lagarde & de Rousiers ("Moving Frostbite to PBR", 2014): rough
//     surfaces are occluded like diffuse, smooth ones only where the view grazes an occluded area;
//   * _HorizonOcclusion: fades reflection vectors that a normal map has pushed below the geometric surface;
//   * _ShadowedReflections: darkens the environment reflection inside the main light's shadow (its shadow
//     attenuation: realtime, plus baked shadowmask in Shadowmask or Subtractive scenes). Not physical (the sky is
//     still visible from a shadowed floor), but it lets a cast shadow read on a polished floor, whose brightness is
//     mostly reflected sky. It is a gameplay-readability control for the RaceMap lane.
//   * _PLANAR_REFLECTION: on a flat receiver of a PlanarReflection component, the environment reflection is the mirrored
//     camera's image of the scene above the plane, sampled in screen space and blurred by roughness through its mips.
//     Only pixels lying on that plane take it (up-facing vertex normal, height at the plane); walls keep the probe.
// Both occlusion terms take URP's indirect occlusion, min(material AO, SSAO), the value stock Lit applies flat.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#if defined(_PLANAR_REFLECTION)
// Per receiver, from PlanarReflection.cs through a property block: x on (0/1), y plane height, z blur mips per unit of
// perceptual roughness, w screen-space offset per unit of normal-map tilt.
TEXTURE2D(_PlanarReflectionTexture);
SAMPLER(sampler_PlanarReflectionTexture);
float4 _PlanarReflectionParams;

half3 PlanarReflectionOrProbe(half3 probe, BRDFData brdfData, float3 positionWS, half3 normalWS, half3 vertexNormalWS,
                              float2 normalizedScreenSpaceUV)
{
    half onPlane = saturate((vertexNormalWS.y - 0.95h) * 20.0h) * saturate(1.0h - abs(positionWS.y - _PlanarReflectionParams.y) * 10.0h);
    half weight = (half)_PlanarReflectionParams.x * onPlane;
    UNITY_BRANCH if (weight <= 0.0h)
        return probe;
    float2 uv = normalizedScreenSpaceUV + (normalWS.xz - vertexNormalWS.xz) * _PlanarReflectionParams.w;
    half lod = brdfData.perceptualRoughness * (half)_PlanarReflectionParams.z;
    half3 mirrored = SAMPLE_TEXTURE2D_LOD(_PlanarReflectionTexture, sampler_PlanarReflectionTexture, uv, lod).rgb;
    return lerp(probe, mirrored, weight);
}
#endif

half3 BuddahGoGlobalIllumination(BRDFData brdfData, half3 bakedGI, half occlusion, float3 positionWS,
                                 half3 normalWS, half3 vertexNormalWS, half3 viewDirectionWS,
                                 float2 normalizedScreenSpaceUV, half mainLightShadow)
{
    half3 reflectVector = reflect(-viewDirectionWS, normalWS);
    half NoV = saturate(dot(normalWS, viewDirectionWS));
    half fresnelTerm = Pow4(1.0h - NoV);

    // Indirect diffuse: lightmap or SH. Stock Lit multiplies by occlusion flat.
    half3 diffuseOcclusion = lerp(occlusion.xxx, GTAOMultiBounce(occlusion, brdfData.diffuse), _MultiBounceAO);
    half3 indirectDiffuse = bakedGI * brdfData.diffuse * diffuseOcclusion;

    // Indirect specular: reflection probes or the sky. Stock Lit multiplies by occlusion flat.
    half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, positionWS, brdfData.perceptualRoughness, 1.0h, normalizedScreenSpaceUV);
#if defined(_PLANAR_REFLECTION)
    indirectSpecular = PlanarReflectionOrProbe(indirectSpecular, brdfData, positionWS, normalWS, vertexNormalWS, normalizedScreenSpaceUV);
#endif
    // Lagarde specular occlusion from the core library (the exponent tightens the cone as roughness falls).
    half specularOcclusion = lerp(occlusion, GetSpecularOcclusionFromAmbientOcclusion(NoV, occlusion, brdfData.roughness), _SpecularOcclusion);
    half horizon = saturate(1.0h + dot(reflectVector, vertexNormalWS));
    specularOcclusion *= lerp(1.0h, horizon * horizon, _HorizonOcclusion);
    specularOcclusion *= lerp(1.0h, mainLightShadow, _ShadowedReflections);

    half3 color = indirectDiffuse + indirectSpecular * EnvironmentBRDFSpecular(brdfData, fresnelTerm) * specularOcclusion;

    if (IsOnlyAOLightingFeatureEnabled())
        color = occlusion.xxx; // Rendering Debugger "Ambient Occlusion" view, as in stock Lit.

    return color;
}

half4 BuddahGoFragmentPBR(InputData inputData, SurfaceData surfaceData, half3 vertexNormalWS)
{
#if defined(_SPECULARHIGHLIGHTS_OFF)
    bool specularHighlightsOff = true;
#else
    bool specularHighlightsOff = false;
#endif

    BRDFData brdfData;
    InitializeBRDFData(surfaceData, brdfData);

#if defined(DEBUG_DISPLAY)
    half4 debugColor;
    if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor))
        return debugColor;
#endif

    const BRDFData noClearCoat = (BRDFData)0;
    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
    uint meshRenderingLayers = GetMeshRenderingLayer();
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);

    // The shadow that darkens reflections only applies where the main light lights this mesh.
    half reflectionShadow = mainLight.shadowAttenuation;
#ifdef _LIGHT_LAYERS
    if (!IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
        reflectionShadow = 1.0h;
#endif

    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

    LightingData lightingData = CreateLightingData(inputData, surfaceData);
    lightingData.giColor = BuddahGoGlobalIllumination(brdfData, inputData.bakedGI, aoFactor.indirectAmbientOcclusion,
                                                      inputData.positionWS, inputData.normalWS, vertexNormalWS,
                                                      inputData.viewDirectionWS, inputData.normalizedScreenSpaceUV,
                                                      reflectionShadow);

#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        lightingData.mainLightColor = LightingPhysicallyBased(brdfData, noClearCoat, mainLight,
                                                              inputData.normalWS, inputData.viewDirectionWS,
                                                              0.0h, specularHighlightsOff);
    }

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_FORWARD_PLUS
    for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
    #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
    #endif
        {
            lightingData.additionalLightsColor += LightingPhysicallyBased(brdfData, noClearCoat, light,
                                                                          inputData.normalWS, inputData.viewDirectionWS,
                                                                          0.0h, specularHighlightsOff);
        }
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
    #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
    #endif
        {
            lightingData.additionalLightsColor += LightingPhysicallyBased(brdfData, noClearCoat, light,
                                                                          inputData.normalWS, inputData.viewDirectionWS,
                                                                          0.0h, specularHighlightsOff);
        }
    LIGHT_LOOP_END
#endif

#if defined(_ADDITIONAL_LIGHTS_VERTEX)
    lightingData.vertexLightingColor += inputData.vertexLighting * brdfData.diffuse;
#endif

#if REAL_IS_HALF
    return min(CalculateFinalColor(lightingData, surfaceData.alpha), HALF_MAX);
#else
    return CalculateFinalColor(lightingData, surfaceData.alpha);
#endif
}

#endif // BUDDAHGO_LIGHTING_INCLUDED
