"""Separa os 20 pisos de imagens/pisos-natureza.jpg (grade 5x4) e faz duas versões:

1. Piso de construção (como os de extract_pisos.py): Assets/Resources/Floors/<id>.png.
2. Textura para pintar o mapa: Assets/Art/Sprites/Environment/Texturas/<id>.png, que
   emenda com ela mesma (menu RaelMMO > Chão no Unity estica uma área do tamanho que quiser).
Os dois têm 64x64: 1 ladrilho = 1 quadrado da grade do chão, igual a todos os pisos.

Os naturais (grama, terra, areia, folhas) têm moldura e não emendam: o miolo é recortado
e as bordas são refeitas misturando o ladrilho com ele mesmo deslocado meio ladrilho (a
mistura é por pixel, sem borrar). Os de desenho fixo (tábuas, tijolos, lajotas, pedras,
ornamento) já foram feitos para encaixar e ficam como estão.

Uso: python Tools/extract_pisos_natureza.py
"""
import os

import numpy as np
from PIL import Image

SRC = "imagens/pisos-natureza.jpg"
FLOORS = "Assets/Resources/Floors"
TEX = "Assets/Art/Sprites/Environment/Texturas"
SIZE = 64   # 1 quadrado da grade, igual aos pisos antigos (extract_pisos.py)

COLS = [(332, 567), (600, 841), (879, 1120), (1158, 1399), (1432, 1667)]
ROWS = [(45, 278), (307, 546), (571, 810), (840, 1078)]

# (id, nome, tema, linha, coluna, natural)
TILES = [
    ("piso_grama", "Grama", "Grama", 0, 0, True),
    ("piso_grama_flores", "Grama com flores", "Grama", 0, 1, True),
    ("piso_grama_trevos", "Grama com trevos", "Grama", 0, 2, True),
    ("piso_mato_seco", "Mato seco", "Grama", 0, 3, True),
    ("piso_folhagem", "Folhagem", "Grama", 0, 4, True),
    ("piso_folhas_outono", "Folhas de outono", "Grama", 3, 3, True),
    ("piso_terra_arada", "Terra arada", "Terra", 1, 0, False),
    ("piso_terra_batida", "Terra batida", "Terra", 1, 1, True),
    ("piso_terra_galhos", "Terra com galhos", "Terra", 1, 2, True),
    ("piso_barro", "Barro", "Terra", 1, 3, True),
    ("piso_areia", "Areia", "Terra", 1, 4, True),
    ("piso_cascalho_terra", "Cascalho na terra", "Terra", 2, 4, True),
    ("piso_pedras_redondas", "Pedras redondas", "Pedra", 2, 0, False),
    ("piso_lajotas_grandes", "Lajotas grandes", "Pedra", 2, 1, False),
    ("piso_pedras_quebradas", "Pedras quebradas", "Pedra", 2, 2, False),
    ("piso_lajotas_hera", "Lajotas com hera", "Pedra", 2, 3, False),
    ("piso_tijolo_vermelho", "Tijolo vermelho", "Pedra", 3, 2, False),
    ("piso_ornamentado", "Ornamentado", "Pedra", 3, 4, False),
    ("piso_tabuas", "Tábuas", "Madeira", 3, 0, False),
    ("piso_tabuas_em_pe", "Tábuas em pé", "Madeira", 3, 1, False),
]


def resample(tile, size):
    """Mediana do miolo de cada bloco: tira o ruído do JPEG e acha a grade nativa."""
    h, w, _ = tile.shape
    out = np.zeros((size, size, 3), np.uint8)
    for y in range(size):
        for x in range(size):
            cy, cx = (y + 0.5) * h / size, (x + 0.5) * w / size
            ry, rx = 0.3 * h / size, 0.3 * w / size
            blk = tile[int(cy - ry):int(cy + ry) + 1, int(cx - rx):int(cx + rx) + 1].reshape(-1, 3)
            out[y, x] = np.median(blk, axis=0)
    return out


def seamless(t, rng):
    """Ladrilho que emenda: perto das bordas usa o próprio ladrilho deslocado meio
    ladrilho (cujas bordas são o miolo original, contínuo na volta); no centro, o
    original. A troca entre os dois é pixel a pixel, sorteada, para não borrar."""
    n = t.shape[0]
    shifted = np.roll(np.roll(t, n // 2, 0), n // 2, 1)
    yy, xx = np.mgrid[0:n, 0:n]
    edge = np.minimum(np.minimum(yy, n - 1 - yy), np.minimum(xx, n - 1 - xx)) / (n / 4)
    use_orig = rng.random((n, n)) < np.clip(edge, 0, 1) ** 1.5
    return np.where(use_orig[:, :, None], t, shifted)


def main():
    a = np.array(Image.open(SRC).convert("RGB"))
    os.makedirs(FLOORS, exist_ok=True)
    os.makedirs(TEX, exist_ok=True)
    rng = np.random.default_rng(48)
    for fid, name, theme, r, c, natural in TILES:
        (x0, x1), (y0, y1) = COLS[c], ROWS[r]
        if natural:
            # Sem a moldura (uns 7% de cada lado).
            m = int((x1 - x0) * 0.07)
            img = resample(a[y0 + m:y1 + 1 - m, x0 + m:x1 + 1 - m], SIZE)
            img = seamless(img, rng)
        else:
            img = resample(a[y0:y1 + 1, x0:x1 + 1], SIZE)
        rgba = Image.fromarray(img, "RGB").convert("RGBA")
        rgba.save(os.path.join(FLOORS, fid + ".png"))
        rgba.save(os.path.join(TEX, fid.replace("piso_", "") + ".png"))
        print(f"  {fid}.png  ({theme}: {name}{', emendável' if natural else ''})")


if __name__ == "__main__":
    main()
