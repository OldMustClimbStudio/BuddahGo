# 技能特效多层设计调研（2026-10-28）

调研日期 2026-10-28，范围是为 BuddahGo 技能特效重做（第一个对象是「如来神掌」）收集业界方法论、题材参考和 Unity 2022.3 URP 14 下的技法与性能资料。六个方向各列出已打开并核实过内容的来源；无法打开的页面只在"未核实"处提及，不作为依据。文末给出三个彼此不同的候选视觉方向，供用户决策，本文不替用户做选择。

调研当天的一个共同限制：GDC Vault（gdcvault.com）全站重定向到维护页，所有 Vault 链接只能通过 Game Developer 的介绍页与 GDC 的 YouTube 频道间接核实；Riot 的 LoL VFX Style Guide PDF 原链接也已失效（重定向到 leagueoflegends.com 首页），社区线程里有第三方镜像但本文未核实其内容。

> **决定（2026-10-28）：** 用户看过参考板后选定「方向 B 国风水墨 + 金为基调，借方向 A 鎏金佛光的形体」，只借方向 C 的一条时序规则（接近白的亮度只在释放那 2–4 帧出现）。文末"未决问题"中：平台只有 PC；掌印纯视觉；NOVA Shader 只作参考不引入；佛手网格面数上限 3000。规范见 [README.md](README.md)，如来神掌的拆解表见 [push-projectile-hands.md](push-projectile-hands.md)。

---

## 1. 特效多层设计方法论

关键词：时间轴分层（anticipation / action / impact / dissipation，特效圈也叫 wind-up / climax / fall-off）、空间分层、读秒（timing）、形状语言（shape language）、剪影（silhouette）、颜色数量与对比、大中小节奏（big-medium-small）。

- **[The Soul of Effects: What is Timing in VFX?（VFX Apprentice 博客）](https://www.vfxapprentice.com/blog/the-soul-of-effects-what-is-timing-in-vfx)**
  - 类型：教学博客（Jason Keyser 创办的 VFX Apprentice，2025-08）。
  - 要点：把一个特效的时间轴拆成 wind-up（蓄势）、climax（高潮）、fall-off（衰减）和 spikes（强度尖峰）。把特效按强度曲线分成 impulse（脉冲：瞬发或 crescendo 渐强）和 rhythm（节律：循环或分段扰动）两类，火球的例子是蓄力与飞行用 rhythm、释放与命中用 impulse。实用技巧："心跳法"——节拍快于心跳（>60 BPM）读作攻击性，治疗类效果应慢于 60 BPM；先用灰色方块做 block-in 把时间轴敲定再上贴图；时间轴图以 0–48 帧为横轴。竞技游戏里 anticipation 的长度受伤害判定时刻约束。
  - 启发：如来神掌「蓄力 1 s → 掌射出（飞行 3 s）→ 命中推飞」天然就是 rhythm（蓄力、飞行）+ impulse（出掌、命中）的组合；1 s 蓄力足够长，可以塞进一个明显的 crescendo，命中一帧要做成全特效最高的 spike。

- **[Video: Applying artistic principles to VFX design for games（Game Developer 对 GDC 2017 演讲的介绍）](https://gamedeveloper.com/art/video-applying-artistic-principles-to-vfx-design-for-games)**
  - 类型：GDC 2017 Visual Effects Bootcamp 演讲，Hadidjah Chamberlin（Blizzard）与 Jason Keyser（Riot）。页面给出免费 YouTube 链接 `https://www.youtube.com/watch?v=mkLGLKcplkM`（视频内容本次未逐帧核实）。
  - 要点：用 LoL 与炉石的正反案例讲 shape / value / color / timing 四项基本功。GDC Vault 原页 `gdcvault.com/play/1024439` 调研当天在维护，未核实。
  - 启发：这是业界最常被引用的"特效艺术原理"入门，适合作为团队统一语言的起点。

- **[Artistic Principles of VFX（Real Time VFX 社区线程）](https://realtimevfx.com/t/artistic-principles-of-vfx/4081)**
  - 类型：社区帖，2018-02，Jason Keyser 本人发布的五集视频系列公告与讨论。
  - 要点：视频系列由上面那场 GDC 演讲和 LoL 风格指南整理而来，主题是 shape、value、color、timing 与"所有原则都要服务 gameplay"。回帖里资深作者普遍反映 timing 是最弱、最常被跳过的一环。
  - 启发：本项目用户的判断"现有特效都是单层"，本质上是 timing 与 value 层级缺失；先补 timing 再补层数。

- **[The Must-Know Artistic Principles of Video Game VFX（VFX Apprentice 博客）](https://www.vfxapprentice.com/blog/five-artistic-principles-gaming-vfx)**
  - 类型：教学博客。
  - 要点：列出 gameplay、shape、value、color、timing、composition 六项（标题写五项，正文六项）。关键句：粒子完成"沟通任务"后就该消散，既减少杂乱也省性能；颜色讨论的是"一个主题内的色深范围"而不是多色并置。
  - 启发：给每一层写一句"它负责告诉玩家什么"，说不出来的层删掉。

- **[10 Design Tips from the League of Legends VFX Style Guide（VFX Apprentice 博客）](https://www.vfxapprentice.com/blog/10-league-of-legends-vfx-design-tips)**
  - 类型：教学博客，对 Riot 公开风格指南的十条摘录（原 PDF 本次无法打开，见上文）。
  - 要点：主元素聚焦、次元素压低；规模与强度匹配技能重要性；避免纯黑纯白，最高亮度只留给命中闪光；glow 用来表达力量、方向和剩余时长；特效的饱和度上限高于环境与 UI；一个主色，补色只做小面积、低饱和点缀；手绘形状、软硬边混用、少叠层；运动模糊，前导边缘（leading edge）是最亮、对比最强的部位；寿命曲线非线性——快速蓄势、早峰值、平滑衰减；特效要短，不遮目标、不拖 overdraw。
  - 启发：直接可用作本项目的"颜色数量"规则：主色 1 + 点缀色 1（低饱和、小面积）；命中闪光是唯一允许接近白的地方。

- **[Diving into Color - Tips & Tricks I use for using color（Real Time VFX 社区线程）](https://realtimevfx.com/t/diving-into-color-tips-tricks-i-use-for-using-color/16220)**
  - 类型：社区帖（Paulina 的博客推介与回帖）。
  - 要点：特效在不同背景上的可读性是最常见的难题。做法有：为亮背景和暗背景准备两套特效、脚本切换；日景用受光（lit）粒子；在亮元素下垫一层深色底粒子（simonschreibt）；关键元素用预乘 alpha（premultiplied alpha），因为 additive 在亮背景上会"消色"，additive 只做高光点缀（Kassyndra）；用 gradient map 做"内亮外暗、外沿高饱和"的轮廓。
  - 启发：本项目赛道是日落下的大理石与沙地，背景偏亮偏暖——纯 additive 金光会被吃掉。核心掌形应走 premultiplied / alpha blend 加深色外沿，additive 只放在闪光与余烬。

- **[VFX as game design tools: the ludology of VFX in God of War Ragnarok and Halo Infinite（Game Developer）](https://gamedeveloper.com/design/vfx-as-game-design-tools-the-ludology-of-vfx-in-god-of-war-ragnarok-and-halo-infinite)**
  - 类型：行业文章，Antonio Gil Herrera（Naughty Dog / Santa Monica / 343 经历），2024-10。
  - 要点：从 shape（剪影与方向性）、color（一致调色板分组机制）、timing（Halo 融合线圈先快速泄漏再按对数放大爆炸，给玩家读尺寸的时间）、scale（随距离放大的粒子保证远处可见）、contrast（任意光照下都可见的护盾）、diegesis（世界内与 UI 式提示）六个维度拆解 VFX 如何传达玩法。
  - 启发：掌的"命中范围"要在飞行阶段就靠 scale 和前导光说清楚，不能等命中。

- **[Useful, free GDC talks（Real Time VFX 社区整理帖）](https://realtimevfx.com/t/useful-free-gdc-talks/219)** 与 **[Video: How Blizzard created the visual effects of Diablo III（Game Developer）](https://gamedeveloper.com/art/video-how-blizzard-created-the-visual-effects-of-i-diablo-iii-i-)**、**[Video: Tips on art directing VFX for stylized games（Game Developer）](https://gamedeveloper.com/design/video-tips-on-art-directing-vfx-for-stylized-games)**
  - 类型：免费 GDC 演讲索引 + 两场演讲的介绍页。
  - 要点：索引帖列了 Diablo III（Julian Love，重点是"messaging"——节奏快读作攻击性）、Red Faction: Armageddon 的沟通与迭代、inFAMOUS: Second Son（Matt Vainio）、Uncharted 4 技术美术文化、simonschreibt 的 "How to (Not) Create Textures for VFX"（2022）等。"Tips on art directing VFX for stylized games" 是 GDC 2017 四位 VFX 美术（Epic / Double Fine / Volition 经历）讨论如何从美术指导推出特效支柱。
  - 启发：本项目场景写实、特效允许更大胆，正是"stylized VFX 在非卡通场景里如何立柱"的问题，这场演讲是最接近的参考。

- 关于"大中小节奏（big-medium-small）"：本轮没有找到可直接打开的 Real Time VFX 原帖专门讨论它，搜索摘要指向的几篇博客（如 fx 动画教学博客引用 Gilland《Elemental Magic》关于"大中小形状搭配、避免对称平行"的论述）本次未打开，标注**未核实**。实际应用上它与上面 LoL 指南的"主元素 / 次元素 / 点缀"是同一件事。

- **未核实**：GDC 2024 "Attention Overload: Creating Readable VFX for Suicide Squad"（`gdcvault.com/play/1034360`，schedule.gdconf.com 页面返回 403），主题是多人同屏的特效过载与可读性，若能看到应优先补读。

---

## 2. 禅 / 佛教 / 东方题材 3D 游戏的技能特效

这一方向资料最薄：《黑神话：悟空》《只狼》《卧龙》《仁王》《永劫无间》均没有找到开发团队公开的技能特效拆解；能核实的是《对马岛之魂》官方博客、原神单个技能的社区重建、水墨类特效的 Unreal 制作访谈，以及法阵 / 符文圈的通用做法。

- **[How stunning visual effects bring Ghost of Tsushima to life（PlayStation Blog，Matt Vainio）](https://blog.playstation.com/2021/01/12/how-stunning-visual-effects-bring-ghost-of-tsushima-to-life/)**（社区讨论见 [Real Time VFX 线程](https://realtimevfx.com/t/vfx-of-ghost-of-tsushima-a-recent-blog/15815)）
  - 类型：官方开发博客，Sucker Punch 首席 VFX 美术。
  - 要点：VFX 团队大部分时间只有两人，靠程序化放置维持内容。全局风场驱动几乎所有效果；上万片树叶各自是一个会落地翻转、随水流漂、决斗时被人物分开的圆片；刀剑血液是 deferred ribbon + billboard、无网格，法线跟随侵蚀边缘。可读性案例："windicator" 引路粒子一开始贴地飞会钻进岩石难以跟随，后来改成直指目标。决斗追求"stylish beauty in motion"而非完全写实。
  - 启发：在写实日系场景里保持可读的关键是"少而大、方向明确"，而不是堆粒子；血液用 ribbon 而非 mesh 的做法可直接套到掌的飞行拖尾上。

- **[See How To Set Up Stylish Ink Scroll VFX In Unreal Engine（80.lv 访谈，Aiden Farmer）](https://80.lv/articles/see-how-to-set-up-stylish-ink-scroll-vfx-in-unreal-engine/)**
  - 类型：开发者访谈。
  - 要点：黑墨卷轴上浮现汉字"视觉效果"，灵感来自《逆水寒》过场的水墨卷轴，也参考崩坏 3 和 Valorant Raze 的喷漆风。流程：收集参考 → Niagara 块状布局 → 多轮迭代；文字揭示用 SmoothStep + 动态参数做遮罩。否掉的方案：从文字里抽粒子、电影式镜头。
  - 启发："经文 / 汉字浮现"这类母题，业界做法是贴图 + 遮罩揭示（mask reveal），不是粒子拼字。

- **[Genshin Impact VFX - Mona Magic Breakdown（Real Time VFX 线程，letsmakefx）](https://realtimevfx.com/t/genshin-impact-vfx-mona-magic-breakdown/16874)**
  - 类型：社区视频拆解（Unreal 重建）。
  - 要点：莫娜的法阵分三步：法阵本体 → 用屏幕坐标（screen position）做"通往星空的传送门"的星河层 → erosion 消散；多层以不同速度叠加产生视差。
  - 启发：原神类法阵的"厚度"来自 2–3 层不同速度的贴图 + 一层屏幕空间的深度错觉，可作为佛光轮 / 莲座地面层的构造参考。

- **[Magic Circle WIP（Real Time VFX 线程，apiotrw）](https://realtimevfx.com/t/magic-circle-wip/12940)** 与 **[Summon Circle – Fumikaki（The Rookies，Unity VFX Graph）](https://www.therookies.co/projects/101617)**
  - 类型：社区作品帖 / 学生作品页。
  - 要点：前者贴图在 Substance Designer 制作、通道打包成灰度图（圈与符文的不透明度由渐变驱动，蓝通道放随机符文文本、每次重启随机偏移），符文用字体 Moon Runes，节奏用 Level Sequencer 控制。后者在 Unity 用 VFX Graph 分四个系统（旋转法阵、环境余烬、召唤爆发、拖尾）分别制作再组合，强调余烬发射率要压低以保可读。
  - 启发：法阵 / 经文圈的 3D 实现是"一张通道打包贴图 + 极坐标或旋转 UV + 分层旋转速度"的贴图方案，网格只需一张面片。

- **[Wukong's Spinning Staff Water VFX Powered By UE5's Niagara & LiquiGen（80.lv，Vitaliy Lupul）](https://80.lv/articles/wukong-s-spinning-staff-water-vfx-powered-by-ue5-s-niagara-liquigen)**
  - 类型：个人作品报道（文章未说明是否为游戏科学官方工作，按个人作品看待）。
  - 要点：棍的拖尾用 Niagara Ribbon（轻微模糊 + UV 平铺），第二条 ribbon 做水花层；水花用 LiquiGen 渲染 8×8 flipbook；水花 sprite 用 masked + dithering 而非 alpha blend，以减少 overdraw 并获得平滑淡出。
  - 启发：快速旋转 / 飞行物体的拖尾用 ribbon + 平铺 UV，次级喷溅用 flipbook；"masked + dither"是在不牺牲观感的前提下压 overdraw 的具体手段。

- **[How can I make a material/postprocessing effect to achieve ink shading like game Okami?（Unreal Engine 论坛）](https://forums.unrealengine.com/t/how-can-i-make-a-material-postprocessing-effect-to-acheive-ink-shading-like-game-okami/83460)**
  - 类型：社区讨论。
  - 要点：《大神》的水墨感不是标准 toon shading：手绘水彩质感贴图 + 融入的纸纹最重要；光照柔和、无深阴影；描边方案有翻法线外扩网格、Fresnel 压黑、深度缓冲描边；Luos 指出把线烤进贴图会显假，真正的墨线随视角与光照变化；后期加画布纹理叠加。
  - 启发：若走"水墨 + 金"方向，墨色应来自贴图与 Fresnel，而不是粒子里的黑烟；全屏画布纹理只在命中一瞬叠加会比常驻更省且更有冲击。

- **[Check Out These Awesome Attack & Cloth Animations Featuring Ink VFX（80.lv，TuxedoPato）](https://80.lv/articles/check-out-these-awesome-attack-cloth-animations-featuring-ink-vfx)**
  - 类型：作品报道。
  - 要点：每一步脚步和每次挥刀都在地面留下不同的墨迹（Maya + Unreal）。
  - 启发：命中后留在地面的"掌印"是水墨方向的天然落点，也是三个候选方向都能共用的地面贴花层。

- 关于《黑神话：悟空》：[机核《黑神话》法术原型考证](https://www.gcores.com/articles/187438)（GRENICO，2024-08-29）只讨论定身法等法术在《西游记》里的出处与数值，不涉及视觉；[80.lv 的 Black Myth 美术合集](https://80.lv/articles/art-dump-a-closer-look-at-black-myth-wukong)只有场景与角色图。Epic 的官方开发者访谈页返回 403，**未核实**。定身法的"金色光环 + 梵文"实现方式没有找到任何一手说明。
- 关于《只狼》《仁王》《卧龙》《永劫无间》《师父》：搜索只命中评测、宣传片制作公司与 ArtStation 的人员署名，没有特效拆解；全部**未核实**。

---

## 3. 派对竞速 / 对抗类游戏的技能与道具可读性

Mario Kart、CTR、Fall Guys、Rocket League 的官方特效拆解基本不存在；能核实的最强资料反而来自 Riot 对"投射物如何对受害者可读"的两篇长文和 LoL 的 clarity 规范，它们正好回答本项目的核心问题——被瞄准的人要能看见飞来的掌。

- **[Anatomy of a League of Legends missile – Part 1 – Creating Areas of Focus（Real Time VFX，Oli "Beardilocks" McDonald，Riot）](https://realtimevfx.com/t/anatomy-of-a-league-of-legends-missile-part-1-creating-areas-of-focus/11614)**
  - 类型：Riot 皮肤组首席 VFX 美术的方法帖，2020-01。
  - 要点：投射物分 primary element（前端、造成伤害、承载玩法信息）和 secondary element（拖尾等只强化主题）。三个目标按优先级排：传达玩法并减少杂乱 > 推主题 > 好看。焦点靠绘画三要素造：value（最高与最低明度都在焦点上）、color（焦点饱和度最高）、shape（焦点细节最多、边缘最锐）。"什么都强调等于什么都不强调"，要留出 area of rest。
  - 启发：佛手掌本体 = primary，金色拖尾与经文 = secondary；拖尾的饱和度与细节必须低于掌本体，否则对手看到的是一条金线而不是一只手。

- **[Anatomy of a League of Legends missile – Part 2 – What are we communicating with our VFX（Real Time VFX，Riot Beardilocks）](https://realtimevfx.com/t/anatomy-of-a-league-of-legends-missile-part-2-what-are-we-communicating-with-our-vfx/13062)**
  - 类型：同一系列第二篇。
  - 要点：特效必须回答三问——这是哪个技能（独特身份，换皮肤也要保留）、它做什么（同类效果共享视觉语言：定身用地面环 + 上升柱，击飞用强烈的垂直元素）、影响多大（越强越大越亮；控制类比纯伤害更值得视觉权重；建议建一张 1–10 的视觉层级表）。
  - 启发：如来神掌的"推飞"属于位移控制，按这套层级应比纯伤害技能更亮更大；反噬变体的 5 掌扇形要用同一身份语言但明显"更弱、更散"。

- **[/dev: Gameplay Clarity in League（Riot 开发博客，2021-03）](https://www.leagueoflegends.com/en-us/news/dev/clarity-in-league/)**
  - 类型：官方规范文章。
  - 要点：clarity 的三目标——清晰玩法、保持层级（当下最重要的事最抢眼）、最少噪音。技能按伤害、控制、可躲避性、对局影响四项评估视觉权重；可躲避的技能必须更显眼；碰撞盒必须与视觉一致（Arcane Comet 的大地面圈被点名为误导）。投射物即使静止也要能读出飞行方向，常用做法是模糊尾部。
  - 启发：掌的碰撞体与视觉尺寸要一致（本项目用户此前已有"collider not visuals"的决策，正好对上）；飞行 3 s 且"很快"，更要靠尾部拖影而不是粒子数量表达速度。

- **[VFX breakdown: Combining 2D and 3D in Blightbound（Game Developer，Joost van Dongen / Ronimo）](https://www.gamedeveloper.com/art/vfx-breakdown-combining-2d-and-3d-in-blightbound)**
  - 类型：开发者拆解，2020-11。
  - 要点：低机位下平放的飞轮投射物显得太薄，于是把它略微朝摄像机倾斜并给俯仰加动画、让粒子和挥击飞出平面；范围技能用"地面圆 + 垂直圆柱 + 动画漩涡"保证在草石上都能读出范围。
  - 启发：BuddahGo 的追尾 / 低机位视角里，一个薄的"掌形面片"会消失，掌需要有厚度（网格或两层交叉面片）并带轻微俯仰动画；地面贴花要配一个低矮的垂直元素。

- **[Mario Klutter（Critical-Gaming 博客，2008）](https://critical-gaming.squarespace.com/blog/2008/5/1/mario-klutter.html)**
  - 类型：设计评论。
  - 要点：Mario Kart Wii 把人数加到 12、道具可掉落赛道后，每个道具的独特性被稀释——"各元素不独特不显著，效果重叠只会冲淡整体设计"。
  - 启发：多人同屏时，每个技能只保留一个最具辨识度的剪影与一个主色；掌的变体（正向单掌 / 反噬五掌）靠数量和扇形而不是换颜色区分。

- **[Ask the Developer Vol. 18: Mario Kart World — Part 1（Nintendo 官方）](https://www.nintendo.com/us/whatsnew/ask-the-developer-vol-18-mario-kart-world-part-1)**
  - 类型：官方开发者访谈。
  - 要点：美术总监石川雅章说 12 人赛道拉开后"画面稀疏、有孤独感"，24 人是为了"随时可能发生任何事的混乱氛围"；24 人要求从一开始就做性能优化。未谈道具可见性与特效。
  - 启发：派对竞速的目标氛围本就是"热闹"，特效不是越少越好，而是层级要清楚；本项目 1–6 人同屏比 24 人宽松很多。

- **[Fall Guys: Ultimate Knockout — how Mediatonic crafted a cute battle royale（GamesBeat）](https://gamesbeat.com/fall-guys-ultimate-knockout-how-mediatonic-crafted-a-cute-battle-royale/)**
  - 类型：开发者访谈。
  - 要点：从 100 人降到 60 人是因为 30 人以上在测试里已"完全疯狂"；关卡有"三个词说不清就太复杂"的规则；物理模拟按 60 人规模削减。未谈特效。
  - 启发：技能特效也可用"三个词"检验：如来神掌 = 金、掌、推。

- **仅经搜索摘要核实（原页 403）**：[Colour coding in Roche Fusion（ModDB）](https://www.moddb.com/news/colour-coding-in-roche-fusion-on-the-importance-of-colour-in-games) 说敌方从不用青 / 黄色弹幕，使敌我一眼可分，并提醒红绿色盲场景下形状与行为必须承担核心含义；[How UX design can help with VFX（Mighty Bear Games，Medium）](https://medium.com/mighty-bear-games/how-ux-design-can-help-with-vfx-a0925f80e0f0) 讨论多人游戏里用 UX 层级思维做特效。Rocket League 官方"Classic Rocket Trails getting an update"新闻页同样 403，**未核实**。

---

## 4. 「如来神掌 / 佛掌 / 掌印」意象的影视与游戏处理

这一方向没有找到任何专业的镜头或特效分析文章；可核实的是影史脉络与几条一手描述，足够说明这一意象的三种历史形态：**掌形光弹（1960–80 年代港片）→ 从天而降的巨掌与云中佛影（《功夫》2004）→ 网游里的"万佛朝宗"大招**。

- **[Kung Fu Hustle（Wikipedia）](https://en.wikipedia.org/wiki/Kung_Fu_Hustle)**
  - 类型：百科。
  - 要点：阿星使的是 Fut Gar 佛家"如来神掌"，特效由香港 Centro Digital Pictures 制作，CGI 与威亚结合，整体被称为"卡通式特效 + 传统中乐"；页面配图说明为"CGI construction of the Buddhist Palm"；并注明现实中的佛家掌法不会在地上留下掌形的坑。剧情元素取自 1964 年《如来神掌》系列，反派"火云邪神"也源于该系列。
  - 启发：电影版把"掌印留在地面 / 墙面"作为最具辨识度的落点——命中后的掌形贴花是这一意象的核心剪影，比飞行中的手更容易被所有人认出。

- **[机核：从《如来神掌》聊香港漫画改编的电影与游戏](https://www.gcores.com/articles/20860)**
  - 类型：中文文化评论。
  - 要点：故事源自小说《千佛掌》；1964 年电影以"惊人的特技效果"风靡；漫画 1982 年创刊；"万佛朝宗"是流传最广的终极大招（漫画中并非最终式）；《功夫》里"从天而降的掌法"是最有名的借用；2015 年漫画《神掌》里如来神掌变成反派武功。
  - 启发："万佛朝宗"（众多佛影 / 掌影同时压下）可以作为反噬五掌扇形变体的命名与视觉母题。

- **[Buddha's Palm (1982) 影评（LoveHKFilm）](https://lovehkfilm.com/reviews/buddhas_palm.htm)**
  - 类型：影评。
  - 要点：1982 邵氏版（黄泰来导演）里如来神掌是从手中射出"红色掌形光弹"的招式，特效为光学 / 手绘（评者称"acrylic paint"）；对手剑气是绿色光刃；整体色彩明亮、风格夸张。
  - 启发：掌形光弹的历史颜色是红，而非金；本项目用金色是对佛像题材的重新诠释，需要骨子里有"佛手"的形体细节才不会被读成普通火球。

- **[The special effects that enlivened 1950s–60s Cantonese wuxia（博客）](https://plawiuk.blogspot.com/2021/09/the-special-effects-that-enlivened.html)**
  - 类型：影史博客（篇幅很长，只读了前段）。
  - 要点：1964 年《如来神掌》四部曲的怪物是人穿戏服；掌风光线"通常直接画在胶片上"，由专门画师完成，演员要站着不动等"whoosh"画进去。
  - 启发：最早的"掌劲"视觉就是手绘的放射线与涂抹——一条粗、几条细、带手绘抖动的放射线，比光滑的程序化光束更有港片味。

- **[《中華英雄 Online》〈如來神掌〉資料片報道（巴哈姆特 GNN）](https://gnn.gamer.com.tw/detail.php?sn=44186)**
  - 类型：游戏新闻。
  - 要点：资料片改编自八〇年代港漫，新增所有门派可修的"如来神掌"武学，宣传语是"從天而降『萬佛朝宗』"，万佛朝宗是第八式、最高境界。未描述画面。
  - 启发：网游对这一意象的惯例是"从天而降"的 AOE 大招；本项目的掌是水平射出的投射物，是对惯例的反转，视觉上要把"向前飞"做得毫不含糊。

- **[The Buddhism Palm Strikes Back（Wikipedia）](https://en.wikipedia.org/wiki/The_Buddhism_Palm_Strikes_Back)**
  - 类型：百科。
  - 要点：1993 年 TVB 武侠喜剧，改编自黄玉郎漫画《如来神掌》，主角龙剑生是龙剑飞后人；页面未描述招式画面。
  - 启发：仅作为意象谱系的补充。

- **仅经搜索摘要核实**：Capcom《Asura's Wrath》（2012）中 Wyzen 的 Gongen 形态是行星尺寸、形似大佛（Daibutsu）的巨像，用一根手指从天上压向主角，被吉尼斯列为最大游戏 Boss（capcom.fandom.com 返回 402，未打开）。这是"佛手从天而降"在 3D 游戏里最极端的尺度示范，若做夸张卡通方向可参考其"手指遮天"的构图。
- 关于国内手游 / 网游里"如来神掌"技能的 3D 特效，只找到攻略页面的机制描述（如《天龙八部·归来》全屏 AOE、《西行纪 燃魂》"大金刚掌从天而降"），均未打开原页，**未核实**；没有任何特效拆解。

---

## 5. Unity URP 下的具体技法与教程

项目环境：Unity 2022.3、URP 14、Shuriken + VFX Graph 14 + Shader Graph、Feel 插件。以下官方文档均核对了对应版本页。

### 5.1 Shuriken 数据流与渲染

- **[Particle System vertex streams and Standard Shader support（Unity 2022.3 手册）](https://docs.unity3d.com/2022.3/Documentation/Manual/PartSysVertexStreams.html)**
  - 类型：官方文档。
  - 要点：Custom Vertex Streams 把每粒子数据传进 shader 顶点阶段，除 Position / Normal / Color / UV 外有 Velocity、Size、Center、Tangent、UV2（与 UV 打包进同一个 TEXCOORD0 的 zw）、AnimBlend，以及可由脚本 `SetCustomParticleData` 填充的 Custom1 / Custom2；shader 的顶点输入结构必须与流布局一致。
  - 启发：掌的"蓄力进度""溶解进度""随机相位"都应走 Custom1.xyzw，而不是靠多套材质。

- **[#unitytips Dev Takeover: VFX and shaders with Harry Alisavakis（Unity 官方博客）](https://unity.com/blog/games/unitytips-dev-takeover-vfx-and-shaders-with-harry-alisavakis)**
  - 类型：官方博客。
  - 要点：URP Shader Graph 里做 unlit、双面、alpha clip 的粒子溶解；溶解量来自 Custom Vertex Streams 的 AgePercent（走 UV0.z / TEXCOORD0.z），或用 Custom Data 模块的 Custom1.x 配曲线获得更细的时序控制；Timeline 原生支持拖入 Particle System 并拖动预览，便于多元素对齐。
  - 启发：这是 URP + Shuriken + Shader Graph 做"按生命期溶解"的最短路径；Timeline 可以当作技能分层时间轴的编排与预览工具。

- **[Renderer module（Unity 2022.3 手册）](https://docs.unity3d.com/2022.3/Documentation/Manual/PartSysRendererModule.html)**
  - 类型：官方文档。
  - 要点：Render Mode 有 Billboard、Stretched Billboard、Horizontal、Vertical、Mesh、None（只画 Trails）；Sort Mode 有 By Distance / Oldest in Front / Youngest in Front / By Depth；Mesh 模式可多网格加权随机、可开 Mesh GPU Instancing（需兼容 shader）。
  - 启发：佛手主体用 Mesh 模式 + GPU Instancing（反噬五掌共享一份网格），拖尾用 None + Trails 单独一个系统。

- **[Trails module（Unity 2022.3 手册）](https://docs.unity3d.com/2022.3/Documentation/Manual/PartSysTrailsModule.html)**
  - 类型：官方文档。
  - 要点：Particle 模式每粒子留一条拖尾，Ribbon 模式把粒子按年龄连成带；Ratio、Lifetime（粒子寿命的倍数）、Minimum Vertex Distance、Width over Trail、Color over Trail；Texture Mode 有 Stretch / Tile / Repeat per Segment / Distribute per Segment；拖尾材质在 Renderer 模块里单独指定，受光材质需开 Generate Lighting Data。
  - 启发：掌飞行 3 s 很快，Ribbon + Tile 纹理 + Width over Trail 收尖是标准做法；Minimum Vertex Distance 决定高速下拖尾是否断裂，要按掌速调。

- **[Shannon Berke – LoL FX + Knowledge Share（Real Time VFX 线程）](https://realtimevfx.com/t/shannon-berke-lol-fx-knowledge-share/1133)**
  - 类型：Riot 美术经验帖。
  - 要点："Gameplay is king"，LoL 没有东西允许过于花哨。拖尾 swoosh 的做法：在 UV 压扁的网格上滚动 diffuse / alpha 贴图，再叠一片近色、不同速度的面片；网格段数不够滚动贴图会显得难看；同一贴图反向半速再滚一层（Maokai 大招）；Flame Painter 适合画抽象拖尾贴图，但结果偏锐利需清理。
  - 启发：掌后面的金色"劲风"用两层反向、不同速度的滚动贴图即可，不必上粒子。

### 5.2 Shader 技法（Shader Graph / HLSL）

- **[Particles Unlit Shader（URP 14 文档）](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/particles-unlit-shader.html)**
  - 类型：官方文档。
  - 要点：Surface Type Opaque / Transparent；Blending Mode 有 Alpha、Premultiply、Additive、Multiply；Color Mode 有 Multiply / Additive / Subtractive / Overlay / Color / Difference；Flip-Book Blending；Soft Particles 与 Camera Fading 都需要 URP Asset 或摄像机开 Depth Texture；Distortion 需要 Opaque Texture。
  - 启发：方向 1 里"核心用 premultiply、点缀用 additive"的建议在 URP 自带 shader 里就能验证；项目若尚未开 Depth Texture，soft particles 会静默失效（搜索中多条论坛帖都是这个问题）。

- **[Polar Coordinates Node（Shader Graph 14 文档）](https://docs.unity3d.com/Packages/com.unity.shadergraph@14.0/manual/Polar-Coordinates-Node.html)** 与 **[Fresnel Effect Node（Shader Graph 14 文档）](https://docs.unity3d.com/Packages/com.unity.shadergraph@14.0/manual/Fresnel-Effect-Node.html)**
  - 类型：官方文档（含生成代码）。
  - 要点：极坐标节点输出 (radius, angle)，`radius = length(UV-Center)*2*RadialScale`，`angle = atan2(delta.x, delta.y)/6.28*LengthScale`——用它把一张横向滚动的经文贴图变成绕圈旋转的光轮；Fresnel 节点是 `pow(1 - saturate(dot(N, V)), Power)`，用于边缘光。
  - 启发：佛光轮、莲座、经文圈都可用一张条状贴图 + 极坐标 UV 做，不需要美术画圆形贴图。

- **[Texture Distortion / Flow（Catlike Coding，Jasper Flick）](https://catlikecoding.com/unity/tutorials/flow/texture-distortion/)**
  - 类型：逐步教程（Built-in 管线时代，2017.4；原理与 URP 相同）。
  - 要点：flow map 的 RG 通道存 2D 向量（非 sRGB 导入）；按时间推进 UV 偏移并取小数部分循环；用 alpha 通道的噪声错开各处的重置时刻；用两份相位相差半周期的采样 A/B 交叉混合消除可见重置。
  - 启发：火焰状金光、袖口劲风都可以用 flow map 做"流动而不滚屏"的效果；NOVA Shader 已内置该功能，可省手写。

- **[Unity Tutorial: Dissolve Shader (Part 1)（Mirza Beig，Substack）](https://mirzabeig.substack.com/p/unity-tutorial-dissolve-shader-part-1)**
  - 类型：教程（Amplify / Shader Graph 通用，2024-07）。
  - 要点：溶解 = 噪声 → step(cutoff) → 乘 alpha；建议 256px 可平铺 Perlin 噪声；贴图噪声便宜、程序噪声灵活；发光边缘在第二部分。
  - 启发：掌的消散用溶解而不是整体淡出，边缘加一圈金色自发光，才有"烧尽"而非"变透明"的质感。

- **[NOVA Shader（CyberAgent，GitHub，MIT）](https://github.com/CyberAgentGameEntertainment/NovaShader)**
  - 类型：开源 URP 粒子 uber shader，要求 Unity 2022.3+、URP、SM 3.5（Mesh GPU Instancing 需 SM 4.5）。
  - 要点：Uber Unlit / Uber Lit / Distortion / uGUI 变体；功能含 Flow Map、Alpha Transition（溶解 / 淡出）、Flip-Book 帧混合、Soft Particles 与 Depth Fade、贴图驱动的 Vertex Deformation、旋转、Tint Map、Emission、视差、灰度 / 渐变色校正、Rim 与亮度控制透明度、Custom Vertex Streams 自动配置工具、可生成剔除未用功能的优化 shader（声称最多省 50% 内存）。
  - 启发：它就是"特效 uber shader 常见结构"的一份可读实现——基础贴图 × 遮罩 × 噪声扰动（flow / distortion）× 过渡（dissolve）× 颜色模式（additive / alpha / premultiply）× 边缘（rim）。即使最终手写 HLSL，也可以照它的参数面组织自家 shader。

- **[Shuriken 粒子与 Shader Graph 讨论（Unity Discussions）](https://discussions.unity.com/t/shuriken-particles-shader-graph/1669355/11)**（仅经搜索摘要核实，未逐条打开）：Unity 6.x 的 URP 粒子 shader 改进与升级陷阱（升级工程里移除 custom vertex streams 后无法再加回）。列在此只作提醒，**未核实**。

### 5.3 VFX Graph 何时值得用

- **[Visual Effect Graph（Unity 2022.3 手册页）](https://docs.unity3d.com/2022.3/Documentation/Manual/VFXGraph.html)** 与 **[Experiment with VFX Graph（Unity Learn，Creative Core）](https://learn.unity.com/tutorial/experiment-with-vfx-graph)**
  - 类型：官方文档 / 官方教程。
  - 要点：VFX Graph 在 GPU 上模拟，粒子数远多于内建系统，官方建议"粒子数量大且行为高度定制"时使用；Learn 教程的对照表——GPU vs CPU、百万级 vs 千级、不能与物理交互 vs 可以、硬件要求更严 vs 全设备、入门更难 vs 更易；基准：100 万粒子 Particle System 约 5 fps，VFX Graph 约 100 fps。
  - 启发：如来神掌的层数多但每层粒子少（几十到几百），且命中要回调玩法，Shuriken 更合适；VFX Graph 只在"金色经文碎片成千上万像沙一样流散"这类纯视觉大量粒子层才值得启用，并且要确认目标机型支持 compute shader。

### 5.4 贴图：程序化噪声与通道打包（channel packing）

- **[Shaders in Universal RP: channel-packed texture（URP 14 文档）](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/shaders-in-universalrp-channel-packed-texture.html)**
  - 类型：官方文档。
  - 要点：URP Lit 的 Metallic(R) / Occlusion(G) / Smoothness(A) 打包示例；导入时必须关 sRGB (Color Texture)；打包顺序不同可在 Shader Graph 里自行拆通道。
  - 启发：特效贴图同理——R 噪声、G 遮罩、B 溶解噪声、A 渐变，一次采样喂四个功能；记得关 sRGB。

- **[Channel Packing In Unreal（Real Time VFX 线程）](https://realtimevfx.com/t/channel-packing-in-unreal/29045)**
  - 类型：社区问答（Unreal，但做法通用）。
  - 要点：一次 Texture Sample 后用 Component Mask 取通道，或用 dot(color, maskVector) 把"取哪个通道"参数化。
  - 启发：Shader Graph 里对应 Split 节点或 Dot Product；uber shader 里把通道选择做成材质参数能减少 shader 变体。

- **[Free Noise Textures for VFX（80.lv，Luos / Yoeri Vleer）](https://80.lv/articles/free-noise-textures-for-vfx)** 与 **[Noise Generator 2000（RayznGames，itch.io）](https://rayzngames.itch.io/noise-generator-2000)**
  - 类型：免费贴图包报道 / 免费桌面工具。
  - 要点：Luos 的包含 68 张噪声（22 张渐变噪声、28 张噪声法线等），Gumroad / itch 免费，文章未写明许可条款；Noise Generator 2000 用 Unity 内置噪声函数在 GPU 生成 Simplex / Gradient / Voronoi，支持平铺、法线输出、写入 alpha、最高 4K 导出，生成贴图可商用。
  - 启发：项目内自己用 Shader Graph 的 Simple Noise / Gradient Noise / Voronoi 烘成贴图也可行；无论哪条路，都应建一个 `Assets/.../VFX/Textures/Noise` 的可平铺噪声库并通道打包。

---

## 6. 性能：粒子预算与 URP 下的 overdraw 诊断

- **[Mobile game optimization tips, part 2（Unity 官方）](https://unity.com/how-to/mobile-game-optimization-tips-part-2)**
  - 类型：官方最佳实践。
  - 要点：透明渲染永远比不透明贵，叠加时更糟；减少 overdraw 的首要手段是减少粒子数量和尺寸；优先用 Unity 自带的粒子 shader；用 Scene 视图 overdraw 模式或 RenderDoc 检查；低端机每个摄像机可占 1 ms CPU；30 fps 预算 33.3 ms、60 fps 预算 16.6 ms；避免全屏后期（如 glow）。
  - 启发：屏幕后期层（方向列表里的"screen post-processing"）在移动端应只在命中一瞬以极短时长出现，PC 可放宽。

- **[Optimizing particle effects for mobile applications（Unity Learn）](https://learn.unity.com/tutorial/optimizing-particle-effects-for-mobile-applications)**
  - 类型：官方教程（2021-02）。
  - 要点：示例烟雾把 Max Particles 从 1000（实际峰值 59）降到 35 再到 30；提早定粒子预算，文中示例为"约 2 ms / 帧"（因项目而异）；减粒子后用淡出时长、生长、速度、角速度补变化；常量发射最便宜，优化过的曲线次之；曲线保持平滑少采样；Renderer 关投影；少用粒子碰撞；尽量 unlit；贴图合图集、共材质；Frame over Time 的帧尺寸统一；确保屏外系统被剔除。
  - 启发：给每个技能定"同屏总粒子数"与"毫秒预算"两条线；如来神掌单掌建议先按 ≤ 150 活跃粒子、反噬五掌 ≤ 400 起步（这是本文按上述文献推的起点，不是官方数字）。

- **[How to Stop the Spread of Overdraw (Before It Kills Your Game)（Game Developer，Ruben Torres Bonet，2020）](https://www.gamedeveloper.com/programming/how-to-stop-the-spread-of-overdraw-before-it-kills-your-game-)**
  - 类型：技术博客。
  - 要点：overdraw 浪费的是带宽，移动端与 VR 最敏感；粒子是最常见的 overdraw 热点，透明物不写深度所以 GPU 无法剔除；对策是裁紧 quad / 用贴合网格、减少叠层、缩小透明面积。数字：目标约 1×，避免 2×，不超过 3×（引自 Nordeus 的 overdraw 工具）；一个全屏后期 ≈ +1×。
  - 启发：掌本体用贴合手形的网格而不是方形面片，正好同时解决剪影与 overdraw。

- **[Rendering Debugger（URP 14 文档）](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/features/rendering-debugger.html)**
  - 类型：官方文档。
  - 要点：Window > Analysis > Rendering Debugger；Rendering > Rendering Debug 下有 Overdraw 视图；Material Filters 可只看 Alpha；Pixel Validation 可高亮 NaN / 超范围值；Play 模式桌面按 LeftCtrl+Backspace 呼出，移动端三指双击；要在构建里用需取消 URP Global Settings 的 Strip Debug Variants。
  - 启发：这是 URP 14 下诊断透明 overdraw 的正式入口；[Unity 2022.3 Optimizing graphics performance](https://docs.unity3d.com/2022.3/Documentation/Manual/OptimizingGraphicsPerformance.html) 同时建议 Scene 视图的 Overdraw draw mode 与 Dynamic Resolution。[Kyle Fang 的笔记](https://endfang.github.io/posts/unity-overdraw)补充：旧版路径是 Render Pipeline Debug > Rendering > Fullscreen Debug Mode > TransparencyOverdraw；Scene 视图 overdraw 模式不计 z-write 所以不完全准；论坛共识是移动端 additive 比 alpha blend 便宜。

- **[Mobile Graphics Best Practices for Artists（Unite Seoul 2020，Owen Wu，Arm）](https://www.slideshare.net/slideshow/unite-seoul-2020-mobile-graphics-best-parctices-for-artists/248482893)**
  - 类型：Arm 开发者关系工程师的 Unite 演讲幻灯片。
  - 要点：alpha blend overdraw 在移动端昂贵；透明网格与不透明分开；透明贴图用多边形网格代替 quad 以减少空白片元；不透明物前到后渲染；每个图元至少产生 10–20 个片元（避免大量微小三角形）；discard / alpha-to-coverage 会强制 late-Z；ASTC 5×5 或 6×6 是好默认；用 Unity overdraw 视图或 Arm Mobile Studio 测量。
  - 启发：掌的手形网格不要切得过碎（片元 / 图元比），溶解用 alpha blend 而不是 clip 在移动端更友好——与 Harry Alisavakis 博客里 alpha clip 溶解的做法要按平台取舍。

- **[Video: Achieving high-quality game VFX on mobile devices（Game Developer 对 GDC 2020 演讲的介绍，Andy Saia / Level Ex）](https://gamedeveloper.com/programming/video-achieving-high-quality-game-vfx-on-mobile-devices)**
  - 类型：GDC 2020 免费演讲，YouTube `https://www.youtube.com/watch?v=c7HBxBfCsas`（视频内容未逐帧核实）。
  - 要点：介绍页只说明主题是"在移动端做复杂视觉效果的实用技术"。
  - 启发：若项目确定要上移动端，这场和上面 Unite Seoul 的幻灯片是两份最直接的资料。

- 关于 Draw call：本轮没有找到官方的"每特效 N 个 draw call"数字。可核实的间接证据是 Unity Learn 教程强调共材质 + 图集 + Sprite 动态合批，Renderer 模块文档的 Mesh GPU Instancing，以及 80.lv Wukong 棍法水效里"masked + dither 代替 alpha blend 降 overdraw"的做法。Arm 的 Unity 开发者指南页只有工具目录（Performance Studio、Frame Advisor、Mali Offline Compiler），具体最佳实践文档重定向后为空，**未核实**。

---

## 对如来神掌与后续技能的候选方向

三个方向共用一套**分层骨架**（时间轴 × 空间），差别在主色、母题与每层的"口味"。骨架先列出，供三个方向引用：

| 时间轴 | 空间层 | 如来神掌（正向单掌）要回答的问题 |
|---|---|---|
| 预备 anticipation（蓄力 1 s） | 施法者身上的氛围光、脚下地面贴花、手部主体网格、少量上升粒子 | 谁在蓄力、朝哪个方向、还有多久 |
| 释放 action（出掌一瞬，2–4 帧） | 瞬时闪光、后坐劲风面片、短促屏幕冲击 | 掌已经出手 |
| 飞行 travel（3 s，很快） | 掌主体网格 + shader、ribbon 拖尾、前导光（leading edge） | 这是什么、往哪飞、有多大、来自谁 |
| 命中 impact（1 帧峰值 + 0.2 s） | 命中闪光、受害者身上的推飞方向元素、地面 / 空中掌印贴花、碎片粒子、短屏幕后期 | 打中谁、往哪推 |
| 消散 dissipation（0.5–1.5 s） | 掌印贴花溶解、余烬粒子、氛围光回落 | 事件结束，屏幕让回给比赛 |

反噬变体（向后扇形 5 掌）沿用同一骨架，但：预备阶段的方向提示指向后方；5 掌每只的主体更小、拖尾更短、饱和度略低（Riot "更弱的要看起来更弱"）；命中与消散层按掌数共享一份预算，不是 ×5。

Buff 期间（15 s）需要一个低成本、低噪音的**持续层**（施法者身上一圈缓慢旋转的光轮或掌心微光），它的作用是告诉对手"这个人现在能出掌"，视觉权重必须低于任何一次出掌。

### 方向 A：克制的鎏金佛光（Restrained Gilded Light）

- **主色**：暗金 / 古铜为主色（低饱和、中明度），象牙白只出现在命中闪光；点缀色是极少量的朱砂红（印章 / 掌印边缘）。
- **母题**：鎏金铜佛手（有真实手指形体与"施无畏印"手势的网格）、细线经文环、莲座地面贴花、檀香般的缓慢余烬。墨色与黑边不用。
- **层结构草案**：
  - 预备：脚下莲座贴花（极坐标 UV 旋转一张经文条贴图，`Polar Coordinates` 节点），手部网格 Fresnel 边缘光随 1 s 蓄力由 0 升到峰值（Custom1.x 驱动），少量金尘上升粒子（≤ 20）。
  - 释放：一张手形的瞬时闪光面片（2 帧，additive），一片向后的劲风面片（两层反向滚动贴图，Shannon Berke 做法）。
  - 飞行：鎏金佛手网格（Mesh 模式 + GPU Instancing，premultiply / alpha blend 保证在亮沙地上不被吃掉），前导边缘用 Fresnel + 最高明度；拖尾是 Ribbon + Tile，宽度收尖，颜色从暗金到透明；拖尾饱和度低于掌本体（Riot primary / secondary）。
  - 命中：一帧象牙白闪光；受害者身上一个指向推飞方向的短锥形劲风；地面 / 空中留一枚金边掌印贴花（这是整个技能的"签名剪影"，参考《功夫》的掌形坑）；碎片是少量金叶状 mesh 粒子；屏幕后期只做 0.1 s 的轻微暗角收缩或不做。
  - 消散：掌印溶解（噪声 step + 金色自发光边）1.0–1.5 s；余烬 ≤ 15 颗，慢于 60 BPM 的节奏（"心跳法"反向用：让结尾安静）。
- **与场景的关系**：最贴近"枯山水、禅院、谧静"的关键词，靠形体细节（手指、手印）而不是亮度取胜；风险是在日落暖光下金色可能与环境同化，需按 Paulina 的建议准备亮 / 暗背景两套参数或加深色外沿。
- **参考来源**：方向 1 的 LoL 十条（主色 1 + 低饱和点缀、最高明度留给命中）、Diving into Color（premultiply、深色外沿）、方向 2 的《对马岛之魂》（少而大、ribbon 血液）、法阵线程（通道打包经文贴图）、方向 5 的 Polar Coordinates / Fresnel / Trails 文档。

### 方向 B：国风水墨 + 金（Ink Wash and Gold）

- **主色**：墨黑与宣纸灰为底，金为高光与前导色；点缀色朱砂红（印章式掌印）。颜色总数仍控制在"墨 + 金 + 一点红"。
- **母题**：泼墨劲风、带飞白的笔触拖尾、宣纸纹理的命中画面、金色篆书经文从墨中浮现（mask reveal）、朱砂印章式掌印。佛手本体是"墨勾线 + 金填"的半剪影。
- **层结构草案**：
  - 预备：脚下墨迹晕开（flow map 驱动的扩散贴图，Catlike Coding 原理 / NOVA Flow Map），其上金色经文沿极坐标浮现；手部网格 Fresnel 压黑描边 + 金色内发光；蓄力声画节奏可略快于 60 BPM 以表达攻击意图。
  - 释放：一道横向泼墨笔触面片（手绘贴图，软硬边混合）加金色闪光；劲风用墨色飞白滚动贴图。
  - 飞行：佛手网格以黑边 + 金 Fresnel 呈现（Okami 线程：墨线靠 Fresnel 与视角，不烤进贴图）；拖尾是 Ribbon 上滚动的"笔触"贴图，末端飞白；前导边缘为纯金最高明度，保证受害者先看见金再看见墨。
  - 命中：0.1–0.15 s 的全屏宣纸纹理叠加（Okami 线程的画布后期，但只在命中一瞬），受害者处炸开一团墨 + 金溅点（flipbook 或少量 mesh 粒子），地面留下朱砂印章式掌印（TuxedoPato 的墨迹落点）。
  - 消散：墨迹按噪声溶解并向外晕开，金经文最后熄灭；总时长可比方向 A 略长（墨迹"留白"是这个风格的一部分），但要在 1.5 s 内让路。
- **与场景的关系**：与枯山水的黑白灰呼应，在暖色日落里墨色是天然的对比色，可读性比纯金更稳；风险是墨色在深色赛道段（阴影、暗石）会丢失，需要金色前导边缘兜底。
- **参考来源**：方向 2 的 Okami 论坛讨论、80.lv 水墨卷轴访谈（mask reveal 文字）、墨迹脚步报道；方向 4 的 1960 年代"画在胶片上"的掌风线条（手绘抖动感）与《功夫》掌印；方向 5 的 flow map 教程与 NOVA Shader 功能表。

### 方向 C：夸张卡通佛掌（Exaggerated Cartoon Palm）

- **主色**：高饱和金黄 + 橙红双色渐变为主，白色描边与冲击线；点缀色天青（与金橙互补的小面积高光）。整体饱和度上限明显高于环境（LoL 十条第 5 点）。
- **母题**：《功夫》式的巨大云中佛影与"从天而降"构图（在预备阶段云中显佛手一瞬）、港漫式放射冲击线与速度线、Asura's Wrath 式的尺度夸张（掌本体可比角色大一倍）、掌形坑洞贴花带卡通裂纹、"万佛朝宗"作为反噬五掌的名字与形态。
- **层结构草案**：
  - 预备：施法者头顶短暂浮现一个半透明巨大佛手剪影（单张面片 + 溶解入场），脚下卡通放射线贴花旋转；手部网格放大 1.2× 并带轻微挤压拉伸（顶点偏移 / Vertex Deformation）。
  - 释放：大面积白色冲击线面片（3–4 帧）、强烈的屏幕空间径向模糊或一次 2–3 px 的镜头抖动（Feel 插件可承担）、掌出手时的"啵"式挤压。
  - 飞行：卡通化金黄佛掌网格，边缘粗白描边（翻法线外扩或 Fresnel 阈值），拖尾为宽、短、分段的速度线（Trails 用 Repeat per Segment），前导处一圈白色高光；为了对手可读，掌本体要比方向 A 大且平视时带俯仰动画（Blightbound 的倾斜法）。
  - 命中：全屏 2 帧白闪 + 卡通星形冲击面片 + "推飞"方向上的拉长速度线；受害者脚下留下带裂纹的掌形坑贴花；碎片是大块低多边形 mesh 粒子（≤ 30），屏幕后期可用短暂色差 / 径向模糊（PC 允许，移动端慎用）。
  - 消散：坑洞贴花快速溶解 0.5 s，星形与速度线按"早峰值、快衰减"曲线收掉；余烬几乎为零，让画面迅速回到比赛。
- **与场景的关系**：与"枯山水、谧静"反差最大，但符合用户"技能特效允许比场景大胆、更有冲击力"的授权，并与派对竞速的"混乱氛围"（Mario Kart World 访谈）一致；风险是多人同屏时过载（Mario Klutter 的"稀释"警告），需要严格执行"只有命中帧允许接近白"和短寿命。
- **参考来源**：方向 4 的《功夫》Wikipedia 与机核文化脉络、1982 版红色掌形光弹影评（卡通化先例）、Asura's Wrath 尺度（仅搜索摘要）；方向 3 的 Blightbound 倾斜与垂直元素、Mario Klutter 与 Mario Kart World 访谈；方向 1 的 timing 文章（impulse 型、早峰值）；方向 6 的全屏后期成本（≈ +1× overdraw）。

### 三个方向共用的未决问题（留给用户）

1. 目标平台是否包含移动端——决定屏幕后期层与 soft particles / Depth Texture 是否可用。
2. 佛手网格由谁建模、面数上限多少（影响 GPU Instancing 与片元 / 图元比）。
3. 掌印贴花是否参与玩法（例如留在赛道上的减速区），若是则它从"消散层"升格为"持续层"，预算与可读性要求都变化。
4. 是否接受在 Shuriken 之外引入 NOVA Shader（MIT）作为 uber shader 基线，或坚持自写 HLSL。

---

## 调研局限与后续可补

- 方向 2 与方向 4 缺一手资料：没有任何《黑神话：悟空》《只狼》《卧龙》《仁王》《永劫无间》《师父》的官方特效拆解，也没有对《功夫》如来神掌镜头的专业视觉分析；现有结论来自官方博客（对马岛）、社区重建（原神、法阵）与影史脉络。
- GDC Vault 维护与 Riot PDF 失效使得两份最权威的方法论文本只能通过介绍页与摘录间接核实；待 Vault 恢复后应补读 "Artistic Principles of VFX"（2017）与 "Attention Overload"（2024）原片。
- 性能数字（1× / 2× / 3× overdraw，~2 ms 粒子预算，每图元 10–20 片元）均来自单一来源，应在本项目目标机型上用 Rendering Debugger 实测后再写进规范。

---

## 7. 参考板链接（2026-10-28）

参考板是一页本地 HTML（缩略图来自 Bing 图片搜索，不进仓库）；这里只留可点开的来源，按方向分组。用户选定方向 B + A 后，最常对照的是加粗的几条。

**方向 A 克制的鎏金佛光**

- **[ArtStation · Buddha Palm FX（manpye）](https://www.artstation.com/search?q=Buddha%20Palm%20FX%20manpye)**：韩国特效师做好的"如来神掌（여래신장）"技能，金色佛手 + 经文环，与本项目题材最接近（搜索入口；两版缩略图分别为 2023 与 2023-03 的作品封面）。
- [ArtStation · Buddha palm（Fenghua Zhong）](https://cdnb.artstation.com/p/assets/images/images/000/265/373/large/fenghua-zhong-buddha-palm.jpg)：巨掌从天而降的概念画构图。
- 贴金佛手实物（素可泰 Wat Sri Chum，Alamy 图库）、青铜佛手近景（图库）：金箔质感与手指形体的材质参考。
- [ArtStation · Golden Spell Cast VFX（Shannon Grosenbacher）](https://www.artstation.com/search?q=Golden%20Spell%20Cast%20VFX)：金色施法的克制叠加。
- [ArtStation · 黑神话：悟空金色书法设计（ikken）](https://cdna.artstation.com/p/assets/images/images/053/174/660/4k/ikken-wukong-golden-shell-v1.jpg)：金色篆书 / 经文母题。
- 对马岛之魂的金叶随风与秋林截图（Twinfinite、CBR 的图集）：暖色场景里金色如何不被吃掉。

**方向 B 国风水墨 + 金**

- **大神 Okami**：一笔斩山（Pocket Gamer 评测截图）、斩击瞬间（Superpower Wiki "Ink Cutting"）、[墨弹 Ink Bullet（Ōkami Wiki）](https://static.wikia.nocookie.net/okami/images/f/fe/Ink_Bullet_Scroll.png)：墨色投射物如何保持可读。
- **对马岛之魂：奇谭 · 墨绘过场**（YouTube "All Classes Ink Animation Cutscenes"）：墨 + 金 + 朱砂的三色组合，几乎就是方向 B 的色板。
- 燕云十六声（Where Winds Meet）· 墨扇（Epic 官方指南图）、墨云（YouTube "Cloud of Ink"）：写实国风场景里的墨色技能。
- 师父 Sifu · [后期高对比（Raoul Desmarest, ArtStation）](https://cdna.artstation.com/p/assets/images/images/047/240/550/large/raoul-desmarest-sifu-highcontrast-01.jpg)：命中瞬间的一瞬全屏风格化。
- [80.lv · UE5 水墨风特效](https://80.lv/articles/impressive-ink-style-vfx-created-in-unreal-engine-5)、Fab 上的 Ink Melee VFX Pack 与 Ink Slash：泼墨劲风与飞白拖尾的商业化做法。
- 对马岛之魂 · 黑泽模式截图：命中帧"宣纸纹理叠加"的质感参考。

**方向 C 夸张卡通佛掌**

- 功夫（2004）：准备出掌（Kung-fu Kingdom）、楼体掌印（Flickr）、佛手压下、"从天而降的掌法"（clip.cafe）。
- 阿修罗之怒：巨指（VG247）、巨手 Boss（YouTube 缩略图）。
- ArtStation：Toon ground impact（Thomas Francis）、Stylized Hit Impact、2D Hit Impact and Implementation（Katie Wright）。
- 马里奥赛车 8：蓝龟壳（Nintendo Life）、金龟壳 Mod（GameBanana）：派对竞速里投射物的可读性。

**意象溯源**：《如来神掌》1964 年系列与 1982 版的剧照 / 海报（Blogger 影评图集、Tars Tarkas、IMDb）：画在胶片上的掌风线条与红色掌形光弹，是"掌印"剪影与卡通化的先例。

**代码参考（用户提供，只参考不搬）**：[lywedo/Unity-Effect](https://github.com/lywedo/Unity-Effect)（URP、Unity 2021.3、MIT，2023-11 后未更新）。有用的部分：`Assets/Effects/Fluid/FlowMap`、`DirectionalFlow` 的手写流动贴图 shader 与编辑器 flow map 生成小工具（可借到掌主体的"能量流动"层）；`Assets/Effects/Fluid/Fluid2D/ChineseInkPainting` 用 compute shader 做的水墨扩散（方向 B 最直接的技术参考）；`Assets/VFXs/` 下约五十个 VFX Graph 小样（花瓣、火焰、丝带、流星、星河、闪光、蝴蝶、云、水花、藤蔓、鱼群），其中带 `Sketch` 日期编号的那批连同 `Common/` 资源看起来是 Keijiro 公开草稿的转载。整体是 VFX Graph 路线且版本偏旧，本项目已定 Shuriken + 手写 shader 为主，只借技法与观感。

**第一次画面评审后补的参考（2026-10-28）**：

- 用户给的观感参考：[keijiro/SplatVFX](https://github.com/keijiro/SplatVFX)（VFX Graph 上的 3D Gaussian Splatting）、[keijiro/DkvfxSketches](https://github.com/keijiro/DkvfxSketches)（Depthkit 体积视频的粒子草稿）。共同点是上千个软小点聚成一个有体积的形，再整体流动、溶解；本项目据此把主体改成"不透明剪影网格 + 致密粒子皮"（README 第 2 节）。
- 水墨实现的 GitHub 调研与结论（画布级湿墨模拟、整屏水彩后期、游戏里的实心墨形三类）见 [push-projectile-hands.md 第 12.3 节](push-projectile-hands.md#123-水墨可行性调研github2026-10-28)：可用的只有"实心形 + 手绘贴图"。

## 8. 参考板：《绝区零》仪玄的"实体 + 外层水墨"（2026-10-28 晚，用户指定）

用户看过第二版材质后的判断："水墨感来自于一些外层的特效，但是不应该是实体。如果一般用实体的话，可以参考绝区零里面仪玄这个角色的特效风格。"本节按截图与官方资料把仪玄的做法拆开，供 [README.md 第 2 节](README.md#2-视觉方向已定) 与 [push-projectile-hands.md 第 13 节](push-projectile-hands.md#13-2026-10-28-第二次评审实体与外层水墨分家) 引用。

### 8.1 角色与技能事实

- 仪玄：《绝区零》2.0（2025-06-06）S 级代理人，阵营云岿山，属性"玄墨"（以太的变体，伤害数字为金色），特性命破。技能名全是墨与云：普攻「墨影凝云」（展开大太极阵）、强化特殊技「墨痕化形」、终结技「符法千重」（张开羽翼、引导万千符箓）、「青溟震击」（张开翅膀发动爆炸）、术法「青溟鸟」、音擎「青溟笼舍」。来源：[B 站绝区零 WIKI · 仪玄](https://wiki.biligame.com/zzz/%E4%BB%AA%E7%8E%84)、[HoYoWiki · Yixuan](https://wiki.hoyolab.com/pc/zzz/entry/752?lang=en-us)、[Fandom · Yixuan](https://zenless-zone-zero.fandom.com/wiki/Yixuan)（技能页 [Ink-Veil Cloud Coalescence](https://zenless-zone-zero.fandom.com/wiki/Basic_Attack:_Ink%E2%80%90Veil_Cloud_Coalescence)、[Ink Manifestation](https://zenless-zone-zero.fandom.com/wiki/EX_Special_Attack:_Ink_Manifestation)）。
- 没有找到官方或技术美术的特效拆解文章；GamesRadar 的 2.0 访谈只用一句"黑压金（black-on-gold）"形容她的攻击（[The big ZZZ 2.0 interview](https://www.gamesradar.com/games/action-rpg/the-big-zenless-zone-zero-2-0-interview-buffing-old-characters-standing-up-to-miyabi-the-missing-idol-faction-and-the-undeniable-yixuan/)）。以下分层是看图归纳，不是官方说法。

### 8.2 看过的画面（链接可点，缩略图不进仓库）

| 画面 | 来源 | 看到什么 |
|---|---|---|
| 强化特殊技出手帧 | [GosuGamers 攻略截图](https://static.gosugamers.net/67/ad/09/b136cfc0f21b9be2f4f1c5e138b22eaef3cca1fe6c60bc28103cd6dcaa.jpg) | 手前一团金色光束 + 金色火星，光束**外侧**裹着一圈黑墨烟，烟在动、半透明；最亮处接近暖白 |
| 攻击瞬间（侧后）| [DBLTAP 截图](https://images2.minutemediacdn.com/image/upload/c_crop,w_1920,h_1080,x_0,y_0/c_fill,w_720,ar_16:9,f_auto,q_auto,g_auto/images/voltaxMediaLibrary/mmsport/dbltap_en_international_web/01jwtkzr71z00rp7snm3.jpg) | 命中点是一团爆开的金色光，身后拖一只**黑墨翅膀**：墨是软烟状的拖尾体，里面嵌着金色火星和金丝；墨在人物后方、压在暗背景上 |
| 终结技翅膀 | [HoYoWiki 视频封面](https://i.ytimg.com/vi/-m-ZPZDJdLM/maxresdefault.jpg) | 整对翅膀是自发光的金，外缘散成金色碎羽和粒子，几乎没有黑；黑只在翅膀后方的烟和画面暗角 |
| 终结技过场 | [YouTube · Yixuan's Ultimate Technique 封面](https://i.ytimg.com/vi/EYmvDv81r5A/maxresdefault.jpg) | 黑色胶片边框 + 洋红 / 暖白的逆光，人物肩甲是明亮的黄金 |
| 「墨痕化形」官方立绘 | [PixieWall 转载](https://www.pixiewall.com/content/wallpapers/full/54/pixiewall-yixuan-zenless-zone-zero-ink-materialization-4k-s1265w.jpg) | 掌中一枚**发光的金球**（实体），黑墨从球的外圈甩出、流到袖口；背景是宣纸灰白 + 大块黑墨 + 金粉，三色分区不混 |
| 「墨迹行云」皮肤翅膀 | [PixieWall 转载](https://www.pixiewall.com/content/wallpapers/full/54/pixiewall-yixuan-trails-of-ink-zenless-zone-zero-wings-4k-q7i84n.jpg) | 黑墨翅膀 + 金色羽缘，墨翅是干笔飞白的大笔触，金是笔触边缘的丝状高光；背景留白 |
| 青溟鸟 / 日常 | [PixieWall 转载](https://www.pixiewall.com/content/wallpapers/full/54/pixiewall-yixuan-zenless-zone-zero-auric-ink-mage-5k-8nq58h.jpg)、[YouTube · Gameplay & Skill Showcase 封面](https://i.ytimg.com/vi/0MD_LRLm6kQ/maxresdefault.jpg) | 角色本体是白 + 明黄 + 黑三色；青溟鸟是黑墨剪影带金边，飞行时拖黑墨尾 |

### 8.3 分层归纳

| 层 | 仪玄的做法 | 质感 / 明度 |
|---|---|---|
| **实体（主体）** | 金色的**光**：光球、光束、翅膀都是自发光的能量体，不是金属，也不是深色剪影 | 画面里最亮；最热处暖白 ≈ `#FFF3C4`，主体金 ≈ `#F2C14E`，边缘琥珀 ≈ `#C8842A`；软边、带细碎金粒和流动的丝状高光；面积小 |
| **金粒 / 金丝** | 火星、碎羽、流线，从实体往外甩，也嵌在墨里 | 中亮，Add；面积中等；是实体与墨之间的过渡层 |
| **外层墨** | 黑墨烟、墨翅、墨尾：半透明、运动中的 smoke / 笔触，只在实体**外圈、后方、拖尾**；永远不静止、不贴在实体表面 | 黑 ≈ `#15151A`，核心不透明、边缘羽化；里面必带金火星；面积最大但压在暗背景上 |
| **玄 / 云** | 深青灰的烟云与画面暗角 | ≈ `#2A2F3A`，最低频的底 |
| **留白 / 闪帧** | 终结技过场的暖白逆光、宣纸灰白背景 | 只在 2–4 帧或过场里出现 |

整体不发黑的原因：(1) 实体永远发光，黑永远不是实体；(2) 黑只在边缘和运动里出现，而且被金火星打破；(3) 面积最大的墨最透明，面积最小的光最亮；(4) 场景偏暗，金天然最亮。第 (4) 条是和本项目最大的差别：赛道是亮沙地 + 蓝天，半透明黑墨压在亮沙上会变成灰污（第一版已经证明），所以外层墨在本项目要比仪玄更紧、更短、带金边。

## 9. 第二轮参考：五条"画出来的形"路线（2026-10-28 晚）

仪玄式分层在浏览器里做了一个原型（claude.ai 私有 artifact，不进仓库），用户看后说"这个本身就很难看，到底有什么办法才能搞个好的""还是帮我找找不同的 reference"。第二轮参考板是一页本地 HTML（缩略图来自 Bing，不进仓库），这里留可点开的来源与结论。

### 9.1 结论：缺的是画出来的形，不是分层

五条路线里没有一条靠程序化噪声加软粒子做出来，它们全是画出来的形：手绘笔触定格、画好的掌印符图、带描边的法相、按形状建的网格。本项目前两版实现和浏览器原型难看的共同原因，是形状贴图都是脚本生成的噪声。**更正（实施会话 2026-10-28 晚）**：第四轮小样（共享目录 `palm-vfx-shots/material-stills/`）的笔触与墨圆已改用克利夫兰美术馆 CC0 书法扫描（月山道宗「一」字）裁出的真实素材，经文是 Noto Serif SC 烘的真实《心经》，只剩掌印 / 破印仍是网格正投影的程序化硬边。它仍然难看，所以"素材是真的"不等于"用对了"，第四轮的问题在用法（见 [push-projectile-hands.md 第 13.6 节](push-projectile-hands.md#136-第四轮小样诊断素材是真的用法不对)）：实体仍是黑底加金点，纯黑大面积压在亮沙上，扫描笔触放大到百余单位后只剩实心黑块。分层、阶段、配表、控制器这些结构可以保留；要先把画出来的形**按原尺度、按画面明度结构**装进去，再谈其他。

### 9.2 五条路线

| 路线 | 代表 | 主体 | 外层 / 墨 | 需要的素材 | 亮沙地上 | 来源 |
|---|---|---|---|---|---|---|
| A 法相 | 燕云十六声「护法掌」「金身」：出掌时身后浮现半透明金色佛身，几秒内消散 | 加色显形的佛身 / 佛掌图 | 可有可无 | 一张立绘级的佛手 / 佛身图 | 好 | [Fextralife wiki](https://static0.fextralifeimages.com/file/wherewindsmeet/0/04/Guardian-palm-mystic-art-wwm-wiki-guide-min.png)、[allthings.how 解锁与空中版](https://allthings.how/guardian-palm-in-where-winds-meet-unlock-tiers-and-aerial-slam/)、[Sportskeeda：功夫致敬](https://widgets.sportskeeda.com/esports/all-kung-fu-hustle-references-where-winds-meet) |
| B 金刚相 | 永劫无间 · 天海：金线描边、内部发光的巨型法相 | 描边 + 自发光的网格 | 少 | 描边 shader + 现有佛手网格 | 好 | [YouTube 封面](https://i.ytimg.com/vi/6n39dqb8psw/maxresdefault.jpg)、[4Gamers 造型图](https://img.4gamers.com.tw/ckfinder-th/image2/auto/2021-09/tianhai-vajra-naraka-bladepoint-210915-192523.jpg) |
| C 掌印符图 | manpye「Buddha Palm FX」（Unity）：画得很精的金色掌印符图从空中落下，配少量火星与一朵花 | 一张掌印图 | 无 | 一张精画的掌印图 | 好 | [ArtStation 封面](https://cdna.artstation.com/p/assets/covers/images/066/700/022/large/manpye-manpye-bdp-thumnail.jpg?1693534830) |
| D 手绘定格 | Hades（手绘笔触 RGB + Alpha 序列帧）、罪恶装备 Strive、龙珠斗士 Z（逐帧画的形，硬边三四阶明度） | 画出来的形 | 就是它 | 每个元素一套 6–16 帧序列 | 好，硬边不糊 | [80.lv Hades 特效幕后](https://80.lv/articles/a-behind-the-scenes-look-at-the-effects-in-hades)、[ArtStation · Beak Driver VFX](https://cdnb.artstation.com/p/assets/covers/images/073/741/423/large/de-andre-martin-de-andre-martin-beakdriverthumbnail.jpg?1710360153)、[Dustloop · DBFZ Cell Kamehameha](https://www.dustloop.com/wiki/images/6/66/DBFZ_Cell_Kamehameha.png) |
| E HoYo 式 | 原神 · 钟离元素爆发、绝区零 · 仪玄（第 8 节） | 金色几何网格 + 自发光 | 外层软墨 + 火星 | 网格 + 画好的碎片 / 墨图 | 一般：软墨在亮沙上偏灰（原型已验证） | [TheGamer 钟离爆发](https://static1.thegamerimages.com/wordpress/wp-content/uploads/2023/12/zhongli-burst-in-genshin-impact-1.jpg) |

### 9.3 素材来源（可买的占位）

- Vefects「Anime Stylized VFX」「Stylized VFX」有 URP 版，含斩击、烟、冲击的手绘风序列帧与可换色 shader（[Gumroad](https://vefects.gumroad.com/l/AnimeStylizedVFX-Unity)；注意 Flipbook VFX 那一包只支持内置管线）。
- 「Stylized Critical Slash FX」：136 个斩击网格 + URP 的 Shader Graph 材质（[itch.io](https://criticalvfx.itch.io/stylized-critical-slash-fx/purchase)），可当劲风 / 拖尾的形。
- 「Unity VFX: Stylized Ground Slash」Udemy 课：Blender 建斩击网格 → 手绘高光与火星贴图 → Unity 组装，是 D 路线的完整流程（[Udemy](https://www.udemy.com/course/unity-vfx-stylized-ground-slash/)）。
- AI 出图：自己的 prompt 生成的掌印、佛手、笔触图可以用，清理成硬边 mask 后进项目；实施会话此前为避开授权问题改用程序化生成，这是误判。

### 9.4 推荐

C + A 合体：主角是一张画好的金色掌印符图（硬边、带掌纹与经文，像印章），出掌时以法相方式在空中显形并飞出；劲风和拖尾用 D 的手绘笔触序列帧（先用买来的包占位，再换自己画的）。软烟墨、噪声滚动、matcap 全部不要。待用户选定后改写 README 第 2 节与 push-projectile-hands.md。
