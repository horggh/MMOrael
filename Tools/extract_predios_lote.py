"""Recorta as construções de imagens/predios_lote/<n>.webp em Art/Sprites/Environment/Predios,
com 192 px de largura (6 unidades a 32 PPU: a área da casa, como Tools/extract_predios_folha.py).

Fundo: transparente (usa o alfa), ou liso/xadrez claro (branco, preto, cinza-escuro), tirado
por inundação a partir da borda, mais as manchas grandes do fundo presas entre as torres.
Imagens com mais de uma construção são separadas pelos grupos de pixels, na ordem de
leitura (linha de cima primeiro).

Uso: python Tools/extract_predios_lote.py
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_boss_pirata import components  # noqa: E402
from extract_predios_folha import border_flood, to_width  # noqa: E402
import escala  # noqa: E402

SRC = "imagens/predios_lote/{}.webp"
OUT = "Assets/Art/Sprites/Environment/Predios"
SHEETS = {
    65: ["boticario"], 66: ["armaria"], 67: ["ferraria"], 68: ["banco"],
    69: ["palacio_real", "mansao_real", "portao_real", "coreto_real"],
    70: ["hospital"], 71: ["catedral"], 72: ["capela"],
    73: ["igreja_grande", "igreja", "capela_simples"],
    74: ["arquearia"], 75: ["torre_magica"], 76: ["guilda_ladroes"],
    77: ["torre_arcana", "guilda_guerreiros", "salao_concerto"],
    78: ["salao_musica"],
}


def background(a):
    rgb = a[:, :, :3]
    if (a[:, :, 3] < 128).mean() > 0.05:
        return a[:, :, 3] < 128
    corner = np.median(np.stack([rgb[3, 3], rgb[3, -4], rgb[-4, 3], rgb[-4, -4]]), 0)
    neutral = (rgb.max(2) - rgb.min(2)) <= 22
    if corner.mean() > 128:           # branco ou xadrez claro
        like = neutral & (rgb.min(2) >= 200)
    else:                             # preto ou cinza-escuro: só o tom do fundo (prédios escuros)
        like = neutral & (np.abs(rgb - corner).max(2) <= 10)
    bg = border_flood(like)
    # Fundo preso dentro do desenho (entre torres, sob arcos): manchas grandes do tom exato.
    exact = like & (np.abs(rgb - corner).max(2) <= 4) if corner.mean() <= 128 else like
    for cy, cx in components(exact & ~bg):
        if len(cy) >= 400:
            bg[cy, cx] = True
    return bg


def main():
    for n, names in SHEETS.items():
        a = np.array(Image.open(SRC.format(n)).convert("RGBA")).astype(int)
        fg = ~background(a)
        # Peças: grupos de pixels (crescidos 3 px para juntar bandeiras e fumaça soltas).
        grown = fg.copy()
        for _ in range(3):
            g = grown.copy()
            g[1:] |= grown[:-1]; g[:-1] |= grown[1:]; g[:, 1:] |= grown[:, :-1]; g[:, :-1] |= grown[:, 1:]
            grown = g
        comps = [c for c in components(grown)]
        big = max(len(c[0]) for c in comps)
        parts = [c for c in comps if len(c[0]) >= 0.05 * big]
        H = a.shape[0]
        parts.sort(key=lambda c: (round(c[0].mean() / (H / 2)) if len(names) > 3 else 0, c[1].min()))
        if len(parts) != len(names):
            print(f"  {n}: {len(parts)} peças para {len(names)} nomes")
        for (cy, cx), name in zip(parts, names):
            keep = np.zeros_like(fg)
            keep[cy, cx] = True
            keep &= fg
            ys, xs = np.where(keep)
            y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
            crop = a[y0:y1, x0:x1].copy()
            crop[:, :, 3] = np.where(keep[y0:y1, x0:x1], 255, 0)
            out = to_width(Image.fromarray(crop.astype(np.uint8), "RGBA"), escala.largura_px(name))
            out.save(f"{OUT}/{name}.png")
            print(f"  {name}: {out.size}")


if __name__ == "__main__":
    main()
