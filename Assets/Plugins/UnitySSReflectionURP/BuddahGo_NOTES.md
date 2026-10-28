# UnitySSReflectionURP in BuddahGo

- Upstream: https://github.com/jiaozi158/UnitySSReflectionURP, commit `555d946` (2023-09-07). MIT, see `LICENSE`.
- Copied: `Assets/ScreenSpaceReflection/` (renderer feature, volume component, shaders, post material), `LICENSE`,
  `README.md`. The upstream sample scene, documentation images and project settings were not copied.
  `ScreenSpaceReflection.mat` is kept byte-identical to upstream (the feature now works on a runtime copy).
- Local patches, each marked `[BuddahGo]` in `ScreenSpaceReflectionURP.cs`:
  - `ForwardGBufferPass.FrameCleanup`: null checks before releasing the forward GBuffer targets. Without them the pass
    throws when the renderer's own normals texture is reused, and URP's frame is left half-finished.
  - `Create` / `Dispose`: the passes get a runtime copy of the post material. They write per-frame values
    (`_DownSample`, step sizes, keywords) into it, which otherwise rewrote the shared `.mat` asset on disk.
  - `AddRenderPasses`: skip reflection-probe captures and preview cameras (SSR is view-dependent and would be baked
    into the probes) and OpenGL, where the forward GBuffer pass clears the camera depth and refills it only from objects
    that have a GBuffer pass.
- Build requirement (from the upstream documentation): in the Forward path the feature redraws opaque objects with URP
  Lit's `GBuffer` pass, and URP strips that pass from every build unless a pipeline asset lists a Deferred renderer.
- Used by: nothing in the project since 2026-10-28, only the local rendering showcase (not in the repository). RaceMap
  used it from phase 2 until planar reflections replaced it; on the user's decision (PR #64 review M6) the feature, the
  RaceMap volume override and `URP-KeepDeferredVariants-Renderer` were removed, which took 1207 shader variants out of
  each build (every GBuffer pass, StencilDeferred and this shader). The scripts stay, compiled into
  `Assembly-CSharp-firstpass`; nothing in a build references the shader or the material.

## Adding SSR back

1. Create a Universal Renderer asset with Rendering Path Deferred and no features, and append it to the renderer list
   of every pipeline asset whose renderer will run SSR. No camera may select it; it only keeps the GBuffer passes in
   builds (about 1200 variants at the time of removal). Without it SSR shows in the editor but is empty in a build.
2. Add the `ScreenSpaceReflectionURP` feature to those renderers with this folder's `ScreenSpaceReflection.mat`, before
   `BlackCurtain` in the High Fidelity renderer (the curtain must darken the reflections). Phase 2 used 75% resolution
   without colour mipmaps on High Fidelity and 50% with Trilinear mipmaps on Balanced.
3. Add a `Screen Space Reflection` override to the scene's post profile with State Enabled; phase 2's values were
   Approximation, quality High, constant thickness 0.75, minimum smoothness 0.4, smoothness fade 0.6, edge fade 0.1.
4. Turn off `PlanarReflection` on the same surfaces, or the two reflections add up.
5. The graphics settings' `reflections` option only drives planar reflections now; an SSR option needs level columns
   and mapping again (the SSR mapping before ceea858 in `GraphicsPipelineMapper`).
Setup and tuning notes: `Docs/rendering/racemap/README.md`, phase 2.
