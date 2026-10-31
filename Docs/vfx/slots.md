# 特效插槽总表

每个技能的特效拆成若干插槽 prefab，技能触发时按 vfx_id 实例化播放（[README 第 4 节](README.md#4-运行时结构设计待实现)）。本表记录每个插槽：做了什么、期望是什么、还差什么。用户逐个处理，做完一个就更新这里。已退休的旧内容在 [retired.md](retired.md)。

**配色**（README 第 2 节）：**正向** 用仪玄色板（墨底、金光、白峰值、外黑内亮）；**反作用**（被命中的受击、`_Anti` 反噬）用红色系（[anti-palette.md](anti-palette.md)）。

**时长**：插槽的 `referenceSeconds` 是调参时用的时长，层窗口用相位百分比，配表改时长后按比例缩放（README 第 4 节"时长缩放"）。所有已接线插槽都是 `Stretch`，参考时长等于现在的配表值；配表改了以后去 `Tools/BuddahGo/VFX/Skill Timing Sync` 看哪些特效变了速，见 README 第 4 节"时长同步"。

**跟随与收尾**：跟着施法者的层三选一：`CasterAttached`（跟位置也跟朝向）、`CasterFollow`（只跟位置，朝向停在出手时）、`CasterRelative`（出手时摆一次，之后留在原地）。层勾 `particlesDieNaturally` 时，窗口结束那一刻层下全部粒子停止发射，插槽等最后一个粒子按寿命死完再销毁；不勾则窗口结束后固定 1.5 秒（× Stretch 倍数）销毁。要和技能同时消失的系统放进 `endWithWindow`：窗口结束前 `endWithWindowSeconds` 秒停止发射，并用这几秒播完它自己的淡出，窗口结束时正好消失（转向反转的阴阳符，1 秒）。见 README 第 4 节"跟随与收尾"。

**VFXtests 与比赛的高度差**：VFXtests 的 `Floor` 在 Player pivot 下 4.25，比赛赛道只在 pivot 下约 0.30（角色体型 1 倍）。贴地内容在 VFXtests 里放到 pivot 下 0.3 以下也看得见，比赛里会埋进赛道；贴地层的高度按比赛的 0.30 定。

更新于 2026-10-30。

## 1. 已有内容并已接线

| 插槽 | vfx_id | 文件 | 配色 | 谁播、播多久 | 现状 |
|---|---|---|---|---|---|
| 通用前摇 | `cast` | `通用前摇/Cast/VFX_Common_Cast` | 正向 | `SkillExecutor` 在施法确认延迟内对每个客户端、每个玩家播放；时长 = 该技能的 `castConfirmDelaySeconds`（参考 1 秒，`Stretch`） | 2026-10-29 用户重做：包里的 `Ying Yang Rune Fall`（缩放 2，用户调过颜色），直接挂在层下。之前复用的旧 `技能Particle.vfx` 已移除（[retired.md](retired.md)）。新内容还没进比赛验证 |
| 通用反噬前摇 | `cast_anti` | `通用前摇/Cast/VFX_Common_Cast_Anti` | 反作用 | 同上，反噬时播放 | 2026-10-29 用户重做：`Ying Yang Rune Fall` 的反噬版，直接挂在层下。之前复用的旧 `反噬Particle.vfx` 已移除。新内容还没进比赛验证 |
| 如来神掌 开火动画 | `palm_release` | `如来神掌/Release/VFX_Palm_Release` | 正向 | `ChargedHandProjectileRuntime` 出手时；`CasterRelative`，在施法者身上的固定点，朝出手方向 | 只有出掌爆发（包的 Ying Yang Frontal，用户已调色）这一层。和子弹 `palm_bullet` 一起播，用户 2026-10-30：两者相辅相成 |
| 如来神掌 子弹 | `palm_bullet` | `如来神掌/Bullet/VFX_Palm_Bullet`（墨手，资产会话做的） | 正向 | 同一次出手的第二个视觉副本，本身就是投射物：蓄力 `buildUpSeconds` 原地聚拢，然后按技能的飞行曲线飞 `projectileLifetimeSeconds`，判定盒和它走同一条路（[push-projectile-hands.md 第 15 节](push-projectile-hands.md#15-飞行速度与判定2026-10-30)）；命中不打断，播完为止 | 2026-10-30 接线。不是插槽（没有 `SkillVfxPhaseController`），时长由 `PalmInkTimeline.ConfigureForProjectile` 跟随技能，不在特效时长同步窗口里。还没进比赛验证 |
| 如来神掌 反噬 开火动画 | `palm_release_anti` | `如来神掌/Release/VFX_Palm_Release_Anti`（`VFX_Palm_Release` 的 prefab 变体） | 反作用（红） | 反噬弹幕出手时，每轮一个：层设为 `burstShared`，只有第 0 发的副本保留并移到整排中心 | 2026-10-30：只改了起始颜色（金 → 血红 `#950000` / 暗血红 `#6F0000`，亮金环 → 中灰 `#3C3C3C`，火星 → 黑火星，三层大面积加色光晕关掉，竹叶 → 墨 / 暗血红），映射见 [push-projectile-hands.md 第 15 节](push-projectile-hands.md#15-飞行速度与判定2026-10-30)。用户要调色在变体上改；改 FrontalBurst 本体会同步过来。还没进比赛验证 |
| 如来神掌 反噬 子弹 | `palm_bullet_anti` | `如来神掌/Bullet/VFX_Palm_Bullet_Anti`（墨手的 prefab 变体） | 反作用（红） | 反噬弹幕每发一只（配表 5 发），和判定盒同路线、同一条飞行曲线，蓄力 0.3 秒、飞 2 秒 | 2026-10-30：只改颜色：手的体积雾 墨 `#000000` + 薄雾血红 `#950000`、不发光；尾烟 墨 + 暗血红 `#6F0000`。还没进比赛验证 |
| 神足通 释放 | `accel_release` | `神足通/Release/VFX_Accel_Release` | 正向 | 服务器 `PlayVfxAll`；时长 = 配表加速时长（现在 3 秒），窗口整段 | 用户调过；含原来角色身上的加速拖尾。2026-10-29：原来按 10 秒调、窗口前 30%（= 3 秒），比赛里实际只给 3 秒，窗口只剩 0.9 秒；改成参考 3 秒、窗口整段，和调参时看到的一样 |
| 禅定缓流 释放 | `slow_release` | `禅定缓流/Release/VFX_Slow_Release` | 正向 | 服务器 `PlayVfxAll`；时长 = 配表区域时长（现在 6 秒），窗口整段；锚点 `CasterFollow`（只跟位置和体型，朝向固定为出手时的朝向），`casterOffset.y = 0.62`；区域结束那一刻全部粒子停止发射、按各自寿命自然消失（`particlesDieNaturally`，最长约 5 秒，随 Stretch 伸缩），死完才销毁 | 用户调过。2026-10-29：原来技能 SO 写死 11 秒、窗口前 55%（= 6 秒）手动对齐区域；改成跟配表，参考 6 秒、窗口整段，表现不变。2026-10-30 按用户要求调整（技能调整会话）：① 原来 `CasterAttached` 跟着施法者转，贴地网格整片旋转，改成只跟随（[README 第 4 节](README.md#4-运行时结构设计待实现)）；② 原来窗口结束后固定 1.5 秒就销毁，寿命 2–5 秒的粒子被瞬间切掉，改成自然消失；③ 比赛里地面只在角色 pivot 下 0.30，内容的 `Ying Yang pulse` 在 pivot 下 0.82，10 个粒子系统里 9 个埋在赛道下面看不见（VFXtests 的 `Floor` 在 pivot 下 4.25，所以那里全都看得见）；用层的 `casterOffset.y = 0.62` 整层抬起，最低的地面圈离赛道 0.10，层内相对位置不变。比赛里验证过（本地玩家与 AI 各放一次：区域 6.0 秒、施法者转 160–179° 时特效朝向不变、位置误差 0、结束后约 5 秒粒子死完再销毁、Console 0 错误）；双机联机没跑 |
| 转向反转 受击 | `reverse_hit` | `转向反转/Hit/VFX_Reverse_Hit` | 反作用（红） | 服务器给每个被反转的玩家播一次；时长 = 反转 10 秒，整段；`particlesDieNaturally`：反转结束那一刻 19 个粒子系统全部停止发射，按各自寿命自然消失 | 用户调过；红色参考值取自这里。2026-10-30 按用户要求改成自然消失（同禅定缓流）。比赛实测（转向反转反噬，播在本地玩家身上）：10.33 秒停止发射，烟、火星、墨浪约 3.5 秒内散完；阴阳符 `yy_1_symbol_alpha_soft` 寿命 12 秒（每 3 秒一发），剩下的 3 个符还挂在玩家上方，到 21.96 秒才死完，插槽 22.34 秒销毁，即反转结束后阴阳符还完整显示最多约 12 秒。用户定"符跟反转一起收"，第一版在反转结束时截短寿命，用户在 VFXtests 看了觉得"一下结束"。第二版按用户要求：技能结束前 1 秒开始消失，消失约 1 秒，和其他特效同步结束。阴阳符放进层的 `endWithWindow`，`endWithWindowSeconds` 1。同时把用户在 VFXtests 实例上对这个符的改动 apply 进 prefab：Simulation Speed 1→5（Start Speed 本来就是 0）、起始旋转 180°、关 3D 旋转。现在一个符实际活 2.4 秒、每 0.6 秒一发，自带淡出只有 0.24 秒。复测：9.47 秒符停止发射并开始淡出（新出生的那个从 0.41 往下，不跳亮），10.47 秒技能结束、其余层停止发射时符正好归零；其余层照旧自然散完，插槽 15.34 秒销毁，Console 0 错误 |
| 一念光明 路线拖尾 | `curtain_hit` | `一念光明/Hit/VFX_Curtain_Hit` | 功能性（黑幕里的路线） | `BlackCurtainViewController` 在本地给每个玩家播；时长 = 黑幕 13 秒 | 原角色身上的拖尾原样搬入，玩法信息，不是美术重做对象 |

### 1.1 比赛实测（2026-10-30，单人赛 1 玩家 + 2 AI，High Fidelity）

本地玩家按真实路径 `SkillExecutor.CastSlotServer` 放每个正向技能，反噬技能直接调技能的 `ExecuteServer` + `ExecuteObservers`，`cast_anti` 用执行器的反噬前摇 RPC 触发。全部插槽按表时长生成并销毁，Console 0 错误：

| 技能 | 生成的插槽 | 实测 |
|---|---|---|
| 神足通 | `cast`、`accel_release` | 1 s + 1.5 s；3 s + 1.5 s |
| 禅定缓流 | `cast`、`slow_release` | 区域 6 s，结束后约 5 s 粒子自然死完（共 11 s） |
| 转向反转 | `cast`、`reverse_release`（内容空）、两个 AI 身上各一个 `reverse_hit` | 10 s + 自然消失约 5 s（阴阳符在最后 1 秒淡出） |
| 一念光明 | `cast`、`curtain_release` | 13 s + 1.5 s；施法者看得见赛道边，所以不播 `curtain_hit`（设计如此） |
| 巨大化 | `cast`、`giant_release`（内容空） | 6 s + 1.5 s |
| 如来神掌 | `cast`、`palm_buff`（旧 prefab）、`palm_release`、墨手子弹 | 正常 |
| 转向反转 反噬 | 施法者身上 `reverse_hit` | 10 s + 自然消失约 5 s（阴阳符在最后 1 秒淡出） |
| 一念光明 反噬 | 三个玩家各一个 `curtain_hit` | 跟黑幕一起结束（约 3 s，黑幕本身只有展开 + 保持 + 淡出） |
| 如来神掌 反噬 | 5 个 `palm_release_anti`（只有第 0 个显示）+ 5 只墨手 | 正常 |
| 神足通 / 禅定缓流 / 巨大化 反噬 | 无插槽（vfxId 空，见第 3 节） | |
| 通用反噬前摇 | `cast_anti` | 1 s + 1.5 s |

**配表改时长实测**（用户问"配表改参数能不能让生效时长变化"）：比赛中把 `ProjectConfigDatabase` 里 `reverseturn_anti` 的 `effectDurationSeconds` 从 10 改成 5、再改成 15，调 `SkillDatabase.ApplyDesignTablesNow()` 重新套表后放转向反转反噬，跑完改回 10（`Skill.xlsx` → `Skill.csv` → 资产这一步没跑，属于配表流程本身）。

| 配表 | `invertDurationSeconds`（反转时长） | `reverse_hit` 停止发射 | 阴阳符 | 插槽销毁 |
|---|---|---|---|---|
| 5 s | 5 | 5 s | 4→5 s 淡出 | 7.65 s（Stretch ×0.5，尾巴 2.5 s） |
| 10 s（原值） | 10 | 10 s | 9→10 s 淡出 | 约 15 s |
| 15 s | 15 | 15 s | 14→15 s 淡出 | 22.69 s（×1.5，尾巴 7.5 s；最后可见粒子 21.03 s 消失） |

玩法时长和特效时长读的是同一个字段，Console 0 错误。第一次尝试中途出现 FishNet Yak 传输层 NullReferenceException、录制脚本丢失（像是 Play 中发生了脚本重载），原因没查；重跑一切正常。

**未决（用户定）**：禅定缓流在比赛里看起来是一大片黑。内容和 VFXtests 完全一样（11 个场景实例对 prefab 0 覆盖），shader 是 Unlit；差别来自环境：区域大多铺在深紫色赛道上（VFXtests 是浅米色地面，半透明的黑墨在那里显棕色），加上比赛的后期（关掉后期会明显变亮偏橄榄色）。另外比赛里插槽从空开始，约 4 秒才长满到 VFXtests 截图那样（6 秒区域的大半时间不满）。

## 2. 已接线、内容还空着

2026-10-29 接线：技能在 `ExecuteObservers` 里调 `SkillAction.PlaySlotOnCaster`，每个客户端在施法者身上本地播放，不加 RPC。比赛里验证过插槽按时长生成和销毁；内容是空的，放进内容后不用再改代码。

| 插槽 | vfx_id | 文件 | 配色 | 谁播、播多久 | 还差什么 |
|---|---|---|---|---|---|
| 巨大化 释放 | `giant_release` | `巨大化/Release/VFX_Giant_Release` | 正向 | `Skill_Giant.releaseVfxId`；时长 = 变大时长（6 秒） | 内容 |
| 转向反转 释放 | `reverse_release` | `转向反转/Release/VFX_Reverse_Release` | 正向 | `Skill_ReverseTurn.vfxId`（原来的 `reverse_turn_trap_vfx` 从没播过）；时长 = 反转时长（10 秒） | 内容 |
| 一念光明 释放 | `curtain_release` | `一念光明/Release/VFX_Curtain_Release` | 正向 | `Skill_BlackCurtain.releaseVfxId`；所有人都播，不受黑幕"谁被影响"的判断限制；时长 = 展开 + 保持 + 淡出（13 秒） | 内容。用户特意放进来的 `BlackCurtainTrail`（层 `BlackCurtainTrail`，带 `PlayerBlackCurtainTrail` 脚本）已应用进 prefab；窗口 0–77%（= 13 秒里的前 10 秒） |

## 3. 还没有插槽（期望）

| 插槽 | 配色 | 说明 |
|---|---|---|
| 如来神掌 蓄力 `Chg` | 正向 | 现在 `palm_release` 只有出掌那一层；蓄力阶段没有表现 |
| 如来神掌 Buf | 正向 | `palm_buff` 还指向实现会话第五轮的旧 prefab（`如来神掌/Buff/VFX_Palm_Buff`），要换成插槽 |
| 禅定缓流 受击（被减速） | 反作用（红） | 区域随施法者移动（配表现在 6 秒），进入的人被减速；被减速的人现在没有表现 |
| 神足通 反噬（自己被减速） | 反作用（红） | `_Anti` 版效果落回自己；目前只有通用反噬前摇。代码已有口：`Skill_Acceleration_Anti.vfxId`（2026-10-29 清空了已删的 `accel_anti_vfx`），填 id 即播 |
| 转向反转 反噬（自己被反转） | 反作用（红） | **已接线（复用）**：`Skill_ReverseTurn_Anti.vfxId = reverse_hit`，在施法者身上播被反转的受击，时长 = 反转时长 |
| 禅定缓流 反噬（先定身再加速） | 反作用（红） | 代码已有口：`Skill_SlowTrap_Anti.vfxId`（空），填 id 即播 |
| 巨大化 反噬（变小） | 反作用（红） | 代码已留口：`Skill_Giant_Anti.releaseVfxId`（空）。做好红色插槽后填 id 即可 |
| 一念光明 反噬 | 反作用（红） | 黑幕落到自己。代码已留口：`Skill_BlackCurtain_Anti.releaseVfxId`（空） |

各技能的反噬要不要单独做插槽，还是只靠通用反噬前摇加玩法效果本身，由用户逐个决定。

## 4. 做一个插槽的步骤

1. 在 `Assets/Scenes/VFXtests.unity` 里改对应的占位物体（`ReleasePlaceholder_*` / `HitPlaceholder_*`），把包里的特效拖进层下面的 `Content`。
2. 用 `Tools/BuddahGo/VFX/Proto/Release Placeholders/Write Release Pose (selected placeholder)` 写回位置；只改了内容和颜色的话，直接 Apply 到 prefab 也行。
3. 在插槽 Inspector 的 Timing preview 里检查不同时长下的节奏，选每层的 `timeScaling`。
4. 新插槽：在 `SkillReleaseProto.Skills` 加一行，运行 `Build All And Place In VFXtests`；在 `Skill.xlsx` 的 VfxRegistry 加一行并导入（导入完 Unity 会弹结果对话框，要点 OK，否则编辑器卡住、MCP 断开）；在 `SkillVfxDatabase` 加条目；把 prefab 路径加进 `VfxPackVendor.EffectPrefabs`；在技能代码里按 vfx_id 播放。
5. 入库前运行 `Copy Used Pack Assets Into Project`（README 第 7 节）。
