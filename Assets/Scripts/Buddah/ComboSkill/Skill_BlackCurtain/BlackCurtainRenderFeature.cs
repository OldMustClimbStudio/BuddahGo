using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Draws the 一念光明 curtain (FX/CurtainEye) for a camera while its BlackCurtainScreenEffect plays. It runs after the
// renderer's own opaque and transparent passes, so it covers the whole map, transparent scenery included (the
// landscape's contact-shadow decals used to show through when it ran before the transparent pass). What a player may
// still see is drawn after it by the gameplay passes that follow it at the same event (RenderingModules.GameplayFeatures,
// in that order): racers (RenderBuddah), the track edges (RenderMapEdge, layer TrackEdge, which the view controller
// shows only to whoever should see them), the route trails (RenderTrails, layer BlackCurtainTrail) and skill effects
// (RenderSkillVfx). Those layers are kept out of the renderer's transparent pass so they draw once, after the curtain.
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
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
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
