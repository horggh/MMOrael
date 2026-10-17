"""Separa os 30 papéis de parede de imagens/papeis-de-parede.jpeg em sprites de 64x64.

Cada papel tem contorno preto: os contornos são achados como grupos de pixels escuros,
o recorte entra 4 px para dentro (o papel se encosta no vizinho sem moldura) e é
reamostrado para 64x64. Saída: Assets/Resources/Walls/<id>.png.

A ordem de WALLS é a ordem por tema usada no jogo (ItemDB e aba Paredes da ferramenta DEV);
o índice é a posição na imagem, linha a linha (6 por linha).

Uso:  python3 Tools/extract_papeis.py
"""
import os
import numpy as np
from PIL import Image

SRC = "imagens/papeis-de-parede.jpeg"
OUT = "Assets/Resources/Walls"
SIZE = 64
INSET = 4

# (id, nome, tema, índice na imagem) — em ordem de tema.
WALLS = [
    ("parede_damasco_vermelho", "Damasco vermelho", "Clássico", 0),
    ("parede_damasco_roxo", "Damasco roxo", "Clássico", 5),
    ("parede_flor_de_lis", "Flor-de-lis", "Clássico", 1),
    ("parede_ornamento_azul", "Ornamento azul", "Clássico", 3),
    ("parede_listras_bege", "Listras bege", "Clássico", 2),
    ("parede_coroas", "Coroas reais", "Clássico", 19),
    ("parede_folhas_douradas", "Folhas douradas", "Clássico", 21),
    ("parede_leque", "Leque art déco", "Clássico", 29),
    ("parede_rosas", "Rosas", "Natureza", 4),
    ("parede_flores_rosa", "Florzinhas", "Natureza", 15),
    ("parede_folhagem", "Folhagem", "Natureza", 14),
    ("parede_arvores", "Árvores e pássaros", "Natureza", 23),
    ("parede_cogumelos", "Cogumelos", "Natureza", 16),
    ("parede_ceu", "Céu", "Natureza", 13),
    ("parede_mar", "Mar", "Natureza", 17),
    ("parede_noite", "Noite estrelada", "Natureza", 12),
    ("parede_xadrez", "Xadrez", "Padrões", 7),
    ("parede_losangos", "Losangos", "Padrões", 6),
    ("parede_ondas", "Ondas retrô", "Padrões", 8),
    ("parede_zigue_zague", "Zigue-zague", "Padrões", 9),
    ("parede_aneis", "Anéis", "Padrões", 10),
    ("parede_mosaico", "Mosaico", "Padrões", 11),
    ("parede_bolinhas", "Bolinhas", "Padrões", 24),
    ("parede_listras_coloridas", "Listras coloridas", "Padrões", 26),
    ("parede_listras_marinho", "Listras marinho", "Padrões", 27),
    ("parede_labirinto", "Labirinto", "Padrões", 28),
    ("parede_azulejos", "Azulejos", "Padrões", 22),
    ("parede_runas", "Runas", "Fantasia", 18),
    ("parede_coracoes_sombrios", "Corações sombrios", "Fantasia", 20),
    ("parede_coracoes", "Corações", "Fantasia", 25),
]


def outline_boxes(a):
    """Caixas dos contornos pretos (grupos escuros grandes), linha a linha."""
    dark = a.mean(2) < 45
    H, W = dark.shape
    seen = np.zeros_like(dark)
    boxes = []
    for y0, x0 in zip(*np.where(dark)):
        if seen[y0, x0]:
            continue
        stack = [(y0, x0)]
        seen[y0, x0] = True
        ys, xs = [], []
        while stack:
            y, x = stack.pop()
            ys.append(y)
            xs.append(x)
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                ny, nx = y + dy, x + dx
                if 0 <= ny < H and 0 <= nx < W and dark[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        if max(xs) - min(xs) > 70 and max(ys) - min(ys) > 70:
            boxes.append((min(ys), min(xs), max(ys), max(xs)))
    boxes.sort(key=lambda b: (round(b[0] / 50), b[1]))
    return boxes


def resample(tile, size):
    h, w, _ = tile.shape
    out = np.zeros((size, size, 3), np.uint8)
    for y in range(size):
        for x in range(size):
            cy, cx = (y + 0.5) * h / size, (x + 0.5) * w / size
            ry, rx = 0.3 * h / size, 0.3 * w / size
            blk = tile[int(cy - ry):int(cy + ry) + 1, int(cx - rx):int(cx + rx) + 1].reshape(-1, 3)
            out[y, x] = np.median(blk, axis=0)
    return out


def main():
    a = np.array(Image.open(SRC).convert("RGB"))
    boxes = outline_boxes(a)
    if len(boxes) != 30:
        raise SystemExit(f"esperava 30 papéis, achei {len(boxes)}")
    os.makedirs(OUT, exist_ok=True)
    for wid, name, theme, idx in WALLS:
        y0, x0, y1, x1 = boxes[idx]
        tile = a[y0 + INSET:y1 - INSET + 1, x0 + INSET:x1 - INSET + 1]
        Image.fromarray(resample(tile, SIZE), "RGB").convert("RGBA").save(os.path.join(OUT, wid + ".png"))
        print(f"  {wid}.png  ({theme}: {name})")


if __name__ == "__main__":
    main()
