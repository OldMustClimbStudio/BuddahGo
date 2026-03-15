# BuddahGo

本仓库用于 `BuddahGo` Unity 项目的团队协作开发与资源同步。

## 项目概览

`BuddahGo` 是一个基于 Unity 的多人竞速原型项目，当前重点包括：

- 基于 `FishNet` 的联机框架
- 组合输入驱动的技能系统
- 基于赛道进度与圈数的排行榜逻辑
- 竞速过程中的特效、状态与表现同步

## 开发环境

- Unity Editor：`2022.3.55f1c1`
- 版本管理：`Git + Git LFS`
- 主要包依赖：`FishNet`、`Input System`、`Cinemachine`、`URP`、`Visual Effect Graph`、`Splines`
- 仓库地址：`https://github.com/OldMustClimbStudio/BuddahGo.git`

## 分支规范

项目目前只保留两个长期分支：

- `main`：稳定版本
- `dev`：开发版本

推荐规则：

1. 日常开发统一在 `dev`
2. 验证稳定后再从 `dev` 合并到 `main`
3. 如果 `dev` 出现严重问题，可以直接从 `main` 重新建立

## 首次在新电脑上拉取项目

第一次在新电脑同步项目时，按下面顺序操作。

### 1. 安装工具

先安装：

- `Git`
- `Git LFS`
- `Unity Hub`
- Unity `2022.3.55f1c1`

安装完 Git LFS 后，先执行一次：

```bash
git lfs install
```

### 2. 克隆开发分支

建议直接克隆 `dev`：

```bash
git clone -b dev https://github.com/OldMustClimbStudio/BuddahGo.git
cd BuddahGo
git lfs pull
```

说明：

- `git clone -b dev ...` 会直接把本地工作目录切到 `dev`
- `git lfs pull` 会把贴图、模型、音频、视频等真实大文件下载下来
- 如果不执行 `git lfs pull`，有些资源可能只会是 LFS 指针文件

### 3. 用正确版本的 Unity 打开

必须使用：

```text
Unity 2022.3.55f1c1
```

在 Unity 中确认以下设置：

```text
Edit > Project Settings > Editor
Version Control: Visible Meta Files
Asset Serialization: Force Text
```

### 4. 首次打开后的正常现象

第一次打开项目时，Unity 会重新生成本地缓存，以下目录不需要从 Git 获取：

- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`
- `Obj/`
- `Build/`
- `Builds/`

首次导入时间较长属于正常现象。

## 已有本地仓库时如何同步最新开发内容

如果电脑上已经有这个项目，只需要同步 `dev`：

```bash
git checkout dev
git pull
git lfs pull
```

含义：

- `git checkout dev`：切到开发分支
- `git pull`：拉取最新代码和文本资源
- `git lfs pull`：拉取最新大文件资源

如果本地还没有 `dev` 分支，可以执行：

```bash
git fetch origin
git checkout -b dev origin/dev
git lfs pull
```

## 打开项目后建议先检查的内容

拉取完成后，建议先检查：

- `Assets/Scenes/RaceMap.unity` 能否正常打开
- `Assets/Scenes/SpecialEffect.unity` 能否正常打开
- Console 是否有脚本编译错误
- 场景中是否有丢失材质、丢失 Prefab、丢失脚本

当前 Build Settings 中启用的场景：

- `Assets/Scenes/RaceMap.unity`

当前保留在项目中的其他场景：

- `Assets/Scenes/SpecialEffect.unity`

## 日常提交流程

日常开发完成后，建议按下面顺序提交：

```bash
git checkout dev
git status
git add Assets Packages ProjectSettings .gitignore .gitattributes README.md
git commit -m "Describe your change"
git push
```

建议：

- 不要直接在 `main` 上开发
- 推送前先确认 Unity 能正常编译
- 推送前先确认没有把无关测试文件一起提交

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

- 项目资源统一放在 `Assets/` 下
- `.meta` 文件必须提交
- `Assets/Sources/` 中的导入源资源允许上传
- Blender 工作文件不上传：`*.blend`、`*.blend1`、`*.blend2`
- 不影响当前项目运行的插件 Demo、示例、教程资源不上传

当前已明确忽略的内容：

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

`Assets/FishNet/Demos/` 默认也会忽略，但目前保留了 `RaceMap` 实际使用的少量文件：

- `NetworkManager.prefab`
- `NetworkHudCanvas.prefab`
- `NetworkHudCanvases.cs`
- 对应的 `.meta` 与 `FishNet.Demos.asmdef`

原则只有一条：

不影响当前项目编辑、运行、合流的文件，就不要上传。

## 可正常打开项目所必需的目录

为了让其他开发者能够正常打开项目，仓库中至少需要有：

- `Assets/`
- `Packages/`
- `ProjectSettings/`
- 对应的 Git LFS 大文件

## 常见问题

### 1. 拉下来后资源丢失或只有指针文件

先执行：

```bash
git lfs install
git lfs pull
```

### 2. 本地没有 `dev` 分支

执行：

```bash
git fetch origin
git checkout -b dev origin/dev
```

### 3. 目录不是正常 clone 下来的仓库

如果出现下面这类问题：

- `origin does not appear to be a git repository`
- `Git can't resolve ref: HEAD`

通常说明当前目录不是一次正常的 `git clone` 结果。最稳的处理方式是删除这个错误目录，重新执行：

```bash
git lfs install
git clone -b dev https://github.com/OldMustClimbStudio/BuddahGo.git
cd BuddahGo
git lfs pull
```

### 4. `git pull` 提示本地改动会被覆盖

如果出现下面这类问题：

```text
error: Your local changes to the following files would be overwritten by merge
```

通常说明本地 Unity 自动改动了某些项目文件，例如：

- `ProjectSettings/EditorBuildSettings.asset`
- 其他 `ProjectSettings` 文件

团队同步以 Git 上的远程版本为主。  
如果只是想先同步远程最新内容，建议先暂存本地改动，再拉取：

```bash
git stash push -m "temp before sync"
git pull
git lfs pull
```

如果只想暂存某一个文件，也可以：

```bash
git stash push -m "temp before sync" -- ProjectSettings/EditorBuildSettings.asset
git pull
git lfs pull
```

同步完成后先确认项目能正常打开，再决定是否需要恢复本地暂存内容。

## 资源检查工具

仓库中已加入一个 Unity Editor 工具，用来辅助检查未使用资源候选：

```text
Tools > Asset Audit > Report Unused Asset Candidates
```

它会基于 Build Settings 中启用的场景分析依赖关系。删除资源前仍需人工确认，特别是运行时动态加载的内容。
