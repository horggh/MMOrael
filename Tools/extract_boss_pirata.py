"""Separa cada quadro de ação do boss pirata (Drake) da folha imagens/boss-pirata.jpg.

A folha tem 3 linhas x 5 colunas, fundo xadrez:
  linha 1: frente, esquerda, costas-direita, costas-esquerda, costas   (direções)
  linha 2: parado, golpe 1, golpe 2, tiro p/ direita, tiro p/ esquerda
  linha 3: dor 1 (chapéu voando), dor 2 (ajoelhado), morte 1, morte 2, morte 3

Cada quadro sai num quadro de tamanho único, com os pés no mesmo ponto (centro da
coluna, base da linha), para a animação não pular. Saída: Assets/Art/Sprites/Monsters/MVP/Resources/Drake/.

Uso: python Tools/extract_boss_pirata.py   (na raiz do projeto)
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_pixelart import resample, background_mask, drop_specks, quantize  # noqa: E402

SRC = "imagens/boss-pirata.jpg"
OUT = "Assets/Art/Sprites/Monsters/MVP/Resources/Drake"
GRID = 512          # a arte tem pixel de 2 px na imagem de 1024
COLS = 5
NAMES = [
    ["frente", "esquerda", "costas_direita", "costas_esquerda", "costas"],
    ["parado", "golpe_1", "golpe_2", "tiro_direita", "tiro_esquerda"],
    ["dor_1", "dor_2", "morte_1", "morte_2", "morte_3"],
]


def components(fg):
    """Grupos de pixels ligados (8 vizinhos): lista de (ys, xs)."""
    H, W = fg.shape
    seen = np.zeros_like(fg)
    out = []
    for y, x in zip(*np.where(fg)):
        if seen[y, x]:
            continue
        ys, xs, stack = [], [], [(y, x)]
        seen[y, x] = True
        while stack:
            cy, cx = stack.pop()
            ys.append(cy)
            xs.append(cx)
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    ny, nx = cy + dy, cx + dx
                    if 0 <= ny < H and 0 <= nx < W and fg[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
        out.append((np.array(ys), np.array(xs)))
    return out


def row_bands(fg, min_gap=3):
    """Faixas de linhas com desenho, separadas por linhas vazias."""
    filled = fg.any(1)
    bands, start, gap = [], None, 0
    for y, f in enumerate(filled):
        if f:
            if start is None:
                start = y
            gap = 0
        elif start is not None:
            gap += 1
            if gap >= min_gap:
                bands.append((start, y - gap))
                start, gap = None, 0
    if start is not None:
        bands.append((start, len(filled) - 1))
    return [b for b in bands if b[1] - b[0] > 20]


def main():
    a = np.array(Image.open(SRC).convert("RGB")).astype(float)
    grid = resample(a, GRID)
    fg = drop_specks(~background_mask(grid))
    H, W = fg.shape
    cell_w = W / COLS

    bands = row_bands(fg)
    print(f"linhas encontradas: {len(bands)} {bands}")
    assert len(bands) == 3, "esperava 3 linhas de quadros"

    # Cada grupo de pixels vai para a coluna do seu centro (o chapéu solto e a fumaça
    # ficam com o quadro deles).
    cells = {}
    for ys, xs in components(fg):
        cy, cx = ys.mean(), xs.mean()
        row = next((i for i, (b0, b1) in enumerate(bands) if b0 - 2 <= cy <= b1 + 2), None)
        if row is None:
            continue
        col = min(COLS - 1, int(cx // cell_w))
        m = cells.setdefault((row, col), np.zeros_like(fg))
        m[ys, xs] = True

    # Alinhamento horizontal pelo tronco (mediana dos pixels entre 25% e 55% da altura):
    # o desenho não fica no centro da coluna da folha, e alinhar pela coluna fazia o
    # boss pular de lado ao trocar de quadro. Na vertical, a base da linha.
    boxes, anchor = {}, {}
    for (row, col), m in cells.items():
        ys, xs = np.where(m)
        boxes[(row, col)] = (ys.min(), ys.max(), xs.min(), xs.max())
        h = ys.max() - ys.min()
        band = (ys >= ys.min() + 0.25 * h) & (ys <= ys.min() + 0.55 * h)
        anchor[(row, col)] = int(round(np.median(xs[band])))
    half_w = int(max(max(abs(x0 - anchor[k]), abs(x1 - anchor[k]))
                     for k, (y0, y1, x0, x1) in boxes.items())) + 2
    height = int(max(bands[r][1] - y0 for (r, c), (y0, y1, x0, x1) in boxes.items())) + 3
    fw, fh = half_w * 2, height
    print(f"quadro: {fw}x{fh}")

    os.makedirs(OUT, exist_ok=True)
    for (row, col), m in sorted(cells.items()):
        cx = anchor[(row, col)]
        base = bands[row][1]
        rgb = np.zeros((fh, fw, 3), int)
        alpha = np.zeros((fh, fw), np.uint8)
        ys, xs = np.where(m)
        ty = ys - base + fh - 2       # pés 2 px acima da base, como o cavaleiro
        tx = xs - cx + half_w
        ok = (ty >= 0) & (ty < fh) & (tx >= 0) & (tx < fw)
        rgb[ty[ok], tx[ok]] = grid[ys[ok], xs[ok]]
        alpha[ty[ok], tx[ok]] = 255
        snapped, n = quantize(rgb, alpha, dist=22)
        out = np.dstack([snapped, alpha]).astype(np.uint8)
        name = NAMES[row][col]
        Image.fromarray(out, "RGBA").save(os.path.join(OUT, name + ".png"))
        print(f"  {name}.png ({n} cores)")


if __name__ == "__main__":
    main()
