"""Recorta as 20 bestas de imagens/bestas.webp (5 x 4, fundo escuro, número embaixo) em
Assets/Resources/Items/besta_01..20.png, do tamanho dos outros ícones (até 64px).

Uso: python Tools/extract_bestas.py
"""
import numpy as np
from PIL import Image
import os
import sys
sys.path.insert(0, os.path.dirname(__file__))
from extract_boss_pirata import components  # noqa: E402

SRC = "imagens/bestas.webp"
OUT = "Assets/Resources/Items"
ROWS = [(30, 250), (310, 540), (590, 835), (875, 1150)]   # sem os números
COLS = [(15, 255), (255, 495), (495, 745), (745, 995), (995, 1245)]
ICON = 64

a = np.array(Image.open(SRC).convert("RGB")).astype(int)
for r, (y0, y1) in enumerate(ROWS):
    for c, (x0, x1) in enumerate(COLS):
        cell = a[y0:y1, x0:x1]
        fg = cell.max(2) > 40                      # fundo quase preto
        comps = components(fg)
        big = max(len(cy) for cy, _ in comps)
        keep = np.zeros_like(fg)
        for cy, cx in comps:
            if len(cy) >= 0.02 * big:
                keep[cy, cx] = True
        ys, xs = np.where(keep)
        crop = cell[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
        m = keep[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
        im = Image.fromarray(np.dstack([crop, m * 255]).astype(np.uint8), "RGBA")
        s = ICON / max(im.size)
        im = im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)
        al = np.array(im)[:, :, 3]
        arr = np.array(im); arr[:, :, 3] = np.where(al >= 110, 255, 0)
        n_ = r * 5 + c + 1
        Image.fromarray(arr).save(f"{OUT}/besta_{n_:02d}.png")
print("ok")
