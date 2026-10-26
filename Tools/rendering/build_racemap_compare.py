#!/usr/bin/env python3
"""Builds before/after side-by-side JPEGs from two RaceMap capture folders.

    python -I Tools/rendering/build_racemap_compare.py <before_label> <after_label> <out_name>

Reads Docs/rendering/racemap/<label>/NN_<view>.png (written by RaceMapViewpointCapture) and writes
Docs/rendering/racemap/<out_name>/NN_<view>.jpg, each a labelled left/right pair at 1920x540 per image row,
plus <out_name>/all.jpg with every viewpoint stacked. JPEG keeps the files small enough for Git LFS reviews.
Needs Pillow.
"""
import os
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "Docs" / "rendering" / "racemap"
HALF_W, HALF_H, BAR = 960, 540, 34


def pair(before_png, after_png, before_title, after_title, view):
    canvas = Image.new("RGB", (HALF_W * 2, HALF_H + BAR), (14, 14, 14))
    draw = ImageDraw.Draw(canvas)
    for i, (png, title) in enumerate(((before_png, before_title), (after_png, after_title))):
        with Image.open(png) as im:
            canvas.paste(im.convert("RGB").resize((HALF_W, HALF_H), Image.LANCZOS), (i * HALF_W, BAR))
        draw.text((i * HALF_W + 12, 10), f"{title}  |  {view}", fill=(240, 200, 90))
    return canvas


def main():
    if len(sys.argv) != 4:
        print(__doc__)
        return 1
    before, after, out_name = sys.argv[1:4]
    out = BASE / out_name
    out.mkdir(parents=True, exist_ok=True)
    rows = []
    for png in sorted((BASE / before).glob("*.png")):
        other = BASE / after / png.name
        if not other.exists():
            print("missing", other)
            continue
        img = pair(png, other, "BEFORE " + before, "AFTER " + after, png.stem)
        target = out / (png.stem + ".jpg")
        img.save(target, quality=90, optimize=True)
        rows.append(img)
        print("wrote", target.relative_to(ROOT))
    if rows:
        stacked = Image.new("RGB", (rows[0].width, sum(r.height for r in rows)), (0, 0, 0))
        y = 0
        for r in rows:
            stacked.paste(r, (0, y))
            y += r.height
        stacked.save(out / "all.jpg", quality=88, optimize=True)
        print("wrote", (out / "all.jpg").relative_to(ROOT))
    return 0


if __name__ == "__main__":
    sys.exit(main())
