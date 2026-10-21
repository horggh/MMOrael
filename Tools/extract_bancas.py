"""Recorta as 36 bancas de imagens/bancas_mercado.webp (grade 6x6, fundo cinza-escuro liso)
em Assets/Art/Sprites/Environment/Decoracao, na escala das outras decorações. Cada célula:
o fundo é o cinza-escuro neutro ligado à borda; o resto (banca, barril, cesto) fica junto.

Uso: python Tools/extract_bancas.py
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
import extract_decoracao  # noqa: E402
from extract_boss_pirata import components  # noqa: E402

SRC = "imagens/bancas_mercado.webp"
NAMES = [
    "banca_macas_verdes", "banca_frutas_tropicais", "banca_hortalicas", "banca_aboboras_alhos", "banca_melancias", "banca_frutas_vermelhas",
    "banca_paes_cestos", "banca_paes_doces", "banca_queijos", "banca_frutos_do_mar", "banca_carnes_penduradas", "banca_presuntos",
    "banca_ervas", "banca_flores", "banca_ervas_medicinais", "banca_cogumelos", "banca_temperos_caixas", "banca_conservas",
    "banca_ovos", "banca_legumes_coloridos", "banca_raizes", "banca_graos_caixas", "banca_sacos_graos", "banca_nozes",
    "banca_bolos", "banca_balas", "banca_azeites", "banca_pratos_prontos", "banca_picles", "banca_mel_queijo",
    "banca_pimentas_alhos", "banca_peixe_seco", "banca_chas_folhas", "banca_chas_bules", "banca_especiarias", "banca_couve_flor",
]


def border_flood(bg):
    m = np.zeros_like(bg)
    m[0, :], m[-1, :], m[:, 0], m[:, -1] = bg[0, :], bg[-1, :], bg[:, 0], bg[:, -1]
    while True:
        g = m.copy()
        g[1:] |= m[:-1]; g[:-1] |= m[1:]; g[:, 1:] |= m[:, :-1]; g[:, :-1] |= m[:, 1:]
        g &= bg
        if (g == m).all():
            return m
        m = g


def main():
    a = np.array(Image.open(SRC).convert("RGB")).astype(float)
    H, W = a.shape[:2]
    xs_cut = np.linspace(0, W, 7).round().astype(int)
    ys_cut = np.linspace(0, H, 7).round().astype(int)
    bg_all = ((a.max(2) - a.min(2)) <= 12) & (a.max(2) <= 62)
    for r in range(6):
        for c in range(6):
            y0, y1, x0, x1 = ys_cut[r] + 4, ys_cut[r + 1] - 4, xs_cut[c] + 4, xs_cut[c + 1] - 4
            keep = ~border_flood(bg_all[y0:y1, x0:x1])
            # Fundo preso dentro do desenho (entre o varal e as carnes): manchas grandes do cinza do fundo.
            for cy, cx in components(keep & bg_all[y0:y1, x0:x1]):
                if len(cy) >= 25:
                    keep[cy, cx] = False
            # Fios da grade da folha (pedaços finos e compridos) saem.
            for cy, cx in components(keep):
                h, w = cy.max() - cy.min() + 1, cx.max() - cx.min() + 1
                if min(h, w) <= 6 or max(h, w) / min(h, w) > 10 or len(cy) < 30:
                    keep[cy, cx] = False
            ys, xs = np.where(keep)
            extract_decoracao.save(a, {"ys": ys + y0, "xs": xs + x0}, NAMES[r * 6 + c])
    print(len(NAMES), "bancas")


if __name__ == "__main__":
    main()
