# 菜单按键图标补充任务（交给生成素材的 agent）

起因：dev 合入玩法机制更新（#74）后，技能改为单键释放（Q/1、E/2、R/3），鼠标瞄准、鼠标左/右键推掌。菜单里要显示的键帽，现有图标缺 **1、2、3** 三个数字键，另需一个「鼠标移动」图标。其余图标都已交付，不要重做。

- 风格、工作流程、后期脚本、检查方法：照 [asset-generation-brief.md](asset-generation-brief.md) 第 1、2 节和第 3 节「按键图标」。
- 本文只列新增的 4 项。完成后在 [asset-list.md](asset-list.md) 的「按键图标」表加对应行（状态「已提供」），在 asset-generation-brief.md 第 4 节「生成记录」加一行。
- **不要**：改 Unity 场景或代码；改 `Assets/UI/MainMenu/MenuKeys.asset`（TMP 图集由开发重建）；覆盖已有的 `key_*.png`。

## 参考图（必须看）

| 文件 | 看什么 |
|---|---|
| `ArtSource/MainMenu/raw/K01_01.png` | **键帽的标准**：圆角方框、粗细、手绘字标、字号占键帽的比例。新键帽要和它是同一套，放进去分不出来 |
| `ArtSource/MainMenu/raw/K02_01.png` | **鼠标图标的标准**：顶视鼠标、粗线轮廓、按键位置实填 |
| `Assets/UI/MainMenu/Art/key_q.png`、`key_w.png`、`key_mouse_left.png` | 已入库的成品（白色墨迹 + 透明底），用来核对尺寸和边距 |

## 新增图标

| ID | 文件名 | 内容 | 用在哪里 |
|---|---|---|---|
| K05 | `key_1.png` | 键帽，字标「1」 | 技能页槽 1 的第二个键帽（Q 旁）；设置页「施展技能」 |
| K06 | `key_2.png` | 键帽，字标「2」 | 槽 2（E 旁） |
| K07 | `key_3.png` | 键帽，字标「3」 | 槽 3（R 旁） |
| K08 | `key_mouse_move.png` | 顶视鼠标，**所有按键都不填**（空心），机身两侧各画一小段表示移动的短弧线或一对方向小箭头 | 设置页「转动佛手」一行，表示鼠标瞄准 |

### 规格

- 成品 128×128，`ink` 模式（白色墨迹 + 透明底），和现有 `key_*.png` 一致，存到 `Assets/UI/MainMenu/Art/`。
- 键帽（K05–K07）：圆角方框的边长、线宽、圆角、四周留白与 `key_q.png` 完全一致；数字高度与 `key_q.png` 的字母高度一致；字形用同一款手绘无衬线细线体，数字必须清晰可辨（尤其「1」不要带过多装饰，「3」不要写成「8」或「B」）。
- K08：外形与 `key_mouse_left.png` 同一只鼠标（同样的大小、线宽、比例），只是按键区不填色；移动提示笔画要比机身线细，且不碰到 128×128 的边。缩到 32px 时仍能看出是鼠标、并且左右键没有被填黑。

### 提示词

先把 `K01_01.png` 作为风格参考图。通用尾句同 asset-generation-brief.md 第 3 节。

K05–K07（一次生成一张 3 个键帽的横排图，再按留白切开；也可以一次一张）：

`Three keyboard keycap icons in a row, each a hollow rounded square drawn with the same heavy hand-inked outline, thin simple sans-serif labels "1", "2", "3" centred inside, minimal, front view, identical size and spacing, same style as the reference image.` + 通用尾句

K08：

`A top-down computer mouse icon, tall rounded capsule outline in heavy ink line with a horizontal line and a vertical line dividing the button area, all buttons left empty (hollow, not filled), plus two short thin curved motion lines on the left and right of the body suggesting sideways movement, same style as the reference mouse image.` + 通用尾句

### 验收

1. 逐个核对数字是「1」「2」「3」，不是别的字形。
2. 把 `key_1.png`、`key_q.png`、`key_e.png` 并排放在一起看：线宽、圆角、字高、边距一致。
3. 叠在黑色和 `#B07A55` 背景上检查：没有白边、灰边、残留方块底。
4. 缩小到 32px：数字和鼠标仍能认出；K08 的两个键没有被填黑。

## 不需要图标的东西（避免多做）

「前进 W」「鼠标左/右键推掌」「Q E R」「A D」「Esc」等已有图标；Practice（练习）入口、执念/反噬的说明、冷却等都用文字，不做图标；手柄不做。
