"""Bake the sutra ring strip T_FX_Sutra_Glyph.png from real text (Docs/vfx/README.md section 7).

The opening of the Heart Sutra (心經, public domain) set in Noto Serif SC (SIL Open Font License 1.1), one character per
square cell, white on transparent black, tileable along U. FX/Additive_Glow wraps it around the charge ring in polar
mode, so the strip's left edge is the start of the ring and characters stand upright toward the ring's outside.

Usage: python Tools/vfx/bake_sutra.py [font path]
The default font path is the Windows 11 copy of Noto Serif SC; any Noto Serif CJK SC/TC file works.
Requires Pillow.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "VFX", "Shared", "Textures", "T_FX_Sutra_Glyph.png")
DEFAULT_FONT = r"C:\Windows\Fonts\NotoSerifSC-VF.ttf"

# 32 cells: the ring repeats the strip, so the text must read as one phrase per lap.
TEXT = "觀自在菩薩行深般若波羅蜜多時照見五蘊皆空度一切苦厄舍利子色不異空空不異色"[:32]
CELL = 128
INK_SCALE = 0.82  # glyph height as a fraction of the cell
WEIGHT = "SemiBold"


def main():
    font_path = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_FONT
    font = ImageFont.truetype(font_path, int(CELL * INK_SCALE))
    try:
        font.set_variation_by_name(WEIGHT)
    except (OSError, ValueError):
        pass  # static font file: use its own weight

    # Supersample, then downscale, for clean edges without a grey halo wider than a pixel.
    ss = 4
    strip = Image.new("L", (CELL * len(TEXT) * ss, CELL * ss), 0)
    draw = ImageDraw.Draw(strip)
    big = ImageFont.truetype(font_path, int(CELL * INK_SCALE * ss))
    try:
        big.set_variation_by_name(WEIGHT)
    except (OSError, ValueError):
        pass
    missing = []
    for i, ch in enumerate(TEXT):
        if font.getmask(ch).getbbox() is None:
            missing.append(ch)
        cx = (i + 0.5) * CELL * ss
        cy = 0.5 * CELL * ss
        draw.text((cx, cy), ch, fill=255, font=big, anchor="mm")
    if missing:
        raise SystemExit("font has no glyph for: " + "".join(missing))

    mask = strip.resize((CELL * len(TEXT), CELL), Image.LANCZOS)
    rgba = Image.merge("RGBA", (mask, mask, mask, mask))
    rgba.save(OUT)
    print("wrote", OUT, rgba.size, "text:", TEXT)


if __name__ == "__main__":
    main()
