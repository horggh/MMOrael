"""Segunda folha de papéis de parede (imagens/papeis-de-parede-2.webp: 6 x 5, fundo escuro)
em sprites de 64x64 em Assets/Resources/Walls/<id>.png, como Tools/extract_papeis.py.
Cada papel é um retângulo claro sobre o fundo; o recorte entra INSET px para dentro (sem a
borda arredondada) e é reamostrado com a mediana de cada bloco (mantém o pixel nítido).
Os ids, nomes e temas também estão em FloorTiles.WallCatalog.

Uso: python Tools/extract_papeis2.py
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_papeis import resample  # noqa: E402

SRC = "imagens/papeis-de-parede-2.webp"
OUT = "Assets/Resources/Walls"
SIZE, INSET = 64, 6
IDS = [
    "parede_rosas_vermelhas", "parede_listras_reais", "parede_flor_de_lis_verde", "parede_damasco_azul", "parede_violetas", "parede_damasco_dourado",
    "parede_listras_coracoes", "parede_margaridas", "parede_reboco", "parede_madeira", "parede_losangos_verdes", "parede_lua_estrelas",
    "parede_trepadeira", "parede_flores_listras", "parede_brocado_roxo", "parede_nuvens", "parede_lacos", "parede_tijolo_reboco",
    "parede_cruzes_roxas", "parede_estrelas_douradas", "parede_ramos_verdes", "parede_losangos_vermelhos", "parede_flocos_azuis", "parede_musgo_tijolos",
    "parede_brotos", "parede_listras_azuis", "parede_leoes", "parede_vasos_plantas", "parede_flores_losangos", "parede_vitral_gotico",
]


def spans(v, minw=40):
    out, s = [], None
    for i, x in enumerate(v):
        if x and s is None:
            s = i
        elif not x and s is not None:
            if i - s >= minw:
                out.append((s, i))
            s = None
    if s is not None and len(v) - s >= minw:
        out.append((s, len(v)))
    return out


def main():
    a = np.array(Image.open(SRC).convert("RGB")).astype(int)
    fg = a.max(2) > 45                       # fundo quase preto
    rows = spans(fg.mean(1) > 0.3)
    cols = spans(fg.mean(0) > 0.3)
    assert len(rows) == 5 and len(cols) == 6, (rows, cols)
    k = 0
    for y0, y1 in rows:
        for x0, x1 in cols:
            # Cada papel tem o próprio retângulo: aperta na linha/coluna dele.
            cell = fg[y0:y1, x0:x1]
            ys = np.where(cell.mean(1) > 0.5)[0]; xs = np.where(cell.mean(0) > 0.5)[0]
            t = a[y0 + ys.min() + INSET:y0 + ys.max() + 1 - INSET, x0 + xs.min() + INSET:x0 + xs.max() + 1 - INSET]
            Image.fromarray(resample(t, SIZE), "RGB").convert("RGBA").save(os.path.join(OUT, IDS[k] + ".png"))
            k += 1
    print(k, "papéis")


if __name__ == "__main__":
    main()
