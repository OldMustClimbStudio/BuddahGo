"""Labelled stills + 3-panel orbit GIF for the frost-lock ice swatches."""
import os, sys
from PIL import Image, ImageDraw, ImageFont
D = sys.argv[1]
F = os.path.join(D, 'frost_frames_v4')
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 30)
small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 22)
NAMES = {1: 'v1.5（已定，偏糊）', 2: 'v4（晶面分明暗、去扭曲、硬裂纹）', 3: 'v4 + 白色棱线'}

def tag(draw, xy, text, f=font):
    x, y = xy
    l, t, r, b = draw.textbbox((x, y), text, font=f)
    draw.rectangle((l - 8, t - 6, r + 8, b + 6), fill=(0, 0, 0))
    draw.text((x, y), text, fill=(255, 255, 255), font=f)

# stills: gameplay (left->right = 1,2,3), near (left->right = 3,2,1)
for name, order, xs, title in (
        ('gameplay', (1, 2, 3), (560, 980, 1330), '比赛视角（游戏摄像机，距离约 97 m，FOV 40）· RaceMap 起跑区 · 黄昏 · High Fidelity'),
        ('near', (3, 2, 1), (150, 760, 1300), '近景（约 55 m，FOV 35）· 同一位置，三个并排相隔 16 m')):
    im = Image.open(os.path.join(F, name + '.png')).convert('RGB')
    d = ImageDraw.Draw(im)
    tag(d, (20, 20), title, small)
    y = 330 if name == 'gameplay' else 270
    for idx, x in zip(order, xs):
        tag(d, (x, y), NAMES[idx].split('（')[0], font)
    out = os.path.join(D, 'frostlock_v4_%s.jpg' % name)
    im.save(out, quality=88)
    print(out)

# orbit GIF: three 640 panels side by side, labelled, downscaled to 1440 wide
frames = []
n = len([f for f in os.listdir(F) if f.startswith('orbit_1_')])
for i in range(0, n, 2):
    canvas = Image.new('RGB', (2160, 780), (16, 16, 16))
    d = ImageDraw.Draw(canvas)
    for s in (1, 2, 3):
        p = Image.open(os.path.join(F, 'orbit_%d_%02d.png' % (s, i))).convert('RGB')
        canvas.paste(p, ((s - 1) * 720, 60))
        tag(d, ((s - 1) * 720 + 12, 12), NAMES[s], small)
    tag(d, (2160 - 230, 740), '环绕 %3d°' % (i * 360 // n), small)
    frames.append(canvas.resize((1350, 488), Image.LANCZOS).convert('P', palette=Image.ADAPTIVE, colors=128))
out = os.path.join(D, 'frostlock_v4_orbit.gif')
frames[0].save(out, save_all=True, append_images=frames[1:], duration=160, loop=0, optimize=True)
print(out, os.path.getsize(out) // 1024, 'KB')
