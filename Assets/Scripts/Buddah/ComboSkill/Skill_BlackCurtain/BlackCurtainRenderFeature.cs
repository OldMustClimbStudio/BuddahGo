using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Draws the 一念光明 curtain (FX/CurtainEye) for a camera while its BlackCurtainScreenEffect plays. It takes the
// curtain's slot, BeforeRenderingTransparents, and feature passes sort ahead of the renderer's own passes at the same
// event, so everything the curtain lets through still draws over it: the glowing edge ribbons and BlackCurtainTrail
// (transparent pass), racers and skill effects (RenderBuddah / RenderSkillVfx). One slot later it covers the edges and
// the caster's route trail.
public class BlackCurtainRenderFeature : ScriptableRendererFeature
{
    private Pass _pass;

    public override void Create()
    {
        _pass = new Pass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!BlackCurtainScreenEffect.TryGetMaterial(renderingData.cameraData.camera, out Material material))
            return;
        _pass.material = material;
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
    }

    private sealed class Pass : ScriptableRenderPass
    {
        public Material material;
        private RTHandle _copy;

        public Pass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            profilingSampler = new ProfilingSampler("BlackCurtain");
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ResetTarget();
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.depthBufferBits = (int)DepthBits.None;
            RenderingUtils.ReAllocateIfNeeded(ref _copy, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_BlackCurtainCopy");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null)
                return;
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler))
            {
                RTHandle color = renderingData.cameraData.renderer.cameraColorTargetHandle;
                Blitter.BlitCameraTexture(cmd, color, _copy);
                Blitter.BlitCameraTexture(cmd, _copy, color, material, 0);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            _copy?.Release();
        }
    }
}
