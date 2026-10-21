"""Consumíveis: imagens/comidas.webp (fundo claro) e imagens/pocoes_bebidas.webp (fundo
escuro), grades 6 x 5 com moldura e número embaixo de cada célula. Cada célula: tira a faixa
do número, e o fundo (cor da borda da célula, ligado à borda) sai; fica o maior desenho e o
que estiver colado nele (fumaça do café, morango do bolo).
Saída: Assets/Resources/Items/<id>.png (lado maior 64 px), ids em NAMES.

Uso: python Tools/extract_consumiveis.py
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_boss_pirata import components  # noqa: E402
from extract_predios_folha import border_flood  # noqa: E402
from extract_armas import icon, grow  # noqa: E402

OUT = "Assets/Resources/Items"
SHEETS = {
    "imagens/comidas.webp": (0.80, 6, [
        "prato_carne_arroz", "espaguete", "salmao_grelhado", "salada", "hamburguer", "pizza",
        "sopa_abobora", "frango_brocolis", "sushi", "ovos_bacon", "panquecas", "taco",
        "acai", "omelete", "feijoada", "frango_arroz", "ramen", "ensopado",
        "lasanha", "salada_caprese", "sanduiche", "batata_frita", "risoto", "burrito",
        "salada_frutas", "bolo_chocolate", "milkshake", "mingau_banana", "bife_milho", "pudim",
    ]),
    "imagens/pocoes_bebidas.webp": (0.80, 16, [
        "pocao_vermelha_p", "pocao_vermelha_m", "pocao_vermelha_g", "pocao_azul_p", "pocao_azul_m", "pocao_azul_g",
        "pocao_verde_p", "pocao_verde_m", "pocao_verde_g", "pocao_laranja_p", "pocao_laranja_m", "pocao_laranja_g",
        "pocao_roxa_p", "pocao_roxa_g", "pocao_rosa_p", "pocao_rosa_g", "pocao_branca", "pocao_dourada",
        "cerveja", "cerveja_metal", "vinho_tinto", "vinho_branco", "cafe", "cha_verde",
        "frango_assado", "bife", "sopa", "pao", "queijo_frutas", "torta_morango",
    ]),
}


def main():
    for src, (keep_h, ins, names) in SHEETS.items():
        rgb = np.array(Image.open(src).convert("RGB")).astype(int)
        H, W = rgb.shape[:2]
        k = 0
        for r in range(5):
            for c in range(6):
                y0, y1 = int(r * H / 5) + ins, int(r * H / 5 + H / 5 * keep_h)
                x0, x1 = int(c * W / 6) + ins, int((c + 1) * W / 6) - ins
                cell = rgb[y0:y1, x0:x1]
                edge = np.concatenate([cell[:3].reshape(-1, 3), cell[:, :3].reshape(-1, 3), cell[:, -3:].reshape(-1, 3)])
                bgc = np.median(edge, 0)
                like = np.abs(cell - bgc).max(2) <= 22
                fg = ~border_flood(like)
                comps = components(fg)
                main = max(comps, key=lambda q: len(q[0]))
                keep = np.zeros_like(fg)
                keep[main] = True
                near = grow(keep, 4)
                for cy, cx in comps:
                    if near[cy, cx].any() or len(cy) >= 0.05 * len(main[0]):
                        keep[cy, cx] = True
                full = np.zeros(rgb.shape[:2], bool)
                full[y0:y1, x0:x1] = keep
                icon(rgb, full).save(f"{OUT}/{names[k]}.png")
                k += 1
        print(src, k)


if __name__ == "__main__":
    main()
