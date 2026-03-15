# BuddahGo

本仓库用于 BuddahGo Unity 项目的团队协作开发。

## 项目概览

`BuddahGo` 是一个基于 Unity 的多人竞速原型项目，当前开发重点包括：

- 基于 `FishNet` 的联机框架
- 组合输入驱动的技能系统
- 基于赛道进度与圈数的排行榜逻辑
- 竞速过程中的特效、状态与表现同步

## 开发环境

- Unity Editor：`2022.3.55f1c1`
- 版本管理：`Git + Git LFS`
- 主要包依赖：`FishNet`、`Input System`、`Cinemachine`、`URP`、`Visual Effect Graph`、`Splines`
- 常驻分支：
  - `main`：稳定版本
  - `dev`：开发版本

## 首次拉取项目

1. 先安装 Git LFS。
2. 克隆仓库。
3. 在仓库目录执行：

```bash
git lfs install
git lfs pull
```

4. 使用 Unity `2022.3.55f1c1` 打开项目。
5. 在 Unity 中确认以下设置：

```text
Edit > Project Settings > Editor
Version Control: Visible Meta Files
Asset Serialization: Force Text
```

## 项目可正常打开的必要内容

要让其他开发者成功打开项目，仓库中至少需要有：

- `Assets/`
- `Packages/`
- `ProjectSettings/`
- 正常可用的 Git LFS 资源

以下目录是本地缓存或构建产物，不提交：

- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`
- `Obj/`
- `Build/`
- `Builds/`

这些目录在 Unity 首次打开项目时会自动重新生成。

## 场景说明

当前 Build Settings 中启用的场景：

- `Assets/Scenes/Menu.unity`
- `Assets/Scenes/RaceMap.unity`

`Assets/Scenes/SpecialEffect.unity` 仍保留在项目中，但目前没有加入 Build Settings。

## 分支规范

项目目前只保留两个分支：

- `main`：稳定可回退版本
- `dev`：日常开发版本

推荐流程：

1. 日常开发统一在 `dev`
2. 在 Unity 中测试通过后，再合并到 `main`
3. 如果 `dev` 出现严重问题，可以直接从 `main` 重新建立

## Git LFS 规则

以下大文件类型由 Git LFS 管理：

- `*.png`
- `*.jpg`
- `*.jpeg`
- `*.tga`
- `*.exr`
- `*.psd`
- `*.fbx`
- `*.wav`
- `*.mp3`
- `*.ogg`
- `*.mp4`
- `*.mov`
- `*.webm`
- `*.sbsar`
- `*.unitypackage`
- `*.dll`
- `*.so`
- `*.dylib`
- `*.a`
- `*.pdf`

以下 Unity 文本资源不要放进 LFS，继续走普通 Git：

- `*.meta`
- `*.unity`
- `*.prefab`
- `*.mat`
- `*.asset`
- `*.cs`

## 资源提交规范

统一规则：

- 项目资源放在 `Assets/` 下
- `.meta` 文件必须提交
- `Assets/Sources/` 中的导入源资源允许上传
- Blender 工作文件不上传：`*.blend`、`*.blend1`、`*.blend2`
- 不影响当前项目运行的插件 Demo、示例、教程资源不上传

当前已明确忽略的内容：

- `Assets/FishNet/Demos/`
- `Assets/Plugins/Feel/FeelDemos/`
- `Assets/Plugins/Feel/FeelDemosHDRP/`
- `Assets/Plugins/Feel/FeelDemosURP/`
- `Assets/Plugins/Feel/MMTools/Demos/`
- `Assets/Plugins/Feel/MMFeedbacks/Demos/`
- `Assets/Plugins/Feel/NiceVibrations/Demo/`
- `Assets/Plugins/Feel/NiceVibrations/HapticSamples/`
- `Assets/TutorialInfo/`
- `Assets/Readme.asset`
- `Assets/Plugins/GabrielAguiarProductions/`

原则只有一条：不影响当前项目编辑、运行、合流的文件，就不要上传。

## 日常提交流程

查看状态：

```bash
git status
```

加入暂存区：

```bash
git add Assets Packages ProjectSettings .gitignore .gitattributes README.md
```

提交：

```bash
git commit -m "Describe your change"
```

推送当前开发分支：

```bash
git push
```

## 资源检查工具

仓库中已加入一个 Unity Editor 工具，用来辅助检查未使用资源候选：

```text
Tools > Asset Audit > Report Unused Asset Candidates
```

它会基于 Build Settings 中启用的场景分析依赖关系。删除资源前仍需人工确认，特别是运行时动态加载的内容。
