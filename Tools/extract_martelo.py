"""Ícone do Martelo de construção (imagens/martelo.jpeg).

O martelo é de metal cinza sobre o xadrez cinza: o fundo sai pela posição dos quadrados
(remoção das janelas, extract_janelas.py), que não apaga o cinza da arte. Depois a imagem é
reduzida para a grade nativa e só o maior bloco (o martelo) fica.
Saída: Assets/Resources/Items/martelo.png.

Uso:  python3 Tools/extract_martelo.py
"""
import sys
import numpy as np
from PIL import Image
sys.path.insert(0, "Tools")
import extract_pixelart as EP
import extract_janelas as J

SRC = "imagens/martelo.jpeg"
OUT = "Assets/Resources/Items/martelo.png"


def main():
    a = np.array(Image.open(SRC).convert("RGB")).astype(float)
    bg, _ = J.background(a)
    fg = ~bg
    # Só o maior bloco: tira manchas soltas do fundo.
    best, seen = None, np.zeros_like(fg)
    for y, x in zip(*np.where(fg)):
        if seen[y, x]:
            continue
        comp = J.flood(fg & ~seen, [(y, x)])
        seen |= comp
        if best is None or comp.sum() > best.sum():
            best = comp
    pitch = EP.detect_pitch(a)
    gw = int(round(a.shape[1] / pitch))
    print(f"  grade {gw} (passo {pitch:.2f}px)")
    grid = EP.resample(a, gw)
    mask = EP.resample(np.dstack([best * 255.0] * 3), gw)[:, :, 0] > 127
    # Furinhos fechados dentro do desenho (metal no mesmo tom do fundo) voltam a ser arte.
    h, w = mask.shape
    border = [(y, x) for y in range(h) for x in (0, w - 1)] + [(y, x) for x in range(w) for y in (0, h - 1)]
    holes = ~mask & ~J.flood(~mask, border)
    seen = np.zeros_like(holes)
    for y, x in zip(*np.where(holes)):
        if seen[y, x]:
            continue
        comp = J.flood(holes & ~seen, [(y, x)])
        seen |= comp
        if comp.sum() <= 30:
            mask |= comp
    ys, xs = np.where(mask)
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
    rgb = grid[y0:y1 + 1, x0:x1 + 1].astype(int)
    alpha = np.where(mask[y0:y1 + 1, x0:x1 + 1], 255, 0).astype(np.uint8)
    snapped, ncols = EP.quantize(rgb, alpha, 20)
    out = np.dstack([snapped, alpha]).astype(np.uint8)
    Image.fromarray(out, "RGBA").save(OUT)
    print(f"  {OUT}: {out.shape[1]}x{out.shape[0]}, {ncols} cores")


if __name__ == "__main__":
    main()
