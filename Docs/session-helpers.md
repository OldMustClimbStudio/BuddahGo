# 会话辅助规则

房间和配装的网络入口仍由 manager 持有。下面的 helper 负责名单访问、本地握手记录、配装规则和身份解析；使用时从现有 manager 入口进入，保留调用侧的权限校验、状态推进和同步通知。

## 职责与调用方

代码路径相对 `Assets/Scripts/`。

| Helper | 调用方与职责 | 保留的边界 |
|---|---|---|
| [RoomRoster](../Assets/Scripts/Network/Room/RoomRoster.cs) | `RoomStateManager` 的名单查询、写回、ready 复位与摘要 | 直接引用 `Players` 的 `IList<RoomPlayerState>`，没有名单副本；网络状态仍由 manager 持有。 |
| [RaceStartHandshake](../Assets/Scripts/Network/Room/RaceStartHandshake.cs) | `RoomStateManager` 的场景 manager ready 与 Assignment / Visual / Gameplay 序号记录；`RoomDiagnostics` 读取摘要 | 七个集合均为私有；使用按 `Stage` 操作的语义 API。RPC、SyncTypes 和场景推进仍在 manager。 |
| [LoadoutRules](../Assets/Scripts/Network/Session/PropertySelection/LoadoutRules.cs) | `PropertiesSelectionManager` 的提交校验、完整性判断与自动补全候选选择 | 合法选项与重复策略由调用方传入；计时器、选择记录、cache 和阶段推进仍在 manager。 |
| [PlayerIdentity](../Assets/Scripts/Foundation/PlayerIdentity.cs) | 房间、配装和结算调用方共用身份查询与姓名回退 | 不启动 Steam 或连接；读取调用方提供的 transport、connection 和可选 lobby。 |

前三个 helper 为 Runtime 内部类型。`PlayerIdentity` 保留公共静态入口；测试通过 friend assembly 访问内部 helper，不需要扩大其可见性。

## 名单与握手

- `RoomRoster.AreAllRequiredPlayersReady()` 对空名单返回 false；有名单时只要求非 host 玩家 ready。`ResetReadyForRoomReturn()` 将 host 设为 ready、其他玩家设为未 ready，仅把有变化的项写回列表。
- `ResetAllReadiness()` 清空全部七个集合；`ResetSequenceReadiness()` 保留场景 manager ready，清空三个阶段的 ready 和序号记录。`RemovePlayer(id)` 仅从全部集合移除该玩家。manager 各入口的重置矩阵见 [P4 验证记录](optimization/p4-validation.md#p4-1-预检与重置矩阵)。
- `TryMarkLocalSequence()` 拒绝未初始化 client、负序号和同序号重复报告；不同序号可报告，包括比上次小的序号。它不要求序号单调递增。
- `AreAllReadyForSequence()` 要求 server 已初始化、序号非负，并逐一匹配当前名单的记录。满足前两个条件时，空名单返回 true；这与房间名单的 ready 判断不同。名单外的旧记录不影响该查询。
- `RecordReadySequence()` 只记录 ready 和序号，不校验调用者。`RoomStateManager.TryRecordReadySequenceServer()` 保留非空且已认证 caller 的检查；`RoomDiagnostics` 通过计数和序号查询读取状态。

## 配装输入与自动补全

`ValidateSubmission()` 先检查数组非空且长度等于槽位数，然后按槽位原地 Trim，将 null 转为空字符串。空槽允许提交，但 `HasCompleteSelection()` 会把空槽判为未完成。遇到非法或不允许重复的技能时立即返回 false：此前及当前槽位已规范化，后续槽位保留原值。例如 `[" a ", " bad ", " c "]` 在第二项非法时变为 `["a", "bad", " c "]`。

`PropertiesSelectionManager` 按选项列表 → 数据库 fallback → 本地 fallback 的顺序构建候选并去重。选项 id 保留原字符串，fallback 才 Trim；不要把两者统一规范化。`FindNextAutoFillSkillId()` 按候选顺序选择，在禁止重复时跳过已使用项，无候选时返回空字符串。重复策略继续通过原位置的 getter 求值。

## 身份回退

- `GetLocalClientId(null)` 返回 -1。
- `GetSteamIdForConnection()` 在 transport 或 connection 为空时返回空字符串，不调用 transport；否则用 `connection.ClientId` 查询地址。null、空和纯空白地址转为空字符串，非空白地址原样返回，不额外 Trim 或验证 Steam ID 格式。
- `ResolvePlayerName()` 依次尝试匹配 lobby 成员、有效且 ID 匹配的本地 Steam 身份，最后回退为 `Player {clientId}`。名单显示名的 Trim/长度限制由 `RoomRoster.SanitizePlayerName()` 单独处理。

## 验证入口与边界

[程序集与测试说明](architecture.md#程序集与测试)提供 Unity Test Runner / MCP 的运行入口。对应的 EditMode 测试是：

- [RaceStartHandshakeTests](../Assets/Tests/EditMode/RaceStartHandshakeTests.cs)：重置、离开玩家清理、本地报告守卫、当前名单与精确序号匹配。
- [LoadoutRulesTests](../Assets/Tests/EditMode/LoadoutRulesTests.cs)：空槽、重复策略、失败时部分规范化、数组长度及候选顺序。
- [PlayerIdentityTests](../Assets/Tests/EditMode/PlayerIdentityTests.cs)：姓名回退、空连接和 transport 地址转发；测试 transport 不启动 socket 或 Steam。

这三个测试类在 `95e57a0` 所记录的最终堆叠 61/61 EditMode 结果中分别通过 5、6、5 项，见 [审查修复验证](optimization/review-fixes-validation.md#最终堆叠验证与发布收尾)。这是该提交的既有结果。它不覆盖真实双端开赛、Steam/跨机流程、完整碰撞矩阵或性能验收，也不将 `RoomRoster` 声称为独立 EditMode 测试类。
