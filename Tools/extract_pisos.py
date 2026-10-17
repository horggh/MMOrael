"""Separa os 20 tipos de piso de imagens/pisos.jpeg (grade 5x4) em sprites de 64x64.

Cada piso é cortado pelas linhas do contorno escuro, reamostrado para 64x64 (a mediana do
miolo de cada bloco, para limpar o JPEG) e salvo em Assets/Resources/Floors/<id>.png.
64 px = 1 quadrado da grade do mapa (o jogo usa 64 px por unidade para os pisos).

A ordem de FLOORS é a ordem por tema usada no jogo (ItemDB e aba Chão da ferramenta DEV).

Uso:  python3 Tools/extract_pisos.py
"""
import os
import numpy as np
from PIL import Image

SRC = "imagens/pisos.jpeg"
OUT = "Assets/Resources/Floors"
SIZE = 64

# Bordas dos pisos na imagem (linhas escuras do contorno), medidas uma vez.
COLS = [(41, 209), (231, 403), (427, 596), (620, 792), (814, 982)]
ROWS = [(26, 187), (209, 374), (396, 560), (582, 748)]

# (id, nome, tema, linha, coluna) — em ordem de tema.
FLOORS = [
    ("piso_liso", "Liso", "Lajotas", 0, 0),
    ("piso_lajotas", "Lajotas", "Lajotas", 1, 1),
    ("piso_lajotas_rachadas", "Lajotas rachadas", "Lajotas", 1, 0),
    ("piso_lajotas_manchadas", "Lajotas manchadas", "Lajotas", 1, 3),
    ("piso_tijolos", "Tijolos", "Lajotas", 1, 2),
    ("piso_rachado", "Rachado", "Desgastado", 0, 2),
    ("piso_pedras_soltas", "Pedras soltas", "Desgastado", 0, 3),
    ("piso_manchas", "Manchas", "Desgastado", 3, 0),
    ("piso_quebrado", "Quebrado com vergalhão", "Desgastado", 2, 3),
    ("piso_arranhado", "Arranhado", "Desgastado", 3, 4),
    ("piso_musgo", "Musgo", "Natureza", 0, 4),
    ("piso_raizes", "Raízes", "Natureza", 3, 3),
    ("piso_cascalho", "Cascalho", "Natureza", 2, 4),
    ("piso_concreto", "Concreto", "Urbano", 0, 1),
    ("piso_canaletas", "Canaletas", "Urbano", 1, 4),
    ("piso_grade", "Grade", "Urbano", 2, 0),
    ("piso_bueiro", "Bueiro", "Urbano", 2, 1),
    ("piso_faixa", "Faixa", "Urbano", 2, 2),
    ("piso_placa", "Placa de metal", "Urbano", 3, 2),
    ("piso_retalhos", "Retalhos", "Urbano", 3, 1),
]


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
    os.makedirs(OUT, exist_ok=True)
    for fid, name, theme, r, c in FLOORS:
        (x0, x1), (y0, y1) = COLS[c], ROWS[r]
        img = resample(a[y0:y1 + 1, x0:x1 + 1], SIZE)
        Image.fromarray(img, "RGB").convert("RGBA").save(os.path.join(OUT, fid + ".png"))
        print(f"  {fid}.png  ({theme}: {name})")


if __name__ == "__main__":
    main()
