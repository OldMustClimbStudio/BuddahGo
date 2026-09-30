# BuddahGo

基于 Unity、FishNet、FishyFacepunch 和 Steam 的多人竞速项目，包含属性/技能选择、预测运动、比赛进度与结算流程。

## 开始开发

使用 Unity `2022.3.55f1c1`、Git 和 Git LFS：

```bash
git lfs install
git clone -b dev https://github.com/OldMustClimbStudio/BuddahGo.git
cd BuddahGo
git lfs pull
```

用指定 Unity 版本打开项目，等待包解析和资源导入。保留 `Assets/`、`Packages/`、`ProjectSettings/`；`Library/` 等缓存由 Unity 生成。
缺少贴图、模型或 DLL 时，先检查 LFS 文件是否下载完整。

## 开发入口

- [harness.md](harness.md)：Agent 共用执行入口。
- [CONTRIBUTING.md](CONTRIBUTING.md)：分支、提交、PR、验证及资源规则。
- [Docs/README.md](Docs/README.md)：架构、网络和预测设计索引。
- [Docs/unity-mcp.md](Docs/unity-mcp.md)：Unity MCP 启动方法。

Unity 中的 `Tools > Asset Audit > Report Unused Asset Candidates` 可辅助排查资源；运行时动态加载仍需结合代码确认。
