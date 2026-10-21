"""Recorta a folha de decoração do mercado (imagens/decoracao_mercado.webp: fundo cinza-escuro
liso, com linhas de grade) em Assets/Art/Sprites/Environment/Decoracao, na escala das outras
decorações (extract_decoracao.SCALE). Mesma separação de peças de extract_decoracao_folha.

Uso: python Tools/extract_mercado.py [--listar <pasta>]
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
import extract_decoracao  # noqa: E402
from extract_decoracao_folha import clusters, drop_specks  # noqa: E402

SRC = "imagens/decoracao_mercado.webp"
NAMES = [
    "barraca_vermelha", "barraca_verde", "barraca_paes", "barraca_peixes", "barraca_temperos",
    "banca_macas", "banca_laranjas", "banca_bananas", "banca_uvas", "banca_peras", "banca_legumes", "banca_abobora_legumes",
    "cesto_macas", "cesto_laranjas", "cesto_bananas", "cesto_uvas", "cesto_peras", "cesto_cenouras",
    "cesto_batatas_mercado", "cesto_tomates_mercado", "cesto_alhos", "cesto_cebolas", "cesto_repolhos",
    "mesa_paes", "mesa_queijos", "mesa_carnes", "mesa_peixes", "mesa_potes", "mesa_garrafas", "mesa_graos",
    "armario_madeira", "estante_potes", "balcao_lanterna", "estante_pratos", "balcao_cofre", "mesa_corte",
    "balanca_mercado", "lousa_cerveja", "placa_loja",
    "saco_milho", "saco_farinha", "saco_fechado", "saco_feijao", "barril_pequeno_mercado", "barril", "barril_largo",
    "barril_tampado", "tina", "balde", "leiteira", "caixote", "caixote_alto", "caixotes_pilha",
    "carrinho_mao", "carroca", "caixote_aberto_mercado", "caixa_ferramentas", "lanterna_mesa", "corda_rolo",
    "vassoura", "cutelo", "faca", "martelo_madeira", "pinca", "banquinho",
]


def main():
    a = np.array(Image.open(SRC).convert("RGB")).astype(float)
    # Fundo e linhas da grade: cinza-escuro neutro (o contorno do desenho tem cor).
    bg = ((a.max(2) - a.min(2)) <= 10) & (a.max(2) <= 60)
    items = clusters(drop_specks(~bg, 150))
    # Linhas da grade (finas ou ralas) não são peças.
    def solid(it):
        h = it["ys"].max() - it["ys"].min() + 1
        w = it["xs"].max() - it["xs"].min() + 1
        return min(w, h) >= 10 and max(w, h) / min(w, h) <= 12 and len(it["ys"]) / (w * h) >= 0.15
    items = [it for it in items if solid(it)]
    # Ordem: linha da folha (pelo meio da peça), depois esquerda -> direita.
    rows = [245, 380, 487, 630, 805, 935]
    items.sort(key=lambda it: (sum(it["ys"].mean() > r for r in rows), it["xs"].min()))
    out = extract_decoracao.OUT
    if "--listar" in sys.argv:
        out = sys.argv[sys.argv.index("--listar") + 1]
        extract_decoracao.OUT = out
        os.makedirs(out, exist_ok=True)
        for i, it in enumerate(items, 1):
            W, H = extract_decoracao.save(a, it, f"{i:02d}")
        print(len(items), "peças")
        return
    print(len(items), "peças,", len(NAMES), "nomes")
    for it, name in zip(items, NAMES):
        W, H = extract_decoracao.save(a, it, name)


if __name__ == "__main__":
    main()
