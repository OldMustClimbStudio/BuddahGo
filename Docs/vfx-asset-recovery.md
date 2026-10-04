> **2026-10-04 复核：** 两个悬空 GUID 不渲染。材质 `b739a3f…` 只在四个 prefab（`沙砾`、`如来神掌`、`射击特效`、`slowtrap_vfxZone`）的根 ParticleSystem 上，这些根发射器 Emission 关闭且无 burst；贴图 `1c0a28d7…` 只在 `UberFXSG` 没有的 `_diff` 属性里。全部分支、60 个 PR ref、本机磁盘与 Asset Store 缓存均无原件。可见的洋红/消失问题由导入缓存引起，用下文的修复菜单处理。

# VFX asset recovery — 2026-10-01

Original acceleration assets are restored. The Unity follow-up below repaired invalid local shader/VFX import artifacts and verified visible original effects. No replacement art or shader was assigned. Full six-skill acceptance remains open.

## Unity follow-up — original effects restored (2026-10-01)

In the single-player feature Editor, Unity 2022.3.55f1c1, URP/ShaderGraph/VFX Graph 14.0.11, Direct3D11, High Fidelity:

- All 19 affected Piloto materials resolved their original shader GUID/fileID, but the imported shader was `Hidden/GraphErrorShader2`. It reported `isSupported=true`, one pass, `ShaderHasError=false`, and no `GetShaderMessages` errors. The graph importer had produced an error fallback; this was not a missing material binding. The actual palm-launch prefab visibly rendered solid magenta particle quads.
- Force-reimporting the unchanged original `UberFXSG.shadergraph` produced `Piloto Studio/UberFXSG`, seven passes, supported with no shader errors. The same launch prefab rendered its original orange fragments, fire, flare and smoke. No graph, subgraph, material property, texture, GUID or pipeline setting was changed.
- The four project VFX graphs initially had no imported shader subassets. In Practice the skill/backfire graphs and palm projectile had empty renderer material arrays and zero live particles. Reimporting the unchanged graphs regenerated 11 supported shader subassets: three each for skill/backfire, one for palm, four for the slow zone, with no compiler errors. HDRP installation or serialized output conversion was not necessary.
- This establishes invalid/stale local import artifacts as the immediate cause. The initiating historical import failure is not established by retained logs. A clean new checkout or a different Editor's Library was not tested; do not claim a shader source incompatibility was fixed or another checkout was repaired.

`Tools > BuddahGo > Repair Original VFX Imports` repeats the five targeted imports and checks for absent shader subassets, unsupported shaders, graph/internal error fallbacks and compiler errors. It is an explicit Edit-mode action, not an automatic startup hook. It preserves source assets. Other checkouts with the same stale import symptoms can run this after receiving the change; the user's main `dev` Editor was not modified.

Observed validation and limits:

| Effect | Actual evidence |
| --- | --- |
| Flecks / palm launch | Original prefab before/after render; real Practice normal palm cast and two hand-push invocations produced visible Piloto particles. Live Flecks renderers had particles and `Piloto Studio/UberFXSG`; the missing-material root had zero particles. |
| Palm VFX Graph | After reimport, a real Practice cast rolled the existing anti variant and created five original palm graph instances, each reporting 30,000 particles with the generated original shader. A separate stationary Play-mode fixture visibly rendered the golden particle hand. Full normal-projectile charge/travel/expiry and repeat acceptance after graph recovery remains for the next skill pass. |
| Buddah burst candidates | Identified the actual `技能Particle` and `反噬Particle` VFX renderers separately from Piloto. The Practice anti cast visibly produced an orange burst; separate Play-mode fixtures rendered both original graphs without magenta. The user's informal word “explosion” does not uniquely identify another asset; no claim is made about an unidentified effect. |
| Acceleration restoration | Original prefab/material/texture imported; a normal acceleration request reached server execution in the first Practice run. The restored particle child/trail/expiry and its built-in particle shader still need isolated visible acceptance; this is not a full acceleration pass. |
| Slow zone | Four generated shaders compiled; gameplay visual acceptance not performed. |

The shipped recovery menu compiled and was executed successfully, reporting 1 + 3 + 3 + 1 + 4 supported shaders. Private probes and screenshots are excluded from commits; evidence is retained locally and shown only in the private task conversation. Runtime probes used actual `RequestCast` and the existing hand-push method, rather than keyboard combo input; fixture renders are identified separately. Existing Animator-controller errors occurred during Practice and are not claimed resolved. No AI, online clients, strict14, benchmark or broad test-suite rerun was performed. The earlier 65-test/handoff result is separate evidence, not a new result for this change.

The restored original acceleration prefab contains a NetworkObject; Unity's FishNet generator added its original GUID to `DefaultPrefabObjects.asset`. This corresponding one-line registry update is included. Existing unrelated local settings/meta changes are preserved.

The historical static recovery report follows; its “not executed” section describes the state before this follow-up.

## Integration

- Base: `dev` at `7389ec8655a2f438d73c189338e055c962eef84f`.
- Compared single-player branch: `af72b224b2a9494866fbcae61ba576a3dbbed8b4`.
- Recovery branch: `fix/asset-recovery`, linked worktree `.worktree/asset-recovery`.
- Both branches have the same affected Buddah prefab, VFX, Piloto assets and package manifests. This is a shared `dev` defect, not a PR59-only regression. Integrate the recovery into `dev` through the normal review process, and include the same commit in PR59 if Practice validation needs it first. No merge, push or PR was performed here.
- Main `dev`, the single-player worktree and other workers' code were not changed. No Unity/Player/MCP operation was performed.

## Restored originals

All ten restored files come from `6d99ca2^`, immediately before their deletion in `6d99ca2` (2026-03-31, `feat: add project config database workflow`). Text assets and `.meta` retain their original Git blobs; the texture matches its original LFS SHA-256.

| Asset | Original GUID | Live dependency |
| --- | --- | --- |
| `Assets/VFX/神足通/Acceleration粒子效果.prefab` | `2b42a39518812af4b9e586e4c0c9bcea` | `Assets/VFX/FeelPrefab/Acceleration_Shared.prefab` |
| `Assets/Plugins/GabrielAguiarProductions/FreeQuickEffectsVol1/Materials/Flare00_AB_2.mat` | `0153253dee1c55945abc95d9be9e19df` | Restored particle renderer |
| `Assets/Plugins/GabrielAguiarProductions/FreeQuickEffectsVol1/Textures/Flare00.PNG` | `e8bb5606fef74314e9e5e19aec6b527e` | Material `_MainTex` |

The other seven files are the three matching asset `.meta` files and four original folder `.meta` files. Only this needed subset of FreeQuickEffects was restored. The material's original built-in shader binding is unchanged and must be checked under the project's URP renderer during runtime acceptance.

## Flecks / Piloto evidence

`Assets/Plugins/Piloto Studio/Materials/Shared/Flecks_Clipped_Alpha 1.mat` is present on both branches with GUID `4eb5327932e172940bcc11fb007f3a40`. Its material content has not changed since `2be1eb5`.

Its rendering references resolve to:

| Dependency | GUID |
| --- | --- |
| `Shaders_Reforged/UberFXSG.shadergraph` | `7f11fe28133f1a74594785107b98f2fe` |
| `Textures/FireNoise.png` | `204e24b3132c6754abe847f62aa2807b` |
| `Textures/Fleks_AlphaClipped.png` | `8b01c0ee0fc4eee48aafddb5e7805659` |

Paths in this table are relative to `Assets/Plugins/Piloto Studio/`. The shader's four referenced subgraphs (Channel Picker, Channel Picker Add, Channel Picker Clamped, Soft Particle) are present with their original GUIDs. `c339fcc` deleted these on 2026-03-26, but `fb24582` restored them that same day and is already an ancestor of both tested branches. Thus that historical deletion does not explain a currently absent Flecks shader dependency. Both checked-out worktrees contain the actual LFS texture bytes, not pointer stubs. This static result does not establish that Unity currently renders or imports the shader correctly.

## Remaining unresolved references

### Original assets not located

- Material GUID `b739a3f02ff77bf48b7636e64c3e3b4c`, fileID `2100000`: referenced by enabled ParticleSystemRenderers in `Assets/VFX/VisualEffectGraph/沙砾.prefab`, `Assets/VFX/如来神掌/如来神掌.prefab`, `Assets/VFX/如来神掌/射击特效.prefab`, and `Assets/VFX/禅定缓流/slowtrap_vfxZone.prefab`. Its filename and original material properties cannot be recovered from the available metadata. The palm launch prefab already referenced it when introduced in `8f2ff43` (2026-03-23); no matching `.meta` was found in the searched Git objects or backups. A deletion commit for this GUID has **not** been established.
- Texture GUID `1c0a28d7a8db4e93bc4f05b6b878cd79`, fileID `2800000`: saved `_diff` texture in `Character_Fire_Aura_Alpha.mat`, `Character_Fire_Aura_add.mat`, and `Character_Fire_Aura_tube2_Flames_add_soft.mat` under Piloto's `Materials/Fire`. Already present as an unresolved reference when those materials were introduced in `2be1eb5` (2026-03-26). Original filename/content and deletion cause remain unknown. Whether this saved property is active in the current shader requires Unity inspection.

The normal palm action still points to `如来神掌VFXGraph.prefab` (`5a7021ddd233f424a86a80b8617d8bc8`) and `射击特效.prefab` (`3383cc1a59c868f4db6e83c4d34f77c8`). Both assets, the underlying palm VFX Graph and its mesh/texture dependencies are present. The missing shared launch material above remains a real unresolved asset reference.

### HDRP metadata / output references

Two more GUIDs do not resolve in the installed URP packages:

- `da692e001514ec24dbc4cca1949ff7e8`: HDRP `AssetVersion.cs`, a hidden material-version object included in Flecks and 18 other scanned Piloto materials. Identity verified against [Unity's original metadata](https://raw.githubusercontent.com/Unity-Technologies/Graphics/2022.3/staging/Packages/com.unity.render-pipelines.high-definition/Editor/AssetProcessors/AssetVersion.cs.meta).
- `081ffb0090424ba4cb05370a42ead6b9`: HDRP `VFXHDRPSubOutput.cs`, serialized in `反噬Particle.vfx`, `技能Particle.vfx`, and `如来神掌.vfx`. Identity verified against [Unity's original metadata](https://raw.githubusercontent.com/Unity-Technologies/Graphics/2022.3/staging/Packages/com.unity.render-pipelines.high-definition/Editor/VFXGraph/VFXHDRPSubOutput.cs.meta).

These are package-type references rather than missing art textures. No HDRP package installation, ProjectSettings change, removal of serialized objects, or speculative pipeline conversion was made. The Unity worker must determine their actual import/rendering impact.

## Search and static validation

- Scanned 29 roots: Buddah prefab, all skill asset definitions and all VFX prefabs. Followed YAML references, escaped ShaderGraph JSON references, VFX references and importer `.meta` dependencies; excluded Unity built-in GUIDs and resolved installed package GUIDs.
- After restoration: 194 project assets in the dependency closure, 55 LFS payload hashes verified, no duplicate project GUIDs, and no missing YAML-local target fileIDs among referenced material/prefab/asset/controller/animation objects. Unity's synthetic prefab fileID `100100000` is excluded from YAML-anchor checks; imported mesh/shader fileIDs require Unity validation.
- Four unresolved GUIDs remain, explicitly listed above. This is not a clean missing-reference or runtime pass.
- Restored files match the original source tree (line-ending normalization for working text; original LFS payload hash for the PNG). Existing affected serialized assets match between `dev` and single-player.
- Searched 4,981 historical `.meta` objects reachable from current refs; additionally searched all locally stored candidate metadata blobs, including unreachable objects: 10,993 blobs in the main repository, 10,539 in the archived recovery bare repository, and 8,451 in the preserved old-project Git repository. No matching original metadata for the four unresolved GUIDs was found there.
- Read-only asset/package metadata scans covered the current project, the migrated clone and the old-project recycle-bin backup: 14,875, 17,038 and 15,362 `.meta` files respectively. No matching originals for the four unresolved GUIDs were found. The two package identities above were subsequently established from official Unity source.
- Migration evidence records all 1,247 local LFS objects retained with matching hashes. The completed archive relocation reports 460 files preserved with SHA-256 verification. These records do not demonstrate deletion of the unresolved material or texture during that cleanup.
- No files were moved out of the recycle bin, deleted, or overwritten in the user's main project. No replacement artwork was downloaded or third-party package executed.

## Unity handoff / acceptance (not executed)

1. In the worker-owned Unity instance after integration, let the exact editor version import the restored assets. Record missing-script, missing-material and shader compilation errors. Inspect Flecks' shader, both textures, four subgraphs and rendered preview; if it fails despite valid GUIDs, capture the compiler/importer error before changing art.
2. Inspect `Acceleration_Shared.prefab` and the restored particle child. Confirm `Flare00_AB_2` resolves to the original `Flare00.PNG`; verify transparent particles actually render with the active URP renderer.
3. Enter Practice and select 神足通 and 如来神掌. Trigger the configured HUD combos (default slots: W–Up–W and Up–W–Up unless overridden). For 神足通 check activation particles, trail, expiry and repeat casting. For 如来神掌 cast, then use normal hand-push inputs during its buff; check charge Progress animation, launched palm, muzzle/launch particles and lifetime cleanup. Check the anti-skill variant's multi-projectile burst separately.
4. Specifically inspect the four prefabs with unresolved `b739a3…` material slots. Do not accept pink/invisible particles or silently assign a default material. A complete original asset backup/package supplying that exact GUID is still needed for strict original-asset recovery.
5. Exercise all six skills (Acceleration, BlackCurtain, Giant, PushProjectileHands, ReverseTurn, SlowTrap), including anti variants where available. Record visible effects, expiry/repeat behavior and console errors. SlowTrap shares the unresolved material and needs explicit observation. This task does not implement AI or change skill behavior.
