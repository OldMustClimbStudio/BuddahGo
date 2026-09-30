# FishNet 开发要点

以本库 `Assets/FishNet/` 的版本和调用方式为准。下面保留会影响正确性的要点，不规定固定检查流程。

## 权限与复制

- 所有权变更和权威状态写入由服务器执行；客户端通过 RPC 请求，服务器校验。
- `ServerRpc` 默认要求所有权。改变 `RequireOwnership` 时保留对应的调用者校验。
- Host 初始化同时涉及服务器和客户端身份；在 `OnStartServer` 中按目的区分 `Owner.IsLocalClient` 与 `IsOwner`。
- SyncTypes 的变化不是立即送达；依赖 RPC 与状态同步的先后关系时，检查实际发送时机。
- SyncCollection 内部对象的修改需要正确标记 dirty；自定义网络数据需要完整、可往返的序列化。
- 向 observers 发消息前确认观察者已建立；不要将 `OnStartServer` 等同于所有客户端已经 ready。
- 网络组件预先放到 prefab，避免运行时动态添加 `NetworkBehaviour`。

## 多人场景

- 通过房间系统调用 FishNet 场景管理。区分 global 与 connection 场景的加载/卸载 API。
- 新场景按名称定位；已有实例或同名堆叠场景按 handle/reference 定位。
- 客户端加入场景的观察关系后才能看到网络对象；普通 Unity 场景加载不等于完成网络登记。
- 跨场景保留的网络对象需要纳入对应迁移数据。场景切换后重新确认对象和连接引用。

## 定位常见问题

| 症状 | 优先查看 |
|---|---|
| RPC 没到达 | 调用侧、所有权、连接存活和实际接收日志 |
| 列表改动不同步 | 修改是否发生在服务器，是否通知集合变化 |
| 切场景后对象不可见 | 客户端场景登记、observer 与对象迁移 |
| Host 正常、远端异常 | 序列化、客户端输入/回放、RPC 返回链路 |
| 角色或相机抖动 | [预测设计](prediction-design.md)中的模拟状态与视觉平滑边界 |
