"""Cut the brush masks T_FX_Brush_Stroke.png and T_FX_Enso_Mask.png from a real calligraphy scan
(Docs/vfx/README.md section 7: shape textures come from real brushwork, not procedural noise).

Source: the 一 of Yueshan Daozong (Chinese, 1629-1709), "Calligraphy in Semi-Cursive Style", Cleveland Museum of Art
2003.354, Open Access, CC0 (https://www.clevelandart.org/art/2003.354). Download the print image
https://openaccess-cdn.clevelandart.org/2003.354/2003.354_print.jpg (788 x 3400) and pass its path.

- T_FX_Brush_Stroke.png (1024 x 256): the stroke alone, head on the left (u = 0), lying along U; A = shape,
  R = position along the stroke (here simply u). The gust and the trail reveal it from the head toward the tail.
- T_FX_Enso_Mask.png (512 x 512): the same stroke bent along an open circle (ensō), head at the top going clockwise,
  for the charge ink circle under the caster.

Masks keep the brush's own antialiased edge; materials cut them at 0.5 (alpha cutout), so the ink is solid and
hard-edged with the real dry-brush breaks. Usage: python Tools/vfx/cut_brush.py <scan path>. Requires Pillow.
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageOps

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "VFX", "Shared", "Textures")

CROP = (200, 1420, 600, 1620)  # the 一 in the print image
SEED = (380, 1520)             # a point inside the stroke (print-image pixels)
PAPER, INK = 70, 200           # inverted-luminance levels mapped to 0 and 255

STROKE_SIZE = (1024, 256)
ENSO_SIZE = 512
ENSO_RADIUS = 0.36             # fraction of the texture size
ENSO_THICKNESS = 0.24          # stroke height mapped onto the ring, fraction of the texture size
ENSO_START_DEG = 100.0         # head position, degrees counter-clockwise from +X
ENSO_SPAN_DEG = 320.0          # clockwise arc length; the gap is the remainder


def ink_levels(scan):
    gray = ImageOps.invert(scan.convert("L"))
    return gray.point(lambda v: 0 if v <= PAPER else 255 if v >= INK else int((v - PAPER) * 255 / (INK - PAPER)))


def isolate(levels, seed):
    """Keeps only the connected stroke under the seed (neighbouring characters touch the crop)."""
    binary = levels.point(lambda v: 255 if v >= 128 else 0)
    ImageDraw.floodfill(binary, seed, 128)
    component = binary.point(lambda v: 255 if v == 128 else 0).filter(ImageFilter.MaxFilter(5))
    return Image.composite(levels, Image.new("L", levels.size, 0), component)


def deskew(mask):
    """Rotates the stroke so its long axis lies along U (principal axis of the ink mass)."""
    w, h = mask.size
    data = mask.load()
    total = sx = sy = 0.0
    for y in range(h):
        for x in range(w):
            m = data[x, y]
            if m:
                total += m
                sx += m * x
                sy += m * y
    cx, cy = sx / total, sy / total
    sxx = syy = sxy = 0.0
    for y in range(h):
        for x in range(w):
            m = data[x, y]
            if m:
                dx, dy = x - cx, y - cy
                sxx += m * dx * dx
                syy += m * dy * dy
                sxy += m * dx * dy
    angle = 0.5 * math.degrees(math.atan2(2 * sxy, sxx - syy))
    return mask.rotate(angle, resample=Image.BICUBIC, expand=True)


def fit(mask, size, pad=0.03):
    """Trims to the ink and stretches it to fill the texture (the stroke is stretched along U by the effect anyway)."""
    mask = mask.crop(mask.getbbox())
    w, h = size
    inner = (int(w * (1 - 2 * pad)), int(h * (1 - 2 * pad)))
    mask = mask.resize(inner, Image.LANCZOS)
    out = Image.new("L", size, 0)
    out.paste(mask, ((w - inner[0]) // 2, (h - inner[1]) // 2))
    return out


def enso(stroke):
    s = ENSO_SIZE
    src = stroke.load()
    sw, sh = stroke.size
    out = Image.new("L", (s, s), 0)
    dst = out.load()
    along_out = Image.new("L", (s, s), 0)
    along_dst = along_out.load()
    radius = ENSO_RADIUS * s
    thickness = ENSO_THICKNESS * s
    for y in range(s):
        for x in range(s):
            dx, dy = x + 0.5 - s / 2, s / 2 - (y + 0.5)
            angle = math.degrees(math.atan2(dy, dx))
            along = (ENSO_START_DEG - angle) % 360.0  # clockwise from the head
            if along > ENSO_SPAN_DEG:
                continue
            u = along / ENSO_SPAN_DEG
            v = (math.hypot(dx, dy) - radius) / thickness + 0.5
            if not 0.0 <= v < 1.0:
                continue
            dst[x, y] = src[min(sw - 1, int(u * sw)), min(sh - 1, int(v * sh))]
            along_dst[x, y] = int(round(u * 255))
    return out, along_out


def save(mask, along, name):
    """A = shape, R = position along the stroke (FXInk.hlsl draws and erases by it), G = B = shape for previews."""
    path = os.path.join(OUT_DIR, name)
    Image.merge("RGBA", (along, mask, mask, mask)).save(path)
    print("wrote", path, mask.size)


def along_u(size):
    w, h = size
    row = Image.linear_gradient("L").rotate(90, expand=True).resize((w, h))  # 0 at the left (head), 255 at the right
    return row


def main():
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    scan = Image.open(sys.argv[1])
    levels = ink_levels(scan.crop(CROP))
    stroke = isolate(levels, (SEED[0] - CROP[0], SEED[1] - CROP[1]))
    stroke = fit(deskew(stroke), STROKE_SIZE)
    save(stroke, along_u(stroke.size), "T_FX_Brush_Stroke.png")
    ring, along = enso(stroke)
    save(ring, along, "T_FX_Enso_Mask.png")


if __name__ == "__main__":
    main()
