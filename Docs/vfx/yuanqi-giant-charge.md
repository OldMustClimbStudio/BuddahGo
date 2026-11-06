# 金足破阵（缘起 #4）特效

> - **这是什么**：缘起「金足破阵」（神足通 + 法相金身，`Yuanqi_GiantCharge`）的特效方向记录。2026-11-06 用户和特效设计会话讨论定的方向，加上还没定、要到编辑器里对着看的项。还没有插槽、没有原型、没有代码。
> - **机制以哪里为准**：玩法见 [../gameplay/yuanqi.md](../gameplay/yuanqi.md)（#4），构筑和表现接口见 [../gameplay/yuanqi-architecture.md](../gameplay/yuanqi-architecture.md) 第 4.7、5.1 节。本文只写画面。
> - **Notion**：「技能 Combo 缘起机制」里这一条的视觉写的还是「后续决定」，没有同步过去。

## 1. 画面要服从的玩法事实

| 事实 | 现值 / 出处 | 对特效的意思 |
|---|---|---|
| 触发时刻 | 第二招 1 秒前摇结束、生效那一刻（前摇期间已经有通用前摇 `cast` 的符落 + 光柱） | 起手不用再做蓄力，直接是释放 |
| 突进 | 水平速度沿车头直接设到上限 +60（基础 80 → 140），0.8 秒内按 剩余比例² 回落（`YuanqiEffectGiantCharge`） | 主体在 0.8 秒内完成；按占位值粗算这 0.8 秒走约 80 个单位 |
| 方向 | 只在起步那一刻沿车头；之后 0.8 秒里仍能转向 | 一次画好的路线可能和实际路径分开 |
| 撞击 | 只在这 0.8 秒、每人一次，M = 3 × 推掌 100；弹开方向 = 施法者指向被撞者（Q9） | 撞点暂不表现（第 3 节第 5 项） |
| 被缓流减速（Q15） | 突进从减速后的上限起步 | 路线长度如果按实际速度算，会变短 |
| 施法者体型 | 金身 ×4，变大过程 0.35 秒；先放神足通、后放金身时，触发那一刻体型还在变 | 施法者锚点层默认整体 ×4（[giant.md](giant.md)） |
| 神足通被消耗 | `ExecuteConsumedObservers` → `StopSlotLocal("accel_release")` → `BeginDissipation`；该层 `clearOnDissipation = 0`，停止发射、已放出的粒子播完 | 拖尾当场断掉，缘起特效要接上这一下 |
| 已有表现 | 施法者相机 FOV +8°，保持 0.15 秒、0.65 秒回落；`YuanqiToastUI` 弹缘起名 | 保留 |
| 表现接口 | `SkillExecutor.YuanqiObserved`（载荷带水平 `Direction`）、`YuanqiImpactObserved`（撞点、冲量）、`RacerSkillPresentationState.BurstChanged` / `BurstSecondsLeft` | 各端都有，不需要新 RPC |

## 2. 已定（用户 2026-11-06）

方向来自两个已有素材的组合：神足通 `accel_release` 里的 Burst Shockwave（[yingyang-vfx-analysis.md 3.6](yingyang-vfx-analysis.md)），加上包里的 Frontal Pulses（[3.3](yingyang-vfx-analysis.md)，比赛插槽里还没用过）。用户看了两张预览帧后说"这个结合其实还不错"。

- **起手：后喷**。触发那一刻，神足通原有的冲击波放大，锥口朝后，从巨人身后喷出去。意思是"神足通被金身放大了，从后面猛推一下"，同时盖住神足通特效被切断的那一下。
- **路线：接力，留一条墨路**。同一刻在地上铺一条 Frontal Pulses，从起点朝车头方向，巨人顺着它冲出去；线留在原地，不跟人走。
- **一次画好**：触发时整条线就出来（带预告：这条线上会被撞飞），不是边冲边铺。
- **偏离（暂定）**：突进中转向导致人离开这条线，接受。线表示"冲出去的那一刻朝哪里"，寿命短，偏一点不碍事。觉得别扭再考虑缩短线，或者人离线后线加速消失。

讨论时用到的外部参考：Sion 大招和 Reinhardt 冲锋（大体型冲撞，身前压力面、撞点集中爆点）、相扑立合（踏地）、Rocket League 的超音速拖尾和撞人爆炸、动画冲击帧（命中 1–3 帧黑白反差，和墨白色板一致）、大神（Okami）的笔触、Ink Sword Slash 包。最后选的方向主要用了"从后面猛推"和"预告冲撞路线"两点。

## 3. 待定

除了第 5 项，都要在编辑器里把素材放到巨人体型、比赛视角下看过再定。驱动 Unity 前先问用户，并且要等编辑器没人在用。

1. **线怎么出现**：瞬间出现（像盖印），还是 0.1–0.15 秒内一笔画出（毛笔拉开）。画得慢会被 140 的速度追上，所以最多 0.15 秒。
2. **线上留什么**：主体是 Pulses 的脉冲组（贴地墨冲面片、往前的流线、白色碎点）。循环组原本 6 秒，超过残留上限 1.5 秒（README 第 3.3 节），墨山 / 墨树 / 竹林剪影里留哪一样、截多短要定。竹叶是绿的，按正向色板换成金或墨。
3. **颜色**：Pulses 黑墨重，铺在深紫赛道上可能读成一块黑（禅定缓流已经出现过，[slots.md 1.1](slots.md)），白边和流线要提亮多少。还要和禅定缓流的地面墨区分开，避免对手误以为是减速区：靠长条形状、朝前的流线和不超过 1.5 秒的寿命。
4. **尺寸与朝向**：
   - 线的长度和宽度按路线来定（约 80 单位，宽度跟巨人身宽），不跟体型整体 ×4；Pulses 原尺寸二十多个包单位长，叠上 ×4 会大得离谱。长度固定成名义距离，还是按实际起步速度算（Q15 时变短），待定。
   - 冲击波在 `accel_release` 里现在朝哪个方向，文档里没有，要在编辑器里看；后喷要锥口朝后。×4 以后在比赛视角里大不大。
5. **先放金身时会不会喷两次**：这个顺序下神足通一生效就被消耗，`accel_release` 在同一刻生成又进入消散，它自己的冲击波可能照常炸一次（部分子系统延迟 0.1 秒，可能放不出来），和缘起的后喷叠在一起。要实测，再决定只留一个（缘起复用它，或者这种顺序下压掉它）。
6. **撞点**：这个方向只管路线。撞点要不要补一个小标记，最后再定；它落在被撞的人身上，按 README 第 2 节算受击，要用红。

## 4. 做的时候要接的地方（实现前的提示，不是方案）

- 缘起目前没有播插槽的入口：`Yuanqi_GiantCharge.PlayObservers` 只做 FOV 抖动。要在缘起 SO 上加 vfx_id，在 `PlayObservers` 里经 `SkillVfxReplicator` 播。
- 新插槽照 README 第 4、7 节登记四处（`SkillReleaseProto.Skills`、`Skill.xlsx` VfxRegistry、`SkillVfxDatabase`、`VfxPackVendor.EffectPrefabs`），入库前跑 `Copy Used Pack Assets Into Project`。
- 路线层按出手时摆一次、之后留在原地（`CasterRelative`）可以做"一次画好"；`CasterRelative` 不支持 `Blend`（[giant.md](giant.md) 第 5 节），尺寸要另外按路线定。
- `feat/yuanqi` 上目前只有禅足冰封有程序化占位（`Yuanqi/Presentation/FrostPlaceholder.cs`、`FrostGroundPlaceholder.cs`，2026-11-06），金足破阵没有；挂接方式可以参考它。
- 先复用后新增（[retired.md](retired.md)）：起手复用 `accel_release` 的冲击波内容，路线复用包里的 Frontal Pulses。
