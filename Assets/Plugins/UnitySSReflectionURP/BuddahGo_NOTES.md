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
  `URP-HighFidelity` and `URP-Balanced` therefore list `Assets/Settings/URP-KeepDeferredVariants-Renderer.asset`
  (Deferred, no features) after their default renderer. No camera selects it. Removing it silently disables SSR in
  builds while the editor still shows it.
- Used by: `URP-HighFidelity-Renderer` (75%) and `URP-Balanced-Renderer` (50%), the RaceMap post profile (volume
  component), and the local rendering showcase (not in the repository). Setup and tuning notes:
  `Docs/rendering/racemap/README.md`.
