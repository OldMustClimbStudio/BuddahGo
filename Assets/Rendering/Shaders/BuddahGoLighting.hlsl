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
//   * _ShadowedReflections: darkens the environment reflection inside the main light's realtime shadow. Not physical
//     (the sky is still visible from a shadowed floor), but it lets a cast shadow read on a polished floor, whose
//     brightness is mostly reflected sky. It is a gameplay-readability control for the RaceMap lane.
// Both occlusion terms take the combined material AO x screen-space AO, the value stock Lit applies flat.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Lagarde specular occlusion: the exponent tightens the occlusion cone as roughness falls.
half BuddahGoSpecularOcclusion(half NoV, half ambientOcclusion, half perceptualRoughness)
{
    half roughness = perceptualRoughness * perceptualRoughness;
    return saturate(pow(NoV + ambientOcclusion, exp2(-16.0h * roughness - 1.0h)) - 1.0h + ambientOcclusion);
}

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
    half specularOcclusion = lerp(occlusion, BuddahGoSpecularOcclusion(NoV, occlusion, brdfData.perceptualRoughness), _SpecularOcclusion);
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

    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

    LightingData lightingData = CreateLightingData(inputData, surfaceData);
    lightingData.giColor = BuddahGoGlobalIllumination(brdfData, inputData.bakedGI, aoFactor.indirectAmbientOcclusion,
                                                      inputData.positionWS, inputData.normalWS, vertexNormalWS,
                                                      inputData.viewDirectionWS, inputData.normalizedScreenSpaceUV,
                                                      mainLight.shadowAttenuation);

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
