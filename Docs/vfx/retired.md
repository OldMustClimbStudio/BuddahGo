# 已退休的特效内容与代码（待一次清除）

用户 2026-10-29 要求：所有已退休的内容和代码都记在这里，等全部技能特效做完、一次性提交时再统一删除。删之前按"引用"一列重新查一次（GUID 引用用 `refscan.py` 的做法：在 `Assets/` 下的 prefab、unity、asset、mat、vfx、shadergraph 里搜 `.meta` 的 guid），引用变了就先改引用再删。

状态分三种：

- **已删**：已经从工程移除，只需提交时确认。
- **待删**：已经没有正在使用的玩法引用，可以直接删（引用列只剩沙盒、测试场景或同批待删的东西）。
- **等替换**：还在用，要等对应的新插槽做好、接线换过去之后才能删。

第 4 节列的是正在使用、看起来像旧东西但**不能删**的，避免误删。

**2026-10-30 已执行一次删除**（提交 `chore(vfx): retire legacy effects and unused first-pass infrastructure`，紧接 a6d81701 之后）：第 1、2 节和墨手线一节里标 **待删** 的文件都已从工程移除，第 3 节里的文件级代码项也已删；逐项记录、随删改动和这次**没删**的例外见第 6 节。下面各表保留原状作历史记录，状态列不再逐行改。

## 0. 之前的特效播放方式（记录，换成插槽之前）

- **通用前摇**：`SkillExecutor.PlayQueuedCastFeedbackObserversRpc` 在施法确认延迟开始时对所有客户端播 Feel 事件 `Skill_Reflection_Shared`（反噬时播 `Anti_Reflection_Shared`，并触发动画 `IsAnti`）。`SkillFeelRouter` 把事件交给 `FeelVFX` 下的 `技能Reflection_Shared` / `反噬Reflection_Shared` 播放器，播放器里的 `MMF_VisualEffect`（模式 Play，声明时长 2 秒）对角色身上的 `VisualRoot/技能Particle` / `反噬Particle` 调用 `Play()`。这两个物体是 VFX Graph（`技能Particle.vfx` / `反噬Particle.vfx`），局部缩放 0.25（`VisualRoot` 4 倍，所以世界缩放 1），`VFXPropertyBinder` 把属性 `Transform` 绑到 `VisualRoot/model`（局部 (0, 0.002, 0)，缩放 100），排序 1。施法者自己另外播 `*_Reflection_Local`（Bloom、白平衡、镜头震动），这部分保留。
- **神足通**：`Skill_Acceleration` 在服务器上 `PlayVfxAll("accel_back_vfx", …)`，`SkillVfxReplicator` 实例化 `Acceleration粒子效果.prefab`，按 `SkillVfxAttachmentSettings` 挂到玩家身上或跟随，`TimedVfxInstance` 计时关掉；同时角色身上的 `PlayerAccelerationTrail` 打开 `Trails/AccelerationTrail` 拖尾。
- **禅定缓流**：`PlayVfxAll("slowtrap_vfx", …)` 播 `slowtrap_vfxZone.prefab`，同样走挂接加计时。
- **转向反转**：配了 `reverse_turn_trap_vfx`，但库里没有条目，什么也不播；被反转的人没有任何表现。
- **一念光明**：`BlackCurtainViewController` 打开每个角色身上的 `PlayerBlackCurtainTrail`（`Trails/BlackCurtainTrail`，层 `BlackCurtainTrail`，由渲染器的 `RenderTrails` 画在黑幕上面）。
- **如来神掌**：`ChargedHandProjectileRuntime` 生成 `palm_release`；Buf 用 `BuddahHandControl.PlayProjectilePushBuffVfxLocal("palm_buf")`；反噬是 `palm_release_anti`。这三个在插槽之前就已经是带 `SkillVfxPhaseController` 的 prefab。
- **复活和瞬移**：`BuddahRespawn` 与 `BuddahPredictedMotor.Events` 直接找角色身上的两个拖尾组件清空。

现在：所有技能按 vfx_id 播插槽 prefab（`SkillVfxReplicator.PlaySlot` / `PlaySlotLocal`），前摇是 `cast` / `cast_anti` 插槽，瞬移由插槽自己清拖尾。插槽清单见 [slots.md](slots.md)。

## 1. 角色身上的特效（2026-10-29 退休）

| 内容 | 状态 | 引用 / 说明 | 替代 |
|---|---|---|---|
| `Buddah.prefab` 的 `VisualRoot/技能Particle`、`VisualRoot/反噬Particle` | 已删 | 备份 `Buddah.before-retire.prefab` 在设计会话的 scratchpad | `cast` / `cast_anti` 通用前摇插槽 |
| `Buddah.prefab` 的 `VisualRoot/Trails`（`AccelerationTrail`、`BlackCurtainTrail`） | 已删 | 同上 | `accel_release` 插槽、`curtain_hit` 插槽 |
| `Assets/VFX/VisualEffectGraph/技能Particle.vfx`、`反噬Particle.vfx` | 待删 | 2026-10-29 先复用在 `VFX_Common_Cast` / `VFX_Common_Cast_Anti` 里跑通；同日用户用包里的 `Ying Yang Rune Fall` 重做了两个前摇，插槽不再引用它们。剩下的引用：本地 `VFXtests` 场景 `Player/VisualRoot` 上的两个旧拷贝（场景不进仓库，删前先从场景里删掉），以及 `Assets/Editor/VfxImportRecovery.cs` 的 GUID 列表（"Skill reflection" / "Backfire reflection" 两行，删资产时一起删掉，否则那个菜单会报缺失）。两个插槽里的 `ModelFrame` 是给 Graph 绑定用的，现在也没用了，可一并删 | `cast` / `cast_anti`（Ying Yang Rune Fall） |
| `FeelPrefab/技能Reflection_Shared.prefab`、`反噬Reflection_Shared.prefab` | 待删 | 里面的 `MMF_VisualEffect` 已去掉，现在是空播放器；`Buddah.prefab` 的 `SkillFeelRouter` 条目 `Skill_Reflection_Shared` / `Anti_Reflection_Shared` 和 `SkillExecutor.PlayQueuedCastFeedbackObserversRpc` 里播这两个事件的两行一起删 | 同上 |
| `FeelPrefab/Acceleration_Shared.prefab`、`Acceleration_Local.prefab` | 待删 | 没有引用；`SkillFeelRouter` 里 `acceleration_shared` 条目的播放器本来就是空的，一起删 | `accel_release` |

## 2. 旧技能特效资产

| 内容 | 状态 | 引用 / 说明 | 替代 |
|---|---|---|---|
| `神足通/Acceleration粒子效果.prefab` | 待删 | `DefaultPrefabObjects.asset`（FishNet 可生成列表）、`SkillVfxDatabase` 的 `accel_back_vfx`、`Acceleration_Shared.prefab` | `accel_release` |
| `神足通/AccelerationTrail.prefab` | 待删 | 只剩 `VFX_Sandbox.unity` | `accel_release` |
| `禅定缓流/slowtrap_vfxZone.prefab` | 待删 | `SkillVfxDatabase` 的 `slowtrap_vfx`、`RaceMap.unity` 里 `VfxStartupWarmup.vfxPrefabs` 的一项（删 prefab 前先从这个列表里移掉） | `slow_release` |
| `禅定缓流/Decceleration_VFX.prefab`、`VisualEffectGraph/Decceleration_VFX.vfx`、`VisualEffectGraph/directorTimeline.playable`、`禅定缓流/SG_DeccelerationVFXParticle.shadergraph`、`禅定缓流/SG_MagicCircle_Appear.shadergraph`、`ShaderGraph/禅定缓流/` 下同名两份、`禅定缓流/阵法-完整.png` | 待删 | 只剩沙盒和彼此 | `slow_release` |
| `禅定缓流/阵法-完整黑底.png` | 待删前先查 | 被 `Assets/Plugins/Piloto Studio/Materials/Fire/Character_Fire_Aura_add.mat`、`Character_Fire_Aura_Alpha.mat` 引用（包材质改过贴图），这两个材质没人用就一起删 | — |
| `如来神掌/如来神掌.prefab`、`如来神掌.vfx`、`如来神掌VFXGraph.prefab`、`射击特效.prefab`、`HandProjectile.prefab`、`pca_HandProjectile*.pcache`（3 个） | 待删 | 只剩沙盒和彼此 | `palm_release` 插槽 |
| 实现会话前几轮的如来神掌：`如来神掌/VFX_Palm.prefab`、`Proto/VFX_Palm_ProtoA.prefab`、`Proto/VFX_Palm_ProtoB.prefab`、`Proto/Yixuan/VFX_Palm_ProtoYX.prefab`、`Mesh_FX_Palm.asset`、`Mesh/`、`Materials/` 里 `Mat_FX_Palm*` / `Mat_FX_PalmProto*` / `Mat_FX_PalmAnti*`、`Shared/Textures/T_FX_*`（8 张） | 待删（`VFX_Palm_Buff` 用到的那部分等替换） | 互相引用；`VFX_Palm_Anti`（同批待删）和 `VFX_Palm_Buff` 还在用其中一部分材质和网格 | `palm_release` 插槽 |
| `如来神掌/VFX_Palm_Anti.prefab` | 待删 | 2026-10-30 `palm_release_anti` 改指 `VFX_Palm_Release_Anti`（开火动画），子弹是新的 `palm_bullet_anti`（`VFX_Palm_Bullet_Anti`），没有玩法引用了；删前确认 VFXtests 场景里没有它的实例 | `palm_release_anti` + `palm_bullet_anti` |
| `如来神掌/Buff/VFX_Palm_Buff.prefab` | 等替换 | `SkillVfxDatabase` 的 `palm_buff` | 如来神掌 Buf 插槽（未做） |
| `Rendering/Shaders/FX/` 下 `FX_Additive_Glow`、`FX_Alpha_Smoke`、`FX_Decal`、`FX_InkFog`、`FX_InkGlass`、`FX_Mesh_Energy`、`FX_SmokeSprite`、`FXCommon.hlsl`、`FXInk.hlsl` | 待删（跟上面的材质一起） | 只被上一行的旧材质引用；README 第 2 节已定不再自写 shader | 包的 `UberFXSG` |
| `Shared/VFX_Template.prefab` | 待删 | 没有引用；`PalmVfxBuilder` 生成的，README 第 7 节的模板思路已被插槽 prefab 取代 | 插槽 prefab（`SkillReleaseProto` 生成） |
| `Shared/YixuanPalette/`（11 个 `YX_*` 材质、`YixuanBloomTrial.asset`、`Backup_VFX_Slow_Release_BeforeYixuan.prefab`） | 待删 | 没有引用；数值已记在 [yixuan-palette.md](yixuan-palette.md) 第 13 节 | — |
| `Assets/VFX/Sandbox/`（`VFX_Sandbox.unity`、`Mat_Sandbox_Floor.mat`） | 待删 | 测试场景改用 `Assets/Scenes/VFXtests.unity` | VFXtests |

## 3. 代码与数据

| 内容 | 状态 | 位置 / 说明 |
|---|---|---|
| `PlayerAccelerationTrail.cs`、`PlayerBlackCurtainTrail.cs` | 待删（`PlayerBlackCurtainTrail` 暂缓） | 角色身上已经没有这两个组件。2026-10-29：用户把带 `PlayerBlackCurtainTrail` 的 `BlackCurtainTrail` 特意放进了 `一念光明/Release/VFX_Curtain_Release`，删这个脚本前要先换掉那里的组件。调用点一起删：`BuddahRespawn.cs`（约 271–273 行，复活时清拖尾）、`SkillExecutor.cs`（`accelerationTrailController` 字段和 `ResolveInHierarchy<PlayerAccelerationTrail>`）、`BuddahPredictedMotor.Events.cs`（约 869–876 行，瞬移重置拖尾）。插槽自己处理瞬移（`SkillVfxPhaseController.TeleportJump`）。`VFXtests.unity` 替身玩家上还挂着，测试场景不入库，可不管 |
| `SkillVfxReplicator` 的非插槽路径：`PlayVfxInternal` 里不带控制器的分支、`FindExisting`、`ConfigureAttachment`、`PlayAllParticleSystems` / `PlayAllPlayableDirectors` / `PlayAllEffects`、`SetAllRenderersEnabled`、`EnableRenderersAfterFrames`、`DisableNetworkComponentsForLocalVfx`（插槽也在调，删前改成插槽自己的版本）、`SkillVfxTag`、`TimedVfxInstance.cs`、`SkillVfxAttachmentSettings.cs`、没人调用的 `PlayVfxAllObserversRpc`（不带参数的那个） | 待删 | 库里还在播的 id 全是插槽 prefab（`palm_buff`、`palm_release_anti` 也带 `SkillVfxPhaseController`），只有已退休的 `accel_back_vfx`、`slowtrap_vfx` 不带。`PrewarmAllVfx` 现在用旧方式预热插槽 prefab（挂到玩家身上、直接播放），删的时候改成只预热、不播放，或者交给 `VfxStartupWarmup`。VfxRegistry 的 `followTargetPosition`、`inheritTargetRotation`、`inheritTargetScale` 三列一起删（配表、导出、生成代码） |
| 技能 SO 上只给旧路径用的字段：`Skill_Acceleration` / `Skill_SlowTrap` / `Skill_SlowTrap_Anti` 的 `vfxLocalOffset`、`vfxLocalEuler`、`vfxStopPlayingBeforeEndSeconds`，`PlayVfxAll` 的同名三个参数 | 待删 | 插槽忽略这三个值（位置由层的锚点决定，结束由窗口决定） |
| VfxRegistry 行 `accel_back_vfx`、`slowtrap_vfx` 和 `SkillVfxDatabase` 里同名两条 | 待删 | 已标"已退休"，没有技能引用 |
| VfxRegistry 行 `accel_anti_vfx`、`reverse_turn_trap_vfx`、`reverse_turn_trap_anti_vfx` | 待删 | 库里没有条目，代码也不播；对应的 SO 字段：`Skill_ReverseTurn.vfxId` 2026-10-29 已改为 `reverse_release`（释放插槽），`Skill_ReverseTurn_Anti.vfxId` 已改为 `reverse_hit`（复用受击插槽），`SkillPresentation` 表同步改了；`Skill_Acceleration_Anti.vfxId` 2026-10-29 已清空（之前每次反噬都报 Unknown vfxId）。反噬改由 `cast_anti` 前摇和各技能以后的反噬插槽表现 |
| 编辑器工具：`PalmVfxBuilder`、`PalmVfxPreview`、`PalmVfxRegistration`、`PalmYingYangProto`、`Proto/YixuanProtoBuilder`、`PalmMeshBaker`、`FxTextureGenerator`、`FxMeshSimplifier`、`SkillVfxSandboxBuilder`，运行时 `SkillVfxSandbox.cs` | 待删 | 生成或预览前几轮方向的资产；`PalmFrontalProto`、`SkillReleaseProto` 已不依赖它们。`PalmVolumeBaker` 先问墨体积那条线是否还用 |
| `PalmInkTimeline` 自带的位移：`travelDistance`、`speedCurve`、`CurveArea()`、`PeakSpeed()` | 已删（2026-10-30） | 用户："速度曲线由技能方继承，特效方只留预览和拉尾烟"。飞行改由技能定义（`Skill_PushProjectileHands.projectileSpeedCurve` + Push 表 `speed`，`ChargedProjectileFlight`），比赛里 `ChargedHandProjectileRuntime` 移动，时间轴的预览也读技能的飞行。prefab 里这两个字段的旧序列化值下次保存时由 Unity 丢弃，备份在设计会话 scratchpad `backup-1030/` |
| `BuddahHandControl.FireChargedProjectileImmediate`、`FireProjectileBurstImmediate` | 待删 | 没有调用方（2026-10-30 搜过）。前者的视觉副本走共用的 `SpawnChargedProjectileVisualObserversRpc`，会带上 `palm_release` 的速度曲线而服务器判定不带，万一被调用两边对不上，所以不要复用，直接删 |
| `ChargedHandProjectileRuntime` 的 `launchEffectPrefab` / `launchEffectWorldOffset` / `SpawnLaunchEffect`，`BuddahHandControl` 的 `_projectileChargedVisualPrefabServer`、`_projectileChargedProgressPropertyServer` 和 `ActivateProjectilePushMode` 的 `chargedVisualPrefab`、`chargedProgressProperty` 参数 | 待删 | 没有调用方传值（开火动画走 `palm_release` 插槽，不用它）：技能总传 `null`，`SpawnChargedProjectileServer` 读出后不再使用，视觉由客户端按 vfx_id 解析 |
| `ChargedHandProjectileRuntime` 的 VFX Graph 进度：`_visualEffects`、`SetProgress` 里写 `Progress` 的部分、`PlayAllVisualEffects`、`progressProperty` 参数链（`DefaultChargedProjectileProgressProperty`、`ProjectileBurstParameters.progressPropertyOverride`） | 待删前先查 | 给旧的 VFX Graph 如来神掌（`如来神掌.vfx`，第 2 节待删）用；`palm_release` 和 `palm_release_anti` 现在都不带 `VisualEffect`。`SetProgress` 里给插槽的 `SetChargeProgress` 要保留 |
| `ChargedHandProjectileRuntime` 的线性减速（`GetCurrentFlightSpeed`、`FlightEndSpeedFraction`、`GetFlightStep` 里的无曲线分支） | 待删 | 2026-10-30 起正向和反噬都带速度曲线，只剩没人调用的 `FireChargedProjectileImmediate`（同批待删）走这条路。删后 `speedCurve` 改成必填 |
| `ProjectileBurstParameters.spawnVisuals`、`visualPrefabOverride`、`progressPropertyOverride` 和 `SpawnChargedProjectileBurst` 里 `if (burst.spawnVisuals)` 那段 | 待删 | 唯一的调用方 `FireChargedProjectileBurstReplicated` 总是传 `false` / `null`；视觉由 `SkillExecutor.PlayChargedBurstVisualsObserversRpc` 在客户端按 vfx_id 生成（2026-10-30 搜过） |
| 命名统一（2026-10-30）后空出来的目录：`通用前摇/Proto/Release`、`如来神掌/Proto/Frontal`、`神足通/Proto/Release`、`禅定缓流/Proto/Release`、`转向反转/Proto/Release`、`转向反转/Proto/Hit`、`一念光明/Proto/Release`、`一念光明/Proto/Hit`、`巨大化/Proto/Release`（以及只剩空子目录的 `Proto/`）和它们的 `.meta` | 待删 | 插槽 prefab 已按 README 第 6 节移到 `<技能>/<插槽>/`（GUID 不变）。`如来神掌/Proto/` 里还有第 2 节的旧原型（`VFX_Palm_ProtoA/B`、`VFX_Palm_YY*`、`Yixuan/`），跟它们一起删 |
| 旧编辑器工具里的旧 id 和路径：`PalmVfxBuilder`（`VFX_Palm_Buf` 路径、`palm` / `palm_anti` / `palm_buf`）、`PalmVfxRegistration`（把库里的 `palm` / `palm_anti` / `palm_buf` 写回旧 prefab）、`PalmYingYangProto`、`Proto/YixuanProtoBuilder`、`SkillVfxSandboxBuilder` | 待删（已在上面的编辑器工具行里） | 命名统一时没改它们：已在待删清单里，改了就是给僵尸代码续命。**不要再运行**，尤其 `PalmVfxRegistration` 会把特效库改回旧 prefab |
| `SkillVfxTests` 里测旧 prefab 的两个用例：`[TestCase(PalmPrefab, "palm", …)]`、`[TestCase(AntiPrefab, "palm_anti", …)]` 和常量 `PalmPrefab`、`AntiPrefab` | 待删（跟 `VFX_Palm`、`VFX_Palm_Anti` 一起） | 测的是第 2 节已退休的 `VFX_Palm.prefab`、`VFX_Palm_Anti.prefab`，它们的控制器 id 没跟着改名 |
| `Docs/vfx/push-projectile-hands-implementation.md` 里描述旧资产生成流程的段落 | 待改 | 删资产时同步改成插槽流程，或整篇标注已过时 |

## 4. 看着像旧的，但还在用（不要删）

- `神足通/SG_Acceleration.shadergraph`、`SG_Acceleration 1.shadergraph`、`acceleration_尾迹.jpg`：`VFX_Accel_Release` 里复制过来的拖尾在用；`acceleration_尾迹.jpg` 还被 `一念光明/SG_BlackCurtainTrail.shadergraph` 用。
- `一念光明/SG_BlackCurtainTrail.shadergraph`：`curtain_hit` 插槽在用。`一念光明/SG_BlackCurtain.shadergraph`：渲染器的黑幕 pass 和两个一念光明技能在用。
- `VisualEffectGraph/沙砾.prefab`：角色身上保留的沙砾。`VisualEffectGraph/角色Glow.anim`：角色动画控制器在用。
- `ShaderGraph/地图/SG_MapEdge.shadergraph`：RaceMap 和空气墙在用。`PreloadShader.shadervariants`：RaceMap 在用。
- `FeelPrefab/技能Reflection_Local.prefab`、`反噬Reflection_Local.prefab`：本地 Bloom、白平衡、镜头震动，还在用。
- `VfxStartupWarmup.cs`：RaceMap 的预热，还在用（只删它列表里的 `slowtrap_vfxZone`）。
- `HandPushProjectileRuntime.cs`：玩法代码（`BuddahHandControl`、AI 测试在用），不是特效。
- `如来神掌/Release/VFX_Palm_Release.prefab`：`palm_release`，如来神掌的开火动画（用户 2026-10-30：开火动画和子弹相辅相成，两个都要）；生成它的 `PalmFrontalProto.cs` 也还在用。
- `如来神掌/Bullet/VFX_Palm_Bullet.prefab`：2026-10-30 起是 `palm_bullet`，如来神掌的子弹。`InkFluidSheet.cs`、`PalmInkTimeline.cs`、`PalmInkLayerSwitch.cs`、`FX_InkVolume.shader`、`FX_InkFluidSheet.shader`、`InkFluid.compute`、`PalmMeshTripo.cs`：墨体积和 Tripo 那条线的进行中工作，归那个会话，其中已退役的层（`InkParticles`、2D 面片）由那个会话记在 `palm-mesh-tripo.md`，提交前并进本表。

## 5. 顺带发现、未确认是否退休

GUID 搜不到引用，但可能被代码按名字加载（`Shader.Find`、`Resources.Load`），删前确认：

- `ShaderGraph/SG_Transparent.shadergraph`、`ShaderGraph/地图/SG_Trail.shadergraph`、`VisualEffectGraph/角色Glow.shadergraph`
- `ShaderGraph/一念光明/` 整个目录（`SG_BlackCurtain` 的重复副本、材质和 `Te.png`；在用的是 `一念光明/` 下那份）、`一念光明/Shader Graphs_SG_BlackCurtain.mat`

## 4. 如来神掌墨手线的退役内容（2026-10-30 登记，详见 [palm-mesh-tripo.md](palm-mesh-tripo.md) 第 16–19 节）

| 内容 | 状态 | 引用 / 说明 | 替代 |
|---|---|---|---|
| 2D 流体面片：`Assets/Scripts/VFX/InkFluidSheet.cs`、`Rendering/Shaders/FX/InkFluid.compute`、`FX_InkFluidSheet.shader`、`如来神掌/Materials/Mat_FX_Palm_InkFluid.mat` | 待删 | prefab 里的 `InkFluid` / `InkFluidSide` 子物体已删（2026-10-29），没有对象再用；`PalmInkTimeline.Apply` 里还有一段 `foreach InkFluidSheet`（找不到就跳过），删脚本时把那段一起删。用户看过粒子版后又要回流体的"感觉"，最终由三维版 `InkFluidVolume` 取代 | `InkFluid3D`（`InkFluidVolume` + `InkFluid3D.compute` + `FX_InkFluid3D.shader`） |
| compute 粒子：`Assets/Scripts/VFX/InkParticles.cs`、`Rendering/Shaders/FX/InkParticles.compute`、`FX_InkParticle.shader`、`如来神掌/Materials/Mat_FX_Palm_InkParticle.mat` | 待删 | prefab 的 `InkParticles` 子物体已于 2026-10-30 删除；用户判定"不是烟雾模拟，是软圆盘硬凑的"，由三维流体取代 | 同上 |
| Shuriken 层材质：`Mat_FX_Palm_Wisp.mat`、`Mat_FX_Palm_Plume.mat`、`Mat_FX_Palm_SurfaceSmoke.mat` | 待删 | `SurfaceWisps` / `Plume` 子物体已删，无引用；`FX_SmokeSprite` 已在第 2 节 | 同上 |
| `Assets/VFX/如来神掌/Mesh/Palm_Tripo_v1.glb` | 保留 | Tripo 原始输出，重生成 `Mesh_FX_Palm_Tripo.asset` / SDF 的来源 | — |
| 调参截图 `Docs/vfx/palm-mesh-tripo/*.png`（约 40 张） | 保留 | 文档各节引用；按"保留调查文档"的规则不删 | — |
| `InkFluidVolume.Tick` 的子步循环（`ceil(dt / maxStep)` 与 `ceil(格数 / maxCellsPerStep)` 取大、上限 6，每个子步跑完整 19 次 dispatch） | 已删（2026-10-30，性能会话） | 帧越慢子步越多的正反馈是 24 掌 147 ms 的主因之一。替代：每帧 1 步（长帧 2 步），手的路径由 `Source` 核的 `_WorldToHandSweep` 扫描覆盖 | 同文件 |
| `InkFluidVolume` 每实例 8 张 3D RenderTexture（`velB` / `denB` / `prsA` / `prsB` / `div` / `curl` 随实例分配） | 已删 | 改成全体共用的静态 scratch + 状态纹理池；每掌 41 MB → 12 MB | 同文件 `ScratchSet` / `StateSet` |
| 整格重心时的两次"dt 0 纯拷贝"平流 pass（`AdvectVelocity` / `AdvectDensity` 带 `_Shift`） | 已删 | `Source` 读旧场时按 `_Shift` 整格偏移取样，效果相同、少两次 pass | `InkFluid3D.compute` |
| `InkFluid3D.compute` 单核 `Jacobi` 每次迭代重绑三张纹理 | 已删 | `JacobiA` / `JacobiB`（及粗网格 `JacobiCA` / `JacobiCB`）固定绑定交替 | 同文件 |
| `FX_InkVolume` 每一步算 SDF 梯度（6 次采样）并在入射点再算一次 | 已删 | 表面带附近逐步算、其余步复用入射法线（全程冻结试过：剪影轮廓项失效、形体变平，墨手会话改回带附近逐步算） | `FX_InkVolume.shader` |
| `FX_InkFluid3D` 固定 `_Steps` 步、整盒子步进 | 已删 | 光线裁到有墨的 AABB（`_Bounds`），步数按长度 | `FX_InkFluid3D.shader` |

## 6. 2026-10-30 删除记录

提交：`chore(vfx): retire legacy effects and unused first-pass infrastructure`（分支 `refactor/vfx-refactor`，a6d81701 的下一个提交；hash 以 `git log` 为准）。
删前用全仓库 GUID 引用图（`Assets/` 下全部文本资产 + `ProjectSettings`，不含 `Assets/Piloto Studio/`）加代码符号搜索逐项核过：下面每一项在删除时都是 0 外部引用，或引用只来自同批删除的文件和本节列出的承载物。
共删 158 个文件（145 个已跟踪 `git rm`，13 个未跟踪直接删，均连 `.meta`）。

### 6.1 删除的资产

| 内容 | 为什么退 | 被什么取代 | 随删改动 |
|---|---|---|---|
| `VisualEffectGraph/技能Particle.vfx`、`反噬Particle.vfx` | 通用前摇改用包里的 Ying Yang Rune Fall | `cast` / `cast_anti` 插槽 | `Assets/Editor/VfxImportRecovery.cs` 的 GUID 表只留 UberFXSG |
| `FeelPrefab/技能Reflection_Shared.prefab`、`反噬Reflection_Shared.prefab` | 去掉 `MMF_VisualEffect` 后是空播放器 | `cast` / `cast_anti` 插槽 | `Buddah.prefab` 删掉 `FeelVFX` 下两个嵌套实例和 `SkillFeelRouter` 的 `Skill_Reflection_Shared` / `Anti_Reflection_Shared`；`SkillExecutor.PlayQueuedCastFeedbackObserversRpc` 不再播这两个事件（Local 反馈不变） |
| `FeelPrefab/Acceleration_Shared.prefab`、`Acceleration_Local.prefab` | 无引用，路由条目本来就是空的 | `accel_release` 插槽 | `SkillFeelRouter` 删 `acceleration_shared`；`Skill_Acceleration.observersFeelEventId` 默认值改空（资产里本来就是空） |
| `神足通/Acceleration粒子效果.prefab`、`AccelerationTrail.prefab` | 神足通改播插槽 | `accel_release` | `SkillVfxDatabase` 删 `accel_back_vfx`；`DefaultPrefabObjects.asset` 删该 prefab；VfxRegistry 删 `accel_back_vfx` 行 |
| `禅定缓流/slowtrap_vfxZone.prefab`、`Decceleration_VFX.prefab`、`VisualEffectGraph/Decceleration_VFX.vfx`、`directorTimeline.playable`、`SG_DeccelerationVFXParticle` / `SG_MagicCircle_Appear`（`禅定缓流/` 与 `ShaderGraph/禅定缓流/` 两份）、`阵法-完整.png` | 禅定缓流改播插槽 | `slow_release` | `SkillVfxDatabase` 删 `slowtrap_vfx`；RaceMap `VfxStartupWarmup.vfxPrefabs` 清空（预热改走表的 warmup 列）；`PreloadShader.shadervariants` 删两个 graph；VfxRegistry 删 `slowtrap_vfx` 行 |
| `如来神掌/如来神掌.prefab`、`如来神掌.vfx`、`如来神掌VFXGraph.prefab`、`射击特效.prefab`、`HandProjectile.prefab`、`pca_HandProjectile*.pcache` × 3 | 重做前的 VFX Graph 掌 | `palm_release` 开火动画 + `palm_bullet` 墨手 | `VfxImportRecovery` 删 "Charged palm" |
| `如来神掌/VFX_Palm.prefab`、`VFX_Palm_Anti.prefab`、`Mesh_FX_Palm.asset`、`Proto/`（`VFX_Palm_ProtoA` / `ProtoB` / `YY` / `YY_Buf` / `Yixuan/VFX_Palm_ProtoYX`、`Mesh_FX_Palm_UV`、四个 `Mat_Proto_*`、空的 `Frontal/`） | 实现会话前几轮的手部网格方案，用户全部否决 | `palm_release` / `palm_release_anti`、`palm_bullet` / `palm_bullet_anti` | `SkillVfxTests` 删 `PalmPrefab` / `AntiPrefab` 常量和四个 `TestCase`，预算用例改测 `VFX_Palm_Buff` |
| `如来神掌/Materials/` 里 48 个 `Mat_FX_Palm_*` / `Mat_FX_PalmProtoA_*` / `Mat_FX_PalmProtoB_*` / `Mat_FX_PalmAnti_*`（含墨手线退役的 `InkFluid`、`InkParticle`、`Wisp`、`Plume`、`SurfaceSmoke`、`InkFog`、`InkGlass`） | 只被上一行的 prefab 引用 | 同上 | 留下 `Mat_FX_Palm_BufEnso`、`Mat_FX_Palm_Halo`（`VFX_Palm_Buff`）、`Mat_FX_Palm_InkFluid3D`、`Mat_FX_Palm_InkVolume`（`VFX_Palm_Bullet`） |
| `Shared/Textures/T_FX_Brush_Stroke`、`T_FX_InkDot_Mask`、`T_FX_PalmPrint_Mask`、`T_FX_PalmPrint_Broken_Mask`、`T_FX_Petal_Mask` | 只被删掉的材质引用 | 包贴图 | 留下 `T_FX_Enso_Mask`、`T_FX_Noise_Packed`、`T_FX_Sutra_Glyph`（Buff 材质在用） |
| `Rendering/Shaders/FX/FX_Decal`、`FX_Mesh_Energy`、`FX_InkFog`、`FX_InkGlass`、`FX_SmokeSprite`、`FX_InkFluidSheet`、`FX_InkParticle`、`InkFluid.compute`、`InkParticles.compute` | README 第 2 节已定不再自写 shader；墨手线的 2D 面片和 compute 粒子被三维流体取代 | 包的 `UberFXSG`；`FX_InkVolume` + `FX_InkFluid3D` + `InkFluid3D.compute` | `PreloadShader.shadervariants` 删 `FX_Decal`、`FX_Mesh_Energy`；`SkillVfxTests.FxShadersArePreloaded` 只查 `FX/Additive_Glow`、`FX/Alpha_Smoke` |
| `Shared/VFX_Template.prefab` | `PalmVfxBuilder` 的模板思路被插槽 prefab 取代 | `SkillReleaseProto` 生成的插槽 prefab | — |
| `Shared/YixuanPalette/`（11 个 `YX_*.mat`、`YixuanBloomTrial.asset`、`Backup_VFX_Slow_Release_BeforeYixuan.prefab`） | 用户撤回，自己手调 | 数值在 [yixuan-palette.md](yixuan-palette.md) 第 13 节 | — |
| `Assets/VFX/Sandbox/`（`VFX_Sandbox.unity`、`Mat_Sandbox_Floor.mat`） | 测试场景改用 `Assets/Scenes/VFXtests.unity`（不入库） | VFXtests | — |
| 命名统一后空出的 `通用前摇/Proto`、`神足通/Proto`、`禅定缓流/Proto`、`转向反转/Proto`、`一念光明/Proto`、`巨大化/Proto`（含空子目录） | 插槽 prefab 已移到 `<技能>/<插槽>/` | — | — |

### 6.2 删除的代码

| 内容 | 为什么退 | 被什么取代 | 随删改动 |
|---|---|---|---|
| `PlayerAccelerationTrail.cs` | 角色身上已无拖尾 | `accel_release` 插槽；瞬移清拖尾由 `SkillVfxPhaseController.TeleportJump` | `SkillExecutor` 删 `accelerationTrailController`、`ShowAccelerationTrail(Local)`、`ResolveAccelerationTrailController`；`OwnerMovementEffectApplier`、`Skill_Acceleration.ExecuteObservers` 删调用；`BuddahRespawn`、`BuddahPredictedMotor.Events` 的 `NotifyTeleportTrailRebases` 只剩 `PlayerBlackCurtainTrail` |
| `TimedVfxInstance.cs`、`SkillVfxAttachmentSettings.cs`、`SkillVfxFollowTarget.cs`、`SkillVfxTag` | 库里全是插槽 prefab，挂接 + 计时的播放路径无人走 | `SkillVfxReplicator.PlaySlot` | `SkillVfxReplicator` 删非插槽分支、`FindExisting`、`ConfigureAttachment`、`PlayAll*`、`SetAllRenderersEnabled`、`EnableRenderersAfterFrames`、`revealDelayFrames`；`PlayVfxAll` 改为 `(vfxId, duration)`；`PrewarmAllVfx` 只实例化（渲染器关、控制器不配置）不播放；`Skill_Acceleration(_Anti)`、`Skill_SlowTrap(_Anti)`、`Skill_ReverseTurn(_Anti)` 删 `vfxLocalOffset` / `vfxLocalEuler` / `vfxStopPlayingBeforeEndSeconds`（SO 资产里的旧值下次保存时由 Unity 丢弃） |
| `InkFluidSheet.cs`、`InkParticles.cs` | 墨手线 2D 面片与 compute 粒子被三维流体取代 | `InkFluidVolume` | `PalmInkTimeline` 删 `sheets` 字段、`Collect` 与 `Apply` 里的面片段 |
| `SkillVfxSandbox.cs`、`Assets/Editor/VFX/SkillVfxSandboxBuilder.cs` | 调试场随 `VFX_Sandbox.unity` 退役 | VFXtests + `SkillVfxPreviewDriver` | — |
| `Assets/Editor/VFX/FxMeshSimplifier`、`FxTextureGenerator`、`PalmMeshBaker`、`PalmVfxBuilder`、`PalmVfxPreview`、`PalmVfxRegistration`、`PalmYingYangProto`、`Proto/YixuanProtoBuilder` | 生成或预览前几轮方向的资产；`PalmVfxRegistration` 会把库改回旧 prefab | `PalmFrontalProto`、`SkillReleaseProto`、`VfxPackVendor`、`PalmMeshTripo` | `Tools/vfxperf/shots.py` 的默认 `CLS` 改为 `PalmFrontalProto` |
| VfxRegistry 行 `accel_anti_vfx`、`reverse_turn_trap_vfx`、`reverse_turn_trap_anti_vfx` | 库里没有条目，代码也不播 | 各技能的 `*_release` / `*_hit` 插槽 | 连同上面两行共删 5 行；`followTargetPosition` / `inheritTargetRotation` / `inheritTargetScale` 三列是 `doc` 列，这轮没动 |

### 6.3 这次没删（有引用或待用户决定）

| 内容 | 为什么留 | 谁在用 / 怎么办 |
|---|---|---|
| `PlayerBlackCurtainTrail.cs` | `一念光明/Release/VFX_Curtain_Release.prefab` 的 `BlackCurtainTrail` 上还挂着（用户特意放的） | 换成层控制拖尾后再删，连同 `BuddahRespawn` / `BuddahPredictedMotor.Events` 里的 rebase |
| `Mat_FX_Palm_BufEnso.mat`、`Mat_FX_Palm_Halo.mat`、`FX_Additive_Glow.shader`、`FX_Alpha_Smoke.shader`、`FXCommon.hlsl`、`FXInk.hlsl`、`T_FX_Enso_Mask` / `T_FX_Noise_Packed` / `T_FX_Sutra_Glyph` | `如来神掌/Buff/VFX_Palm_Buff.prefab`（`palm_buff`，等替换）在用；`FXCommon.hlsl` 还被 `FX_InkVolume` 包含 | 如来神掌 Buf 插槽做好后一起退 |
| `禅定缓流/阵法-完整黑底.png` | `Assets/Plugins/Piloto Studio/Materials/Fire/Character_Fire_Aura_add.mat`、`Character_Fire_Aura_Alpha.mat`（已跟踪的旧包）引用 | 那两个材质无人用；是否连旧包目录一起清由用户定 |
| 第 5 节的 `SG_Transparent`、`地图/SG_Trail`、`VisualEffectGraph/角色Glow.shadergraph`、`ShaderGraph/一念光明/`、`一念光明/Shader Graphs_SG_BlackCurtain.mat` | 0 引用，但不是这轮重做留下的 | 用户决定 |
| `Assets/VFX/Shared/预选/` 四个 prefab | 用户自建，仍引用包目录 | 用户决定 |
| 第 3 节里只在文件内部的代码项：`ChargedHandProjectileRuntime` 的 VFX Graph 进度链与线性减速、`BuddahHandControl.FireChargedProjectileImmediate` / `FireProjectileBurstImmediate` 与 `_projectileChargedVisualPrefabServer` 一组、`ProjectileBurstParameters.spawnVisuals` 等、`launchEffectPrefab` | 是活文件里的死代码，不是独立文件；这次只做文件级退役 | 下一轮单独做 |
