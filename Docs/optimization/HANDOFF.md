# 交接：架构优化执行

你负责在分支 `refactor/architecture-optimization` 上执行 [refactor-plan.md](refactor-plan.md)，把代码架构整理干净，同时让玩家、联机和回放可观察到的行为保持与基线一致。

- 方案和评估是本分支的前两个提交，只有文档，没有代码改动。
- 证据在 [audit-report.md](audit-report.md)。
- 进度的唯一来源是 [progress.md](progress.md)。

仓库通用规则仍以 [harness.md](../../harness.md) 和 [CONTRIBUTING.md](../../CONTRIBUTING.md) 为准；本文只补充这次任务特有的内容。

## 开工前（每次会话）

1. 在主项目根目录建 worktree，或进入已有的 worktree：

   ```bash
   git worktree add .worktree/architecture-optimization refactor/architecture-optimization
   ```

2. 执行 `git lfs pull`。方案是在只有 LFS 指针的检出上写的，你需要完整资源才能构建和跑 R 项。
3. 执行 `git merge origin/dev`，把 dev 的新提交并进来。有冲突就先解决，并在 progress.md 的备注里记录。
4. 打开 progress.md，找到第一个不是 `done` 的步骤，从它开始。

## 每个步骤的流程

1. 在 progress.md 中把该步骤标为 `doing`。
2. 读 refactor-plan.md 里该步骤的全文，以及 §2 的不变式 I1–I7。
3. 跑步骤里写的**预检**。
   - 结果与方案描述一致：继续。
   - 不一致：标为 `blocked`，写明差异，跳到下一个与之无依赖的步骤，或者停下来找人。
4. 修改。只改该步骤范围内的文件。Unity 顺带重新序列化的无关资源，用 `git restore` 丢弃。
5. 跑该步骤要求的 R 项（定义见方案 §5），再根据实际触及的系统补测其他 R 项。
6. 提交。提交标题写成 `refactor: [P1-3] remove legacy lobby chain` 这种格式（类型按 CONTRIBUTING 选择）。提交说明写清：
   - 预检证据（grep 或 refscan 的输出摘要）
   - I6 的 diff 结论
   - R 项结果
7. 在 progress.md 标为 `done`，填入提交 hash 和 R 结果。

**完成标准**：步骤的"完成标准"全部满足，要求的 R 项全部与基线一致，progress.md 已更新。静态检查不能写成"运行通过"；某项 R 在当前环境无法执行时，写"未执行 + 原因"，该步骤标 `done-unverified`。

## 阶段合并

- 一个阶段（P0…P7）的步骤全部完成后，从本分支向 `dev` 开 PR。PR 使用 CONTRIBUTING 的 Summary / Validation 模板，列出本阶段的步骤 ID 和各自的 R 结果。
- 合并后先 `git merge origin/dev`，再继续下一阶段。
- P7（资源）可以和 P2–P5 并行，但要走单独的 PR。

## 需要停下来找人的情况

以下情况都要标 `blocked`、写明原因，并请团队决定：

- 步骤需要做决策项 D1–D8 中的任何一项。决策结果记在 progress.md 的决策表里。
- 需要打破不变式 I1–I3，例如给序列化字段改名、改 RPC 签名或 replicate/reconcile 结构。
- R7 出现任何 div≠0，或者某项 R 与基线不一致且找不到原因。
- 发现新的行为缺陷。记到 progress.md 的"新发现"里，按 D8 的方式处理，不在重构提交里顺手修。

## 环境

- **Unity 版本**：2022.3.55f1c1。编译和读 Console 可以用 Unity MCP，见 [Docs/unity-mcp.md](../unity-mcp.md)。
- **联机测试**：沿用团队已有的做法，host 用 Development Build、client 用 Editor（或 ParrelSync 克隆）。`agent-exchange/console/*phase4b*` 中的日志就是这样采集的，可以作为日志格式参考。
- **确定性判据（R7）**：在 Standalone 宏里保留 `BUDDAH_PREDICTION_SHADOW`，看 `[D-LOC HEARTBEAT]` 行里的 `loc-div/tel-div/mod-div/hof-div`。
- **性能判据（R8）**：看 PerfProbe 的 GC.Alloc 和 VisualShakeProbe 的 `pos-davg-mean`。二者都依赖对应的宏，并且在 Editor 或 Development Build 中运行。

## 已知陷阱

- **LightingData 显示已修改**：在 P0-2 之前，新检出后 `Assets/Scenes/RaceMap/LightingData.asset` 会显示为已修改（`.gitattributes` 把它当文本处理）。不要提交这个文件。
- **DefaultPrefabObjects 会被自动重写**：FishNet 的 PrefabGenerator 已启用（`Assets/FishNet.Config.XML`），增删 prefab 时会自动改写 `Assets/DefaultPrefabObjects.asset`。只有 P1-8 允许提交这个文件的改动，其他步骤出现改动要丢弃。
- **`Legacy/` 目录名有误导**：`UI/InGameUI/Legacy/` 里的 `LeaderboardTMPUI`、`ResultDecisionUI` 仍挂在 `RaceMap.unity` 上。判断死活看 refscan，不看目录名。
- **UnityEvent 按程序集限定名引用脚本**：`MainMenuUI`（3 处）和 `RaceFinishManager`（1 处）被 UnityEvent 以 `ClassName, Assembly-CSharp` 引用。改名或移动程序集后要验证按钮能用（R10）。
- **`??` 用在 `UnityEngine.Object` 上**：会绕过 Unity 的假 null 判断（例如 `PlayerScaleEffect.cs:37/:174`）。现有代码不要改（改了会改变行为）；新代码使用显式的 `== null` 判断。
- **运行时 AddComponent 的组件**：`SceneFadeController`、各种 `*Effect`、Validation 组件、`PlayerFinishPresentationController` 在 YAML 里查不到引用，但它们是存活的。缓存这些组件时，缓存为 null 必须允许重新查找。

## 死代码复核

删除任何脚本或成员之前都要跑复核脚本：

```bash
python3 Tools/Audit/refscan.py              # 列出 YAML 与 C# 引用都为 0 的脚本
python3 Tools/Audit/refscan.py LobbyManager # 查询指定类型的引用数
```

- startupHooks 列为 True 的脚本通过 `RuntimeInitializeOnLoadMethod`、编辑器菜单等方式启动，属于存活。
- 以下情况需要补充 grep：
  - 成员级删除：`grep -rn "\bName\b" Assets --include='*.cs'`
  - UnityEvent 和 Timeline 绑定：`grep -rn "m_MethodName: Name" Assets`
  - 字符串反射：`grep -rn "\"Name\"" Assets/Scripts`
