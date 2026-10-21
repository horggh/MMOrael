"""Entradas de caverna (portal entre mapas, como o portal_mapa): imagens/entrada_caverna.webp
e imagens/entrada_caverna_escura.webp (fundo xadrez claro desenhado) em
Assets/Art/Sprites/Environment/Decoracao/Passagens/entrada_caverna*.png, com 4 quadrados de largura.
Sai o xadrez ligado à borda e as manchas grandes de xadrez presas (os dois tons misturados).

Uso: python Tools/extract_entradas_caverna.py
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_boss_pirata import components  # noqa: E402
from extract_predios_folha import border_flood  # noqa: E402
from extract_passagens import save  # noqa: E402
import extract_passagens  # noqa: E402

extract_passagens.OUT = "Assets/Art/Sprites/Environment/Decoracao/Passagens"

for src, name in (("imagens/entrada_caverna.webp", "entrada_caverna"),
                  ("imagens/entrada_caverna_escura.webp", "entrada_caverna_escura")):
    a = np.array(Image.open(src).convert("RGBA")).astype(int)
    rgb = a[:, :, :3]
    light = ((rgb.max(2) - rgb.min(2)) <= 20) & (rgb.min(2) >= 200)
    bg = border_flood(light)
    for cy, cx in components(light & ~bg):
        lum = rgb[cy, cx].mean(1)
        if len(cy) >= 300 and lum.max() - lum.min() >= 15:
            bg[cy, cx] = True
    keep = ~bg
    # Pedaços soltos (brilhos do xadrez): só fica o desenho principal.
    comps = components(keep)
    big = max(len(c[0]) for c in comps)
    keep[:] = False
    for cy, cx in comps:
        if len(cy) >= 0.02 * big:
            keep[cy, cx] = True
    save(a, keep, name, width=4 * 32)
