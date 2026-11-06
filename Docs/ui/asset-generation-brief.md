# 主菜单美术素材生成说明（交给生成素材的 agent）

本文是自包含的任务说明：照着做就能产出全部可以生成的菜单素材，并放进项目。
- 风格规范：[style-guide.md](style-guide.md)
- 资源表：[asset-list.md](asset-list.md)，ID 和本文一一对应；做完一项，就把那一行的"状态"改成"已提供"。

## 本轮任务（2026-11-06 第二轮）

**只做 A18 朱砂点和 A19 皮肤页弧线**，说明在第 3 节对应的条目里（标了"第二轮"）。其余各项都已交付，不要重做。工作流程、后期处理脚本和检查方法照第 1、2 节。

## 0. 任务概述

为《佛陀竞速》（BuddahGo，佛教和东方题材的赛车游戏）主菜单生成笔墨风格的 UI 素材。
- 游戏的视觉语言叫"一笔"：黄昏日式神社的 3D 场景当背景，UI 只是几笔白色的墨。底部一条细墨线贯穿全屏，当菜单基线；选中项用墨线和朱砂点标出；圆相（禅宗一笔画的圆）表示"一圈 / 进度"。
- 要的是**手工毛笔质感**：真实的飞白、鬃毛痕、毛边和墨色浓淡。不要矢量感、光滑边缘、电脑字体感、发光、渐变色或 3D 效果。

### 参考图

**a. 项目自己的图**（可以直接当图生图的参考图）
| 文件 | 看什么 |
|---|---|
| `Assets/UI/UI资源/文字PNG/New/如来神掌.png`（同目录还有 5 个技能名） | **书法笔迹的标准**：标题和皮肤名都要像同一个人写的 |
| `Assets/UI/UI资源/排行PNG/壹.png` | 书法的另一种用法（排名） |
| `Docs/ui/references/style/target_title_screen.webp` | **用户认可的标题画面**：竖排书法居中、右下一枚朱印、两侧细线夹着 BUDDAHGO（AI 生成的效果图） |
| `Docs/ui/references/mockups/01_title.png` … `10_confirm.png` | 我们的效果图 v0.2：每种素材在画面里的用法和大小（里面的笔触是程序画的占位，**质感不要照这个**） |
| `Docs/rendering/racemap/menu_dusk_compare/menu_main.jpg` | 菜单场景（黄昏神社），素材最后要叠在这样的画面上 |

**b. 第三方游戏截图**（只用来理解风格，**不能临摹，也不能当图生图的参考图**，以免产出和原作相似的素材）
| 文件 | 看什么 |
|---|---|
| `Docs/ui/references/style/got_main_menu_brush_highlight.webp` | 对马岛之魂主菜单：白色墨刷衬底表示选中，字很小，场景当背景 → A11、A04–A06 |
| `Docs/ui/references/style/got_difficulty_select.webp` | 对马岛之魂难度选择：毛边细线夹住内容，撕纸按钮 → A07–A08、A12 |
| `Docs/ui/references/style/got_item_emblem_paper_button.webp` | 对马岛之魂物品选择：墨点飞溅的徽记、笔刷箭头、撕纸按钮 → A14、A15、A12 |
| `Docs/ui/references/style/got_side_dialog.webp` | 对马岛之魂确认弹窗：侧边渐变压暗，毛边线 → A07–A08 |
| `Docs/ui/references/style/got_chapter_card_calligraphy.webp` | 对马岛之魂章节卡：竖排毛笔大字的气势 → A01 |
| `Docs/ui/references/style/gris_pause_rings.webp`、`gris_settings.png`、`gris_quit_confirm.png` | GRIS：细线圆环组织画面、极度克制 → A09–A10 的气质（我们的是毛笔圆相，不是几何线） |
| `Docs/ui/references/style/abzu_settings.webp`、`abzu_pause.webp` | ABZÛ：小字、只染一层色，不加面板 → 整体克制程度 |

**每项素材主要看哪几张**：
| 素材 | 参考图 |
|---|---|
| A01、A02、A16 书法 | `如来神掌.png`（笔迹）、`target_title_screen.webp`（排版与气势）、`got_chapter_card_calligraphy.webp`（只看气势） |
| A03 印章 | `target_title_screen.webp`（印章大小和位置） |
| A04–A06 墨线、A11 墨刷衬底 | `got_main_menu_brush_highlight.webp`、`mockups/02_home.png` |
| A07–A08 细线 | `got_difficulty_select.webp`、`got_side_dialog.webp`、`mockups/01_title.png` |
| A09–A10 圆相 | `mockups/03_solo.png`、`mockups/09_countdown.png`、`gris_pause_rings.webp` |
| A12 宣纸签 | `got_difficulty_select.webp`、`got_item_emblem_paper_button.webp`、`mockups/03_solo.png` |
| A14、A15 墨点和箭头 | `got_item_emblem_paper_button.webp`、`mockups/05_skill.png` |
| A17、K01–K02 光标和按键 | `mockups/08_settings.png`（按键提示的大小和用法） |
| A18 朱砂点 | `mockups/02_home.png`（基线墨线末端的点）、`mockups/08_settings.png`（列表左边线外的点）、`got_item_emblem_paper_button.webp`（只看墨点的笔意） |
| A19 皮肤页弧线 | `mockups/04_skin.png`（弧线和排在弧上的皮肤名）、`Assets/UI/MainMenu/Sprites/Menu_arc.png`（现在的占位：形状和弧度照它） |

## 1. 工作流程（每一项都照这个做）

1. **生成原图**：按第 3 节的提示词生成。笔墨类一律是**纯白背景、纯黑墨迹**，平面扫描感，没有阴影、纸纹、透视和边框。尽量用最高分辨率。
2. **检查原图**：对照该项的验收标准，不合格就重新生成。书法类要**逐字检查汉字是否写对**，写错直接作废。
3. **保存原图**：存到 `ArtSource/MainMenu/raw/<ID>_<序号>.png`（项目根目录下，不在 `Assets/` 里，所以不会被 Unity 导入）。
4. **后期处理**：按第 2 节的脚本转成成品：
   - 笔墨类：转成"白色墨迹 + 透明底"；
   - 彩色类（印章、宣纸签）：去掉白底，保留原色。

   然后裁边，缩放到目标尺寸，需要拆段的拆段。
5. **放进项目**：成品存到 `Assets/UI/MainMenu/Art/<交付文件名>`。
6. **更新资源表**：在 [asset-list.md](asset-list.md) 里把这一项改成"已提供"，并在下面的"生成记录"里写一行：用了哪条提示词、哪张原图、做过哪些手动修整。

**不要**：改 Unity 场景或代码；覆盖 `Assets/UI/MainMenu/Sprites/` 下现有的占位图（开发接入时自己替换）；使用任何有版权的字体或图片。

## 2. 后期处理脚本

需要 Python 3 和 Pillow（`pip install pillow`）。把下面的脚本存为 `ArtSource/MainMenu/process.py`。

```python
"""Turn generated art into game-ready PNGs.

ink:    black ink on white -> white ink, darkness becomes alpha (keeps dry-brush and ink density).
colour: coloured art on white -> same colours, white background removed.
Then trim to content, pad, and fit into the target size (keeping aspect).

usage:
  python -I process.py ink    raw.png out.png 560 2000 [--pad 24] [--black 40 --white 235]
  python -I process.py colour raw.png out.png 256 256  [--pad 8]  [--white 240]
"""
import argparse
from PIL import Image, ImageOps

ap = argparse.ArgumentParser()
ap.add_argument("mode", choices=["ink", "colour"])
ap.add_argument("src"); ap.add_argument("dst")
ap.add_argument("w", type=int); ap.add_argument("h", type=int)
ap.add_argument("--pad", type=int, default=16)
ap.add_argument("--black", type=int, default=40, help="ink: grey at or below this is full ink")
ap.add_argument("--white", type=int, default=235, help="grey at or above this is background")
ap.add_argument("--no-fit", action="store_true", help="keep the trimmed size, only scale to height h")
a = ap.parse_args()

src = Image.open(a.src).convert("RGB")
grey = ImageOps.grayscale(src)

if a.mode == "ink":
    lo, hi = a.black, a.white
    alpha = grey.point(lambda v: 255 if v <= lo else 0 if v >= hi else int(255 * (hi - v) / (hi - lo)))
    out = Image.new("RGBA", src.size, (255, 255, 255, 0))
    out.putalpha(alpha)
else:
    hi = a.white
    alpha = grey.point(lambda v: 0 if v >= hi else 255 if v <= hi - 60 else int(255 * (hi - v) / 60))
    out = src.convert("RGBA")
    out.putalpha(alpha)

box = out.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
if box:
    out = out.crop(box)
if a.no_fit:
    s = (a.h - 2 * a.pad) / out.height
    out = out.resize((max(1, round(out.width * s)), a.h - 2 * a.pad), Image.LANCZOS)
    canvas = Image.new("RGBA", (out.width + 2 * a.pad, a.h), (255, 255, 255, 0))
else:
    s = min((a.w - 2 * a.pad) / out.width, (a.h - 2 * a.pad) / out.height)
    out = out.resize((max(1, round(out.width * s)), max(1, round(out.height * s))), Image.LANCZOS)
    canvas = Image.new("RGBA", (a.w, a.h), (255, 255, 255, 0))
canvas.alpha_composite(out, ((canvas.width - out.width) // 2, (canvas.height - out.height) // 2))
canvas.save(a.dst)
print(a.dst, canvas.size)
```

**拆段**（墨线、细线）：先用 `--no-fit` 按目标高度处理成一整条，再按第 3 节该项的说明裁出起笔、中段和收笔。中段要能**无缝平铺**：从笔画最均匀的一段裁出来，左右两端各取 32px 互相交叉淡化，然后把两张并排拼起来检查接缝。

**处理后检查**：把成品分别叠在黑色和 `#B07A55`（黄昏场景的平均色）背景上看。不能有白边、灰边或残留的方块底；飞白要保留；墨迹最浓处的 alpha 要接近 255。

## 3. 素材逐项说明

尺寸是成品尺寸（1080p 显示尺寸的 2 倍）。"通用尾句"是指在每条笔墨类提示词后面都加上这一句：

> `Pure black ink on a pure white background, flat top-down scan, no paper texture, no shadow, no frame, no text other than specified, very high resolution.`

---

### P0

**A01 标题书法「佛陀竞速」竖排**
- 成品：`menu_title_vertical.png`，560×2000，`ink` 模式。
- 提示词：
  `Chinese brush calligraphy of exactly four characters "佛陀竞速", written vertically in one column, top to bottom. Xingkai semi-cursive style like a master calligrapher, confident fast strokes, strong dry-brush texture (飞白) at stroke ends, ink density varies, one character slightly larger for rhythm.` + 通用尾句
- 参考图：`如来神掌.png`（笔迹要像同一个人写的）。
- 验收：四个字都对、没有多余笔画或字；四个字竖排在一列；有明显的飞白；笔迹和技能名书法风格一致。

**A02 标题书法「佛陀竞速」横排**
- 成品：`menu_title_horizontal.png`，1800×480，`ink` 模式。
- 提示词：和 A01 相同，把 `written vertically in one column, top to bottom` 换成 `written horizontally in one row, left to right`。
- 要求：最好把 A01 的成品也当参考图，让两张像同一个人写的。
- 验收：同 A01，另外两张放在一起看要像同一手笔迹。

**A03 印章**
- 成品：`menu_seal.png`，256×256，`colour` 模式，`--pad 8`。
- 提示词：
  `A square Chinese seal stamp impression in vermilion red ink (#B8352A), the single character "佛" in seal script (篆书), carved stone seal look, rough uneven border and small ink gaps, flat top-down scan on a pure white background, no shadow, centered, square.`
- 验收：方形；"佛"是篆书而且字形正确；颜色接近 #B8352A；边缘有石刻的残缺感；不能是电脑字体的印章效果。

**A04–A06 选中墨线（起笔 / 中段 / 收笔）**
- 成品：`menu_ink_head.png` 160×48、`menu_ink_body.png` 256×48（可平铺）、`menu_ink_tail.png` 480×48，`ink` 模式。
- 提示词：
  `A single horizontal Chinese ink brush stroke, very long and thin, left to right: a heavy pressed start on the left, a steady even body in the middle, and a long dry-brush (飞白) tail on the right that breaks into separate bristle streaks. Wide panoramic composition, the stroke is about 1/20 of the image height.` + 通用尾句
- 处理：用 `--no-fit` 处理到高 48 的整条，然后：
  - 起笔：从左端裁 160；
  - 收笔：从右端裁 480；
  - 中段：从中间均匀的一段裁 256，按第 2 节做成可平铺。
- 验收：三段接起来看不出接缝；中段平铺 5 次没有明显重复感；收笔有清楚的分叉鬃毛痕。

**A07–A08 毛边细线（中段 + 左右两端）**
- 成品：`menu_line_body.png` 512×16（可平铺）、`menu_line_end_l.png` 128×16、`menu_line_end_r.png` 128×16，`ink` 模式。
- 提示词：
  `A single very thin horizontal line drawn with a fine brush, slightly uneven and frayed edges, a few tiny breaks, both ends fading out naturally. Wide panoramic composition.` + 通用尾句
- 处理：整条处理到高 16，线粗约 2–3px（不够细的话，处理前先把原图纵向压缩）。两端各裁 128 得到 `end_l` 和 `end_r`，中间裁 512 做成可平铺。
- 验收：线细而稳，不能断成虚线；两端自然消失；平铺无接缝。

---

### P1

**A09 粗圆相**
- 成品：`menu_enso_bold.png`，1440×1440，`ink` 模式；另附 `menu_enso_bold.json`。
- 提示词：
  `A single Zen ensō circle painted in one continuous brush stroke, bold and heavy, the circle is not closed and leaves a gap at the upper right, heavy pressed start and a dry-brush (飞白) end, slightly imperfect round shape.` + 通用尾句
- JSON 内容（在图上看清后估计填写；角度以图片中心为原点，0° 指向右，顺时针为正，y 轴向下）：
  `{ "startDeg": <起笔角度>, "endDeg": <收笔角度>, "clockwise": true|false, "strokeWidthPx": <最粗处宽度> }`
- 验收：一笔完成；有缺口；起笔重、收笔有飞白；圆心大致在图片中心，四周留空均匀。

**A10 细圆相**
- 成品：`menu_enso_thin.png`，800×800，`ink` 模式；另附 `menu_enso_thin.json`（格式同 A09）。
- 提示词：同 A09，把 `bold and heavy` 换成 `thin and light, delicate fine brush`。
- 验收：线宽约 8–12px（在 800 尺寸下）；其余同 A09。

**A18 朱砂点**（第二轮）
- 成品：`menu_dot.png`，64×64，`ink` 模式，`--pad 6`。画成**纯白**墨迹，游戏里由程序染成朱砂色 #B8352A，所以原图照样是黑墨白底。
- 用途：选中光标、就绪状态。在屏幕上只有 6–12px 大（主页基线墨线的末端、设置页列表左边线外、殿门菜单、皮肤名前、技能页悬停圈上），现在用的是程序画的正圆 `Menu_dot`，看起来像电脑圆点。
- 提示词：
  `A single Chinese brush dot (the "丶" stroke of calligraphy): the brush pressed down once and lifted, a full round-ish body slightly heavier on one side with a short soft tail where the brush left, natural uneven edge, solid ink, not a perfect circle, no splatter.` + 通用尾句
- 多生成几个，挑一个。处理后墨点大致居中，四周留空均匀。
- 验收：
  - 缩到 12px 和 8px 看，仍然是一个饱满的"点"，不是一团糊或一条线；
  - 看得出是一笔点出来的（一侧略重、带极短的出锋），但**不要**明显的拖尾或飞溅；
  - 墨迹实心，最浓处 alpha 接近 255（染成朱砂色后要醒目）；
  - 把成品染成 #B8352A，叠在黑色和 #B07A55 上看，边缘干净。

**A11 短墨刷衬底**
- 成品：`menu_brush_bar.png`，600×120，`ink` 模式。
- 提示词：
  `One short wide horizontal dry ink brush swipe, like a quick stroke made with a broad flat brush, dense in the middle with visible bristle streaks, both ends tapering and breaking up naturally.` + 通用尾句
- 验收：中段较实，适合在上面叠一行深色文字；两端自然收尾。

**A12 宣纸签**
- 成品：`menu_paper_tag.png`，480×140，`colour` 模式，`--pad 4`。
- 提示词：
  `A small horizontal strip of torn Japanese washi paper, off-white cream colour (#F4EFE6), all four edges torn with soft fibrous deckled edges, subtle paper fibres visible, flat top-down scan on a pure white background, no shadow, no text.`
- 注意：纸本身也很白，白底不好去掉。改用纯黑背景生成（提示词里换成 `on a pure black background`），处理时手动把黑底转成透明。
- 验收：四边是撕纸毛边；纸色接近 #F4EFE6；中间约 60% 的区域质感均匀，可以九宫格拉伸。

**A13 纸纹**
- 成品：`menu_paper_grain.png`，1024×1024，灰度，**不透明**、不去底。
- 提示词：
  `Seamless tileable texture of handmade washi paper fibres, very subtle low contrast, light grey on white, even lighting, no folds, no stains.`
- 处理：转灰度，调整色阶让对比更低；四边做成无缝（平铺 2×2 检查接缝）。
- 验收：2×2 平铺看不出接缝，也没有明显的重复斑块。

---

### P2

**A14 墨点飞溅**（3 张）
- 成品：`menu_splash_01.png` 至 `menu_splash_03.png`，各 512×512，`ink` 模式。
- 提示词（每张换一种形状描述）：
  `A few Chinese ink splatter drops and flicks from a brush, scattered small dots and one elongated splash, organic shapes.` + 通用尾句
- 验收：三张形状各不相同；墨点边缘自然；没有大面积糊成一团的墨块。

**A15 笔刷箭头 ‹ ›**
- 成品：`menu_chevron_l.png`、`menu_chevron_r.png`，各 120×200，`ink` 模式。
- 提示词：
  `A single chevron arrow shape "‹" pointing left, drawn in two quick connected Chinese brush strokes, dry-brush ends.` + 通用尾句（右箭头另外生成一张，把 `‹ pointing left` 换成 `› pointing right`；**不要直接镜像左箭头**）
- 验收：一眼能看出方向；笔触风格和墨线一致。

**A16 皮肤名书法**（5 张）
- 成品：`skin_name_baiyu.png`（白玉）、`skin_name_yinshen.png`（银身）、`skin_name_xueyan.png`（雪岩）、`skin_name_zhishi.png`（织石）、`skin_name_zhehong.png`（赭红），各约 900×400，`ink` 模式。
- 提示词：同 A02，把四个字换成对应的两个字。
- 名称已于 2026-11-06 定稿，与资源表 A16 一致。
- 验收：同 A01，笔迹要和标题一致。

**A19 皮肤页弧线**（第二轮）
- 成品：`menu_arc.png`，400×1760，`ink` 模式。**不要**用 `process.py` 的自动裁边和缩放把弧压变形：按下面的几何放进 400×1760 的画布（可以先裁边，再按这个位置手动摆）。
- 用途：皮肤页左侧，五个皮肤名沿这条弧排开，屏幕上显示为 200×880。现在是程序画的 `Assets/UI/MainMenu/Sprites/Menu_arc.png`。
- 形状（照占位图）：一段很细的弧，**向左凸**，像"("。它是一个大圆的一部分，圆心在弧的右边：
  - 弧的中点（上下正中，y = 880）贴着画布左边，x 约 6；
  - 从中点往上、往下都向右弯，到上下两端（y 约 6 和 1734）时 x 约 380；
  - 线宽 4–6px（屏幕上 2–3px），粗细基本均匀，中间可以略重；
  - 两端各约 1/4 的长度逐渐变淡、变细到消失（占位图在距中点 3/8 处的 alpha 约为一半）。
- 提示词：
  `A single long thin arc drawn with a fine Chinese brush in one smooth stroke, shaped like a tall opening parenthesis "(": the middle of the arc bulges to the left and both ends curve to the right, part of a large circle. Very thin, slightly uneven line with a few tiny dry-brush breaks, both ends fading out and thinning naturally. Tall narrow composition.` + 通用尾句
- 验收：
  - 形状和占位图 `Menu_arc.png` 叠在一起基本重合（向左凸，中点在左、两端在右）；
  - 线细而连贯，不能断成虚线；两端自然消失；
  - 缩到 200×880 看，线宽约 2–3px，和毛边细线 A07 是同一种笔意。

**A17 光标**
- 成品：`menu_cursor.png`，64×64，`ink` 模式。
- 提示词：
  `A small hollow circle drawn with a single fine brush stroke, nearly closed, thin even line.` + 通用尾句
- 验收：缩小到 32px 时仍然是清晰的圆圈。

---

### 按键图标（K01–K04）

K01、K02（键盘、鼠标）已交付。
- 风格：笔墨描边的空心键帽，里面的字用细线体，统一一套。
- 提示词模板：
  `A single keyboard keycap icon drawn as a hollow rounded square in fine Chinese brush line, the label "<键名>" inside in a thin simple style, minimal, front view.` + 通用尾句
- 成品：每个键一张，128×128，`ink` 模式，命名为 `key_<名称>.png`（例如 `key_enter.png`、`key_esc.png`、`key_q.png`）。
- 键位：方向键 4 个、Enter、Esc、空格、Q、E、R、W、A、S、D，以及鼠标左键、鼠标右键。
- 验收：所有图标的线宽和圆角一致；缩小到 32px 时仍然能认出键名。
- **K03、K04（手柄）不做**：已定只支持键鼠。

### 声音（S01–S09）

**不要生成。** 声音由人从授权音效库里挑选，要求见 [asset-list.md](asset-list.md) 的"声音"一节。

## 4. 生成记录

做完一项就在这里加一行。

| ID | 原图文件 | 用了哪条提示词或改了什么 | 手动修整 | 日期 |
|---|---|---|---|---|
| A01 | `ArtSource/MainMenu/raw/A01_01.png` | 竖排佛陀竞速，行楷、飞白。对照项目技能字形 | 逐字核对；等比裁切留白 | 2026-11-06 |
| A02 | `ArtSource/MainMenu/raw/A02_01.png` | 横排佛陀竞速，沿用 A01 字形作参考 | 逐字核对；等比裁切留白 | 2026-11-06 |
| A03 | `ArtSource/MainMenu/raw/A03_01.png` | 朱砂色佛字方印，石刻残边 | 按绿色通道提取印迹覆盖度，统一朱砂色 | 2026-11-06 |
| A04 | `ArtSource/MainMenu/raw/A04_01.png` | 长横笔，重起、平行中段、分叉干收 | 整笔按高处理，截左 160px，末端渐变对接 | 2026-11-06 |
| A05 | `ArtSource/MainMenu/raw/A04_01.png` | 沿用 A04 同一原笔 | 截中段 256px，32px 过渡，连续拼五次检查 | 2026-11-06 |
| A06 | `ArtSource/MainMenu/raw/A04_01.png` | 沿用 A04 同一原笔 | 截右 480px，保留分叉 | 2026-11-06 |
| A07 | `ArtSource/MainMenu/raw/A07_01.png` | 极细横线、两头渐隐 | 截中段 512px 并平滑接缝；主体约 2.5px | 2026-11-06 |
| A08 | `ArtSource/MainMenu/raw/A07_01.png` | 沿用 A07 同一原笔 | 各裁 128px，匹配中段截面 | 2026-11-06 |
| A09 | `ArtSource/MainMenu/raw/A09_01.png` | 粗圆相，右上开口、重起干收 | 按实际笔端方位估读角度，逆时针 | 2026-11-06 |
| A10 | `ArtSource/MainMenu/raw/A10_01.png` | 细笔圆相，右上开口 | 轻微增厚，按实际笔端方位估读角度，顺时针 | 2026-11-06 |
| A11 | `ArtSource/MainMenu/raw/A11_01.png` | 宽干刷、实心中段、两头散锋 | 等比缩放 | 2026-11-06 |
| A12 | `ArtSource/MainMenu/raw/A12_01.png` | 黑底米白撕纸，四边纤维，中心均匀 | 黑底提取纤维遮罩，重建纸色；Sprite 设置九宫格边界 | 2026-11-06 |
| A13 | `ArtSource/MainMenu/raw/A13_01.png` | 均匀、低对比的手工和纸纤维 | 灰度 235–252，双轴 64px 搭接，2×2 检查 | 2026-11-06 |
| A14 | `ArtSource/MainMenu/raw/A14_01.png` | 长斜甩墨、放射点、弯曲扇形，三枚分区独立绘制 | 分区裁出三张 | 2026-11-06 |
| A15 | `ArtSource/MainMenu/raw/A15_01.png` | 同图分区独立绘制左右箭头，两笔相接 | 裁去分区边框，分别输出 | 2026-11-06 |
| A16 | `ArtSource/MainMenu/raw/A16_01.png` | 按定稿名称横写白玉、银身、雪岩、织石、赭红 | 按行裁出五张，逐字核对 | 2026-11-06 |
| A17 | `ArtSource/MainMenu/raw/A17_01.png` | 细笔近闭合小圆，有小缺口 | 保留 4px 内边距，缩至 32px 检查 | 2026-11-06 |
| K01 | `ArtSource/MainMenu/raw/K01_01.png` | 同版圆角墨线键帽、手绘字标、四向键；空格使用空格符号 | 按留白分割 14 个有效键，统一边距，32px 检查 | 2026-11-06 |
| K02 | `ArtSource/MainMenu/raw/K02_01.png` | 顶视鼠标，分别实填左键或右键 | 分割两枚，32px 检查 | 2026-11-06 |
| A18 | `ArtSource/MainMenu/raw/A18_01.png` | 第 3 节朱砂点提示词，同版三枚不同落笔角度 | 取右侧短锋圆点，ink 留边 6px；8px、12px 染朱砂色双底检查 | 2026-11-06 |
| A19 | `ArtSource/MainMenu/raw/A19_01.png` | 第 3 节左凸细笔弧线提示词，弧深约为高度的 21% | ink 提取笔迹，按占位图中心线逐行配准，控制线宽，两端各四分之一渐隐；200×880 双底检查 | 2026-11-06 |
