"""Ícone da Pedra de retorno (imagens/pedra-retorno.png).

O brilho em volta da pedra é semitransparente: o xadrez aparece tingido de azul através
dele. Usa a mesma limpeza do portal (extract_portal.py): fundo pela posição dos quadrados e
quadradinhos tingidos alternando claro/escuro, na imagem grande, antes de reduzir à grade.
Saída: Assets/Resources/Items/pedra_retorno.png.

Uso:  python3 Tools/extract_pedra_retorno.py
"""
import sys
import numpy as np
from PIL import Image
sys.path.insert(0, "Tools")
import extract_pixelart as EP
import extract_janelas as J
import extract_portal as P

SRC = "imagens/pedra-retorno.png"
OUT = "Assets/Resources/Items/pedra_retorno.png"


def main():
    a = np.array(Image.open(SRC).convert("RGB")).astype(float)
    bg, _ = J.background(a)
    bg |= P.tinted_squares(a, bg)
    fg = J.drop_specks(~bg, 12)
    pitch = EP.detect_pitch(a)
    gw = int(round(a.shape[1] / pitch))
    grid = EP.resample(a, gw)
    mask = EP.resample(np.dstack([fg * 255.0] * 3), gw)[:, :, 0] > 127
    # Fora do contorno escuro da pedra, o que tem pouca cor é o fundo visto pelo brilho:
    # sai, a partir da borda. Pedra (dentro do contorno), anéis ciano e faíscas ficam.
    sat = grid.max(2) - grid.min(2)
    lowsat = mask & (sat < 75)
    h, w = mask.shape
    seeds = [(y, x) for y, x in zip(*np.where(lowsat))
             if y in (0, h - 1) or x in (0, w - 1) or not mask[max(0, y - 1):y + 2, max(0, x - 1):x + 2].all()]
    mask &= ~J.flood(lowsat, seeds)
    mask = J.drop_specks(mask, 3)
    ys, xs = np.where(mask)
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
    rgb = grid[y0:y1 + 1, x0:x1 + 1].astype(int)
    alpha = np.where(mask[y0:y1 + 1, x0:x1 + 1], 255, 0).astype(np.uint8)
    snapped, ncols = EP.quantize(rgb, alpha, 16)
    out = np.dstack([snapped, alpha]).astype(np.uint8)
    Image.fromarray(out, "RGBA").save(OUT)
    print(f"  {OUT}: {out.shape[1]}x{out.shape[0]}, {ncols} cores (grade {gw})")


if __name__ == "__main__":
    main()
