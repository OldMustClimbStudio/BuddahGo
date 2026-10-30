# 特效插槽总表

每个技能的特效拆成若干插槽 prefab，技能触发时按 vfx_id 实例化播放（[README 第 4 节](README.md#4-运行时结构设计待实现)）。本表记录每个插槽：做了什么、期望是什么、还差什么。用户逐个处理，做完一个就更新这里。已退休的旧内容在 [retired.md](retired.md)。

**配色**（README 第 2 节）：**正向** 用仪玄色板（墨底、金光、白峰值、外黑内亮）；**反作用**（被命中的受击、`_Anti` 反噬）用红色系（[anti-palette.md](anti-palette.md)）。

**时长**：插槽的 `referenceSeconds` 是调参时用的时长，层窗口用相位百分比，配表改时长后按比例缩放（README 第 4 节"时长缩放"）。所有已接线插槽都是 `Stretch`，参考时长等于现在的配表值；配表改了以后去 `Tools/BuddahGo/VFX/Skill Timing Sync` 看哪些特效变了速，见 README 第 4 节"时长同步"。

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
| 禅定缓流 释放 | `slow_release` | `禅定缓流/Release/VFX_Slow_Release` | 正向 | 服务器 `PlayVfxAll`；时长 = 配表区域时长（现在 6 秒），窗口整段 | 用户调过。2026-10-29：原来技能 SO 写死 11 秒、窗口前 55%（= 6 秒）手动对齐区域；改成跟配表，参考 6 秒、窗口整段，表现不变 |
| 转向反转 受击 | `reverse_hit` | `转向反转/Hit/VFX_Reverse_Hit` | 反作用（红） | 服务器给每个被反转的玩家播一次；时长 = 反转 10 秒，整段 | 用户调过；红色参考值取自这里 |
| 一念光明 路线拖尾 | `curtain_hit` | `一念光明/Hit/VFX_Curtain_Hit` | 功能性（黑幕里的路线） | `BlackCurtainViewController` 在本地给每个玩家播；时长 = 黑幕 13 秒 | 原角色身上的拖尾原样搬入，玩法信息，不是美术重做对象 |

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
| 禅定缓流 受击（被减速） | 反作用（红） | 区域随施法者移动 10 秒，进入的人被减速 2 秒；被减速的人现在没有表现 |
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
