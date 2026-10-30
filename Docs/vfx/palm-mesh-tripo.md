# 如来神掌佛手白模：Tripo 图生 3D 记录

2026-10-29 · 分支 `refactor/vfx-refactor` · 通用规范见 [README.md](README.md)，技能设计见 [push-projectile-hands.md](push-projectile-hands.md)。本文只记录白模资产怎么来的、参数是什么、怎么再生成一版；它是不是最终主体由用户在设计会话里决定。

## 1. 结果（v1，待用户看图确认）

| 项 | 值 |
|---|---|
| 可用网格 | `Assets/VFX/如来神掌/Mesh/Mesh_FX_Palm_Tripo.asset` |
| 原始输出 | `Assets/VFX/如来神掌/Mesh/Palm_Tripo_v1.glb`（Tripo 直出，无贴图、无材质、带 UV；工程没装 glTF 导入器，Unity 把它当普通文件，只作留档） |
| 面数 / 顶点 | 4 782 三角面 / 3 152 顶点（预算 ≤ 5 000） |
| 轴向 | 掌心朝 +Z，手指朝 +Y，拇指在 −X 侧；中心在原点 |
| 尺寸 | 59.9 × 90.0 × 34.5（等比缩放到高 90，对应参考碰撞盒 60 × 90；厚度 34.5 是拇指外展造成的，手掌本身约 15） |
| 手腕 | 参考图底座已裁掉，手腕处是平切口（y = −45） |
| 掌心法轮 | 参考图掌心的法轮在 5 000 面下只剩一圈浅浮雕，正面平光下隐约可见；不要的话换水墨参考图或在 Editor 里抹平 |
| 三视图 | [v1-three-views.png](palm-mesh-tripo/v1-three-views.png)（Editor 内 `PreviewRenderUtility` 渲染，灰色 URP Lit）；Tripo 自己的预览 [v1-tripo-preview.png](palm-mesh-tripo/v1-tripo-preview.png)；旋转前的原始轴向 [v1-raw-axes.png](palm-mesh-tripo/v1-raw-axes.png) |

## 2. 参考图与裁切

用户给的三视图 `hand_gold_front_side_back.webp`（1704 × 923，金色佛手，掌心有法轮；水墨版 `hand_ink_front_side_back.webp` 同形体，未用）。按非白像素列分段裁出三张，去掉底部的底座（只取 y < 820 的部分），各自居中贴到 900 × 900 白底：

| 视图 | 裁切框（像素） | 用途 |
|---|---|---|
| 正面 | (60, 0, 693, 820) | v1 的 `front` |
| 侧面 | (709, 0, 1010, 820) | v1 未用：侧视图里拇指最近、掌心朝左，即相机在正面图的屏幕右侧；Tripo 文档没有写 `left` / `right` 是以物体还是以观者为准，手是手性体，标错会得到镜像不一致的输入，所以第一版不冒险 |
| 背面 | (1050, 0, 1644, 820) | v1 的 `back` |

裁切脚本只用 PIL，运行时在会话 scratchpad，没有进仓库；裁好的三张在共享笔记目录 `tripo-hand-ref/` 旁。

## 3. 接口与参数（v1）

| 项 | 值 |
|---|---|
| 密钥来源 | Unity 包 MCP for Unity 的安全密钥库（`SecureKeyStore.Current.TryGet("tripo")`，Windows 凭据管理器）。密钥只在 Editor 进程内读取，不经过 MCP 桥、聊天、环境变量或仓库 |
| 域名 | `https://api.tripo3d.ai/v2/openapi`。新域名 `https://openapi.tripo3d.com/v3` 对这把 `tsk_` 密钥一律回 401 `Invalid API key`（`/user/balance` 探测过），v2 的 `/user/balance` 正常（余额 2 420 点） |
| 上传 | `POST /upload`，multipart 字段 `file`，返回 `data.image_token` |
| 任务 | `POST /task`，body 见下；返回 `data.task_id` |
| 轮询 | `GET /task/{task_id}`，`data.status` 为 `queued / running / success / failed`，`data.progress` 0–100；成功后 `data.output.base_model`（texture=false 时只有 base_model，没有 `model` / `pbr_model`）与 `data.output.rendered_image` |
| task_id | `0af456f5-f192-419d-82ba-96f64b665ab0` |
| 耗时 / 花费 | 排队 + 生成约 2 分 15 秒；20 点 |

v1 请求体：

```json
{
  "type": "multiview_to_model",
  "model_version": "v3.1-20260211",
  "texture": false,
  "pbr": false,
  "files": [
    { "type": "png", "file_token": "<front>" },
    {},
    { "type": "png", "file_token": "<back>" },
    {}
  ],
  "face_limit": 5000
}
```

`files` 按 `[front, left, back, right]` 排，缺的视图放空对象。Tripo 回显的输入里 `geometry_quality` 为 `standard`、`export_uv` 为 true（默认）。没有提示词：图生 3D 接口不收文本。

## 4. 导入与轴向修正

Tripo 的 GLB 是 glTF 右手系、+X 朝前（文档里 `export_orientation` 默认 `+x`）。工程没有 glTFast，所以 `Assets/Editor/VFX/PalmMeshTripo.cs` 自带一个最小 GLB 读取器（`ImportGlb`）：读 JSON + BIN 块，合并所有 primitive 并应用节点变换，X 取反、三角形反绕序换成 Unity 左手系（手是手性体，这一步不能省），生成 Mesh 资产。导入后实测包围盒 0.383 × 1.000 × 0.665：掌心朝 −X、手指 +Y、拇指 −Z。

之后在 Editor 里绕 Y 转 +90°（−X → +Z），按包围盒中心归零，等比缩放到高 90，另存为 `Mesh_FX_Palm_Tripo.asset`；中间的 raw 资产已删，GLB 保留。缩放与碰撞盒的关系沿用实现笔记的做法：主体按碰撞盒等比缩放，不逐轴贴合。

## 5. 工具：`Assets/Editor/VFX/PalmMeshTripo.cs`

| 方法 | 作用 |
|---|---|
| `Start(statusPath, front, left, back, right, faceLimit, model, version, smartLowPoly, api)` | 后台线程：上传各视图 → 提交任务 → 轮询 → 下载 GLB 与预览到 `Assets/VFX/如来神掌/Mesh/Palm_Tripo_<version>.glb`。进度写到 `statusPath`（JSON：phase / task_id / progress / error / glb），请求体写到 `<statusPath>.request.json`，任务回执写到 `<statusPath>.task.json`。`api` 取 `v2` 或 `v3`。**domain reload 会杀掉这个线程**（v1 就被别的会话的重编译打断过一次），任务本身在 Tripo 那边继续，之后用 `Poll` 接着拿 |
| `Poll(statusPath, taskId, version, api)` | 主线程同步查一次任务并在成功时下载；每隔 10 s 调一次即可，不怕重载 |
| `ImportGlb(glbPath, assetName)` | 第 4 节的 GLB → Mesh 资产 |
| `RenderViews(meshAssetPath, pngPath, size)` | 用 `PreviewRenderUtility` 渲染 +Z / +X / −Z 三视图拼成一张 PNG，不碰打开的场景 |
| `CheckKey()` | 用两个域名的余额接口探测密钥有效性，只返回状态码与去掉密钥的正文 |

调用方式：通过 Unity MCP 的 `execute_code` 反射调用（`System.Type.GetType("BuddahGo.VFX.EditorTools.PalmMeshTripo, Assembly-CSharp-Editor")`）。CodeDom 片段里不要放中文路径，所以 `Start` 的输出目录写死在脚本里，脚本读状态文件拿 GLB 路径。

## 6. 再生成一版时

- 换参考：把新的正 / 侧 / 背图裁成白底 PNG，`Start` 里给路径；版本号递增（`v2`…），每版的请求体和回执都留在 scratchpad 并复制到共享笔记 `palm-tripo/`。
- 想要侧视图参与：先用一张明显不对称的测试图（比如写着 L / R 的方块）跑一次 `image_to_model` + 多视图，确认 Tripo 的 `left` / `right` 以谁为准，再把第 2 节的侧面图填到对应槽。
- 面数：`face_limit` 直接给目标值（Tripo 自己减面，v1 的 4 782 面质量可用）；要更干净的四边形拓扑可试 `smart_low_poly: true`（500–20 000 面，复杂体可能失败）。要保留掌心法轮就先出 20 000 面再在 Editor 里用 `FxMeshSimplifier` 减。
- 更细的几何：`geometry_quality: "detailed"`（v3.0+ 才有，花费更高）。
- 新域名 v3 接口（`inputs` 用 `{front: token}` 对象、`model` 代替 `model_version`、`GET /tasks/{id}`、`output.model_url`）脚本已写好分支，等密钥在新域名可用再切。

## 7. 未决

- 用户看过三视图后的意见：手势、拇指外展角度、手腕切口要不要收圆、掌心法轮要不要。
- 这只白模对应设计里的哪一层（设计交接笔记里用户已否决过几版手部网格方案，当前时间轴是一张白纸）；由设计会话与用户定。
- `Palm_Tripo_v1.glb` 要不要进仓库（160 KB，留档用；没有导入器时它在工程里只是普通文件）。

## 8. 水墨玻璃 shader `FX/InkGlass`（2026-10-29，用户当场追加）

用户看过白模后的要求：按水墨版参考图做半透明材质，画风与 Piloto「Realistic Ying Yang VFX」一致；整体要"若隐若现、有形无体"；黑色部分是噪声驱动的表面墨斑即可，不必做"墨在玻璃里面"的体积感。

| 项 | 值 |
|---|---|
| shader | `Assets/Rendering/Shaders/FX/FX_InkGlass.shader`，手写 URP HLSL，走 `FXCommon.hlsl` 的约定（不读深度 / 不透明纹理，`_FxAlpha` / `_FxDissolve` 可驱动） |
| 材质 | `Assets/VFX/如来神掌/Materials/Mat_FX_Palm_InkGlass.mat`，已挂到 `VFXtests` 场景的临时对象 `Palm_Tripo_Preview` 上（场景未保存） |
| 墨纹贴图 | 包里的 `Assets/Piloto Studio/Textures/Tx_Noise_20.png`（1024² 平铺云雾噪声，R 通道），按物体空间三平面采样，不依赖 UV |
| 截图 | 赛道机位 [inkglass-v1-scene.png](palm-mesh-tripo/inkglass-v1-scene.png)，白底三视图 [inkglass-v1-views.png](palm-mesh-tripo/inkglass-v1-views.png) |

做法（和包的语言对应）：

- **只有黑白**：玻璃本体是白色、几乎全透（alpha 0.04）；墨是黑；灰只出现在墨斑周围的"水晕"和背面透出的轮廓里。
- **有形无体**：形靠两样东西撑——视角 Fresnel 把轮廓处拉成墨线并提高不透明度（`_RimPower` 1.8、`_RimStrength` 0.9、`_RimAlpha` 0.5），以及主光的一个很紧的白色高光（`_Gloss` 140，HDR 1.8，顺带抬 alpha 让高光在透明处也看得见）。背面先画一遍（`Cull Front`，alpha × 0.25，轮廓墨线只留 15%），正面再画一遍，所以透过去能看见远侧的指缝，但不会像裂纹。
- **墨斑 = 噪声三色映射**：云雾噪声 → 白 / 灰 / 黑三段（`_InkCoverage` 0.21、`_InkSoftness` 0.1、`_InkMidWidth` 0.09），再在墨斑与水晕交界处加一圈更深的"干墨边"（`_EdgeDarken` 0.5），对应包里 Color Ramping 的黑 / 深灰 / 白。阈值按贴图直方图定：`Tx_Noise_20` 的 R 通道 85 分位 0.81、65 分位 0.75，所以墨芯取最亮的约 15%，水晕再占约 15%；第二层细节噪声（2.6 倍、权重 0.35）打碎边缘。墨斑随时间慢慢漂（`_InkFlow` 0.015）。`_InkDepth`（沿视线推进采样位置的视差）保留但设 0。
- **形体明暗**：主光包裹漫反射只用 15%（`_Shading`），避免玻璃变灰。

调参记录：

| 版 | 改动 | 结果 |
|---|---|---|
| 1 | 本体 alpha 0.12、覆盖 0.38、柔度 0.25、水晕宽 0.35 | 整只手像灰白大理石：贴图偏亮（中位数 0.69），水晕阈值 0.44 把几乎整张表面都染成灰 |
| 2 | 本体 alpha 0.04、覆盖 0.34、柔度 0.45、加干墨边、背面轮廓 40% | 仍是磨砂灰；背面轮廓透出来像裂纹 |
| 3 | 阈值按直方图：覆盖 0.16、柔度 0.08、水晕 0.06；背面轮廓 15% | 第一次读成"若隐若现"的玻璃手，墨斑偏少偏小 |
| 4（当前） | 覆盖 0.21、柔度 0.1、水晕 0.09、平铺 0.0065（墨斑更大）、墨芯 alpha 0.92、水晕 alpha 0.3 | 待用户评 |

已知问题 / 可调项：掌根横向的一条深线是网格本身的折痕（Fresnel 在折痕处变黑），要去掉就重算平滑法线；想要"呼吸"式的忽隐忽现可以给 `_RimStrength` / `_FxAlpha` 加时间调制，目前是静态的；墨斑在飞行中会随物体空间固定不动，只有慢漂。

## 9. 墨雾 shader `FX/InkFog`（2026-10-29，用户第二次改方向）

用户看过第 8 节的玻璃版后否决："不是玻璃，而是压根就没有模型"：水墨当噪点，在算好的 mesh 上不断翻涌成一种混沌感；掌心浓墨聚集，手背稀薄，内部留空，边缘自然飘散，没有实体外壳和掌心标志；贴图或粒子参考插件包。参考图是三张水墨烟雾聚成的手。

| 项 | 值 |
|---|---|
| shader | `Assets/Rendering/Shaders/FX/FX_InkFog.shader`，手写 URP HLSL，走 `FXCommon.hlsl` 约定；只画正面、Alpha 混合、不写深度、不读深度纹理 |
| 材质 | `Assets/VFX/如来神掌/Materials/Mat_FX_Palm_InkFog.mat`，挂在 `VFXtests` 的临时对象 `Palm_Tripo_Preview` 上（第 8 节的玻璃材质 `Mat_FX_Palm_InkGlass` 保留但不再挂） |
| 贴图 | 云雾 `Tx_Noise_20`（墨）+ `Noise_TilingSoftWebbing`（域扭曲与顶点翻涌），都来自 `Assets/Piloto Studio/Textures/`，物体空间三平面采样 |
| 截图 | 受害者（掌心）侧 [inkfog-v1-palm-side.png](palm-mesh-tripo/inkfog-v1-palm-side.png)、施法者（手背）侧 [inkfog-v1-back-side.png](palm-mesh-tripo/inkfog-v1-back-side.png)、白底三视图 [inkfog-v1-views.png](palm-mesh-tripo/inkfog-v1-views.png) |

做法：

- **mesh 只是骨架**：不画任何表面、不打光；每个像素只算"这里的墨有多浓"再去查噪声。顶点沿法线按滚动噪声抖动（`_Wobble` 1.5 单位），轮廓一直在变，看不出网格。
- **密度场**（决定噪声阈值）：掌心侧（物体 +Z 法线）1.0、手背侧 0.35（`_PalmDensity` / `_BackDensity`）；每个形体中间空、墨往轮廓聚（`_CoreDensity` 0.35 → 1，按视角 Fresnel 的 0.6 次方），这就是"内部留空"；轮廓之外再按 Fresnel 高次方削掉一点（`_EdgeFray`），让边缘散成丝。
- **体积感**：同一网格画三层壳（沿法线外推 0 / 3 / 6 单位，`_ShellSpacing` 3），每层密度打五折（`_ShellFalloff` 0.5）、噪声相位错开，所以轮廓外面有飘出去的墨丝，相机动时三层互相滑动。
- **翻涌**：云雾噪声沿 +Y 以 8 单位 / 秒上滚（`_Rise`），采样位置先被第二张噪声做域扭曲（`_Warp` 8 单位、慢漂 0.04），再叠一层 3 倍细节（权重 0.6）；阈值放软（`_Softness` 0.5）让墨从灰到黑有渐变。
- **sRGB 陷阱**：两张贴图都是 sRGB 导入，shader 采到的是线性值（R 中位数 0.69 变成 0.43），第一版因此几乎全透明。`_NoiseLow` 0.22 / `_NoiseHigh` 0.68 把线性值的有效区间拉到 0–1 后再比阈值；域扭曲噪声按线性均值 0.3 居中。`_Debug` 1 / 2 / 3 能直接看噪声、密度、掌心权重 + Fresnel + 原始云雾。

调参记录（每轮都是白底正视图 + 场景机位）：

| 版 | 改动 | 结果 |
|---|---|---|
| 1 | 单层壳，阈值按 sRGB 值 | 几乎看不见（线性值问题） |
| 2 | 线性区间重映射，密度 0.7 / 中心 0.5 | 大片浅灰水晕，墨斑少，手指像被切平的圆柱 |
| 3 | 三层壳（间距 3）、平铺 0.02、扭曲 12、水晕 0.3 | 噪声盖过手形，手指糊成一团 |
| 4 | 中心密度 0.15、壳间距 1.5、软度 0.5、不要水晕 | 又太淡：轮廓只剩细线 |
| 5（当前） | 密度 0.9、中心 0.35、Fresnel 0.6 次方、壳间距 3、细节 3 倍 × 0.6、扭曲 8 | 正视图读得出墨聚成的手：掌心浓、手指轮廓、手背薄；施法者机位看到的是手背，所以很淡，这是要求的"手背稀薄" |

待用户评后再做：外飘的墨丝粒子层（用包的烟雾材质从网格表面发射，Shape = Mesh）；动态效果只有看 Play 才能评，静帧只看得出分布。

## 10. 体积墨雾 `FX/InkVolume`（2026-10-29，用户第三次细化）

用户看过第 9 节的壳层墨雾后定方向："对，但进一步细化需要变成体积雾、水蒸气"，随后补充"水墨质感的流动感"。做法从"在网格表面画雾"改成"在包围盒里对真正的体积做光线步进"，网格只用来烘一张距离场。

| 项 | 值 |
|---|---|
| shader | `Assets/Rendering/Shaders/FX/FX_InkVolume.shader`：画在一个单位立方体上（`Cull Front`，相机进盒子也能用），每个像素沿视线穿过盒子做自适应步进求墨的消光 |
| 烘焙工具 | `Assets/Editor/VFX/PalmVolumeBaker.cs`：`BakeSdf`（网格 → 有符号距离场，64 × 96 × 48，格子约 1.2 单位，包围盒外扩 12 单位；距离用逐三角形精确最近距离并行算，符号用临时 MeshCollider 沿三轴射线奇偶投票，32 s）、`BakeNoise3D`（64³ 四倍频可平铺值噪声） |
| 资产 | `Assets/VFX/如来神掌/Mesh/SDF_FX_Palm_Tripo.asset`（R8，0.5 = 表面，0 / 1 = ∓18 单位，`_SdfRange` 36）、`Noise3D_FX_Ink.asset`（R8 64³）、材质 `Mat_FX_Palm_InkVolume.mat`；场景里 `Palm_Tripo_Preview/Volume` 是那个立方体（局部位置 = 盒原点 + 尺寸 / 2，缩放 = 尺寸），手本身的 MeshRenderer 已关 |
| 截图 | 掌心侧三个时刻（t = 0 / 1.5 / 3 s）[inkvolume-v1-palm-3frames.png](palm-mesh-tripo/inkvolume-v1-palm-3frames.png)、手背侧 [inkvolume-v1-back-side.png](palm-mesh-tripo/inkvolume-v1-back-side.png)、只看距离带不加噪声的调试图 [inkvolume-v1-sdf-shape-debug.png](palm-mesh-tripo/inkvolume-v1-sdf-shape-debug.png) |

密度场（每个采样点）：

- **薄壳**：`band = 1 − |sdf − (−1)| / 2`（平滑），即表面内 3 单位到外 1 单位之间；壳内再往里是空的（内部留空）。**薄壳是读出手形的关键**：从外面看，掌心壳和手背壳落在同一像素上，"中空"本身看不见；参考图里"轮廓深、中间浅"来自视线擦着薄壳的切线走得长、正穿走得短，壳越薄（相对手指半径）对比越强，壳厚 5 时整只手就是一团烟（第 6–7 轮）。
- **溢出**：壳外 `0.2 × exp(−距离 / 3)`，并且噪声阈值随距离抬高（`farness`），所以壳外只剩最强的丝，不会整盒都是雾（第 2–5 轮整箱雾的原因就是溢出沿整条视线积分）。
- **掌心 / 手背**：用 SDF 的 z 向梯度判断表面朝向，+Z（掌心）1.0、−Z（手背）0.25。
- **水汽**：3D 噪声先被自身做域扭曲（14 单位、漂移 0.03），再沿 +Y 以 3 单位 / 秒上升，叠 2.7 倍细节（权重 0.5）；`_NoiseLow` 0.35 / `_NoiseHigh` 0.75 重映射后过软阈值 0.32 / 柔度 0.3。流动感 = 域扭曲随时间漂 + 上升；三帧对比图能看出墨丝在翻。
- **手腕**：平切口处噪声阈值抬 0.7、密度减半，雾在手腕处被撕成丝而不是一刀平。
- **盒边**：离盒面 6 单位内淡出。
- **积分**：Beer–Lambert，消光 0.6 / 单位；薄处灰（`_ThinColor` 0.5）、厚处黑。步进自适应：离壳远时步长 = 到壳距离 × 0.6（最大 6），壳内 0.8 单位，最多 120 步，透射率 < 0.01 提前结束。

两个坑：

1. **`Texture3D.SetPixels` 对 RHalf 写不进有效数据**（读回全 0 / 采样为常数），改成 R8 + `SetPixelData<byte>` 并把距离归一化到 0–1 才正常。噪声 R8 一开始就没问题。
2. 第 2–5 轮"整箱烟"不是 SDF 错（`_Debug` 1 只看形状时手是对的），是壳外密度沿长路径积分后不可忽视；体积效果里任何"微小的常数密度"都会被路径长度放大。

性能：盒子占屏时每像素最多 120 步 × 4 次 3D 采样（SDF + 2 次梯度 + 噪声 3 次扭曲 + 2 次噪声），编辑器 1280 × 720 单只手肉眼无卡顿，没有量化；量产前要做：步数按质量档位降、远距离降步、6 只同屏的实测。

调参记录：

| 版 | 改动 | 结果 |
|---|---|---|
| 1 | RHalf SDF、带宽 7、溢出 0.15 | 整箱烟（SDF 读成常数） |
| 2–3 | R8 SDF、读回验证 | 仍整箱烟（溢出沿路径积分） |
| 4–5 | 溢出指数衰减 + 阈值随距离抬高、盒边淡出 | `_Debug` 1 能看见手形；加噪声后仍读成一团（壳太厚，前后壳叠加） |
| 6–7 | 带宽 3.5、噪声细一倍、三个时刻连拍 | 一团烟，流动能看出来 |
| 8 | **壳厚 2、消光 0.6、自适应步进、手腕淡出** | 第一次读出手形：掌缘、手指深墨，中间灰雾 |
| 9（当前） | 阈值 0.32（掌心留白更多）、手腕改噪声撕开、溢出 0.2、手背 0.25 | 待用户评 |

## 11. 填充度参数、轮廓项与表面黑烟（2026-10-29，用户第四次反馈）

用户对第 10 节的三点反馈：(1) 水墨不等于一团黑雾，形体轮廓必须读得清，但不要写死 / 刻意渲染表面；要一个 0–1 的参数，越接近 1 体积雾越填满实体，动态随机；(2) 那张糊图是什么——答：`_Debug` 1 的距离场调试图，不是效果；(3) 底层有了，还缺表面效果：像冒黑烟，动向随机，从网格表面源源不断往上冒。

### 11.1 `FX/InkVolume` 新增

| 参数 | 作用 |
|---|---|
| `_Fill` 0–1 | 雾带向内的深度 = `_BandWidth + _Fill × _FillDepth`（`_FillDepth` 40）。0 = 只有表面附近 2 单位的壳、内部空；1 = 整个实体都是雾。三档对比 [inkvolume-v2-fill-0-05-1.png](palm-mesh-tripo/inkvolume-v2-fill-0-05-1.png) |
| `_ContourFloor` 0.5、`_ContourBoost` 1.5、`_RimPower` 2 | 轮廓项：用 SDF 梯度（三轴各两次采样）当表面法线，与视线夹角越接近 90°（掠射）越算"轮廓"；轮廓处雾永远不被噪声完全掏空（floor），密度再加成（boost）。这是形体在任何填充度下都读得出的原因，而且它只在剪影处起作用，不是画表面 |

动态随机：噪声域扭曲随时间漂移、整体沿 +Y 上升，没有固定周期；`_TimeOffset` 只是给静帧用的。

### 11.2 表面黑烟 `SurfaceSmoke`（Shuriken）

挂在 `Palm_Tripo_Preview/SurfaceSmoke`，由 scratchpad 的 `setup_smoke.py` 通过 `execute_code` 生成（参数都在脚本里，可重复执行覆盖）：

| 模块 | 设置 |
|---|---|
| Shape | Mesh（`Mesh_FX_Palm_Tripo`，Triangle 表面发射，法线偏移 1）——"从网格表面源源不断冒" |
| Main | 速率 80 / s，寿命 1.75–3.25 s，起始大小 6–14，起始速度 1.2–4，随机旋转，世界空间，上限 400 |
| Velocity over Lifetime | y 7.2–12（往上冒），x / z ±2（随机方向） |
| Noise | 强度 4、频率 0.08、滚动 0.3、2 倍频、带阻尼——"动向随机" |
| Size / Color / Rotation over Lifetime | 0.5 → 1 → 2 倍放大；黑 → 深灰，alpha 0 → 0.85 → 0.5 → 0；自转 ±0.6 rad/s |
| Texture Sheet | 包里的 `Smoke_cloud_Sharp_1_render_all`（8 × 8 序列帧，整张播一遍，随机起始帧） |
| 材质 | `Mat_FX_Palm_SurfaceSmoke.mat`，URP Particles/Unlit，Alpha 混合，不写深度（没有软粒子：Balanced / Performant 没有深度纹理） |

静帧：模拟到 2 / 3 / 4 s 的连拍 [surface-smoke-v1-3frames.png](palm-mesh-tripo/surface-smoke-v1-3frames.png)（体积 `_Fill` 0）。第一版速率 40、大小 14 时烟团太散太大，改成 80 / 10。

未做 / 待定：烟的材质还没换成包的 `UberFXSG` 三色映射（现在是黑色贴图直接 Alpha 混合，灰得偏均匀）；烟量、大小、上升速度等玩法时序定了再调；粒子数 400 超出 README 第 8 节的 600 总预算的一大半，量产要压。

### 11.3 用户看过 11.1 / 11.2 后的三条意见与处理（同日）

1. **烟要一缕一缕的，像气态行星表层的流纹，不要一团团**：`SurfaceSmoke`（序列帧烟团）停用，换成 `SurfaceWisps`：粒子本身不画（`renderMode None`），只画 Trails（每粒子一条，寿命 0.8 × 粒子寿命，宽 1.3 → 0 渐细，`Stretch` 贴图，包里的 `Trail_TaperedSoft_02`，材质 `Mat_FX_Palm_Wisp.mat` 双面 Alpha）；从网格表面发射 50 / s，寿命 2.5–4.5 s，上升 1.5–3，绕 Y 轨道 0.4–0.8 rad/s，径向 −1.2 把丝拉回手面，低频噪声（强度 3、频率 0.03、2 倍频）让它们弯成流纹。第一版（宽 2.5、噪声 7、`Trail_SmokyArcane`）是硬边粗带、飞得太远。连拍 [surface-wisps-v1-3frames.png](palm-mesh-tripo/surface-wisps-v1-3frames.png)。
2. **"这个效果不错，整个实体也能有这样的轮廓感就好了"**（指 `_Fill` 0 那张只有剪影笔触的图）：体积 shader 加嵌套等距轮廓层——SDF 每 `_LayerSpacing` 6 单位一层、`_LayerWidth` 1.2 厚、`_LayerStrength` 0.8，`_Fill` 决定往里有几层；`_InnerSolid` 0–1 在"只有轮廓层"和"实心雾"之间混（现 0.2）。等距层和外表面平行，所以轮廓项对它们同样起作用，实体内部也是一圈圈笔触。噪声按 `_NoiseStretch` (1, 0.35, 1) 沿 Y 拉长成流纹。手腕平切口不再参与轮廓项（之前会画出一圈椭圆）。对比 [inkvolume-v3-layers-fill-0-1.png](palm-mesh-tripo/inkvolume-v3-layers-fill-0-1.png)。
3. **初稿参考**（水墨玻璃三视图）：用户提醒回头对照——白底、墨以水晕聚在轮廓和关节，中间留白。当前体积版的方向与之一致，差在墨晕的"湿边"质感（三色映射）还没做进体积里。

**交付物**：`Assets/VFX/如来神掌/Bullet/VFX_Palm_Bullet.prefab`（根：手网格 MeshRenderer 关 → `Volume` 立方体挂 `Mat_FX_Palm_InkVolume` → `SurfaceWisps` 粒子），拖进场景即可，根的 +Z 是掌心方向。预览对象已从 `VFXtests` 场景移除，编辑器按用户要求关闭。

## 12. 烟雾模拟算法与热气扭曲（2026-10-29，用户第五次反馈）

用户看过 11.3 的丝状烟后："完全就是一块一块的，不是模拟烟雾"，给了三张参考（舞台干冰雾、墨入水、细烟柱），要求：(1) 表面冒出的黑烟要像参考那样；(2) 实体内部加"火堆上方那种空间扭曲"；(3) 做调研、写合理的随机烟雾算法、在模型上持续播放；不用再出图，做完在 Unity 里直接看。

### 12.1 调研结论

- 粒子的运动场用 **curl noise**：标量噪声取旋度得到无散度速度场，粒子跟着走不会聚堆也不用解压力；"活"的感觉来自噪声场随时间慢慢演化，不是粒子本身（[shader-lab 的 curl particles](https://easymaking-site.pages.dev/shader-lab/30-curl-particles.html)）。Unity Shuriken 的 Noise 模块内部就是 curl noise（多次采样 Perlin 合成），强度是寿命曲线，频率低 = 柔慢，高 = 急促；Damping 让强度随频率缩放；Octaves 叠细节（[Unity 手册 Noise module](https://docs.unity3d.com/2021.2/Documentation/Manual/PartSysNoiseModule.html)）。
- 烟团的形不用序列帧也可以：**用噪声侵蚀一张径向渐变**，每个粒子随机偏移噪声 UV 避免重复，侵蚀强度随寿命增大，烟团边缘随淡出而撕开（Velan Studios，[Tip of the Brush](https://medium.com/velan-studios/tip-of-the-brush-the-joy-of-technical-vfx-4ba15deba9fe)）。
- 学术上细烟（香烟、墨入水）用 **涡丝 / 涡环** 原语做（Angelidis & Neyret，[Simulation of Smoke Based on Vortex Filament Primitives](https://hal-univ-tlse3.archives-ouvertes.fr/INRIA/inria-00402131v1)；Houdini 的 [filament particles](https://www.sidefx.com/docs/houdini/dopparticles/filaments.html) 是它的工程版）；游戏里一般用预渲染序列帧或 GPU 大量粒子（[讨论](https://hn.nuxt.dev/item/46497589)）。本项目不做涡丝求解，用 curl noise + 侵蚀精灵近似。
- 层流 → 湍流的过渡（参考图里烟柱下段直、上段卷）没有现成资料，按物理直觉做成"噪声强度随寿命上升"：出生时几乎只有浮力，中段开始卷，末段完全湍流并扩散变淡。

### 12.2 算法（`Plume` 粒子系统 + `FX/SmokeSprite`）

挂在 `VFX_Palm_Bullet` prefab 下的 `Plume`（由 scratchpad `setup_inksmoke.py` 生成，可重复执行覆盖）：

| 环节 | 实现 |
|---|---|
| 发射 | Shape = Mesh（`Mesh_FX_Palm_Tripo` 三角面），22 / s，法线偏移 0.5，初速 0.4–1.2，寿命 3.4–5.6 s，预热 |
| 浮力 | Velocity over Lifetime y = 6 × 曲线(0 → 0.15, 0.4 → 1, 1 → 0.45)：层流起步慢、中段最快、末段冷却变慢；径向 +0.3 离开表面；轨道 ±0.25 轻微绕手；Limit Velocity 阻尼 0.25 |
| 湍流 | Noise（curl）强度 7 × 曲线(0 → 0.08, 0.35 → 0.5, 1 → 1)，频率 0.035，3 倍频 × 0.5，滚动 0.25，Damping，High；位置 1、旋转 0.6、尺寸 0.15 |
| 扩散 | Size over Lifetime 0.45 → 1.2 → 2.6（起始 8.4–15.6，末期约 30）；alpha 0 → 0.8 → 0.56 → 0；自转 ±0.35 |
| 精灵形状 | `FX/SmokeSprite`（`Mat_FX_Palm_Plume.mat`）：径向渐变先被低频噪声扭曲（`_Warp` 0.5），再被 `Tx_Noise_20` 侵蚀；噪声按粒子随机种子偏移并随寿命旋转（0.25 圈）、漂移；侵蚀阈值随寿命 0.1 → 0.7，柔度 0.5，所以烟团出生是软团、中段长出卷边、末段撕成丝。薄处灰 (0.45)、厚处黑。数据通过 Custom Data：Custom1.x = 寿命 0–1，Custom1.y = 每粒子随机常数；顶点流 Position / Color / UV / Custom1.xyzw |
| 细丝 | `SurfaceWisps`（11.3 的 Trails）降到 20 / s、alpha 0.45，只做配角 |

### 12.3 热气扭曲（`FX/InkVolume` 的 `_INKVOLUME_HAZE`）

材质开关 `_HazeOn`。光线步进时记下第一次接近雾带的采样点，用 3D 噪声在那一点取一个随时间快速上升（25 单位 / 秒）的二维偏移，乘以雾的累计厚度（`_HazeStrength` 0.12）和 `_HazeScale` 0.02 屏幕比例，去采 `_CameraOpaqueTexture`（URP 的 `SampleSceneColor`），再把墨按透明度叠在扭曲后的场景色上，整个包围盒以不透明方式输出（没有雾的像素输出的就是原场景色，看不出差别）。**前提**：管线资产的 Opaque Texture 要开。当前 `GraphicsSettings.currentRenderPipeline` 是 `URP-HighFidelity`，它已经开着，所以没改任何管线资产；Balanced / Performant 没开，在那两档下要么关掉 `_HazeOn`，要么接受黑盒（`SampleSceneColor` 会采到空纹理）——这条和 README 第 7 节"扭曲只在 High Fidelity 生效"一致。副作用：盒子变成不透明绘制，排在它之前的其他透明物在盒子范围内会被盖掉。

**现状**：`VFX_Palm_Bullet` 的实例放在 `VFXtests` 场景 (0, 75, 7)，场景已保存，prefab 已应用；编辑器按用户要求留着开。自检静帧只看了"没有黑盒、烟在冒"，烟的动态没有评。

## 13. 地板穿插、移动拖尾、分层开关（2026-10-29，用户第六次反馈）

| 问题 | 原因 | 处理 |
|---|---|---|
| 底部一穿进地板就整块被吃掉 | 体积盒子画的是背面（`Cull Front`），默认 `ZTest LEqual`：地板挡住盒子背面的像素时整条视线被丢弃，哪怕雾在地板前面 | `FX/InkVolume` 改 `ZTest Always`，遮挡改在步进里做：`_INKVOLUME_DEPTHCLIP`（材质开关 `_DepthClipOn`，默认开）读场景深度，把视线换算成体积单位后截断步进终点。需要管线的 Depth Texture（High Fidelity 有；Balanced / Performant 没有时关掉开关，雾会穿过前景物体但不会被吃掉）。烟精灵同理加 `_SMOKE_SOFT` 软粒子淡出（`_SoftDistance` 6） |
| 移动时烟要自然拖尾成"鬼火" | 粒子已经是世界空间，发射器走了粒子留在原地 | `Plume` / `SurfaceWisps` 加 Inherit Velocity（Initial，0.35 / 0.5：出生时带一点手的速度，再被浮力和 curl 接管）和 Rate over Distance（每单位 0.05 / 0.03：投射物 600 单位 / 秒时约 30 / 18 每秒，越快拖尾越密）。拖尾长度 = 速度 × 粒子寿命（4.5 s），要短就缩寿命或加 Limit Velocity |
| 想分层看 | | prefab 根上新组件 `PalmInkLayerSwitch`（`Assets/Scripts/VFX/`）：`solo` 下拉单独看 Mesh（白模）/ Volume / Plume / Wisps，或用四个勾选组合；编辑模式下改了立即生效，关掉的粒子层会清空 |

自检：手腕压进地板后体积在地板处被正确截断、烟精灵贴地淡出（`check_floor2.png`）；发射器以 120 单位 / 秒平移 2.5 s 后，烟团留成一条尾（`check_trail.png`）。两张都只在 scratchpad，没有进仓库。场景里另有一个不是本会话加的 `DebugOBJ` 方块和被别人移到 (0, 71, 164) 的 prefab 实例，都没动。

### 13.1 第七次反馈："还是一块一块的，没有线条性"（同日）

用户指的是烟本身的算法：面片精灵不管怎么侵蚀都是"片"，参考图（墨入水、细烟柱）的本质是**连续的流线叠成的薄层**。改法：

- `SurfaceWisps` 升为主烟：粒子本体不画，只画每个粒子的连续轨迹（Trails，寿命 = 粒子全程、顶点间距 0.3、宽 1.3 × 曲线 0.6 → 1 → 0.15、`Trail_TaperedSoft_02` 拉伸贴图、黑、alpha 0.4）；90 / s 从网格表面发射，寿命 3.5–6.5 s，上升 1.75–3.5，绕 Y 0.18–0.35，径向 −0.5 先贴着手面再离开；curl 噪声强度 4、频率 0.022、2 倍频、滚动 0.08——低频 + 弱强度让每根线平滑地弯而不是抖；同屏上限 600 根。线和线叠在一起的地方自然变深，形成"层"。
- `Plume`（面片烟）保留但默认关（`PalmInkLayerSwitch.showPlume = false`），想对比可以在开关里打开。
- 体积材质 `_EdgeFade` 12、`_Spill` 0.12、`_SpillFalloff` 2.5：之前从上往下看地板时能看见一个六边形（盒子剪影）的淡灰，那是溢出雾被盒面切出来的边，现在在盒面前就衰减完。
- 热气合成改成只在有墨或有扭曲的像素写入（alpha = max(墨, 扭曲)），没有雾的像素不再碰帧缓冲。

验证状态：流线版只在 `Plume` 还开着时自检过一帧（线条形态对），关掉 `Plume` 后没再出图——此时编辑器已被别人切到 `RaceMap` 场景进了 Play 模式，我没有打断；prefab 已应用，`VFXtests` 里的实例是保存过的。

## 14. 墨入水流体模拟 `InkFluid`（2026-10-29，用户选定路线 C）

用户看过 [palm-smoke-options.md](palm-smoke-options.md) 后选了流体模拟。实现：

| 件 | 位置 | 作用 |
|---|---|---|
| `InkFluid.compute` | `Assets/Rendering/Shaders/FX/` | 2D Stable Fluids（Stam 1999）：Source → 半拉格朗日平流速度 → 旋度 + 涡量约束 + 浮力 → 散度 → Jacobi 压力（24 次）→ 投影 → 平流密度。矩形网格 `_Size`。源项是把手的 SDF 沿面片法线前后各 `halfDepth` 步进 12 次取覆盖率，覆盖处把密度**推向目标值** `_InjectTarget`（不是累加，所以快速移动时拖尾也是满强度，静止时不会越积越浓），并注入向上 + 随机横向 + 手速度 × `dragInject` 的速度 |
| `InkFluidSheet.cs` | `Assets/Scripts/VFX/` | 挂在一个 Quad 上，`[ExecuteAlways]`，编辑模式用 `EditorApplication.update` 跑；手移动时把整张场反向平移（`_Shift`），墨留在世界里成拖尾。两种朝向：`HandLocal`（固定在手的局部空间，法线 `localNormal`，不随相机转）、`FaceCamera`（广告牌）。`sheetSize`（x, y 世界单位）、`centreOffset`（面片中心相对手的偏移）、`cellSize`（每格世界单位，网格 = 尺寸 / 格）|
| `FX_InkFluidSheet.shader` | 同上目录 | 密度 → 黑墨 + 灰晕，边缘淡出，软粒子 |
| `Mat_FX_Palm_InkFluid.mat` | `Assets/VFX/如来神掌/Materials/` | 两张面片共用 |

prefab `VFX_Palm_Bullet` 下两张面片：`InkFluid`（掌心面，法线 +Z，280 × 280）和 `InkFluidSide`（侧面，法线 +X，900 × 280，中心后移 300，装拖尾）。分层开关 `Fluid` 一起控制。

### 14.1 用户反馈与处理

1. **密度太高** → 稀疏靠 `targetDensity`（0.3；0.45 就是"太高"那版，0.22 时拖尾几乎看不见）、`densityDissipation`（0.992，越小消得越快；0.985 时 2 s 的拖尾基本消光）、材质 `_Threshold` / `_Opacity`。手调方法见 14.2。
2. **测拖尾** → 侧面片以 200 单位 / 秒沿 +Z 飞 2 s，侧视截图里手后拖出约 400 单位的墨尾（scratchpad `fluid2_trail.png`）。拖尾长度 ≈ 速度 × 密度半衰期；`densityDissipation` 0.992 在 60 fps 下半衰期约 1.4 s。
3. **"为什么总是面向摄像机，Game 里会看到旋转"** → 第一版是广告牌（`FaceCamera`），Scene 视图里它对着 Scene 相机、Game 里对着主相机，所以两边看到的朝向不同、转相机时面片跟着转。改成默认 `HandLocal`：面片固定在手的局部空间，和相机无关；为了侧视不露扁，用掌心面 + 侧面两张正交面片（十字）。

### 14.2 手调（Inspector 上直接改，编辑模式实时生效）

| 想要 | 改 |
|---|---|
| 更稀 / 更浓 | `InkFluidSheet.targetDensity`（0.1–0.5）；细调 `Mat_FX_Palm_InkFluid` 的 `_Threshold`（切掉淡墨）、`_Opacity` |
| 墨消得快 / 慢（拖尾短 / 长） | `densityDissipation`（0.97 快 … 0.995 慢） |
| 丝更细更多 | `vorticity` 调高（6–12）、`cellSize` 调小（更贵） |
| 升得快 / 慢 | `buoyancy`、`injectVelocity` |
| 出手时墨被带走多少 | `dragInject`（0 不带，1 完全跟手） |
| 侧面拖尾装不下 | `InkFluidSide.sheetSize.x` 加大、`centreOffset.x` 同步后移一半 |
| 只看流体层 | 根上 `PalmInkLayerSwitch.solo = Fluid` |

性能：每张面片每帧 ~30 次 dispatch（含 24 次 Jacobi），掌心面 256 × 256、侧面 824 × 256；未量化。

### 14.3 "拖尾"指烟雾残留（鬼火），不是剪影拖带（同日）

用户澄清：要的是手经过后留在空中、自己上升卷散的烟雾残留。14.1 那版残留几乎不升、被平拖成一条带，原因是浮力 8 太弱而注入的墨又带着手的速度（`dragInject` 0.6）。改：`dragInject` 0.1（墨不跟手走）、浮力 30、`velocityDissipation` 0.985 当空气阻力——这两个一起决定残留的终端上升速度 ≈ 浮力 × 密度 × dt / (1 − 衰减)，密度 0.3 时约 10 格 / 秒（11 单位 / 秒），残留在 3 秒里慢慢升 30 单位而不是飞出面片（第一次试浮力 30 不加阻力，残留 3 秒升了 270 单位直接出顶）。涡量 10 让残留卷。面片加高：掌心 300 × 360（中心上移 40）、侧面 900 × 420（中心后移 300、上移 70）。测试改成"飞 2 s 后停 1.2 s"再从后侧 3/4 机位截图，看路径上留下的残留。

续 14.3：整只剪影每帧灌进流体会把路径画成一条均匀灰带（`fluid2_trail` 第 2 版）。鬼火残留应是从**表面边缘**稀疏冒出的丝，所以源项改成：`edgeOnly`（0 整只剪影，1 只取剪影的边缘——SDF 步进只部分命中的格子）× 随机发射斑点（`sparsity` 同时发射的斑点比例、`sparsityScale` 斑点大小，每 0.25 s 换一批）。斑点的哈希里加了累计位移 `_EmitOffset`，使斑点固定在世界而不是手的局部格子，否则手一动斑点就被画成平行横线（第 3 版）。当前：`targetDensity` 0.4、`injectRate` 20、`edgeOnly` 1、`sparsity` 0.25、`sparsityScale` 6、浮力 30 / 速度衰减 0.985、`densityDissipation` 0.995、`dragInject` 0.1；材质阈值 0.03、不透明度 1.4、对比 0.9。

### 14.4 烟从网格表面发出（同日）

用户："烟雾缭绕按表面 mesh，不是体积，也就是烟雾发出的位置。"源项加模式 `source`：`MeshSurface`（默认）= 只在手的网格表面与面片平面相交处发射，取面片平面上的 SDF，|sdf − sourceInside| < `surfaceBand`（4 单位）的格子为源；`SilhouetteRim` = 之前的沿法线穿透投影（整只剪影 / 剪影边缘）。两种模式都过稀疏斑点门。掌心面片切在手的中平面，所以发射源是手在该平面上的截面轮廓；要让掌心正面也冒烟，可以把掌心面片沿 +Z 移到掌面（`centreOffset` 只能在面片内移，面片沿法线的偏移要在 prefab 里直接挪 `InkFluid` 子物体——`FaceCamera` 外的 `HandLocal` 模式每帧会把位置重设为 hand.position + 面内偏移，所以法线方向偏移需要加字段，未做）。

## 15. 烟改随机动量 + 聚拢 / 消散时间轴（2026-10-29，用户第十一次反馈）

用户："效果可以了"后两条：(1) 烟不要往上的动量，普通的周围随机动量即可，这层烟几乎完全服务于移动时在世界留下的痕迹；(2) 开始做聚拢与消散：整段约 6 s，前 0.5 s 聚拢，最后 1 s 按曲线消散。另外报了一个错："Particle Orbital Velocity curves must all be in the same mode"——`SurfaceWisps` / `Plume` 的 Orbital Y 用了两常数模式而 X / Z 是常数模式，Inspector 一碰就报；已把三轴统一为同一模式（线速度三轴同理）。

### 15.1 随机动量

`InkFluid.compute`：注入速度改为随机方向（每个发射斑点一个角度）× `swirl`，`injectVelocity`（上升）置 0；`Forces` 加一项随机漂移场 `_Drift`（按格子块 + 每 0.5 s 换一批的哈希，乘密度），`buoyancy` 置 0。现值：swirl 30、drift 25、vorticity 10、`densityDissipation` 0.994。效果：静止时烟在手周围缓慢乱飘，移动时沿路径留下的残留就地打转、慢慢散。

### 15.2 `PalmInkTimeline`（`Assets/Scripts/VFX/`，挂在 prefab 根）

| 字段 | 值 | 作用 |
|---|---|---|
| `duration` / `gatherSeconds` / `dissipateSeconds` | 6 / 0.5 / 1 | 时间轴长度与两端 |
| `gatherCurve` | EaseInOut | 0 → 0.5 s：体积 `_FxAlpha` 0 → 1、`_FxDissolve` 1 → 0（雾从噪声里聚出来），烟注入 0 → 1 |
| `dissipateCurve` | (0,0) → (0.6,0.35) → (1,1)，末端陡 | 最后 1 s：体积 `_FxDissolve` 0 → 1，烟注入关、流体密度额外衰减（`InkFluidSheet.fadeOut`），粒子发射率 × (1 − d) |
| `playOnEnable` | 开 | 运行时实例化即播放；`loop` 可循环 |
| `preview` | 编辑器用 | 勾上在编辑模式循环播放，便于手调 |
| `Play()` / `Stop()` / `time` | | 技能代码接入点：出掌时 `Play()`，`duration` 以后对上投射物寿命 |

驱动方式：体积走 `MaterialPropertyBlock`（不改材质资产，多实例互不影响）；流体面片走 `emissionScale` / `fadeOut` 两个字段；粒子按初始发射率缩放。自检：手以 60 单位 / 秒飞行，时间轴 0.25 / 3 / 5.6 s 三个时刻截图（`timeline_t025/t3/t56.png`）。

### 15.3 第十二次反馈：挂件不跟、子物体 transform 乱、残留像盖章、散聚感不够（同日）

| 问题 | 原因 | 处理 |
|---|---|---|
| 拖根对象时部分子物体留在原地；子物体 transform 不是干净的本地值 | 两张流体面片每次模拟 tick（已限 30 Hz）才把**世界**坐标写回手上，拖动时落后一拍；写的是世界坐标，Inspector 里本地值看着是乱的 | `InkFluidSheet.PlaceLocal()`：`HandLocal` 模式只写本地 transform（localRotation = LookRotation(法线)，localPosition = 面内偏移，localScale = 尺寸），值不变就不写；子物体天然跟随父级，拖动零延迟。`OnValidate` 里改参数即时摆放 |
| 移动留下的残留一直是同一份画像 | 发射斑点整块同向、密度不扩散，剪影截面被原样盖章到路径上 | 注入方向改为**每两格一个随机角度**、每 1/6 s 换一批（不再整块同向）；斑点缩到 3 格、同时发射 15%，目标密度升到 0.6，让流体自己把少量浓墨吹散；`AdvectDensity` 加扩散（`diffusion` 0.2，每步向四邻均值混），残留像人边走边抽烟留下的那条随机散开、越来越淡的带 |
| 散聚感 | 之前只做淡入淡出 | 流体加 `_Pull`：沿面片平面上 SDF 的梯度给力，正值把墨往表面吸、负值推开。时间轴聚拢段：`pull = +60 × (1 − g)`，发射带宽 × 5 → 1（墨从更远处被吸向手）；消散段：`pull = −60 × d`（墨被推散）。体积聚拢段：`_Spill` 0.6 → 0.12、`_SpillFalloff` 14 → 2.5（远处的雾收拢到表面）；消散段：`_Warp` 8 → 40、`_Spill` → 0.5（雾被扭曲撕开再散）。都走 MaterialPropertyBlock，基准值从材质读 |

时间轴新字段：`smokePull`、`gatherBandScale`、`gatherSpill`、`gatherSpillFalloff`、`dissipateWarp`、`dissipateSpill`。

发射斑点门改为平滑值噪声（`ValueNoise` + smoothstep），斑点是圆团不是方块；`sparsity` 0.3、`sparsityScale` 5。

### 15.4 第十三次反馈：粒子要在世界里自己飞、子物体 transform 要干净、锚点偏移（同日）

- **粒子**：`SurfaceWisps` 本来就是世界空间，但开着 Inherit Velocity（0.5）、上升速度 1.75–3.5、绕轨道、径向拉回，看起来像跟着父节点走且往上。现改：Inherit Velocity 关，线速度三轴 ±0.6 随机，轨道 / 径向 0，噪声强度 5、频率 0.025（低频随机漂移），限速 6 带阻尼；发射率 18 / s + 每单位距离 0.08（移动时沿路径留）。重复的面片烟 `Plume` 从 prefab 删除。
- **子物体 transform**：两张流体面片的偏移、朝向、尺寸全部烘进运行时生成的四边形网格（`InkFluidSheet.BuildMesh`，网格顶点在手空间；`HideAndDontSave`，启用时重建），子物体 transform 一律 (0,0,0) / (0,0,0) / (1,1,1)；`Volume` 子物体保留 (0,0,0) + 缩放 = 包围盒尺寸。模拟用 `sheetToHandLocal` 矩阵代替 transform 求面片基向量。`OnValidate` 里改网格会触发 Unity 的 "SendMessage cannot be called during OnValidate" 警告，已改为 `delayCall` 延后。
- **锚点**：用户截图里 Move 手柄离手很远。原因是 Scene 工具栏的 Pivot 模式是 **Center**：手柄放在所有子渲染器包围盒的中心，而侧面流体面片长 900、中心在手后 300，把中心拉到手后方约 150。已把编辑器切到 **Pivot** 模式（手柄在根对象的 transform 上）；这是编辑器设置，不随 prefab 走，别的机器要自己切。没有把面片的包围盒改小：改小会让视野里只有拖尾、手在屏幕外时整张面片被剔除。

### 15.5 第十四次反馈：还是有向上的喷雾（同日）

读回场景实例的实际参数：`SurfaceWisps` 粒子没有向上分量（重力 0、力场关、线速度三轴 ±0.6、噪声无偏向）；向上的是两张流体面片——第十一轮只把脚本默认值改了，prefab 实例里还留着旧值 `injectVelocity` 40（新墨的初速度朝面片 +Y，即手指方向）和 `buoyancy` 30（按密度的向上加速度）。两项都在实例上清零、`ResetFluid()` 重置、prefab 已应用，脚本默认值也改为 0。

验证：边模拟边读回密度贴图，8 秒内三次采样质心 y 稳定在 127 ± 1（掌心面片 280×328 格），没有净上漂；只剩随机 `drift` 30、`swirl` 35 和涡量 10 在推墨。

### 15.6 第十五次反馈：散雾和拖尾再加重一点；颜色怎么调；palm 怎么分层（同日）

**加重散雾 / 拖尾**（手以 60 单位 / 秒飞 3 秒后从侧后方截图，`scatter-v1-before.png` → `scatter-v2-after.png`）：

| 位置 | 字段 | 之前 | 现在 | 作用 |
|---|---|---|---|---|
| `InkFluid` / `InkFluidSide` | `drift` | 30 | 60 | 随机环境动量，墨离开表面走得更远（向外散雾） |
| 同上 | `swirl` | 35 | 50 | 源头随机初速度 |
| 同上 | `targetDensity` | 0.3 | 0.45 | 注入的浓度上限（更浓） |
| 同上 | `sparsity` | 0.3 | 0.4 | 同时发射的表面比例（更多源） |
| 同上 | `densityDissipation` | 0.993 | 0.996 | 每步残留比例，残留寿命约 ×1.7（拖尾更长） |
| 同上 | `diffusion` | 0.2 | 0.25 | 向邻格扩散（散得更开、更柔） |
| `SurfaceWisps` | Rate over Time / Distance | 18 / 0.08 | 24 / 0.2 | 移动时沿路径多留粒子 |
| 同上 | Start Lifetime | 3.5–6.5 | 5–8 | 粒子残留更久 |
| 同上 | Noise Strength | 5 | 7 | 漂得更远 |

手调口诀：想要"更远"调 `drift`、`swirl`；想要"更浓"调 `targetDensity`、`sparsity`、材质 `_Opacity`；想要"留得更久"调 `densityDissipation`（0.99–0.999，越接近 1 越久）和粒子 Lifetime；想要"更散"调 `diffusion`。改完流体字段后在 Inspector 右键组件 → `ResetFluid`，或者直接拖一下手看新残留。

**颜色在哪里调**（三层各自一个材质，都在 `Assets/VFX/如来神掌/Materials/`，都是"稀薄色 → 浓墨色"按密度插值，所以换色调要两项一起改）：

| 层 | 材质 | 浓处 | 稀处 | 其他 |
|---|---|---|---|---|
| 体积（手的形体雾） | `Mat_FX_Palm_InkVolume` | `_InkColor`（默认近黑 0.01/0.01/0.015） | `_ThinColor`（灰 0.5/0.5/0.53） | 浓稀由光线穿过的厚度 × `_Extinction` 决定；`_Extinction` 越大越黑；`_Fill` 0–1 决定内部填多满 |
| 流体面片（表面冒烟 + 世界残留） | `Mat_FX_Palm_InkFluid` | `_InkColor` | `_WashColor`（0.45/0.45/0.48） | `_Opacity` 总透明度、`_Contrast` 浓淡对比、`_Threshold` 低于多少不画 |
| 粒子流线 | `Mat_FX_Palm_Wisp` | `_InkColor` | `_ThinColor` | `_Opacity`；另外粒子系统 Start Color 和 Color over Lifetime 会乘在上面（想做颜色随寿命变化就改那里，材质管基调） |

想做彩色（比如依璇配色研究里的金 / 朱砂），把三份 `_InkColor` 改成深色调、`_ThinColor` / `_WashColor` 改成同色系的浅色；三份一致才不会看出接缝。时间轴不碰颜色，只改 `_FxAlpha` / `_FxDissolve` / `_Spill` / `_Warp`，所以材质上改色随时生效。

**palm 现在怎么分层**（prefab `VFX_Palm_Bullet`，根上 `PalmInkLayerSwitch` 可 solo 或勾选单层）：

| 子物体 | 层 | 画什么 | 空间 |
|---|---|---|---|
| 根（`Buddah` 手的 MeshRenderer 默认关） | `Mesh` | 白模，只做对位用 | 手本地 |
| `Volume` | `Volume` | SDF 光线步进体积：表面一层薄壳 + 内部按 `_Fill` 的嵌套等值壳 + 外溢 `_Spill` + 热扭曲；手的"形" | 手本地（缩放 = 包围盒） |
| `InkFluid`（掌面，法线 +Z，300×360） | `Fluid` | 2D 流体：掌心正面冒出的墨和残留 | 面片跟手，墨留在世界 |
| `InkFluidSide`（侧面，法线 +X，900×420，向后伸 300） | `Fluid` | 同上，侧视和移动拖尾主要靠它 | 同上 |
| `SurfaceWisps` | `Wisps` | Shuriken 流线粒子，从网格表面发出后自己飞 | 世界 |

`Plume` 层枚举还在（兼容旧场景），对象已删。三层都是透明队列：体积 `ZTest Always` + 场景深度截断，面片和粒子软粒子，互相不排序，叠加顺序由 Render Queue 决定（体积 3000、粒子 3000（材质固定）、面片 3001，同队列的按到相机距离排）。

**`InkFluid` 是什么、去掉会怎样**：它是掌面那张 2D 流体面片——一块 300×360 的平面贴在手的 XY 平面上（法线 +Z，即掌心朝向），上面跑 Stable Fluids（`InkFluid.compute`），网格表面落在这块平面上的格子注入墨，墨在面片上被随机动量吹散并留在世界里。它负责：正面看手时表面冒出的烟、掌心 / 手指轮廓周围的散雾、手在 X / Y 方向移动时的残留。`InkFluidSide` 是同样的东西竖在 YZ 平面上（侧视、向后伸 300），负责侧面看和沿 Z 前冲时的拖尾。

去掉 `InkFluid`：正面视角只剩体积雾（形体）和粒子流线，表面不再冒烟、周围没有散雾，侧面片从正面看是一条线几乎不可见；侧视和前冲拖尾不受影响。性能上省掉一半流体开销（每帧约 30 次 dispatch、280×328 格）。想保留正面散雾又嫌多，更稳的做法是把它的 `targetDensity` / `sparsity` 降下来，或者在 `PalmInkLayerSwitch` 里临时关掉 `Fluid` 层对比看。

### 15.7 问题：拉走模型后原地留下一片"阴影"（同日）

不是光照阴影：`Volume` 渲染器虽然 Cast Shadows = On，但 `FX_InkVolume.shader` 只有一个 `InkVolume` pass、没有 ShadowCaster，投不出影；面片和粒子投影都关着。留在原地的是**流体面片的墨残留**——它按设计固定在世界里（手动时场反向平移），这就是前面要的拖尾 / 鬼火。手停着的时候，掌面面片上的墨沿整只手的表面轮廓持续积累成一片手形的浓墨；一拉走，这整团手形的墨留在原位，看起来就像一片灰色的手影，然后慢慢散开、淡掉。复现：手原地跑 2 s 后瞬移 +X 150、再过 0.5 s 截图，原位留有一片灰雾（`ghost-stamp-front.png` / `ghost-stamp-top.png`）。

第十五轮把 `targetDensity` 提到 0.45、`densityDissipation` 提到 0.996 之后，这团残留更浓、更久（0.996 每步 → 每秒剩 79%，从 0.45 衰到画不出来约 10 s；0.993 时约 6 s），所以"影子"更显眼了。可调：

- 想留得短：`densityDissipation` 回 0.993（或更低），`targetDensity` 回 0.3。
- 想留但别像一整片影子：提高 `dragInject`（现 0.1 → 0.4 左右），拉走时残留带着手的动量被拉成一条带而不是整团停在原地；或提高 `diffusion`，团块散得更快。
- 想淡处不画：材质 `Mat_FX_Palm_InkFluid` 的 `_Threshold` 从 0.03 提到 0.06–0.1，只保留浓的那部分残留。

## 16. 去掉流体面片，烟改为网格表面发射的世界空间粒子（2026-10-29，第十六次反馈，新会话接手）

用户在场景实例（`VFXtests` 里的 `VFX_Palm_Bullet`）上直接删掉了 `InkFluid`，然后指出：(1) 残留会随根节点运动抖动，Shuriken 世界空间粒子从来没有这个问题；(2) 流体那套是"粘性的"，不是要的烟雾散开；(3) `InkFluidSide` 看起来是一整片，不是按网格表面散布。三条都是 2D 流体面片的结构问题，不是参数：

- **抖动**：面片是手的子物体随手连续移动，墨"留在世界里"靠每次模拟 tick（编辑态 ≤ 30 Hz）把整张场按手的位移反向平移（`_Shift`），两者不同步，残留在"跟手走一下、被搬回去一下"之间跳；非整数格的平移还走双线性重采样，每搬一次糊一点。
- **粘性**：Stable Fluids 的压力投影把墨当不可压缩液体解，团块保体积、互相挤着走，涡量约束卷丝、`diffusion` 糊边，是"墨入水"的物理，不是烟在空气里膨胀变稀被撕碎。
- **一整片**：墨只存在于面片平面内，`MeshSurface` 源项只能取网格与该平面相交的那条轮廓线（4 单位带）当源，墨从这条线出来后在平面内扩散、整张填成灰膜。要从整只手的表面散布，源必须是三维的：世界空间粒子（Shape = Mesh）或 3D 流体格子（成本不现实）。

用户决定：**去掉流体面片，烟改为从网格表面发射的世界空间粒子。**

### 16.1 做了什么

| 项 | 处理 |
|---|---|
| 场景实例 `InkFluidSide` | 删除，并把实例应用回 prefab；prefab 现在只有 `Volume`、`SurfaceWisps`、`SurfaceSmoke` 三个子物体（`InkFluid` 一并从 prefab 消失） |
| 新子物体 `SurfaceSmoke` | Shuriken，世界空间，Shape = Mesh（`Mesh_FX_Palm_Tripo`，Triangle，沿法线 + 0.35 随机方向，`normalOffset` 0.5），画 `Mat_FX_Palm_Plume`（`FX/SmokeSprite` 噪声侵蚀烟精灵，Custom1 = 寿命 / 种子顶点流），Billboard、按距离排序、上限 500 |
| `PalmInkLayerSwitch` | 层枚举改为 `None / Mesh / Volume / Smoke / Wisps`：`Smoke` 占原 `Plume` 的槽位、对应 `SurfaceSmoke`；`Fluid` 层和 `showFluid` 删除 |
| 未动 | `PalmInkTimeline` 仍按 `InkFluidSheet` 子物体循环（找不到就跳过，无害），粒子发射率仍由时间轴缩放；`SurfaceWisps` 参数未改 |
| 留在仓库但已无人用 | `Assets/Scripts/VFX/InkFluidSheet.cs`、`Assets/Rendering/Shaders/FX/InkFluid.compute`、`FX_InkFluidSheet.shader`、`Mat_FX_Palm_InkFluid.mat`。要不要删由用户定（删掉要同步改 `PalmInkTimeline`） |

### 16.2 `SurfaceSmoke` 参数（第一版，等用户看）

| 模块 | 值 | 意图 |
|---|---|---|
| Emission | 24 / s + 0.25 / 单位距离 | 静止时表面持续冒，移动时沿路径留残留 |
| Start Lifetime / Size / Speed | 3–5 s / 4.9–9.1 / 1.6–4（沿表面法线） | 出表面后慢慢离开 |
| Velocity over Lifetime（世界） | 三轴 ±1 随机 | 随机环境动量，无上升 |
| Noise | 强度 10、频率 0.03、滚动 0.15、2 层、阻尼 | 低频乱飘 |
| Limit Velocity | 5、阻尼 0.1 | 不飞远 |
| Size over Lifetime | 0.55 → 1（25%）→ 2.8 | 烟膨胀 |
| Color over Lifetime | α 0 → 0.5（15%）→ 0.3（55%）→ 0 | 膨胀时变稀、淡掉 |
| Rotation over Lifetime | ±0.4 rad/s | 缓慢自转 |
| Inherit Velocity / Gravity / Force | 关 / 0 / 关 | 发出后不跟父节点 |

自检：原地模拟 4 s 得 93 个粒子，包围盒半径 38 / 48 / 22（手是 30 / 45 / 17），即 4 s 内平均散出约 8 个单位；编译无错误；prefab 已应用、场景已保存。效果好坏未验证，等用户在 Unity 里看。

手调口诀：想"散得更远"调 Noise 强度 / Limit Velocity 上限 / Start Speed；想"更浓"调 Color over Lifetime 的 α 和 Emission；想"块变丝"把 Size 调小、Emission 调高、`Mat_FX_Palm_Plume` 的 `_ErodeStart` / `_ErodeEnd` 提高（侵蚀更狠）；想"留更久"调 Start Lifetime。两层粒子（`SurfaceSmoke` 烟体、`SurfaceWisps` 流线）都可在根上的 `PalmInkLayerSwitch` 里 solo 对比。

### 16.3 第十七次反馈：水墨风保持了吗；拖动时烟要像火柴烟那样跟着源点（同日）

**烟跟随源点**。用户要的是火柴烟：源点附近的烟随火柴头移动，越老的烟越落在后面留在世界里。这和前面"粒子发出后不跟父节点"的口径有冲突，按本次要求改。做法是 `SurfaceSmoke` / `SurfaceWisps` 打开 Inherit Velocity，模式 **Current**（每帧叠加发射器当前速度），乘一条随寿命衰减的曲线：出生 1.0 → 寿命 30% 时 0.45 → 寿命 60% 时 0 → 之后 0；`emitterVelocityMode = Transform`，所以编辑器里拖根节点也算发射器在动。模拟空间仍是世界，老烟不受影响。自检：手以 60 单位 / 秒沿 +X 滑 2 s（到 x = 120），寿命 < 25% 的烟平均在 x = 100（紧跟手后），寿命 > 60% 的烟平均在 x = 11（留在起点附近）。

想调：曲线起点（1.0）是刚出生的烟跟得多紧；衰减到 0 的位置（0.6）是烟多久脱离源点，越小脱得越早、残留越多；Emission 的 Rate over Distance（0.25 / 单位）决定路径上留多少。

**水墨风**。之前 `Mat_FX_Palm_Plume` 是中性灰烟（稀处 0.45 灰，核心软、侵蚀软），读起来是灰雾不是墨。按 [仪玄配色研究](yixuan-palette.md) 改成：

| 字段 | 之前 | 现在 | 意图 |
|---|---|---|---|
| `_InkColor` | 0.02 / 0.02 / 0.025（近纯黑） | 0.11 / 0.11 / 0.094（`#1C1C18` 暖橄榄墨） | 墨带色相，不用纯黑 |
| `_ThinColor` | 0.45 / 0.45 / 0.48（浅灰） | 0.22 / 0.204 / 0.157（`#383428` 墨晕） | 稀处是化开的墨，不是白雾 |
| `_Opacity` | 0.9 | 1.0 | |
| `_CoreHardness` / `_Softness` | 1 / 0.4 | 1.8 / 0.18 | 核心实、边缘利，像笔触 |
| `_ErodeStart` / `_ErodeEnd` | 0.05 / 0.65 | 0.2 / 0.9 | 一出生就有破边，死前撕成丝 |
| `_Warp` / `_NoiseTiling` | 0.5 / 0.7 | 0.7 / 1.2 | 轮廓更扭、纹理更密 |
| `_Spin` / `_Drift` | 0.25 / 0.4 | 0.2 / 0.5 | |
| `SurfaceSmoke` Color over Lifetime α | 0 → 0.5 → 0.3 → 0 | 0 → 0.7（12%）→ 0.45（55%）→ 0 | 墨更实 |

只改了烟精灵材质；`Mat_FX_Palm_InkVolume`（稀处 0.5 灰）和 `SurfaceWisps`（粒子色近纯黑）没动，三层要统一色调时一起改（15.6 节的表）。prefab 已应用、场景已保存。

## 17. 恢复流体面片，模拟网格固定在世界空间（2026-10-29，第十八次反馈）

用户看过粒子版后："完全失去之前那种感觉了"。之前的感觉来自 Stable Fluids 的连续密度场（墨团连成片、浓淡渐变、涡量拉出的细丝），`FX/InkFluidSheet` 只是把密度按阈值和对比度画成墨，粒子精灵叠加做不出连续场。用户决定：**恢复流体，抖动从根上修。**

### 17.1 做法：`InkFluidSheet.orientation = WorldFixed`（新默认）

- 第 16 节定位的抖动根源是"面片随手连续移动、场只在 tick 时反向搬回"。现在网格**固定在世界里**：第一次放置时以手的位置和 `localNormal`（手空间）定下锚点 `anchorPos` / `anchorRot`，之后手在不动的格子上扫过，每格的墨就留在它所在的世界位置，不再有逐帧的反向平移和重采样。
- 画面片的不是 prefab 里的子物体，而是一个运行时生成、不挂在任何父节点下的隐藏对象 `InkFluidSide (world quad)`（`HideAndDontSave`，场景里不可选、不存盘），所以父节点怎么动都碰不到它。子物体自己的 MeshFilter 为空，只做参数载体；`PalmInkLayerSwitch` 切子物体的 Renderer 开关时，world quad 跟着镜像。
- 手快走出格子时**整格重心**：手离放置点超过 `recenterFraction`（0.25）× 面片尺寸，锚点按 `round(偏移 / cellSize)` 个整格平移，同一帧把场平移同样的整数格。整数格的平移在平流里正好采到像素中心，是精确拷贝，不糊。`emitOffset` 同步累加，发射斑点仍固定在世界。
- `Source` 核的 `_SheetToHand` 改为 `hand.worldToLocal × TRS(anchor)`，手相对网格任意位置、任意朝向都能正确取到网格表面截线。`dragInject` 用的手速度仍按手在面片空间的位移算。
- 手旋转时面片朝向不跟着转（转要重采样）；要重新对齐右键组件 → `Recentre()`（清空墨、把面片重新放到手上）。`ResetFluid()` 只清墨不动锚点。
- 旧的 `HandLocal` / `FaceCamera` 模式保留（走原来的逐 tick 搬场路径），没有用到。

### 17.2 场景与 prefab 的变化

- `SurfaceSmoke` 粒子（第 16 节）删除；`SurfaceWisps` 的 Inherit Velocity 关回去（恢复到用户认可的第十五轮状态）；`Mat_FX_Palm_Plume` 第 16.3 节的改色留着，没有对象在用。
- `InkFluidSide` 按第十五轮的参数重建（900×420、中心后移 300 / 上移 70、cell 1.1、`targetDensity` 0.45、`sparsity` 0.4、`swirl` 50、`drift` 60、`vorticity` 10、`diffusion` 0.25、`densityDissipation` 0.996、`velocityDissipation` 0.985、`dragInject` 0.1、`localNormal` +X），`orientation = WorldFixed`，`recenterFraction` 0.25。掌面 `InkFluid` 未重建（用户之前自己删的）。
- `PalmInkLayerSwitch` 的 `Fluid` 层恢复（枚举 `None / Mesh / Volume / Smoke / Wisps / Fluid`）。
- prefab 已应用、场景已保存；world quad 没有被序列化进场景或 prefab。

### 17.3 自检（Tick 由反射驱动，手在 (0, 72.9, 583.7)）

| 步骤 | world quad | 墨质量 | 墨的世界质心 |
|---|---|---|---|
| 原地 2 s | (0, 142.9, 283.7)，= 手 + 后 300 / 上 70 | 575 | z = 582.7（在手上） |
| 手 +Z 100，再 0.5 s | 没动（0.00） | 866 | z = 623.5（旧墨留在 583、新墨在 683 之间） |
| 手 +Z 300，再 0.5 s | 跳了 300.30 = 整 273 格 | 1202 | z = 700.5（旧墨仍在原处，新墨在 883） |

即：手小幅移动时面片和墨都纹丝不动；大幅移动触发一次整格重心，墨的世界位置不变。编译无错误。效果和"粘性"手感未验证，等用户看；调"粘性"的旋钮：`pressureIterations`（少 → 更松）、`diffusion`（小 → 边缘更硬）、`drift` / `swirl`（大 → 更快散）。

## 18. 三维化：compute 粒子从整只手的网格表面散布（2026-10-29，第十九次反馈）

用户：感觉对了，但流体还是"一片"；要的是烟按手部实体的**三维表面形状**发射、在三维空间里存在，"我们做的是粒子特效"。三条路（三维流体体积 / GPU 粒子聚成墨 / 多张面片）里用户选了 GPU 粒子，VFX Graph 用不用交给我定：**自己写 compute 粒子**，不碰"VFX Graph 仍不用"的规则。随后用户重申：(1) 不带方向喷出，围绕表面网格散布，寿命短、产量高；(2) 散布的目的是实例移动时模拟出拖尾；(3) 效果优先，性能其次。

### 18.1 实现（三个新文件）

| 文件 | 作用 |
|---|---|
| `Assets/Rendering/Shaders/FX/InkParticles.compute` | `Emit`：按三角形**面积加权**在整只手的网格表面随机取点（二分累计面积表），各向随机的小初速度，不继承手速（可调）；`Update`：世界空间，curl 噪声流场（三通道 fbm 势场取旋度，无散，所以粒子聚成片和丝而不是堆成团；场固定在世界、随时间缓慢演化，残留原地打转）、按手的 SDF 梯度把贴近表面的粒子向外推（`pushBand` 内）、进了实体直接弹出、阻尼 + 限速；`Clear`。环形缓冲，最老的先被覆盖 |
| `Assets/Rendering/Shaders/FX/FX_InkParticle.shader` | `SV_VertexID` 生成面向相机的小软圆盘（每粒 6 顶点），单粒很淡（`_Opacity` 0.05），密处叠成实墨、稀处是墨晕；颜色按寿命从 `_InkColor`（`#1C1C18`）到 `_WashColor`（`#383428`）；软粒子；`_FxAlpha` / `_FxDissolve` 留给时间轴 |
| `Assets/Scripts/VFX/InkParticles.cs` | 组件：建三角形表、每 tick 发射 + 更新，`RenderPipelineManager.beginCameraRendering` 里对每个相机 `Graphics.DrawProcedural`（没有 Renderer 组件，层级里没有东西跟手动）。编辑模式 30 Hz 模拟。`emissionScale` / `fadeOut` 字段给时间轴（时间轴还没接） |

材质 `Assets/VFX/如来神掌/Materials/Mat_FX_Palm_InkParticle.mat`。prefab 新子物体 `InkParticles`；`PalmInkLayerSwitch.showFluid` 置 false（流体面片留在 prefab 里但不画，对比用；粒子层的开关是组件自己的 enabled）。

### 18.2 参数（第一版）

| 字段 | 值 | 对应用户要求 |
|---|---|---|
| `capacity` / `rate` | 65536 / 20000 每秒 | 产量高 |
| `lifeMin` / `lifeMax` | 0.8 / 2.0 s | 寿命短 |
| `startSpeed` / `randomDirection` | 2 / 1.0 | 不带方向，贴着表面各向散布 |
| `inheritVelocity` | 0 | 残留留在世界，手动就拖尾 |
| `noiseStrength` / `noiseFrequency` / `noiseScroll` / `noiseOctaves` | 14 / 0.04 / 0.15 / 2 | 表面周围缓慢卷动 |
| `damping` / `maxSpeed` | 1.0 / 10 | 不飞远 |
| `pushBand` / `pushStrength` | 3 / 30 | 不进实体 |
| 材质 `_Opacity` / `_Size` / `_SizeGrow` / `_Core` | 0.05 / 1.0 / 1.5 / 1.2 | 单粒淡、叠起来是墨 |

自检：原地 3 s 后 27.6k 粒子存活、无 NaN、离手最远 57 单位（手半高 45）；截图 `palm-mesh-tripo/particles-v1-rest.png`（正面：烟贴着手形，手指逐根可读）、`particles-v1-trail.png`（手以 60 单位 / 秒前冲 1.5 s 后从侧后方看：路径上留下一条逐渐变稀的尘状拖尾）。编译无错误，prefab 已应用、场景已保存。

性能：每帧两次 dispatch（发射 + 65536 粒更新，更新里每粒 18 次噪声采样 + 7 次 SDF 采样）、一次 393k 顶点的过程式绘制；未量化，六人同屏未测。先做效果，后面要压的话先降 `capacity` / `rate`、`noiseOctaves` 1、`_Size` 大一点换数量。

手调：想"更贴表面"降 `noiseStrength` / `maxSpeed`、减 `lifeMax`；想"拖尾更长更浓"加 `lifeMax`、`rate`、`_Opacity`；想"丝更明显"把 `noiseFrequency` 调高（纹理更细）、`_Size` 调小；想"更黑"加 `_Opacity` 或 `_Core`。

### 18.3 第二十次反馈：密度略大、要颜色调整功能（同日）

用户："效果还不错"，两条：(1) 散布密度稍大，反过来略升寿命、降产量；(2) 提供颜色调整功能。

- **密度**：`rate` 20000 → 12000 每秒，`lifeMin` / `lifeMax` 0.8 / 2.0 → 1.0 / 2.6 s。原地 3 s 存活 27.6k → 21.4k；拖尾因寿命长了一点而更长、更稀。截图 `particles-v2-rest.png` / `particles-v2-trail.png`。
- **颜色全部搬到 `InkParticles` 组件上**（Inspector 直接调，编辑模式即时生效，不用再进材质）：

| 字段 | 作用 |
|---|---|
| `colorOverLife`（Gradient） | 每个粒子从出生到死亡的颜色和透明度曲线。默认暖橄榄墨 `#1C1C18` → 墨晕 `#383428`，alpha 1 → 1。想做"外黑内亮"或带色墨，在这里加色标：比如 0 处玄青黑 `#111A19`、0.4 处朱砂、1 处淡墨 |
| `opacity` | 单粒透明度（0.05）。密处叠成实墨，所以"更黑"先调它，再调 `core` |
| `size` / `sizeGrow` | 出生大小（1）和一生的放大倍数（1.5） |
| `core` | 圆盘硬度（1.2）：高 = 小而实的芯 |

实现：组件把 Gradient 烘成 128×1 的色带贴图（`HideAndDontSave`），和 `_Opacity` / `_Size` / `_SizeGrow` / `_Core` 一起通过 `MaterialPropertyBlock` 送给 `FX/InkParticle`；shader 的 `_InkColor` / `_WashColor` 属性删除，改为采样 `_ColorRamp`。材质 `Mat_FX_Palm_InkParticle` 上剩下的只有默认值和 `_FadeIn` / `_FadeOutStart` / `_SoftDistance`（淡入淡出的时点和软粒子距离）。prefab 已应用、场景已保存。

## 19. 三维烟雾模拟 `InkFluid3D`（2026-10-29，第二十一次反馈）

用户看穿了粒子版："不是在做烟雾模拟，是靠软圆盘强行拉成这样的，为什么不做烟模拟？"——对：粒子版只有运动是近似（程序化无散流场推粒子，无压力求解、粒子互不作用），画面靠叠加平均，所以糊。我在第 18 节把三维流体标成"成本高"下得太早。用户决定：**做三维烟雾模拟。**

### 19.1 实现（三个新文件）

| 文件 | 作用 |
|---|---|
| `Assets/Rendering/Shaders/FX/InkFluid3D.compute` | 3D Stable Fluids，和二维 `InkFluid.compute` 同一流程换成 3D 纹理：`Source`（手的 SDF 在 `sourceInside ± surfaceBand` 的薄壳 = **整只手的三维表面**注入墨和随机速度，稀疏斑点门用世界固定的 3D 值噪声）→ `AdvectVelocity`（半拉格朗日 + 整格 `_Shift`）→ `Curl`（涡量矢量 + 模长）→ `Forces`（三维涡量约束 `cross(N, ω)`、随机漂移场、时间轴 `pull` 沿 SDF 梯度、浮力 0）→ `Divergence` → `Jacobi × N` → `Gradient`（投影，盒壁法向速度清零）→ `AdvectDensity`（衰减 + 六邻扩散）；`Clear`；`Stats`（InterlockedAdd 读回质量和质心，自检用）。线程组 8×8×4 |
| `Assets/Rendering/Shaders/FX/FX_InkFluid3D.shader` | 在隐藏的世界盒子上体积光线步进：轴对齐盒子的 slab 求交、停在场景深度、每步密度 → 边缘渐隐 → 阈值 → 对比度 → Beer-Lambert 消光，前向累积；颜色稀处 `_WashColor` 浓处 `_InkColor`；起点抖动去条纹；预乘输出。`Cull Front` + `ZTest Always`，相机进盒子也能画 |
| `Assets/Scripts/VFX/InkFluidVolume.cs` | 组件：网格**轴对齐、固定在世界**，第一次放置时盒子中心 = 手 + `centreOffset`（手轴系，之后冻结成世界轴）；手离放置点超过 `recenterFraction` × 盒子尺寸就按整格重心（精确拷贝不糊，和 17 节相同）；渲染对象是隐藏、无父节点的世界盒子（`HideAndDontSave`，内置 Cube 网格），`MaterialPropertyBlock` 送密度纹理和盒子范围；编辑模式 30 Hz 模拟；`ReadStats()` / `CellToWorld()` 自检；`Recentre()` 清墨并把盒子放回手上 |

材质 `Assets/VFX/如来神掌/Materials/Mat_FX_Palm_InkFluid3D.mat`。prefab 新子物体 `InkFluid3D`；`InkParticles` 组件关掉（留着对比），`InkFluidSide` 仍隐藏。

### 19.2 参数（第一版）

| 字段 | 值 | 说明 |
|---|---|---|
| `resolution` / `cellSize` | 80 × 80 × 128 格 / 2 单位 | 盒子 160 × 160 × 256，81.9 万格；8 张 3D 纹理（速度、涡量 ARGBHalf，其余 RHalf）约 30 MB |
| `centreOffset` | (0, 0, −80) | 盒子中心在手后 80，手前有 48、手后有 208 的拖尾空间（手沿 +Z 飞） |
| `recenterFraction` | 0.25 | 手偏离放置点超过 40 / 40 / 64 单位就整格重心 |
| `pressureIterations` | 12 | 二维版 24；三维先省一半 |
| `injectRate` / `targetDensity` / `surfaceBand` / `sourceInside` | 12 / 0.45 / 3 / −0.5 | 表面薄壳源 |
| `sparsity` / `sparsityScale` | 0.4 / 4 | 稀疏斑点 |
| `swirl` / `dragInject` / `injectVelocity` / `buoyancy` | 25 / 0.1 / 0 / 0 | 无上升 |
| `vorticity` / `drift` / `diffusion` | 6 / 30 / 0.12 | |
| `velocityDissipation` / `densityDissipation` | 0.985 / 0.994 | |
| 材质 `_Threshold` / `_Contrast` / `_Extinction` / `_Steps` / `_EdgeFade` | 0.03 / 1.1 / 0.35 / 64 / 0.08 | 颜色 `_InkColor` `#1C1C18`、`_WashColor` `#383428` |

### 19.3 自检

| 步骤 | 盒子 | 墨 |
|---|---|---|
| 原地 2 s | min (−80, 9, 151) | 质量 2503，质心在手上（z 358） |
| 手 +Z 100 | 超过 64 的阈值，整格重心 +100（50 格） | 质量 3009，质心 z 401，旧墨仍在 358 附近 |
| 手 +Z 300 | 再重心 +200 | 质量降到 1594：最早的墨落到盒子后缘以外被裁掉（拖尾最长 208 单位） |

CPU 侧每 tick 0.18 ms（只是 dispatch 开销，GPU 时间未测）。截图 `palm-mesh-tripo/fluid3d-v1-rest.png`（正面：墨聚成手形，手指可读，边缘锐利）、`fluid3d-v1-side.png`（侧面：有厚度，不再是一片）、`fluid3d-v1-trail.png`（手前冲后侧后方：原位留下一团残墨）。编译无错误，prefab 已应用、场景已保存，世界盒子没有被序列化。

未做：时间轴仍未接三维流体（字段 `pull` / `bandScale` / `emissionScale` / `fadeOut` 已留）；GPU 耗时和六人同屏未测——要压的话 `resolution` 减半（成本 ÷ 8）、`pressureIterations` 8、`_Steps` 48；拖尾想更长把 `resolution.z` 加大或 `centreOffset.z` 再往后。

### 19.4 第二十二次反馈：寿命太长像"阴影留在原地"、密度太高（同日）

| 字段 | 之前 | 现在 | 作用 |
|---|---|---|---|
| `densityDissipation` | 0.994 | 0.975 | 每秒剩 84% → 47%，残留约 2.5 s 散尽（之前近 10 s，所以像一片影子停在原地） |
| `dragInject` | 0.1 | 0.4 | 新墨带四成手速，残留被拉成顺着路径的一条，不再是整团停住 |
| `targetDensity` | 0.45 | 0.25 | 浓度上限 |
| `sparsity` | 0.4 | 0.3 | 同时发射的表面比例 |

自检：原地 2 s 质量 2503 → 374（薄了很多，手形仍清晰，`fluid3d-v2-rest.png`）；手以 60 单位 / 秒连续前冲 1.5 s 后从侧面和正上方看（`fluid3d-v2-trail.png` / `fluid3d-v2-top.png`）：手后拖着一条越来越稀的丝状残留，几十单位外就散尽。注意第 19.3 节那种"瞬移 300"的测试会把旧墨甩到盒子后缘之外，不代表真实拖尾；连续移动的测试脚本是 `fluid3d_trail_check.py`。prefab 已应用、场景已保存。

手调：拖尾想长一点把 `densityDissipation` 往 0.985 调（每秒剩 64%）；想更"带着走"加 `dragInject`；想更浓加 `targetDensity` 或材质 `_Extinction`。

## 20. 只在前进时分泌 + 完整发射时间线（2026-10-29，第二十三次反馈）

用户："我们始终都是往前推进的，停在原地就不需要渲染分泌；然后模拟一个完整的发射时间线：从锚点开始，先聚拢，然后获得动量向前冲击，再曲线减速，最后随着速度降低完成消散后结束。整个动画效果足够即可。"

### 20.1 分泌随速度（`InkFluidVolume`）

新字段 `emissionSpeedRef`（60 单位 / 秒，默认）和 `emissionAtRest`（0）：每 tick 由手的位移算速度，注入率乘 `lerp(emissionAtRest, 1, speed / emissionSpeedRef)`。静止时不冒墨（`emissionAtRest` 0），达到 60 单位 / 秒满量。`emissionSpeedRef` 置 0 回到"一直冒"。

### 20.2 发射时间线（`PalmInkTimeline`，新增 Launch motion 块）

| 字段 | 值 | 作用 |
|---|---|---|
| `duration` / `gatherSeconds` / `dissipateSeconds` | 4 / 0.5 / 1.5 s | 整段 4 s；前 0.5 s 聚拢，最后 1.5 s 消散 |
| `driveMotion` | 开 | 时间轴自己推动根对象：`Play()` 记下锚点（位置）和朝向（`transform.forward`），之后沿朝向按速度曲线积分位移；`Stop()` 和循环重启都回锚点。接到技能代码时关掉，由投射物自己走，时间轴只管视觉 |
| `peakSpeed` | 150 单位 / 秒 | 速度曲线顶点 |
| `speedCurve` | (0, 0) → (0.125, 0) → (0.2, 1) → (0.6, 0.3) → (1, 0)，横轴是归一化时间 | 聚拢时静止；0.5 → 0.8 s 冲到峰值（获得动量）；之后曲线减速；末尾归零 |
| `speed`（只读） | | 当前速度 |

时间轴同时驱动三维流体：`emissionScale = present`、`fadeOut = dissipate`、`pull`（聚拢时吸向表面、消散时推开）、`bandScale`；`Play()` 时对流体 `Recentre()`（清墨、盒子放回锚点）。`Volume` 的聚拢 / 消散（`_FxAlpha` / `_FxDissolve` / `_Spill` / `_Warp`）不变。

### 20.3 自检（编辑态按 30 Hz 跑一整段，侧面机位，`timeline-v3-t03/t09/t20/t36.png`）

| 时刻 | 走过 | 速度 | 流体 | 画面 |
|---|---|---|---|---|
| 0.30 s | 0 | 0 | 注入 0（静止不分泌），墨量 0 | 体积雾正从噪声里聚成手形，没有烟 |
| 0.90 s | 40 | 149 | 满量，墨量 199 | 冲出去，手后拖一条墨 |
| 2.03 s | 169 | 69 | 满量，墨量 215 | 减速中，拖尾跟着变稀 |
| 3.63 s | 214 | 3 | `fadeOut` 0.48，墨量 0 | 几乎停住，烟已散尽，体积雾在撕开消散 |

`Stop()` 后根对象回到锚点。编译无错误，prefab 已应用、场景已保存。在 Unity 里看整段：勾根对象 `PalmInkTimeline.preview`（循环播放，每圈回锚点），或 Play 模式下 `playOnEnable`。

手调：冲击感改 `speedCurve` 的 0.2 处（更早 / 更陡）和 `peakSpeed`；减速手感改 0.6 处的值和切线；拖尾在减速段想留久一点把 `densityDissipation` 往 0.985 调；消散和速度不同步就改 `dissipateSeconds`。

未做：技能代码接入（`SkillVfxLayer` 的 push_projectile_hands 槽位）——投射物由网络驱动时把 `driveMotion` 关掉、`Play()` 在出掌时调用；六人同屏性能。

### 20.4 第二十四次反馈：60 Hz、射程 5 倍可调、聚散两端必须全无、场景里的洋红面片（同日）

| 要求 | 处理 |
|---|---|
| 改 60 Hz | `InkFluidVolume.editorHz` / `PalmInkTimeline.editorHz`（60，编辑态 tick 上限）；`maxStep` 1/60。衰减改成**与步率无关**：`velocityDissipation` / `densityDissipation` 的含义是"每 1/30 s 保留多少"，实际按 `pow(x, dt × 30)` 施加，所以改帧率不改手感 |
| 射程拉到约 5 倍、可调 | `PalmInkTimeline.travelDistance`（默认 1070 = 之前 214 的 5 倍），`peakSpeed` 改为只读派生值 = `travelDistance / (速度曲线面积 × duration)`，现在约 748 单位 / 秒 |
| 聚散两端全无 | 体积 `_FxAlpha` 从 `gather` 改为 `present = gather × (1 − dissipate)`，t = 0 和结束时都精确为 0（之前末端靠 `_FxDissolve` = 1 兜底）；三维流体 `Play()` 时清空、结束时 `fadeOut` 归零 |
| 场景里一块洋红的 900×420 面片 | 是已退役的 2D 面片 `InkFluidSide` 的隐藏世界四边形（材质和 shader 都正常、`ShaderHasError` 为 false，但渲染器在分层开关关掉子物体后仍处于启用态）。处理：`InkFluidSide` 子物体从场景实例和 prefab 删除，孤儿四边形销毁；2D 面片的脚本 / compute / shader / 材质仍留在仓库 |

高速下暴露的三个 bug（都在 `InkFluidVolume`，已修）：

1. **注入量随时间不随路程**：748 单位 / 秒时手每帧走 12 单位，每格只被扫过一瞬，注入 `rate × dt` 几乎为 0，拖尾空。改：`speedFactor` 低于 `emissionSpeedRef` 时线性降（静止 = `emissionAtRest`），高于时**线性升**（上限 40×），每单位路程留下的墨与速度无关。
2. **盒子重心时把当帧刚注入的墨一起平移**：重心的整格 `_Shift` 原来混在同一步的平流里，新墨也被搬走。改：先用 `dt = 0`、无衰减、无扩散的纯拷贝把旧场平移，再正常走该步。
3. **重心阈值大于盒子前缘余量**：`recenterFraction` 0.25 × 384 = 96 单位 > 手到前壁的 52 单位，手冲出盒子前壁后不再注入，拖尾成一段一段。改：`recenterCells`（4 格 = 8 单位）一超就重心，拷贝是精确的所以频繁也无代价。另外子步里手的矩阵按路径插值（之前所有子步用同一个帧末位置），快手画的是连续带。

盒子：`resolution` 80 × 80 × 192（384 长）、`centreOffset` (0, 0, −140)，手前 52、手后 332；122.9 万格。`ReadStats` 的质量改为 1/256 精度（之前 1/16，薄墨读成 0）。

自检（60 Hz，`timeline-v4-t03/t09/t20/t36.png`）：0.30 s 静止无烟；0.92 s 速度 742、走 205，手后一条连续的烟带；2.02 s 速度 350、走 833，烟带变稀；3.62 s 速度 17、走 1068，几乎散尽；`Stop()` 回锚点。编译无错误，prefab 已应用、场景已保存。

### 20.5 第二十五次反馈：颜色调了没反应；聚拢要"从别处的气聚成手形"，只在第一秒，启动后不变（同日）

**颜色**：三维流体的颜色原来只在材质 `Mat_FX_Palm_InkFluid3D` 上，组件上没有。现在搬到 `InkFluid3D` 子物体的 `InkFluidVolume` 组件（Colour 块，通过 MaterialPropertyBlock 覆盖材质，编辑模式即时生效）：`inkColor`（浓墨，默认 `#1C1C18`）、`washColor`（墨晕 `#383428`）、`threshold`（低于不画，0.03）、`contrast`（1.1）、`extinction`（每单位消光 0.35，越大越黑）。`Volume` 层（形体雾）的颜色仍在 `Mat_FX_Palm_InkVolume` 材质上（15.6 节）。

**从空中聚拢**（只在前 `gatherSeconds` = 1 s；启动后不再有聚拢项）：

| 步骤 | 实现 |
|---|---|
| `Play()` 时撒墨 | `InkFluidVolume.SeedGather(inner, outer, density)` → compute `Seed` 核：手的 SDF 距离在 `[fluidSeedInner, fluidSeedOuter]`（12–60 单位）壳内，按三层值噪声取稀疏小团（`clump = (n − 0.56) × 6`），密度 `fluidSeedDensity`（0.18）。看起来是手周围空中飘着的淡墨丝 |
| 第一秒吸向表面 | `InkFluidVolume.gatherSpeed`（时间轴每帧设为 `fluidGatherSpeed × (1 − gather)`，70 格 / 秒起、随 EaseInOut 归零）→ compute `Forces`：有墨的格子速度被拉向 `−sign(sdf − sourceInside) × ∇sdf × gatherSpeed`，到表面两单位内减速停下，从里从外都收到表面壳 |
| 启动后 | `gatherSpeed` = 0，`pull` 只在消散段为负（推开），`bandScale` 固定 1；分泌随速度（20.1） |
| 速度曲线 | 第一段静止延长到 0.25（= 1 s），`(0,0) → (0.25,0) → (0.32,1) → (0.65,0.3) → (1,0)` |

顺带修了 `SampleSdf`：烘焙的 SDF 体积（84×114×58）之外原来一律返回 36，在流体盒子里大部分区域梯度为零、"壳"判断也失效（第一次试撒墨把整个盒子都填了，质量 12 万）。现在体积外返回"体积边缘值 + 到体积盒的距离"，梯度在整个盒子里都指向手。

自检（60 Hz，`gather-v2-g005 / g04 / g08.png`）：0.05 s 手周围一圈淡墨丝、质心离手 7 单位；0.4 s 墨丝向中间收、手形轮廓浮现；0.8 s 聚成完整手形、质心离手 9 单位（残余的少量外圈）；1.22 s 已起飞 77 单位。prefab 已应用、场景已保存。

手调：聚拢范围 `fluidSeedOuter`；空中墨的多少 `fluidSeedDensity`；收得快慢 `fluidGatherSpeed`；聚拢时长 `gatherSeconds`（改了要同步把 `speedCurve` 第二个点挪到 `gatherSeconds / duration`）。

### 20.6 第二十六次反馈：手掌本身的颜色；聚拢还有"不透明度慢慢拉满"的感觉（同日）

**手掌本身 = `Volume` 层**。新组件 `Assets/Scripts/VFX/InkVolumeColor.cs` 挂在 `Volume` 子物体上：`inkColor`（浓墨）、`thinColor`（薄雾亮侧）、`extinction`（整体浓度）、`fill`（0 壳 / 1 实心）、`innerSolid`（内部实雾比例）、`threshold`（薄雾阈值），走 MaterialPropertyBlock，材质 `Mat_FX_Palm_InkVolume` 不动，即时生效；第一次启用时从材质读当前值（现在读到的是 extinction 0.6、fill 1、threshold 0.32），所以挂上去画面不变。和时间轴写的 `_FxAlpha` / `_FxDissolve` / `_Spill` / `_Warp` 共用同一个属性块，互不覆盖。

**"不透明度慢慢拉满"**：体积层的 `_FxAlpha` 原来 = 聚拢曲线值，整整 1 s 从 0 爬到 1，看着像淡入而不是聚形。时间轴新字段：`volumeAlphaMax`（0.7，体积层的透明度上限，用户要求先试 70%）和 `volumeFadeInFraction`（0.4：在聚拢的前 40% 就把透明度拉到上限，之后聚拢只剩形状在收）。`_FxAlpha = volumeAlphaMax × fadeIn × (1 − dissipate)`，两端仍为 0；`_FxDissolve` 不变（噪声里聚出形状那部分）。prefab 已应用、场景已保存，未截图，等用户看。

### 20.7 第二十七次反馈：加自发光；再来一点点 bloom（同日）

**自发光（`Volume` 层）**：`FX_InkVolume.shader` 新增 `_EmissionColor`（HDR）、`_EmissionStrength`（0 = 关）、`_EmissionInner`（发光深度，物体单位）。做法（第二十八轮定稿，前面九版都"只是亮一点的金，不像发光"）：

- 发光在光线步进里按发射 − 吸收累积：每步 `glow += 透射率 × (1 − e^(−该步消光)) × em²`，`em = saturate((深度 − sdf) / 2 深度)`——手内部到深度处为 1、表皮 0.5、外溢的雾里渐隐；深处的光要穿过前面的雾才被看到。`glow ≤ alpha`，乘强度后进 HDR。
- **加色输出**：pass 的混合改为预乘 `Blend One OneMinusSrcAlpha`，输出 `color × alpha + emis`，发光不再被雾的透明度压住，能溢到身体之外、被 bloom 接住。热扭曲分支同样改成预乘。
- 当前实例：颜色 (2, 1.5, 0.85)（白金热芯，HDR）、强度 2、深度 12。`InkVolumeColor` 组件上对应 `emissionColor`（HDR 取色）、`emissionStrength`、`emissionInner`。截图 `emission-v10.png`（底色是用户改的金色 (0.9, 0.78, 0.53)）。想收一点把 `emissionStrength` 降到 1、或颜色降到 1.5 倍。

**bloom**：`VFXtests` 的全局 Volume 原来用的是 `RaceMap_Environment_Profile`（和比赛场景共用，Bloom 强度 0 是之前定的"比赛不开 bloom，光晕由特效自己画"）。没有动比赛的 profile：克隆了一份 `Assets/Scenes/VFXtests_Environment_Profile.asset` 给 `VFXtests` 的 Volume 用，Bloom 强度 1.0、阈值 1.0、scatter 0.75（阈值 1 = 只接 HDR 的部分，沙地和天空不晕）。比赛场景不受影响；要比赛里也有，得改 `RaceMap_Environment_Profile` 或在技能里自己画光晕（README 11.5 方案 B）。主相机 `Cam_Caster` 的后处理和 HDR 都已开。

截图 `palm-mesh-tripo/emission-v1.png`（聚拢完成、静止，经后处理相机）。prefab 已应用、场景已保存。

### 20.8 第二十九次反馈：层次感不够、整只手像一个大块；外面一层奇怪的东西（2026-10-30）

**块状的原因**（读回实例：`inkColor` 金 (0.9, 0.78, 0.53)、`thinColor` 暗金、`emissionStrength` 8、`fill` 1、`innerSolid` 0）：浓处薄处都是同一种金，自发光拉到最大 8 把整只手连轮廓一起填平，体积本身没有任何受光明暗。

**处理（`FX_InkVolume.shader` + `InkVolumeColor`，Form 块）**：

| 项 | 做法 | 字段 |
|---|---|---|
| 受光明暗 | 光线第一次碰到雾的位置取 SDF 法线（整个像素用这一个法线），`色 × lerp(1, 0.45 + 0.75 × max(0, n·L), 强度)` | `shadeStrength`（0.7）、`shadeLightDir`（物体空间 (0.4, 0.8, 0.5)） |
| 边缘压暗 | 轮廓处 `n·视线` 趋近 0 → 压暗 | `rimDark`（0.6）、`rimDarkPower`（2） |
| 发光只在内部 | 发光权重从 `(深度 − sdf) / 2 深度`（表皮已经一半）改为 `−sdf / 深度`（表皮 0、深处 1） | `emissionInner` 12 |
| 两色分开 | 浓墨色改暖墨 `#3C3628`，用户的金色挪到薄处色 | `inkColor` / `thinColor` |
| 发光强度 | 8 → 2 | `emissionStrength` |

对比 `form-v1-before.png`（用户当时的值）/ `form-v1-after.png`：手指逐根有明暗、轮廓压暗、掌心核心发光，形体读得出来。颜色是我为了演示形体改的，用户可改回；明暗和边缘项不动颜色，只调亮度。prefab 已应用、场景已保存。

**"外面一层奇怪的东西"**（用户截图：手右后方一层带竖纹的淡黄薄膜）：聚拢完成态分别开 / 关 `InkFluid3D` 截图（`veil_fluid_on/off`）两张一样，**不是三维流体**。剩下的可能是 `Volume` 层的外溢雾（`_Spill`，表面带之外按噪声阈值撕出来的蒸汽，聚拢开始时时间轴把它放大到 `gatherSpill` 0.6 / 衰减 14 再收拢），在聚拢的前半段最明显。未在我的截图里复现，等用户说它出现的时刻；要验证可临时把时间轴 `gatherSpill` 设成和材质 `_Spill` 一样。

### 20.9 速度曲线归技能侧（2026-10-30，设计会话改的）

用户定：飞行速度属于技能不属于特效。设计会话改了 `PalmInkTimeline.cs`（它的 scratchpad 有改前备份）：

- 删除 `travelDistance`、`speedCurve`、`CurveArea()` / `PeakSpeed()`（20.2 / 20.4 节里这几项**作废**）。飞行形状现在是 `Skill_PushProjectileHands.projectileSpeedCurve`（我的曲线去掉前 1 s 聚拢）× Push 表 charged 行 `speed` 890 × `lifetimeSeconds` 3，由新脚本 `ChargedProjectileFlight` 算。
- `driveMotion` 变成"预览技能的飞行"：新字段 `flightSource`（空 = `Skills/Skill_PushProjectileHands.asset`），`Play()` 时 `gatherSeconds = buildUpSeconds`、`duration = buildUp + lifetime`，只读 `peakSpeed` / `range` 显示技能值。
- 新 `ConfigureForProjectile(gather, flight)`：`ChargedHandProjectileRuntime` 调用，关 driveMotion / loop / preview、按技能设时长、再 `Play()`。
- `Play()` 无论是否 driveMotion 都 `Recentre()` + `SeedGather()`，比赛里也有聚拢。
- `SkillVfxDatabase` / `VfxRegistry` 的 `palm` 已指向 `VFX_Palm_Bullet.prefab`，并和其他插槽一样挪到 Buddah 层。
- prefab 里残留的旧 `travelDistance` / `speedCurve` 序列化值下次保存会被 Unity 丢掉，不用管。**特效侧不再加运动曲线。**

本线保留的字段不受影响：`holdVisible`、`volumeAlphaMax` / `volumeFadeInFraction`、`fluidSeed*` / `fluidGatherSpeed`、聚散曲线。编译无错误。

### 20.10 第三十次反馈：墨改浅色后模型边缘出现像素台阶；"是不是本体渲染有问题导致形体不明确"（2026-10-30）

分层截图（`alias1_*`：聚拢完成态，同机位分别只开体积 / 只开流体）：**体积层（手的本体）边缘是平滑的**——它读的是烘焙 SDF（64×96×48，三线性插值）加噪声阈值，没有台阶。台阶来自**三维流体层**：格子 2 单位一格，表面壳只有 1–2 格厚，渲染时再过一道硬阈值，墨是深色时台阶藏在暗处，改成浅色后衬着沙地就露出来。所以本体渲染没问题；形体不明确是 20.8 节的原因（同色、发光填平、无明暗），和台阶是两件事。

处理（`FX_InkFluid3D.shader`）：(1) 密度采样改为平滑小数的三线性（Quilez 的 `f² (3 − 2f)` 技巧），插值 C1 连续，格子不再出折痕；(2) 阈值改软切：`smoothstep(threshold, threshold + softness, d)`，新字段 `InkFluidVolume.softness`（0.06；越大越软）。prefab 已应用。要进一步消除可把 `resolution` 加密（成本按格数涨）或 `surfaceBand` 3 → 4。

未解之谜：用后处理相机、关掉 `Volume` 渲染器、相机离手很近时，流体盒子在截图里整屏洋红（`alias*_fluidonly.png`）；远机位或不开后处理时正常，开着 Volume 也正常，用户的 Scene / Game 视图里没出现过。材质、shader 都无错误。先记着。

### 20.11 第三十一次反馈：大拇指旁一块直边的凸起（2026-10-30）

分层截图：只开体积层也有，所以是**本体 SDF 的问题**。直边柱状 = 烘焙时把拇指旁整列体素误判成"内部"：`PalmVolumeBaker.BakeSdf` 原来用 3 个轴向的射线奇偶投票（2/3 多数），Tripo 网格在拇指处有接缝 / 重叠面，两个轴的射线都沿接缝走，整列翻成内部。

试过缠绕数（generalized winding number）判内外：对这只网格更糟——三角形朝向不一致，表面附近缠绕数抖动，手变成一团颗粒（内部体素 17460）。最终改为 **14 个方向的射线奇偶投票**（6 个轴向正负 + 8 个体对角线，≥ 8 票算内部）：单列误判压不过多数，表面保持奇偶法的干净（内部体素 37446，烘焙 17 s）。`SDF_FX_Palm_Tripo.asset` 已原地重烘（同网格 64×96×48、margin 12、range 36），`InkFluidVolume.sdf` 和 `Mat_FX_Palm_InkVolume._Sdf` 重新指向新资产。对比 `sdf-thumb-slab-before.png` / `after.png`：凸起消失，两侧视角拇指形状正常。prefab 已应用、场景已保存。再生成一版网格时，重烘用 `Assets/Editor/VFX/PalmVolumeBaker.cs` 的 `BakeSdf`（脚本 `rebake_sdf.py` 在本会话 scratchpad）。

### 20.12 第三十二次反馈：给外层单开一个浓淡参数，要有散雾感（2026-10-30）

本体之外那层蒸汽是 `FX/InkVolume` 的 `_Spill`（表面带之外、按噪声阈值撕开的雾），原来只在材质上，而且时间轴每帧按材质值覆盖。现在 `InkVolumeColor` 加 **Outer vapour** 块：`outerDensity`（0–1，静止时外层雾的浓度；0.12 是现值）、`outerFalloff`（雾向外伸多远，物体单位；2.5 是现值）。时间轴聚拢 / 消散时仍会把它放大再收拢，但基准改读组件值（`PalmInkTimeline.Apply` 里每帧取 `InkVolumeColor`，没有组件才退回材质）。想要散雾感：`outerDensity` 0.4–0.6、`outerFalloff` 8–15、再把 `threshold` 调低一点让雾少撕掉一些。

一个失误：加字段时用 `ReadFromMaterial()` 读初值，把组件上用户改过的颜色 / 形体 / 发光一并刷回了材质默认值；已按上一轮记录的值恢复（墨 `#3C3628`、薄处金、extinction 0.6、fill 1、innerSolid 0、threshold 0.32、发光 (2, 1.5, 0.85) × 2、深度 12、明暗 0.7、边缘 0.6）。若用户在这之间又改过别的值，那些会丢，得重调。prefab 已应用、场景已保存。

补（2026-10-30）：恢复组件值时发现 **Prefab 模式打不开 `VFX_Palm_Bullet`**（"应有 1 个根，找到 2 个"）：`InkFluidVolume` 在预览场景里启用时把隐藏盒子 `MoveGameObjectToScene` 进了预览场景。修：`PlaceBox` 里编辑态若 `EditorSceneManager.IsPreviewSceneObject(gameObject)` 则不生成盒子（`Bind` 对空盒子直接返回）。已编译、`LoadPrefabContents` 验证单根。组件值已按 20.12 的清单恢复到场景实例并应用回 prefab。

### 20.13 性能会话重做流体后的复检（2026-10-30）

总控派的性能会话（"修复 · 墨手联机性能"）改了 `InkFluidVolume.cs` / `InkFluid3D.compute` / `FX_InkFluid3D.shader` / `FX_InkVolume.shader`（LOD 步长、法线只算到入射）/ `PalmInkTimeline.cs`（缓存）：中间纹理全体共用、状态纹理走池子、子步改 Source 核扫路径、粗压力求解、GPU AABB 裁光线、`simulationBudget`（最近 8 只模拟）。数字和细节在 [push-projectile-hands.md §15.1](push-projectile-hands.md)，本文不重复。

我的复检（编译无错）：整段发射（`timeline_run.py`，现在要临时把 `driveMotion` 打开——实例上设计会话把它关了，预览走技能资产的飞行曲线：峰值 890、射程 1070）——0.3 s 聚拢中、0.9 s 静止聚拢完、2.0 s 速度 573 走 693 单位、3.6 s 速度 29 走 1066，拖尾连续（`perf-rework-check-t20.png`，比之前略"团"一点，是粗压力求解的可压感，用户看得出就关 `coarsePressure`）；聚拢（`gather_run.py`，已去掉对旧 `speedCurve` 字段的引用）0.05 → 0.8 s 质心从 6.9 收到 4.7 单位，形体正常。流线、残留、表面发出、无上升都还在。

### 20.14 复检后两条：拖尾"团"感看得出；轮廓形体又变弱（2026-10-30）

- 团感：实例上 `InkFluidVolume.coarsePressure` 关掉（回到全分辨率 12 次压力迭代），性能会话留的开关，关了烟团不再"可压"。
- 轮廓变弱：性能会话把 `FX_InkVolume` 的 SDF 法线改成"光线第一次碰到雾之后就冻结为入射法线"，但 `Density()` 里的剪影轮廓项（`rim = (1 − |n·rd|)^power`，让掠射处的雾收紧、剪影读得出）和掌 / 背面密度都靠每步的局部法线，冻结后手指深处和背面的轮廓全丢，形体变平。折中：样本落在表面带附近（`|sdf − centre| < 2.5 × bandWidth`）时仍每步算法线，其余（深处、远外）复用入射法线——表面带只占步数的一小部分，性能省的大头还在。prefab 已应用、场景已保存。

实例当前的本体值（用户调过）：innerSolid 0.164、发光 2.56、外层雾 0.424 / 伸 5.9。
