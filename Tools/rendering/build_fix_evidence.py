#!/usr/bin/env python3
"""Builds the JPEG evidence for the 2026-10-27 play-test fixes from local PNG captures.

    python -I Tools/rendering/build_fix_evidence.py

Inputs (local, git-ignored PNGs under Docs/rendering/racemap/):
  hdrdev_hf_*, hdrpr_hf_*, hdrfix_hf_*, hdrpr_bal_*, hdrfix_bal_*  edge-line states (hidden / off / on / vNNN),
      rendered with an HDR camera buffer like the game (dev = a scratch copy of dev 5f91e51's RaceMap.unity,
      pr = the branch before the SG_MapEdge fix, fix = after it)
  float_before/, float_after/  side close-ups and gameplay-camera frames from an autopilot Solo lap
Outputs: Docs/rendering/racemap/fix_edge_line/*.jpg and Docs/rendering/racemap/fix_buddha_ground/*.jpg.
Needs Pillow.
"""
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "Docs" / "rendering" / "racemap"
BAR = 30
GOLD = (240, 200, 90)


def load(folder, view):
    return Image.open(BASE / folder / (view + ".png")).convert("RGB")


def amplified(a, b, gain=4):
    return ImageChops.difference(a, b).point(lambda v: min(255, v * gain))


def peak_box(diff, w, h):
    """Crop box of size w x h centred on the brightest column/row band of a difference image."""
    grey = diff.convert("L")
    cols = [sum(grey.crop((x, 0, x + 32, grey.height)).histogram()[24:]) for x in range(0, grey.width - 32, 32)]
    rows = [sum(grey.crop((0, y, grey.width, y + 32)).histogram()[24:]) for y in range(0, grey.height - 32, 32)]
    cx = cols.index(max(cols)) * 32 + 16
    cy = rows.index(max(rows)) * 32 + 16
    x0 = min(max(0, cx - w // 2), grey.width - w)
    y0 = min(max(0, cy - h // 2), grey.height - h)
    return (x0, y0, x0 + w, y0 + h)


def grid(cells, cols, cw, ch, titles):
    rows = (len(cells) + cols - 1) // cols
    canvas = Image.new("RGB", (cols * cw, rows * (ch + BAR)), (14, 14, 14))
    draw = ImageDraw.Draw(canvas)
    for i, (im, title) in enumerate(zip(cells, titles)):
        x, y = (i % cols) * cw, (i // cols) * (ch + BAR)
        canvas.paste(im.resize((cw, ch), Image.LANCZOS), (x, y + BAR))
        draw.text((x + 10, y + 9), title, fill=GOLD)
    return canvas


def edge_line():
    out = BASE / "fix_edge_line"
    out.mkdir(exist_ok=True)
    views = ["01_start_grid", "02_marble_stretch", "03_corner", "04_intro_shot1"]
    cells, titles = [], []
    for view in views:
        pr_leak = ImageChops.difference(load("hdrpr_hf_hidden", view), load("hdrpr_hf_off", view))
        box = peak_box(pr_leak, 640, 360)
        for label, folder in (("dev 5f91e51", "hdrdev_hf"), ("PR #64 before fix", "hdrpr_hf"), ("after fix", "hdrfix_hf")):
            cells.append(load(folder + "_hidden", view).crop(box))
            titles.append(f"{view} | {label} | skill off (crop 1:1)")
        for label, folder in (("dev 5f91e51", "hdrdev_hf"), ("PR #64 before fix", "hdrpr_hf"), ("after fix", "hdrfix_hf")):
            cells.append(amplified(load(folder + "_hidden", view), load(folder + "_off", view)).crop(box))
            titles.append(f"{view} | {label} | line's contribution x4 (skill off minus line removed)")
    grid(cells, 3, 640, 360, titles).save(out / "skill_off_dev_before_after.jpg", quality=88, optimize=True)

    cells, titles = [], []
    for view in ("01_start_grid", "04_intro_shot1"):
        on_leak = ImageChops.difference(load("hdrpr_hf_on", view), load("hdrpr_hf_off", view))
        box = peak_box(on_leak, 640, 360)
        for label, folder in (("before fix", "hdrpr_hf"), ("after fix", "hdrfix_hf")):
            for state, name in (("v025", "fade 25%"), ("on", "skill active 100%")):
                cells.append(load(folder + "_" + state, view).crop(box))
                titles.append(f"{view} | {label} | {name}")
    grid(cells, 4, 480, 270, titles).save(out / "skill_active_unchanged.jpg", quality=88, optimize=True)

    cells, titles = [], []
    for view in ("01_start_grid", "03_corner"):
        pr_leak = ImageChops.difference(load("hdrpr_bal_hidden", view), load("hdrpr_bal_off", view))
        box = peak_box(pr_leak, 640, 360)
        for label, folder in (("Balanced before fix", "hdrpr_bal"), ("Balanced after fix", "hdrfix_bal")):
            cells.append(amplified(load(folder + "_hidden", view), load(folder + "_off", view)).crop(box))
            titles.append(f"{view} | {label} | line's contribution x4")
    grid(cells, 2, 640, 360, titles).save(out / "balanced_before_after.jpg", quality=88, optimize=True)
    print("wrote", out.relative_to(ROOT))


def buddha_ground():
    out = BASE / "fix_buddha_ground"
    out.mkdir(exist_ok=True)
    moments = [("1_intro", "start grid, intro (just before GO)"), ("2_go_plus1", "1 s after GO"),
               ("4_stretch_p30", "lap 30% (straight)"), ("5_stretch_p60", "lap 60%")]
    cells, titles = [], []
    for tag, name in moments:
        for label in ("before", "after"):
            cells.append(Image.open(BASE / ("float_" + label) / (tag + "_side.png")).convert("RGB"))
            titles.append(f"{label} | {name} | side camera 24 m away, 0.9 m above the lane")
    grid(cells, 2, 960, 540, titles).save(out / "side_closeups_before_after.jpg", quality=88, optimize=True)

    cells, titles = [], []
    for tag, name in (("3_go_plus4", "4 s after GO"), ("5_stretch_p60", "lap 60%")):
        for label in ("before", "after"):
            cells.append(Image.open(BASE / ("float_" + label) / (tag + "_game.png")).convert("RGB"))
            titles.append(f"{label} | {name} | gameplay camera")
    grid(cells, 2, 960, 540, titles).save(out / "gameplay_before_after.jpg", quality=88, optimize=True)
    print("wrote", out.relative_to(ROOT))


if __name__ == "__main__":
    edge_line()
    buddha_ground()
