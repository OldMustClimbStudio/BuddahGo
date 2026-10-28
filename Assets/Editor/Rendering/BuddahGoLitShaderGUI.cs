// Material inspector for BuddahGo/Lit: Unity's default property drawing plus two things the default inspector cannot
// do. Switching a URP Lit material to BuddahGo/Lit carries its keywords over (URP Lit derives _NORMALMAP and the other
// map keywords in its own inspector, so they have no property that would carry them), and the alpha-clip toggle keeps
// the render queue and RenderType tag in step, as URP's inspector does.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuddahGo.Rendering.Editor
{
    public class BuddahGoLitShaderGUI : ShaderGUI
    {
        // Keyword -> BuddahGo/Lit toggle property. "Off" toggles store 1 when the keyword is absent.
        static readonly string[,] OnToggles =
        {
            { "_NORMALMAP", "_NormalMapOn" },
            { "_PARALLAXMAP", "_ParallaxMapOn" },
            { "_METALLICSPECGLOSSMAP", "_SpecGlossMapOn" },
            { "_OCCLUSIONMAP", "_OcclusionMapOn" },
            { "_EMISSION", "_EmissionOn" },
            { "_ALPHATEST_ON", "_AlphaClip" },
            { "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", "_SmoothnessTextureChannel" },
        };
        static readonly string[,] OffToggles =
        {
            { "_SPECULAR_SETUP", "_WorkflowMode" },
            { "_SPECULARHIGHLIGHTS_OFF", "_SpecularHighlights" },
            { "_ENVIRONMENTREFLECTIONS_OFF", "_EnvironmentReflections" },
            { "_RECEIVE_SHADOWS_OFF", "_ReceiveShadows" },
        };

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            EditorGUI.BeginChangeCheck();
            base.OnGUI(materialEditor, properties);
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var target in materialEditor.targets)
                    UpdateAlphaClipState((Material)target);
            }
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            // The previous shader's keywords are still on the material here; the switch would drop the ones that have
            // no toggle property behind them.
            var previousKeywords = new HashSet<string>(material.shaderKeywords);
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            if (oldShader != null && oldShader != newShader)
                CarryOverKeywords(material, previousKeywords);
        }

        /// <summary>
        /// Sets BuddahGo/Lit's toggle properties from a keyword set (for example a URP Lit material's) and applies the
        /// drawers, so the material keeps its maps, workflow and options. Usable from migration scripts.
        /// </summary>
        public static void CarryOverKeywords(Material material, ICollection<string> keywords)
        {
            for (int i = 0; i < OnToggles.GetLength(0); i++)
                SetIfPresent(material, OnToggles[i, 1], keywords.Contains(OnToggles[i, 0]) ? 1f : 0f);
            for (int i = 0; i < OffToggles.GetLength(0); i++)
                SetIfPresent(material, OffToggles[i, 1], keywords.Contains(OffToggles[i, 0]) ? 0f : 1f);
            MaterialEditor.ApplyMaterialPropertyDrawers(material);
            UpdateAlphaClipState(material);
        }

        /// <summary>Alpha-clipped materials render in the AlphaTest queue with the TransparentCutout tag, as in URP Lit.</summary>
        public static void UpdateAlphaClipState(Material material)
        {
            bool clip = material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > 0.5f;
            material.SetOverrideTag("RenderType", clip ? "TransparentCutout" : string.Empty);
            material.renderQueue = clip ? (int)RenderQueue.AlphaTest : -1;
        }

        static void SetIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }
    }
}
