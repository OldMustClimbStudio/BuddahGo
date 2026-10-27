#!/usr/bin/env python3
"""Builds the anti-aliasing evidence for RaceMap from local captures of RaceMapTemporalNoise.

    python -I Tools/rendering/build_aa_evidence.py

Inputs (local, git-ignored PNG folders under Docs/rendering/racemap/):
  aa_bal/, aa_dev/   slow sub-pixel sweeps (1/16 px per frame): current branch and a scratch copy of dev 5f91e51's scene
  aa_motion/         race-speed sweeps (6 px per frame at the player's distance), identical camera path per config
  clip_*/            autopilot Solo laps recorded from the gameplay camera (rows bottom-up, flipped here)
Outputs:
  Docs/rendering/racemap/aa_fix/flicker_heatmaps.jpg   frame crop above its flicker map (brightest change over the sweep, x6)
  Docs/rendering/racemap/aa_fix/motion_frames.jpg      four consecutive race-speed frames per config, 1:1 crops
  Logs/rendering/aa_clips/*.webp                       animated versions (local only: .webp is not under Git LFS)
Needs Pillow.
"""
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageOps

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "Docs" / "rendering" / "racemap"
OUT = BASE / "aa_fix"
LOCAL = ROOT / "Logs" / "rendering" / "aa_clips"
GOLD = (240, 200, 90)
BAR = 26
SAND = (1100, 300, 1740, 660)  # view 01: sunlit sand ripples, curb and lane edge


def frames(folder, config, view, count):
    return [Image.open(BASE / folder / f"{config}__{view}__f{i:02d}.png").convert("RGB") for i in range(count)]


def flicker_map(seq):
    acc = None
    for a, b in zip(seq, seq[1:]):
        d = ImageChops.difference(a, b).convert("L")
        acc = d if acc is None else ImageChops.lighter(acc, d)
    return acc.point(lambda v: min(255, v * 6)).convert("RGB")


def labelled_grid(cells, cols, w, h):
    rows = (len(cells) + cols - 1) // cols
    canvas = Image.new("RGB", (cols * w, rows * (h + BAR)), (14, 14, 14))
    draw = ImageDraw.Draw(canvas)
    for i, (img, title) in enumerate(cells):
        x, y = (i % cols) * w, (i // cols) * (h + BAR)
        canvas.paste(img.resize((w, h), Image.LANCZOS) if img.size != (w, h) else img, (x, y + BAR))
        draw.text((x + 8, y + 7), title, fill=GOLD)
    return canvas


def heatmaps():
    columns = [
        ("aa_dev", "dev_hf_msaa_smaa", "dev 5f91e51, High Fidelity: 8x MSAA + SMAA"),
        ("aa_bal", "hf_msaa_smaa", "before, High Fidelity: 8x MSAA + SMAA"),
        ("aa_bal", "hf_msaa_only", "after, High Fidelity: 8x MSAA"),
        ("aa_bal", "bal_smaa_high", "before, Balanced: SMAA"),
        ("aa_bal", "bal_taa", "after, Balanced: TAA"),
    ]
    cells = []
    for folder, config, title in columns:
        seq = frames(folder, config, "01_start_grid", 16)
        cells.append((seq[0].crop(SAND), title))
    for folder, config, title in columns:
        seq = frames(folder, config, "01_start_grid", 16)
        cells.append((flicker_map(seq).crop(SAND), "flicker x6 | " + title))
    labelled_grid(cells, len(columns), 640, 360).save(OUT / "flicker_heatmaps.jpg", quality=90, optimize=True)


def motion():
    configs = [("hf_current", "before, High Fidelity"), ("hf_fixed", "after, High Fidelity"),
               ("bal_current", "before, Balanced"), ("bal_taa", "after, Balanced")]
    box = (1100, 380, 1580, 650)
    cells = []
    for config, title in configs:
        seq = frames("aa_motion", config, "01_start_grid", 30)
        for i in range(12, 16):
            cells.append((seq[i].crop(box), f"{title} | frame {i}"))
    labelled_grid(cells, 4, 480, 270).save(OUT / "motion_frames.jpg", quality=92, optimize=True)


def animate(path, columns, count, crop=None, flip=False):
    """columns: list of (title, [frames]); writes a side-by-side animated WebP at 30 fps (slower than the game)."""
    out = []
    w, h = (crop[2] - crop[0], crop[3] - crop[1]) if crop else columns[0][1][0].size
    for i in range(count):
        canvas = Image.new("RGB", (w * len(columns), h + BAR), (14, 14, 14))
        draw = ImageDraw.Draw(canvas)
        for j, (title, seq) in enumerate(columns):
            img = seq[i]
            if flip:
                img = ImageOps.flip(img)
            if crop:
                img = img.crop(crop)
            canvas.paste(img, (j * w, BAR))
            draw.text((j * w + 8, 7), title, fill=GOLD)
        out.append(canvas)
    out[0].save(path, save_all=True, append_images=out[1:], duration=33, loop=0, quality=90, method=4)


def local_clips():
    LOCAL.mkdir(parents=True, exist_ok=True)
    crop = (1100, 300, 1740, 660)
    animate(LOCAL / "motion_high_fidelity.webp",
            [("before: 8x MSAA + SMAA", frames("aa_motion", "hf_current", "01_start_grid", 30)),
             ("after: 8x MSAA", frames("aa_motion", "hf_fixed", "01_start_grid", 30))], 30, crop)
    animate(LOCAL / "motion_balanced.webp",
            [("before: SMAA", frames("aa_motion", "bal_current", "01_start_grid", 30)),
             ("after: TAA", frames("aa_motion", "bal_taa", "01_start_grid", 30)),
             ("option: FXAA", frames("aa_motion", "bal_fxaa", "01_start_grid", 30))], 30, crop)
    for name in ("clip_hf_msaa_smaa", "clip_hf_msaa_only", "clip_bal_smaa", "clip_bal_taa", "clip_bal_fxaa"):
        seq = [Image.open(p).convert("RGB") for p in sorted((BASE / name).glob("f*.png"))]
        if seq:
            animate(LOCAL / (name + ".webp"), [(name.replace("clip_", "race lap: "), seq)], len(seq), flip=True)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    heatmaps()
    motion()
    local_clips()
    print("wrote", OUT.relative_to(ROOT), "and", LOCAL.relative_to(ROOT))


if __name__ == "__main__":
    main()
