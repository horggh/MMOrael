"""Ícone do Martelo de Guerra do GM (imagens/martelo_gm.jpg).

Como o martelo de construção (extract_martelo.py), o fundo xadrez sai pela posição dos
quadrados. A diferença é o brilho azul em volta da cabeça: ele é semitransparente, então
na imagem aparece misturado ao xadrez (quadradinhos azulados). Aqui cada pixel do brilho
é "desmisturado": sabendo o cinza do xadrez naquele ponto e a cor do brilho, sai quanto
de brilho há (alfa) e o pixel vira azul translúcido, sem o xadrez por trás.
Saída: Assets/Resources/Items/martelo_gm.png.

Uso:  python Tools/extract_martelo_gm.py
"""
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, "Tools")
import extract_pixelart as EP  # noqa: E402
import extract_janelas as J    # noqa: E402

SRC = "imagens/martelo_gm.jpg"
OUT = "Assets/Resources/Items/martelo_gm.png"
GLOW = np.array([90.0, 225.0, 255.0])   # azul do brilho
MIN_PART = 40                           # pedaços menores (ruído do JPEG) saem


def main():
    a = np.array(Image.open(SRC).convert("RGB")).astype(float)
    bg, _ = J.background(a)
    p, ox, oy, lo, hi = J.checker_fit(a)
    H, W, _ = a.shape
    ys, xs = np.mgrid[0:H, 0:W]
    expected = np.where((np.floor((xs + ox) / p) + np.floor((ys + oy) / p)) % 2 == 0, lo, hi)

    # Partes da arte: tudo que não é fundo, menos os grãozinhos soltos.
    fg = ~bg
    keep, seen = np.zeros_like(fg), np.zeros_like(fg)
    for y, x in zip(*np.where(fg)):
        if seen[y, x]:
            continue
        comp = J.flood(fg & ~seen, [(y, x)])
        seen |= comp
        if comp.sum() >= MIN_PART:
            keep |= comp

    # Desmistura o brilho: pixel = xadrez × (1 − a) + brilho × a, canal a canal.
    e = expected[:, :, None]
    den = GLOW[None, None, :] - e
    per = (a - e) / np.where(np.abs(den) < 1, 1, den)
    alpha_est = per.mean(2)
    spread = per.max(2) - per.min(2)
    glow = keep & (alpha_est > 0.02) & (alpha_est < 0.8) & (spread < 0.35)
    alpha = np.where(keep, 1.0, 0.0)
    alpha[glow] = np.clip(alpha_est[glow], 0.05, 1.0)
    rgb = a.copy()
    rgb[glow] = GLOW

    # Grade nativa (pré-multiplicado, para a borda do brilho não escurecer).
    pitch = EP.detect_pitch(a)
    gw = int(round(W / pitch))
    print(f"  grade {gw} (passo {pitch:.2f}px), brilho em {int(glow.sum())} px")
    pre = EP.resample(rgb * alpha[:, :, None], gw).astype(float)
    al = EP.resample(np.dstack([alpha * 255.0] * 3), gw)[:, :, 0].astype(float) / 255.0
    col = np.where(al[:, :, None] > 0.01, pre / np.maximum(al[:, :, None], 0.01), 0)
    al[al < 0.08] = 0

    ys, xs = np.where(al > 0)
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
    col = np.clip(col[y0:y1 + 1, x0:x1 + 1], 0, 255).astype(int)
    a8 = (al[y0:y1 + 1, x0:x1 + 1] * 255).round().astype(np.uint8)
    snapped, ncols = EP.quantize(col, a8, 18)
    out = np.dstack([snapped, a8]).astype(np.uint8)
    Image.fromarray(out, "RGBA").save(OUT)
    print(f"  {OUT}: {out.shape[1]}x{out.shape[0]}, {ncols} cores")


if __name__ == "__main__":
    main()
