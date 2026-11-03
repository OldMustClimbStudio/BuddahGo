# 巨大化与特效体型适配

巨大化（`giant`，体型 ×4）和它的反噬（`giant_anti`，×0.3）会改变角色的体型。挂在角色身上的技能特效都要回答一个问题：体型变了，特效怎么变。本文记录 2026-11-03 的调查结论、目前的适配办法（`SkillVfxLayer` 的 `Blend`），以及还没适配的地方。技能之间互相适配的总任务见 [../gameplay/skill-adaptation.md](../gameplay/skill-adaptation.md)。

**做新特效、改特效时的提醒**：凡是锚点为 `CasterAttached` / `CasterFollow` / `CasterRelative` 的层，都会在巨人身上放大 4 倍、在反噬身上缩到 0.3 倍。默认（`Whole`）是整体等比例放大。做完一层要在巨人体型下看一遍，再按第 3 节决定要不要改成 `Blend`。

## 1. 体型从哪里来，怎么传到特效

- `PlayerScaleEffect` 缩放的是角色**根节点**的 `localScale`（[PlayerScaleEffect.cs:127](../../Assets/Scripts/Buddah/ComboSkill/Skill_Giant/PlayerScaleEffect.cs)），根节点基础缩放是 1；`VisualRoot` 是它的子节点，模型跟着变大。过渡是 smoothstep：变大 0.35 s，恢复 0.35 s；反噬缩小 0.25 s，恢复 0.25 s。
- 倍数和时长来自配表 `Skill.xlsx`：`SkillEffectGiant` 表里 giant 是 4、giant_anti 是 0.3；持续时间来自 `Skill.effectDurationSeconds`，两者都是 6 s。SO 上的默认值（5×）会被配表覆盖。
- 插槽的施法者锚点层在开始时会从 prefab 拆出来，`SkillVfxPhaseController.PlaceOnCaster` 每帧把层放到施法者身上（`CasterRelative` 只放一次）。层的位置偏移乘体型，层自己的缩放由 `SkillVfxLayer.FollowCasterScale` 决定（[SkillVfxPhaseController.cs:352](../../Assets/Scripts/VFX/SkillVfxPhaseController.cs)）。
- 插槽的粒子系统都是 Scaling Mode = Hierarchy、Simulation Space = Local。所以在 `Whole` 下，层一放大，粒子的大小、发射范围、速度、位移就全部跟着放大，已经出生的粒子也会一起胀大，效果就像放大镜。
- 层拆出来以后，`scaledRoot` 上的配表 `scale_multiplier` 和碰撞盒适配都不再作用于它（`localScale` 被直接改写）。这是现状，还没处理。

## 2. 逐个插槽的现状（2026-11-03）

| 插槽 | 锚点 | 体型变化时 |
|---|---|---|
| `slow_release`（禅定缓流） | `CasterFollow` | **`Blend`**（第 3 节）。判定区也跟着放大：触发器是角色根节点的子物体（[MovementSlowTrapZoneEffect.cs:133](../../Assets/Scripts/Buddah/ComboSkill/Skill_SlowTrap/MovementSlowTrapZoneEffect.cs)），所以面积放大 4 倍和玩法一致 |
| `cast`、`cast_anti` | `CasterAttached` | `Whole`：整体 ×4 / ×0.3，实时跟体型 |
| `accel_release`（神足通） | `CasterAttached` | `Whole`；层里的 2 条 `TrailRenderer` 宽度是世界单位，不跟体型，巨人身上显得细 |
| `reverse_hit`（转向反转受击） | `CasterAttached`，施法者 = 被命中的人 | `Whole`：被命中的人是巨人时 ×4，缩小时 ×0.3 |
| `curtain_hit`（一念光明拖尾） | `CasterAttached` | 只有偏移跟体型，拖尾宽度不变 |
| 一念光明黑幕（屏幕效果） | — | 只有眼睛中心的偏移乘体型（[BlackCurtainScreenEffect.cs:194](../../Assets/Scripts/Buddah/ComboSkill/Skill_BlackCurtain/BlackCurtainScreenEffect.cs)），在屏幕上的大小不变 |
| `palm_release` / `_anti`（如来神掌出掌） | `CasterRelative` | 按出手那一刻的体型放大，之后固定 |
| 墨手 `palm_bullet` / `_anti` | 不是插槽 | 手和判定盒 ×4（[BuddahHandControl.cs:854](../../Assets/Scripts/Buddah/BuddahHandControl.cs)）。墨烟网格是世界固定的：80×80×192 格 × 2 单位，`centreOffset` 不乘体型，手的注入用 `hand.lossyScale`（[InkFluidVolume.cs:385](../../Assets/Scripts/VFX/InkFluidVolume.cs)）。4 倍的手比网格宽，墨烟会被截断（按代码推断，没实测） |
| `giant_release`、`reverse_release`、`curtain_release` | `CasterAttached` | 还没有内容；以后放进来的内容默认 `Whole` |

## 3. `Blend`：面积缓出、细节换代、墨浪领路

用户 2026-11-03 定的方向：体型变化时，特效的面积要跟上，但不要把整个特效直接放大；变大、变小之间要有自然的衔接。代码在 `SkillVfxLayer`（`casterScaling`，[SkillVfxLayer.cs:73](../../Assets/Scripts/VFX/SkillVfxLayer.cs)、`FollowCasterScale` 第 226 行起），规则在 `SkillVfxRules`（[SkillVfxTypes.cs:244](../../Assets/Scripts/VFX/SkillVfxTypes.cs) 起）。

层的粒子系统分成三类：

- **撑面积的系统**（`areaSystems`，比如地面圆环、大墨渍、往外扩的脉冲）：保持 Hierarchy，跟着层放大。体型一变，层缩放不再直接跳到新值，而是变大用 `scaleUpSeconds`（默认 1 s，ease-out cubic）、变小用 `scaleDownSeconds`（默认 0.8 s，smoothstep）追上去。过渡中途透明度按 `1 − scaleAlphaDip·sin(πu)` 往下压一点（默认 0.3），把贴图被拉大的过程藏起来。UberFX shader 没有统一的透明度参数，所以压的是每颗粒子的 `startColor` alpha。
- **细节系统**（其余全部，比如烟、亮点、小墨点）：prefab 里改成 **Scaling Mode = Shape**，层缩放只扩大出生范围，不放大粒子本身。Shape 模式会丢掉粒子节点自己的缩放对大小和运动的作用，所以要把节点缩放折算进 `startSize`、`startSpeed`、velocity 和 noise 的整条曲线；"两个常数之间随机"要两头都乘，`startSizeMultiplier` 只改上限。新出生的粒子大小 × `面积倍数^detailSizeExponent`（默认 0.3：×4 → ×1.52，×0.3 → ×0.70），发射量 × `面积倍数²`，限制在 `[1/maxDetailEmission, maxDetailEmission]`（默认 6）以内，保持密度。体型开始变化那一刻在场的细节粒子在 `handoffSeconds`（默认 0.8 s）内淡出；缩小过程中细节暂停发射，避免新粒子留在缩小后的区域外。
- **墨浪**（`scaleSurge`，可选）：一次性的环，1 颗 burst 粒子，colorOverLifetime 渐入渐出。插槽配置时它从层里拆出来，每次体型变化开始时重播，缩放 = 面积 × `surgeLead`（默认 1.075），始终领在区域边缘外面。变大时向外推，变小时向内收。

体型先变到哪一侧都一样：巨大化（1 → 4 → 1）和反噬（1 → 0.3 → 1）走同一套规则，变大一侧用 up 参数，变小一侧用 down 参数。层第一次放到施法者身上时，直接取当时的体型，不播衔接，所以巨人放出来的禅定缓流一开始就是大的。

**把一层改成 `Blend` 的步骤**：

1. 层上 `Caster Scaling` 选 `Blend`，把撑面积的系统拖进 `Area Systems`。
2. 其余系统改成 Scaling Mode = Shape，并把节点缩放折算回大小、速度、velocity、noise（第一次改禅定缓流用的是一段 `execute_code` 脚本，见 commit `fc29b054` 的说明；正常体型下用包围盒核对，烟的包围盒和改之前完全一致）。
3. 需要墨浪就复制层里一个环形系统，做成一次性的 1 颗粒子，拖进 `Scale Surge`。粒子系统数加 1，要同步改 `SkillVfxTests.ParticleSystemCeilings`。
4. 用巨人（×4）和反噬（×0.3）各看一遍变大、变回。

## 4. 验证

- EditMode：`SkillVfxScaleBlendTests`（规则；巨大化 ×4 与反噬 ×0.3 两个方向；Whole 不变；经过控制器时贴地偏移立刻跟体型、层缩放从旧值开始），墨浪只在窗口内播放，加上原有的 VFX 测试，共 27 个通过。
- 编辑器：在一个不保存的空场景里用真实 prefab、`SkillVfxPhaseController.Configure` 和 `FollowCasterScale` 逐帧步进渲染，对比现状、巨大化、反噬三栏动图。实测时序：变大 3.0 s 开始，3.5 s 到 ×3.5，4.0 s 到 ×4；变回 7.0 s 开始，8.0 s 回到 ×1；墨浪每次只有 1 颗粒子，活 1.1 s。
- 比赛实测（2026-11-03，单人赛 1 玩家 + 2 AI，`AITuningHarness` 起比赛，本地玩家直接调技能的 `ExecuteServer` + `ExecuteObservers`，`Time.captureDeltaTime` 固定 1/30 s，主相机每 2 帧录一张）：第 1 s 放禅定缓流，第 2.5 s 放巨大化，层缩放 2.67 s ×1.36，2.93 s ×3.35，3.47 s 到 ×4；换成反噬时 2.93 s ×0.65，3.47 s 到 ×0.3。两次都推了一圈墨浪。Console 里只有 dev 带来的 RaceMap Missing Prefab，没有特效报错。
- 比赛里发现并修掉（`b53166b5`）：巨大化 / 反噬比区域晚 1.5 s 结束，体型恢复时区域已经在散，墨浪单独推出一圈空环。现在墨浪只在层窗口内播，窗口结束后体型变化只缓动缩放。修复之后没有再进比赛拍"区域还在时恢复体型"（先放巨大化、再放缓流）这一段，测试 `SurgePlaysOnlyWhileTheWindowRuns` 覆盖了这一点。
- 测试台没有后期，背景是米色平地，颜色只能在比赛录像里看。

## 5. 还没适配（后续任务）

- 其他施法者锚点插槽（`cast`、`cast_anti`、`accel_release`、`reverse_hit`）还是 `Whole`。贴身的特效整体放大不一定错，要逐个看过再定。
- 拖尾宽度不跟体型（`accel_release`、`curtain_hit`）。
- 墨手的墨烟网格不跟体型。
- `CasterRelative` 不支持 `Blend`（只放一次）。
- `scaledRoot` 的配表 `scale_multiplier` 对施法者锚点层不起作用（第 1 节）。

这些都列在 [../gameplay/skill-adaptation.md](../gameplay/skill-adaptation.md) 的待办里。
