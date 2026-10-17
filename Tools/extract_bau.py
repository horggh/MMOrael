"""Sprites do baú: fechado e aberto (vista de frente, no mapa) e o ícone na diagonal.

Usa o mesmo processo do extract_pixelart.py (grade nativa, xadrez vira transparência).
Os dois baús de frente têm a mesma largura e a mesma base, então trocam um pelo outro
sem o baú "pular" quando abre.

Uso:  python3 Tools/extract_bau.py
"""
import os
import sys
import numpy as np
from PIL import Image
sys.path.insert(0, "Tools")
import extract_pixelart as EP

FRONT = "imagens/bau-frente.jpeg"       # fechado à esquerda, aberto à direita
DIAGONAL = "imagens/bau-diagonal.jpeg"  # ícone do inventário
FRONT_GRID = 448
DIAGONAL_GRID = 360


def extract(src, grid_w):
    """Devolve as peças da imagem, da esquerda para a direita, já recortadas."""
    a = np.array(Image.open(src).convert("RGB")).astype(float)
    grid = EP.resample(a, grid_w)
    bg = EP.background_mask(grid)
    fg = ~bg
    out = []
    for x0, x1 in EP.split_columns(fg):
        sub = fg[:, x0:x1 + 1]
        ys = np.where(sub.any(1))[0]
        xs = np.where(sub.any(0))[0]
        y0, y1 = ys.min(), ys.max()
        rgb = grid[y0:y1 + 1, x0 + xs.min():x0 + xs.max() + 1].astype(int)
        alpha = np.where(bg[y0:y1 + 1, x0 + xs.min():x0 + xs.max() + 1], 0, 255).astype(np.uint8)
        snapped, _ = EP.quantize(rgb, alpha)
        out.append(np.dstack([snapped, alpha]).astype(np.uint8))
    return out


def save(img, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray(img, "RGBA").save(path)
    print(f"  {path}: {img.shape[1]}x{img.shape[0]}")


def main():
    closed, opened = extract(FRONT, FRONT_GRID)
    save(closed, "Assets/Resources/Props/bau_fechado.png")
    save(opened, "Assets/Resources/Props/bau_aberto.png")
    icon, = extract(DIAGONAL, DIAGONAL_GRID)
    save(icon, "Assets/Resources/Items/bau.png")


if __name__ == "__main__":
    main()
