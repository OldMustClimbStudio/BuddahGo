# 缘起合入 dev 时还开着的项

> - **这是什么**：PR #75（`feat/yuanqi`）整体合入 dev 时，三对缘起（#1 金身巨掌、#2 禅足冰封、#4 金足破阵）还没做完、没验证或等用户决定的事，集中在一处，作为之后“精修技能特效”分支的起点。日期 2026-11-07，按当时的代码和文档核对。
> - **怎么用**：每一项只写一句现状和出处，细节以出处为准。做完一项就在出处改成结论，并从这里删掉；新发现的开放项也先记到这里。
> - **已经定了、不在这里的**：冰体用 v4、不加棱线（[禅足冰封特效](../vfx/yuanqi-frost-lock.md) 第 4 节）；#1 金身巨掌不另做特效，沿用神掌随掌倍率缩放的现有表现（[缘起技能设计](yuanqi.md) #1）；#4 金足破阵按第一版插槽交付，调参由用户自己在 VFXtests 里做（[金足破阵特效](../vfx/yuanqi-giant-charge.md) 第 5 节）。

## 1. 特效精修（用户在本地 VFXtests 里调）

VFXtests 是用户本地的场景，不入库。两个缘起的预览都由菜单放进去：`Tools/BuddahGo/VFX/Proto/金足破阵/Place Preview In VFXtests`、`Tools/BuddahGo/VFX/Proto/禅足冰封/Place Preview In VFXtests`，场景观感由 `Tools/BuddahGo/VFX/Proto/Apply RaceMap Look To VFXtests` 还原成 RaceMap。所有尺寸和亮度都要在比赛镜头下判断（金身 ×4 时镜头约在 390 m 外）。

### 1.1 #4 金足破阵（`giant_charge_release`，[yuanqi-giant-charge.md](../vfx/yuanqi-giant-charge.md) 第 3、5 节）

第一版的默认值都是会话给的，用户还没看过、没定：

- 线怎么出现：现在是约 0.12 秒一笔画出（包里的扫线 2.5 倍速），不是瞬间盖印。
- 线上留什么：只留白边墨块（`Tree_pulse_floor`），截到 1.5 秒；墨山、竹林没用。
- 颜色：沿用包的颜色；用 `startColor` 提不了亮，要提亮得改材质。还要和禅定缓流的墨区分开。
- 尺寸：路线固定约 80 m 长、25 m 宽，不按起步速度算（Q15 被减速时不变短），不跟体型缩放；后喷用 `accel_release` 的冲击波，尺寸 40（×1.25）、在巨人身后 16 m。先放神足通的顺序下，触发那一刻体型还是 ×1，后喷会占满画面，要不要跟体型（`Whole`）或缩小没定。
- 撞点标记：没做；要做的话按受击用红（[README](../vfx/README.md) 第 2 节）。
- 粒子预算：插槽 19 个粒子系统，`SkillVfxTests` 按 19 登记；[README](../vfx/README.md) 第 8 节的预算要用户定。
- “喷两次”已经实测（训练模式 S8）：先放金身时 `accel_release` 自己的冲击波没有出现，只有缘起的后喷。这一项不用再调。

### 1.2 #2 禅足冰封（`frost_lock_release`、`frost_lock_hit`，[yuanqi-frost-lock.md](../vfx/yuanqi-frost-lock.md) 第 5、8 节）

- 地面：“冰面 + 放射裂纹 + R 处的环”，0.12 秒铺开、最后 25% 淡出；节奏没和用户对过。
- 颜色：全是中性白。冰要不要加一点淡青；受害者身上的冰是否按受击用红，都没定（[slots.md](../vfx/slots.md) 的 `frost_lock_hit` 一行同样写着待定）。
- 冰块出现和解冻：0.09 秒从 88% 长到全尺寸，最后 0.22 秒化掉、掉 24 颗碎冰；解冻预警（裂纹按剩余时间生长）没做。
- 冰的造型：还是小样的测试形（包住身体包围盒的大晶块加九根斜刺），正式造型要围绕“头 + 双手”。
- 坡道：地面是平面面片，坡上会悬空或穿插（工程没有 URP decal）。
- 改了半径 R（`YuanqiEffectFrostLock.radiusMeters`）要重跑 `FrostLockProto`，否则环不在 R 上（`SkillVfxTests.FrostLockGroundRimStandsAtTheFreezeRadius` 会报）。`FrostLockProto` 重建会冲掉手调。

### 1.3 两对共同没看过的

- Balanced / Performant 画质档。
- 禅足冰封在金身反噬缩小（×0.3）的人身上的冰块。金足破阵在这个体型下不会触发，不用看。
- AI 触发这两个缘起时的画面。
- 每次 Play 里第一次播新插槽时编辑器卡约 0.9 秒（推测是着色器变体首次编译，没查）；打包后是否也卡没测（[训练模式验证记录](practice-verification.md) 2026-11-06 一节）。

### 1.4 #1 金身巨掌

不做专属特效（已定）。已知的相关问题记在 [../vfx/giant.md](../vfx/giant.md) 第 5 节和 [skill-adaptation.md](skill-adaptation.md)：×4 墨手的墨烟网格不跟体型（按代码推断，没实测），×4 墨手离相机近时体积渲染开销大。

## 2. 没验证的

- **联机两端**：远端客户端的冻结、突进、反噬限速的预测和 reconcile，缘起表现在各端是否一致（两个插槽都由 `PlayObservers` / `FrozenChanged` 在各端本地播，没有新 RPC），黑幕在对方屏幕上的效果。[README §7.3](README.md#73-阶段-c验证) 的联机一行仍是“未做”。
- **用户试玩**：手感、数值和画面。用户的安排是等所有技能都设计、实现完再统一试玩。
- **训练模式没覆盖的点**：#2 先神足通、后缓流时的消耗；复活后状态板清空、缘起再触发；冻结中被推（[训练模式验证记录](practice-verification.md) 的“没有验证的”）。

## 3. 等用户决定

- **缘起的数值**：#2 的禁锢 N（1.5 秒）、半径 R（12 米），#4 的突进（上限 +60、0.8 秒）和撞击 M（3），#4 施法者的 FOV 抖动（8 度）。都是阶段 A 的占位值，在配表或 SO 字段上，试玩后调（[README §8](README.md#8-待定清单)）。
- **缘起效果算不算控制**：#2 的禁锢是否计入受害者的控制上限（[缘起技能设计](yuanqi.md) 的“缘起相关的规则”）。
- **AI 的缘起参数**：`AI/SkillTuning` 的 `yuanqi*` 几列、`Personality.yuanqiAffinity` 都是建议（实现采用）（[README §8](README.md#8-待定清单)）。
- **SO 和配表的数值不一致**：神足通、缓流、神掌 SO 的时长和冷却，以及反噬变体 SO（`Skill_Acceleration_Anti` 时长 6 而表是 3，`Skill_BlackCurtain_Anti` 执念 100 而表是空）。运行时以表为准；同步 SO 还是删掉这些被表接管的字段，等用户定（[退役记录](retired.md) 第 3、6 节，#74 review 第 6 项）。
- **SoloCJK 字体子集重烘**：比赛 HUD 里“冻结”、缘起名还显示英文或 id（[README A6](README.md#a6-hud)）。菜单技能页的缘起列表用的是另一套字体（MenuSerif），已经补字。

## 4. PR #74 code review 的剩余项

出处：[PR #74 的 code review 评论](https://github.com/OldMustClimbStudio/BuddahGo/pull/74)（2026-11-06）；第 1–4、7、8 项已由 #77 修掉。

| 项 | 现状（2026-11-07） |
|---|---|
| 5 dev 退役清单里“待删 [PR#63]”的死代码 | 还在（`IntroSequenceManager` 的时间线 handler、`CreatePushGrace` 等工厂方法、`PlayerCamera.SetRuntimeOffset`、`HandPushProjectileRuntime.cs` 等，[退役记录](retired.md) 后半“确定没用、等 PR #63 合并后再删”）。PR #63 已关闭、#74 已合入，等用户定在哪个 PR 删 |
| 6 两处 SO 与配表的漂移 | 已记进 [退役记录](retired.md) 第 3、6 节，等用户定（见上面第 3 节） |
| 9 `allowSelfHit` 的说法过时 | 已改（退役记录第 2 节，2026-11-07） |
| 10 设计表工具文档过时、旧生成器没记退役 | 已改 `Tools/design-tables/README.md`，`build_tables.py` / `v2_layout.py` 记进退役记录第 9 节（保留，过时勿用） |
| 11 预测修饰入口在 #74 里没有调用方 | 随 #75 解决：`ApplyBurstToOwner`、`EndAccelerationForOwner`、`EndScaleForOwner`、`BurstChanged` 都由缘起调用 |
| 12 S14 的计时 | 随 #75 解决（bfa14498） |
| 14 配表审查里其余 [机制] 条目 | 没有逐条复核（M1.1、M1.4–6、M3–M10、M13、L1、L3、L5、L6、L11、L12） |
| 15 客户端可能多打“inferred anti”警告 | 还在（`SkillExecutor` 的 `ResolveAntiSkillId`）；现在每行都配了 `antiSkillId`，只是潜在噪音 |

## 5. 其他

- **其余缘起对**：15 对里只设计了 3 对，其余等用户设计（[缘起技能设计](yuanqi.md)）。这不是遗留。
- **`PropertySelectionEnd.playable`**：选择场景退役后没有任何引用，还留着它在 `Global/Timeline`、`UI/UiTiming` 的两行（[../ui/retired.md](../ui/retired.md)）。
