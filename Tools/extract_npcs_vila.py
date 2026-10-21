"""Recorta os 12 NPCs de imagens/npcs_vila.webp (2 tabelas de 6 linhas x 4 direções, fundo
cinza-escuro com sombra embaixo) em Assets/Art/Sprites/NPCs/Vila/<npc>_<direção>.png, na
altura dos outros NPCs (~53 px a 32 PPU, a altura do personagem).

Uso: python Tools/extract_npcs_vila.py
"""
import os
import numpy as np
from PIL import Image

SRC = "imagens/npcs_vila.webp"
OUT = "Assets/Art/Sprites/NPCs/Vila"
SCALE = 53 / 140          # personagem adulto da folha ~140 px
ROWS = [(42, 203), (207, 371), (375, 539), (543, 708), (712, 880), (884, 1054)]
DIRS = ["frente", "esquerda", "direita", "costas"]
LEFT_COLS = [(162, 297), (301, 432), (436, 570), (574, 714)]
RIGHT_COLS = [(888, 1021), (1025, 1157), (1161, 1297), (1301, 1441)]
LEFT = ["mercador", "ferreiro", "lojista", "curandeira", "guarda", "fazendeiro"]
RIGHT = ["padeira", "pescador", "idosa", "idoso", "moca", "rapaz"]


def border_flood(bg):
    m = np.zeros_like(bg)
    m[0, :], m[-1, :], m[:, 0], m[:, -1] = bg[0, :], bg[-1, :], bg[:, 0], bg[:, -1]
    while True:
        g = m.copy()
        g[1:] |= m[:-1]; g[:-1] |= m[1:]; g[:, 1:] |= m[:, :-1]; g[:, :-1] |= m[:, 1:]
        g &= bg
        if (g == m).all():
            return m
        m = g


def cut(a, x0, x1, y0, y1):
    c = a[y0:y1, x0:x1]
    # Fundo e sombra: cinza-escuro neutro, ligado à borda.
    bg = ((c.max(2) - c.min(2)) <= 9) & (c.max(2) <= 52)
    keep = ~border_flood(bg)
    # Sobras pequenas soltas (pontinhos da sombra).
    ys, xs = np.where(keep)
    c, keep = c[ys.min():ys.max() + 1, xs.min():xs.max() + 1], keep[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    im = np.dstack([c, keep * 255]).astype(np.uint8)
    h, w = keep.shape
    size = (max(1, round(w * SCALE)), max(1, round(h * SCALE)))
    al = keep.astype(float)[:, :, None]
    pre = Image.fromarray((c * al).astype(np.uint8)).resize(size, Image.LANCZOS)
    alr = np.array(Image.fromarray((keep * 255).astype(np.uint8)).resize(size, Image.LANCZOS)).astype(float)
    rgb = np.clip(np.array(pre).astype(float) * 255 / np.maximum(alr, 1)[:, :, None], 0, 255)
    return Image.fromarray(np.dstack([rgb, np.where(alr >= 128, 255, 0)]).astype(np.uint8), "RGBA")


def main():
    os.makedirs(OUT, exist_ok=True)
    a = np.array(Image.open(SRC).convert("RGB")).astype(int)
    for names, cols in ((LEFT, LEFT_COLS), (RIGHT, RIGHT_COLS)):
        for name, (y0, y1) in zip(names, ROWS):
            for d, (x0, x1) in zip(DIRS, cols):
                cut(a, x0, x1, y0, y1).save(f"{OUT}/{name}_{d}.png")
    print("ok")


if __name__ == "__main__":
    main()
