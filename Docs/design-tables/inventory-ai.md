# 配表筛查清单：AI 对手（驾驶档位、技能人格、机会谓词）


筛查日期 2026-10-12，基线 dev 9ff6338。本文件只做盘点，不含实现。总览与跨域结论见 [README.md](README.md)。路径相对 `Assets/Scripts/` 或仓库根。

> 2026-10-26 玩法深化之后（代码 0dbc551）：AI 适配（玩法 README A7）没有做，AI 的表与读取方都没改，下文仍按基线成立，但这几处的前提变了：AI 仍经 `ComboSkillInput.InjectServerKey` 逐键搓招，所以 `Skill/ComboInput`、`Skill/ComboBinding` 只剩 AI 在读，按键时长约束（`keyMaxSeconds` 小于 `inputWindowSeconds`）也只对 AI 有意义；`BacklashSeverity` 按技能区分反噬代价，统一反噬之后已过时（反噬对每个技能都一样）；去掉全局施法锁后 AI 可能叠放技能、放得更频繁；AI 不做缘起决策，但会偶然触发；手部瞄准估算读 `BuddahHandControl.HandRotationSpeed`（= `Push.basic.rotationSpeedDegPerSec`，也是鼠标转手上限）。A7 完成后按玩法 README 7.2 删 `ComboBinding / ComboInput` 与搓招相关列、补缘起决策参数。
## 结论先行

- 共有两份真正被运行时读取的配置源：
  1. 驾驶档位：`Assets/Resources/AI/{Easy,Normal,Hard}.asset`。由 `Tools/ai/difficulty-*.json` 生成，36 个字段两边完全一致。
  2. 技能 AI 配置：`Assets/Resources/AI/SkillPersonalities.json`，包含人格、技能难度和机会调参。
- 其余数值全部硬编码在 C# 里：机会谓词、局势和目标评分、承诺状态机、快照派生量、规划器常量。
- 没有 AI 专属的追赶机制，没有自定义卡住恢复或传送。没有名为 fumble 的独立概念，对应的是驾驶 `MistakeProbability`、`AngleNoiseDegrees` 和搓招 `KeyMistakeProbability`。追赶只存在于对全体赛车生效的 `ObsessionFigure.catchupRecoveryA`。
- AI 名字列表不存在。AI 显示名取自 JSON 的 `DisplayName`（P1–P5 占位名），`SoloSetupPanel` 从不传 `AINames`。
- 运行时权威：驾驶以 `.asset` 为准（`Resources.Load`），`Tools/ai/*.json` 只是 Editor 生成源，代码字段初始值只在 asset 缺失时兜底且与 asset 差异很大。技能以 `SkillPersonalities.json` 为准（`JsonUtility`），代码初始值只在 JSON 缺字段时生效。

路径缩写：P=AI/AIDifficultyProfile.cs，PS=AI/AIDifficultyProfiles.cs，DRV=AI/AIRacerDriver.cs，TVP=AI/ThrustVectorPlanner.cs，SRL=AI/SplineRacingLine.cs，TWP=AI/TrackWallProbe.cs，CAT=AI/AISkillCatalog.cs，DEC=AI/AISkillDecision.cs，CST=AI/AISkillCaster.cs，CMT=AI/AISkillCommitment.cs，WLD=AI/AISkillWorld.cs，SCH=AI/AISkillDecisionSchedule.cs，JSON=Resources/AI/SkillPersonalities.json，ASSET=Resources/AI/{Easy,Normal,Hard}.asset，TJ=Tools/ai/difficulty-{easy,normal,hard}.json，TOOL=Assets/Editor/AIDifficultyAssetTool.cs，RID=Match/Contracts/RacerId.cs，SMS=Match/Contracts/SoloMatchSettings.cs，MSM=Network/Match/MatchSpawnManager.cs，SSP=Match/UI/SoloSetupPanel.cs。数值写作 E/N/H。

## 表 1：AI_DrivingTier（每档一行，主键 Difficulty）

运行时读取 ASSET：`PS.Load:18`，`PS.Resolve:30` 为每车实例化一份。生成链 TJ → `TOOL.Build:11-28`。范围校验写了两处：P 的 `[Range]/[Min]` 与 `P.ValidateConfiguration:56-70`。

设计概念映射：刹车许可=`MaxThrustAngleDegrees`（≤90 不刹车，H=90）；避墙=`UseWallCorridor/WallMargin/RolloutWallWeight/RolloutCenterWeight`；规划精度=`LookaheadSeconds`、Rollout*、`ThrustReplanTicks`；每车随机=`SpeedNoise/LateralOffsetNoise/NoiseSeed`；驾驶失误=`MistakeProbability/AngleNoiseDegrees/WobbleTicks`；反应=`ReactionTicks+ReactionJitterTicks`；横向线偏=`LateralOffset+LateralOffsetNoise`。

| 字段 | 代码位置(def) | ASSET 行 | E/N/H | 读取方 | 建议列:类型 | 备注 |
|---|---|---|---|---|---|---|
| TargetSpeed | P:50 (80) | :48 | 67/74/80 | PS.Resolve:40, TVP.Plan:208,245, TVP.Rollout:177,192 | TargetSpeed:float m/s | Resolve 硬 clamp [20,80] |
| LateralOffset | P:51 (0) | :49 | 0/0/0 | PS.Resolve:41, TVP.Plan:240 | LateralOffset:float m | 差异全来自 Noise |
| ReactionTicks | P:53 (0) | :50 | 8/3/0 | DRV.TryGetOverride:159-160; 视野受损时 DRV:53 叠加 | ReactionTicks:int 0–30 | |
| ReactionJitterTicks | P:39 (0) | :38 | 5/3/0 | DRV.ReactionJitter:60-65 | ReactionJitterTicks:int | 特性 0–30，校验无上限 |
| MaxThrustAngleDegrees | P:48 (170) | :47 | 170/170/90 | TVP.Plan:267,299,319,324 | MaxThrustAngleDeg:float 90–178 | 即"是否允许刹车"；测试要求 H ≤ N |
| PlanningBrakeAcceleration | P:17 (10) | :22 | 10×3 | TVP.Plan:208 → SRL.PreparePace:104-129 | PlanningBrakeAccel:float | |
| ThrustPaceFactor | P:16 (.9) | :21 | .9×3 | 同上 | ThrustPaceFactor:float 0–1 | |
| LookaheadSeconds | P:9 (1) | :15 | 1×3 | TVP.Plan:244; 视野受损 DRV:50 相乘 | LookaheadSeconds:float | |
| SpeedMargin | P:10 (2) | :16 | 2×3 | TVP.Plan:248,261, Rollout:196 | SpeedMargin:float | |
| LateralDamping | P:11 (1) | :17 | .986×3 | TVP.Plan:258, Rollout:194 | LateralDamping:float | def≠asset，偏工程 |
| PredictionGain | P:15 (.15) | :20 | .3×3 | TVP.Plan:247,255-258 | PredictionGain:float | def≠asset |
| AttitudeDeadbandDegrees / HysteresisDegrees | P:12-13 (1.5/.5) | :18-19 | 1.5/.5×3 | TVP.Plan:266-267 | 高级列组 | 偏工程 |
| RolloutSeconds | P:19 (2) | :23 | 1.8/2/2 | TVP.Plan:276,283; DRV:51 | RolloutSeconds:float | |
| RolloutHoldSeconds | P:20 (.6) | :24 | .6×3 | TVP.Plan:285 | | |
| ThrustReplanTicks | P:21 (2, Range1–6) | :25 | 3/2/2 | TVP.Plan:272-274 | int | 不进校验 |
| RolloutSubsteps | P:22 (2, Range1–4) | :26 | 2×3 | TVP.Plan:280 | int | 不进校验 |
| RolloutSampleTicks | P:40 (6) | :39 | 6×3 | TVP.Plan:284 | int | |
| RolloutTail | P:31 (0, Range0–2) | :31 | 1×3 | TVP.Rollout:186-187 | enum{Anchor,KeepCandidate,FullThrust} | def≠asset，不校验 |
| SwitchPenaltyPerDegree | P:47 (.3) | :46 | .3×3 | TVP.Plan:300 | float | |
| RolloutLateralWeight / LateralVelocityWeight | P:41-42 (1/.7) | :40-41 | 1/.7×3 | TVP.Rollout:175,183 | float | UseWallCorridor=true 时 LateralWeight 不生效 |
| RolloutOverspeedWeight | P:43 (1) | :42 | 0×3 | TVP.Rollout:184 | float | def≠asset，三档全 0 即关闭 |
| RolloutUnderspeedWeight | P:44 (1) | :43 | 1/1/3 | TVP.Rollout:184 | float | |
| RolloutProgressWeight / TerminalWeight | P:45-46 (1/1) | :44-45 | 1/1×3 | TVP.Rollout:181,185 | float | |
| UseWallCorridor | P:24 (false) | :27 | true×3 | TVP.Rollout:153 | bool | def≠asset；测试要求三档 true |
| WallMargin | P:25 (1.5) | :28 | 1.5×3 | TVP.Rollout:159 | float m | |
| RolloutWallWeight | P:26 (.2) | :29 | .7/.2/0 | TVP.Rollout:173 | float | 测试要求 E≥N≥H |
| RolloutCenterWeight | P:27 (.02) | :30 | .02×3 | TVP.Rollout:173 | float | |
| SpeedNoise | P:34 (0) | :33 | 6/4/2 | PS.Resolve:40 开局每车抽一次 ± | float m/s | 受 [20,80] clamp，H 档只会变慢 |
| LateralOffsetNoise | P:35 (0) | :34 | 3.5/2.5/1.5 | PS.Resolve:41 | float m | |
| AngleNoiseDegrees | P:36 (0) | :35 | 10/5/2 | TVP.Plan:315-319 高斯摆动 | float | |
| WobbleTicks | P:37 (30, Range1–120) | :36 | 30×3 | TVP.Plan:317 | int | 校验无上限 |
| MistakeProbability | P:38 (0) | :37 | .25/.1/.03 | TVP.Plan:310 改选次优; DRV:52 叠加 | float 0–1 | 驾驶层"失误"(Fumble) |
| NoiseSeed | P:33 (0) | :32 | 0×3 | PS.Resolve:39 覆盖 | 不进表 | 运行时派生，TJ 中无此字段 |

## 表 2：AI_DrivingGlobal（全局常量，全部硬编码；[Eng]=偏工程）

- TargetSpeed clamp 区间 `[20,80]` — PS.Resolve:40 — 建议 TargetSpeedMin/Max:float — 80 是写死的车辆上限假设，与 TVP TopSpeed 79 / TopPace 78、SRL 80 m 窗口耦合。
- 每车种子公式 — PS.Resolve:33,35 — [Eng]。ResourcePath "AI/" — PS:15 — [Eng]。
- 缺失 Profile 兜底 — DRV.TryGetOverride:124-129 回退 Normal；PS.Resolve:25-29 asset 缺失返回字段默认值。
- RecoverySpeed 5 m/s、RecoveryVelocityErrorDegrees 90° — TVP:26 — TVP.Plan:220, Rollout:195 — 建议 AI_DrivingGlobal.RecoverySpeed / RecoveryVelErrDeg — 低速/倒行时沿赛道全推（AI 唯一"卡住恢复"）。
- [Eng] SideSwitchLateral 3 / HoldLateral 18 (TVP:27)；TopSpeed 79 / TopPace 78 (TVP:28，与车速 80 耦合)；MinBrakeAngle 120 / MaxTarget 178 (TVP:29)；AttitudeAcceleration 4.82 (TVP:30)；Min/MaxPredictionSeconds .3/1 (TVP:31)；Reanchor 200/180/360 (TVP:32)；ReprojectLateral 15 m、CollisionMemorySeconds .5、WobbleClampSigmas 3 (TVP:33)；VelocityHeadingMinSpeedSquared 25 (TVP:34)；搜索段数 8/40/6 (TVP:35)；FixedCandidates {0,±30,±60,±90,±120,±170} (TVP:23)；AnchorOffsets {±5,±10} (TVP:24)。
- [Eng] 赛车线采样间距 2 m、最少 16 采样 (SRL.Capture:75,77)；曲率弦跨 12 m (SRL:113,149)；WindowPace 80 m (SRL:132，与车速耦合)；墙探测 MaxDistance 80 / Height 1.5 (TWP:11)。
- [Eng] 驾驶器：侧碰 |n.y|<0.7、地面 n.y≥0.99 (DRV:90,96)；默认摩擦 0.6 (DRV:98-99)；地面接触有效期 0.1 s (DRV:142)；模型对比窗口 60 tick (DRV:169)。

## 表 3：AI_SkillDifficulty（每档一行；JSON:10-12；读取 CAT.ForDifficulty:92-96；代码默认 CAT:31-45）

| 字段 | E/N/H | 代码默认 | 读取方 | 建议列 | 校验范围 (CAT) |
|---|---|---|---|---|---|
| RiskWeight (:32) | 0/.5/1 | 0 | DEC.Utility:152 | RiskWeight:float | 0–2 (:132) |
| SevereStopProbability (:33) | 1.01/.7/.45 | 1.01 | DEC.Utility:146 (severity≥.5) | SevereStopProb | 0–1.01，1.01=无停手 |
| MildStopProbability (:34) | 1.01/1.01/.65 | 1.01 | DEC.Utility:146 | MildStopProb | 0–1.01 |
| DecisionSeconds (:35) | .3/.25/.2 | 0 | CST.Decide:159-160 | DecisionIntervalSec | .1–2 |
| ReactionMin/MaxSeconds (:36-37) | .6–1/.3–.5/.1–.2 | 0 | CST.SampleReaction:260, CST.Tick:148-149 | ReactionMinSec/MaxSec | 0–1 / Min–1.5 |
| KeyMistakeProbability (:38) | .15/.06/.01 | 0 | CMT.NextKey:35, CST.Decide:172 | KeyMistakeProb | 0–1 |
| AimToleranceDegrees (:39) | 20/12/6 | 0 | CST.Aim:206-207 | AimToleranceDeg | 1–45 |
| AdaptSeconds/ReadaptSeconds (:40-41) | 2,1/1,.6/.4,.3 | 0 | DRV:147-148 → AISteeringPerception.Observe (CMT:62-72) | ReverseAdaptSec/ReadaptSec | 0–5 |
| ImpairedLookaheadMultiplier (:42) | .5/.6/.75 | 0 | DRV:50-51 乘 Lookahead 与 Rollout | BlindLookaheadMul | .1–1 |
| ImpairedMistakeAddition (:43) | .2/.1/.05 | 0 | DRV:52 | BlindMistakeAdd | 0–1 |
| ImpairedReactionTicks (:44) | 10/6/3 | 0 | DRV:53 驾驶 + CST:148 技能 | BlindReactionTicks:int | 0–30；一字段两用 |
| DecisionNoise (:45) | .1/.05/.02 | .05 | CST.Decide:174 乘性噪声 | DecisionNoise | 0–.5 |

驾驶 ReactionTicks 叠加后无 30 上限再校验（E 档最大 8+10=18）。视野受损触发源在 AI 范围外（黑幕时长）。

## 表 4：AI_Personality（每人格一行；JSON:3-7；CAT.Validate:105-126 要求恰好 5 个 = RacerId.MaxAI）

| Id | DisplayName | Loadout 槽0/1/2 | Patience θ | TempoSeconds τ | Target | ImpairedTargetMul | HandsShots |
|---|---|---|---|---|---|---|---|
| keeper | P1 守位型 | slowtrap/acceleration/giant | .25 | 1.5 | 1=Behind | 1 | 0 |
| hunter | P2 追猎型 | push_projectile_hands/acceleration/reverseturn | .25 | 1.2 | 0=Ahead | 1 | 4 |
| brawler | P3 车群型 | giant/push_projectile_hands/slowtrap | .3 | 1.0 | 2=PackCentre | 1 | 3 |
| trickster | P4 干扰型 | reverseturn/blackcurtain/slowtrap | .3 | 2.0 | 3=EnteringCorner | 1 | 0 |
| opportunist | P5 机会型 | acceleration/push_projectile_hands/blackcurtain | .35 | 1.5 | 4=Impaired | 1.6 | 2 |

字段：Id (CAT:15) 主键，仅 Telemetry 用；DisplayName (CAT:16) 由 MSM.SpawnSoloAI:166 作 AI 显示名（应走本地化）；Loadout[3] (CAT:17) 由 MSM:170 SetSlotsServer、CST.Configure:60 读取，外键技能表，槽 0/1 三键招、槽 2 四键招，CAT.Validate:116-119 对照 Selectable 池；Patience (CAT:21) / TempoSeconds (CAT:22) 由 DEC.CastProbability:155-156 读取，范围 0–.6 / .5–4；Target (CAT:23) 由 DEC.TargetScore:179-192 读取，JSON 存整数建议改枚举名；ImpairedTargetMultiplier (CAT:24) DEC.Utility:149-151 只对 Acceleration/Hands 生效，1–2；HandsShots (CAT:25) CST.Aim:197，代码 0–6 文档 1–6。

### 表 4b：AI_PersonalitySkillWeight（Weights[6]，CAT:19，按 AISkillKind 顺序 [Acceleration, SlowTrap, Giant, Hands, Reverse, Curtain]；读取 DEC.Utility:148）

| 人格 | Acc | SlowTrap | Giant | Hands | Reverse | Curtain |
|---|---|---|---|---|---|---|
| keeper | 1 | 1.3 | .8 | 0 | 0 | 0 |
| hunter | 1 | 0 | 0 | 1.3 | .9 | 0 |
| brawler | 0 | 1 | 1.3 | .9 | 0 | 0 |
| trickster | 0 | .8 | 0 | 0 | 1.2 | 1.2 |
| opportunist | 1.2 | 0 | 0 | 1 | 0 | .8 |

不在配装里的技能权重是死数据；数组按枚举序号索引，配表应改用 SkillId。

### 表 4c：AI_PersonalitySituationBias（SituationBias[5]，CAT:20，顺序 [Pursued, LeadingAlone, Chasing, Pack, TrailingAlone]；读取 DEC.Utility:148）

| 人格 | Pursued | LeadingAlone | Chasing | Pack | TrailingAlone |
|---|---|---|---|---|---|
| keeper | 1.4 | .5 | .9 | 1 | 1 |
| hunter | .8 | .6 | 1.4 | 1 | 1.1 |
| brawler | 1 | .4 | .9 | 1.5 | .4 |
| trickster | 1.1 | .7 | 1.2 | 1.2 | 1 |
| opportunist | 1 | .8 | 1.2 | 1 | 1 |

与 ai-personalities.md 逐项核对一致。

## 表 5：AI_SkillOpportunity（每技能一行）

技能 Id ↔ 枚举映射写死在 CAT.SkillIds:76-77：acceleration, slowtrap, giant, push_projectile_hands, reverseturn, blackcurtain → AISkillKind 0–5。

- BacklashSeverity[6] — JSON:14，代码默认 CAT:65 相同 — Acc 1, SlowTrap .1, Giant .5, Hands .3, Reverse .9, Curtain .4 — DEC.Utility:145-152 — BacklashSeverity:float 0–1。
- 严重/轻微分界 `severity >= .5` — DEC:146 — AI_SkillGlobal.SevereSeverityThreshold。
- 全局效果可见时不叠加 — DEC:70-71、CST.CanContinueCast:82、WLD.IsGlobalEffectVisible:121-133 — BlockWhenGlobalVisible:bool，只对 Reverse/Curtain。

各谓词硬编码：
- Acceleration (DEC:72-78)：`StraightSeconds < 3` 不放 → MinStraightSec=3；分=StraightFraction；有效期=StraightSeconds；已加速时不放。
- SlowTrap (DEC:93-96)：gap<0、-gap≤TrapDistance、relative>.1、|ΔLateral|≤LaneWidth；分 `.3+.7*(1+gap/TrapDistance)` → ScoreBase .3 / ScoreSlope .7；有效期 `min(6,-gap/relative)` → MaxValiditySec 6（文档写 2–3 s）。
- Giant (DEC:98-105,131-132)：|gap|≤ContactDistance 且欧氏≤ContactDistance*1.5 → EuclidMul 1.5；有效期<2 跳过 → MinValiditySec 2；每接触 .5 分，min(1, contacts*.5) → ScorePerContact .5。
- Hands (DEC:106-114)：0<gap≤HandsDistance、!PushProtected、欧氏≤HandsDistance*1.5（写作平方×2.25）、|yaw|≤110°、转向时间≤AimHorizonSeconds；分 `(.35+.65*(1-gap/HandsDistance))*(1-.5*|yaw|/180)` → EuclidMul 1.5 / MaxYawDeg 110 / ScoreBase .35 / ScoreSlope .65 / YawPenalty .5。
- Reverse (DEC:115-121,133-134)：每对手 `CornerFraction/(1+|gap|/FarDistance)` 累加，乘自身 StraightFraction；有效期 `max(2, CornerSeconds)` → MinValiditySec 2。
- Curtain (DEC:115-121,135-136)：同 Reverse 计分；|gap|<NearDistance 每人 +.25 → PackBonus .25；不乘直道比例。
- WindowSeconds (DEC:158-159)：|relative|<.1 返回 6，clamp 0..6 → MaxWindowSec 6 / MinRelSpeed .1。
- CastProbability (DEC:155-156)：`1-exp(-max(0,u-θ)*Δt/max(.01,τ))`，Δt 上限 1 s (CST:159) → MaxElapsedSec 1。
- Utility (DEC:140-153)：`w*o*bias*noise(*ImpairedMul) - RiskWeight*P*severity`；有效期<所需时间返回 -∞ (:144)。结构固定。

## 表 6：AI_SkillGlobal / 机会调参（JSON:14 Tuning；代码默认 CAT:51-65 与 JSON 完全相同，重复来源）

| 字段 | 值 | 读取方 | 校验 (CAT:140-146) |
|---|---|---|---|
| SnapshotSeconds | .15 | WLD.Advance:55 | .05–.5 |
| SituationHysteresisSeconds | 1.5 | CST.UpdateSituation:265 | 0–5 |
| NearDistance | 60 | DEC.Situation:55,60, SelectStateWindow:170, Curtain:119, PackCentre:190 | 1–500 |
| FarDistance | 300 | DEC.Situation:60-61, :117, :170 | Near–2000 |
| TrapDistance | 65 | DEC:94-95 | 1–500 |
| ContactDistance | 35 | DEC:99-100 | 1–100 |
| HandsDistance | 300 | DEC:107-113 | 1–1800 |
| LaneWidth | 14 | DEC:94 | 1–100 |
| StraightCurvature | .0035 | WLD:53 → AISkillTrackTable:172 | .00001–.1 |
| CornerCurvature | .006 | WLD:53 → :173 | Straight–1 |
| KeyMinSeconds/KeyMaxSeconds | .2/.3 | CST:135, CMT.NextKey:45, CST.Decide:170 | .05–.3 / Min–.34；Max 必须 < ComboSkillInput.stepWindowSeconds 0.35，硬耦合 |
| AimHorizonSeconds | 2 | DEC:111 | .1–5 |
| SpeedDropFraction | .2 | DEC.IsImpaired:16, WLD:78, DEC:128,174,184 | .01–1 |

快照派生硬编码 (WLD.Advance)：刚减速最短受损窗口 .5 s（WLD:78，前提前一帧速度>5）→ SpeedDropImpairSec .5 / SpeedDropMinSpeed 5（DEC:16 重复 5f）；被缩小判定 Scale<.99 (WLD:76, DEC:15) [Eng]；直道前瞻 speed*10 → StraightLookaheadSec 10 (WLD:93)；直道秒数分母 max(5,speed) (WLD:94) [Eng]；弯道前瞻 speed*2 → CornerLookaheadSec 2 (WLD:95)；弯道有效期 2+2*Fraction → CornerValidityBase/Scale 2/2 (WLD:96)；赛道查表格长 4 m、最少 16 格 (WLD:167) [Eng]。

目标评分 (DEC.TargetScore:179-192)：基础 `1/(1+|gap|)`；Impaired +2、Ahead +1、Behind +1、EnteringCorner +CornerFraction、PackCentre 邻近每人 +.25 → AI_TargetPreference(Pref, Bonus)；距离范围 [-Near, Far] (DEC:170)。
局势判定 (DEC.Situation:45-63)：车群=附近≥2 人 (:59) → PackMinNeighbours 2；其余只用 Near/Far，结构写死。

## 表 7：AI_SkillCommitment / 施法执行规则（全部硬编码，全局）

- 每次承诺最多重试 1 次 — CMT.NextKey:38 — MaxComboRetries 1。
- 失误只在第 2 键及之后 — CMT:35 [规则]。失误后重试延迟 inputWindow+delta — CMT:40（inputWindow 来自 ComboSkillInput）。首键在决定后下一 tick — CMT:26 [Eng]。
- 截止 Deadline = tick + (ValidSeconds - age) — CST:184；到期取消 CST:133。其他取消原因（规则，不进表，应写文档）：finished/outside-race CST:118-124；casting-disabled/input-blocked :125-126；combo-not-accepted :143；retry-exhausted :145；rejected :243；effects-reset :249。被定身时不发起新决策但不取消承诺 (CST:129,153)。
- 所需时间 CST:170-173：`comboSeconds + KeyMistake*(window+delta+comboSeconds) + ConfirmationSeconds + 2*delta`（ConfirmationSeconds 来自 SkillExecutor，文档写 1 s）。
- 掌形连发间隔 CST:212：`max(StepWindowSeconds+delta, ServerPushCooldown)`，参数都在范围外。
- 决策交错 `tick % MaxAI == racerIndex` 且与快照不同帧 — SCH:13, WLD:119 [Eng]。随机种子派生 CST:62-63 [Eng]。

## 表 8：AI_Roster（全局）

- AIBase 10000 — RID:9 — 协议常量，不建议交策划。诊断代码多处写死 10000–10004。
- MaxAI 5 — RID:10 — SMS:17 范围校验、SSP:87 固定 5 个 AI、CAT.Validate:107 人格数必须 5、SCH:13。
- 产品 AI 数量 — SSP:87 `new SoloMatchSettings(RacerId.MaxAI, _difficulty)` — SoloAICount 5 — 产品无数量选项；诊断工具允许 0–5。
- 人格分配 — MSM.SpawnSoloAI:165 `Personalities[index]` — 隐含按 JSON 顺序。
- AI 名字 — MSM:166 `AINames[index]` 优先否则 DisplayName — 产品中 AINames 永远为空；仓库无独立名字列表。
- 默认难度 — SSP:23 PlayerPrefs 默认 1=Normal。难度标签「简单/普通/困难」— SSP:11 — 应进本地化。

## 重复与遗留来源

驾驶档位 5 份数据：Tools/ai/difficulty-*.json（生成源）；Resources/AI/*.asset（运行时权威，多出 NoiseSeed 0）；AIDifficultyProfile.cs 字段初始值（过时，仅 asset 缺失时用）；Docs/single-player/ai-skills.md:44（与 asset 一致）；Tools/ai/thrust-vector/rollout-2026-10-02/player/*/configuration.json（历史快照）。

代码默认 vs asset 差异：LateralDamping 1/.986；PredictionGain .15/.3；UseWallCorridor false/true；RolloutTail 0/1；RolloutOverspeedWeight 1/0；噪声失误抖动 0/见表1；TargetSpeed 80/E67 N74；ReactionTicks 0/E8 N3；RolloutWallWeight .2/E.7 H0；MaxThrustAngle 170/H90；ThrustReplanTicks 2/E3；RolloutSeconds 2/E1.8；RolloutUnderspeedWeight 1/H3。

范围校验两份且不一致：ReactionJitterTicks 特性 0–30 校验无上限；ThrustReplanTicks/RolloutSubsteps/RolloutTail 只有特性；WobbleTicks 特性 120 校验无上限。

技能 AI：AISkillTuning 代码默认 (CAT:51-65) 与 JSON:14 一模一样；AISkillDifficulty 部分有代码默认；两份文档（ai-skills.md:103-114、:80；ai-personalities.md）又抄一遍，目前一致；校验范围写死 CAT.Validate:132-146，HandsShots 代码 0–6 文档 1–6。

技能 Id 与枚举顺序：CAT.SkillIds:76-77 写死，与配置仓库重复；顺序决定 AISkillKind、Weights[]、BacklashSeverity[] 下标。

写死车辆最高速 80：TVP:28 (79/78)、PS.Resolve:40、SRL:132。

MaxAI 相关写死：数组固定 6（AISkillSnapshot.Racers[6] DEC:22；WLD._entries[6]/_lastCasts[6] WLD:14,22）；AITuningHarness:91 Clamp(0,5)；AISkillLifecycleHarness:81,92,107,122,185 写死 10000–10004；AISkillEffectHarness:55 写死 10000。

快照写入不读取的死字段 [Eng]：AISkillRacerSnapshot.LastCastSeconds/Suppressed/VisionImpaired/TrackDistance (DEC:9-14)，仅 WLD:83-97 写入。

过时文档状态（2026-10-12 基线；2026-10-26 已不成立：两份文档的状态行已更新，余下的“暂不实现”带 2026-10-03 的覆盖说明）：ai-skills.md:5,:153 与 ai-personalities.md:5,:139 仍写"未实现"。beam planner 已在 20401e5 删除，仅 Tools/ai/README.txt:9-11 有说明。

文档与代码语义差异：黑幕后瞄准 5 s 规则（ai-skills.md:99）代码没有；缓流有效期文档 2–3 s 代码 min(6,…)；巨大化"防守值"代码只有 ±ContactDistance 计数；LeadingAlone 判定文档"前方无人或>D_far"代码只要前方有人即 TrailingAlone (DEC:61-62)；HandsShots 下限文档 1 代码 0。

## 排除项（仅工程）

AI/Diagnostics/**（停滞 5 s<2 m、跳变 +10 m、逆行 forwardDot<-0.1、超时 420/900 s、每圈 240 s、结算 45 s、预热 30 s 窗口 60 s、目标 60 FPS、夹具距离、Pmax 0/100 覆盖、障碍物名单、--ai-a1-profile/--ai-profiles 覆盖通道）；AISkillTelemetry.cs、PlanObservation.cs；BuddahMotionModel.cs（无可调常量）；AISkillAcceptanceTools.cs（不写默认值）；AIDifficultyAssetTool.cs（TJ→ASSET 转换，配表落地后退役）；表 2 全部 [Eng]。

## 待确认问题

1. AI 名字是否需要独立名字池？AINames 入口从未使用。
2. 追赶归属：唯一追赶是 ObsessionFigure.catchupRecoveryA=0.01 (GameData/ObsessionFigure.cs:22)，配置仓库另有 GlobalRuleCatchupRecoveryA 键。放执念/全局规则表还是另建 AI 追赶表？
3. 卡住恢复：是否需要 AI 专属参数？
4. TargetSpeed [20,80] clamp 是否配表？H 档加噪声只会变慢是否有意？
5. ImpairedReactionTicks 是否拆成驾驶/技能两列？
6. Weights[6] 是否改为每槽一个权重？
7. MaxAI 与数组容量 6 是否允许策划改？建议锁为协议常量。
8. 普通档风险数值文档标"建议默认值、未确认"，配表是否加"待确认"列？
9. KeyMaxSeconds 上限 .34 隐含依赖 stepWindowSeconds 0.35，是否做表间外键校验？
10. RolloutOverspeedWeight 三档全 0、RolloutLateralWeight 开墙体通道时无效，是否仍暴露？
11. 上述文档/代码语义差异以哪方为准？
