#!/usr/bin/env python3
"""Per-view difference statistics between two RaceMap capture folders, plus an optional labelled sheet.

    python -P Tools/rendering/capture_diff.py <before_label> <after_label> [--sheet <out_name>] [--gain 16]

(-P, not -I: -I also hides the user site-packages where numpy may live.)

Reads Docs/rendering/racemap/<label>/NN_<view>.png (RaceMapDuskColorStudy or RaceMapViewpointCapture) and prints, per
view present in both: mean absolute difference in 8-bit levels (over RGB), the 99th and 99.9th percentile of the
per-pixel maximum channel difference, the share of pixels differing by more than 2 and 8 levels, and the maximum.
With --sheet it writes Docs/rendering/racemap/<out_name>/all.jpg: one row per view, before | after | |difference| x gain.
Used to judge whether a rebake reproduces the committed one (Docs/rendering/ARCHITECTURE.md, Bake reproducibility).
Needs Pillow and numpy.
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "Docs" / "rendering" / "racemap"
CELL_W, CELL_H, BAR = 640, 360, 30


def load(path):
    with Image.open(path) as im:
        return np.asarray(im.convert("RGB"), dtype=np.int16)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("before")
    ap.add_argument("after")
    ap.add_argument("--sheet")
    ap.add_argument("--gain", type=float, default=16.0)
    args = ap.parse_args()

    before_dir, after_dir = BASE / args.before, BASE / args.after
    views = sorted(p.name for p in before_dir.glob("[0-9][0-9]_*.png") if (after_dir / p.name).exists())
    if not views:
        raise SystemExit(f"no common NN_*.png views in {before_dir} and {after_dir}")

    rows = []
    print(f"{'view':<22}{'mean':>7}{'p99':>6}{'p99.9':>7}{'>2':>9}{'>8':>9}{'max':>5}")
    for name in views:
        a, b = load(before_dir / name), load(after_dir / name)
        d = np.abs(a - b)
        dmax = d.max(axis=2)
        stats = (d.mean(), np.percentile(dmax, 99), np.percentile(dmax, 99.9),
                 (dmax > 2).mean() * 100, (dmax > 8).mean() * 100, int(dmax.max()))
        print(f"{name[:-4]:<22}{stats[0]:>7.2f}{stats[1]:>6.0f}{stats[2]:>7.0f}{stats[3]:>8.2f}%{stats[4]:>8.2f}%{stats[5]:>5}")
        rows.append((name[:-4], a, b, d, stats))

    if args.sheet:
        sheet = Image.new("RGB", (CELL_W * 3, (CELL_H + BAR) * len(rows)), (14, 14, 14))
        draw = ImageDraw.Draw(sheet)
        for r, (view, a, b, d, s) in enumerate(rows):
            y = r * (CELL_H + BAR)
            diff = np.clip(d * args.gain, 0, 255).astype(np.uint8)
            cells = ((a.astype(np.uint8), f"{args.before}"), (b.astype(np.uint8), f"{args.after}"),
                     (diff, f"|diff| x{args.gain:g}  mean {s[0]:.2f}  >8: {s[4]:.2f}%  max {s[5]}"))
            for c, (img, title) in enumerate(cells):
                sheet.paste(Image.fromarray(img).resize((CELL_W, CELL_H), Image.LANCZOS), (c * CELL_W, y + BAR))
                draw.text((c * CELL_W + 10, y + 9), f"{view}  |  {title}", fill=(240, 200, 90))
        out = BASE / args.sheet
        out.mkdir(parents=True, exist_ok=True)
        sheet.save(out / "all.jpg", quality=88)
        print(f"wrote {out / 'all.jpg'}")


if __name__ == "__main__":
    main()
