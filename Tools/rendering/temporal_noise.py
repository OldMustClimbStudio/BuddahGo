#!/usr/bin/env python3
"""Scores the shimmer in frame sequences written by RaceMapTemporalNoise (Assets/Editor/Rendering).

    python -I Tools/rendering/temporal_noise.py <label> [--region x0,y0,x1,y1]

Reads Docs/rendering/racemap/<label>/<config>__<view>__fNN.png. The camera slid sideways by a fraction of a pixel per
frame, so a stable image changes very little between frames; shimmer shows up as large frame-to-frame changes.
Per config and view it prints:
  flicker   mean absolute change between consecutive frames (0-255, largest channel), averaged over the frame
  >8        share of pixels that change by more than 8 levels from one frame to the next (visible flicker)
  sharp     mean absolute horizontal+vertical gradient of the first frame, to spot an AA that wins by blurring
Needs Pillow.
"""
import sys
from collections import defaultdict
from pathlib import Path

from PIL import Image, ImageChops, ImageFilter, ImageStat

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "Docs" / "rendering" / "racemap"


def max_channel(img):
    r, g, b = img.split()[:3]
    return ImageChops.lighter(ImageChops.lighter(r, g), b)


def score(frames, box):
    flicker, visible = [], []
    prev = None
    for path in frames:
        img = Image.open(path).convert("RGB")
        if box:
            img = img.crop(box)
        if prev is not None:
            diff = max_channel(ImageChops.difference(img, prev))
            hist = diff.histogram()
            total = sum(hist)
            flicker.append(sum(i * c for i, c in enumerate(hist)) / total)
            visible.append(sum(hist[9:]) / total * 100.0)
        prev = img
    first = Image.open(frames[0]).convert("L")
    if box:
        first = first.crop(box)
    edges = first.filter(ImageFilter.FIND_EDGES)
    sharp = ImageStat.Stat(edges).mean[0]
    return sum(flicker) / len(flicker), sum(visible) / len(visible), sharp


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 1
    label = sys.argv[1]
    box = None
    if "--region" in sys.argv:
        box = tuple(int(v) for v in sys.argv[sys.argv.index("--region") + 1].split(","))
    groups = defaultdict(list)
    for png in sorted((BASE / label).glob("*__*__f*.png")):
        config, view, _ = png.stem.split("__")
        groups[(config, view)].append(png)
    order = []
    for config, view in groups:
        if config not in order:
            order.append(config)
    views = sorted({v for _, v in groups})
    print("%-14s " % "config" + " ".join("%-34s" % v for v in views))
    for config in order:
        cells = []
        for view in views:
            frames = groups.get((config, view))
            if not frames or len(frames) < 2:
                cells.append("%-34s" % "-")
                continue
            f, v, s = score(frames, box)
            cells.append("%-34s" % ("flicker %5.2f  >8 %5.2f%%  sharp %5.1f" % (f, v, s)))
        print("%-14s " % config + " ".join(cells))
    return 0


if __name__ == "__main__":
    sys.exit(main())
