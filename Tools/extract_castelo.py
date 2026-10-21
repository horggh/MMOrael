"""Decoração de castelo (imagens/decoracao_castelo.jpg: 5 linhas, fundo cinza-azulado liso):
mesas, cadeiras, tronos, estátuas, gárgulas, armaduras, estandartes, lustres, candelabros,
tochas, braseiros, armários, estantes, baús, escrivaninha, lareira, tapeçarias, escudos,
barris, tapetes. Saída: Assets/Art/Sprites/Environment/Decoracao/<nome>.png já no padrão de
64 px por quadrado (Tools/escala.py; ex.: cadeira ~1,2 de altura, armadura ~1,8).
Os arquivos entram no registro do Tools/padronizar_64.py (não são ampliados de novo).

Uso: python Tools/extract_castelo.py
"""
import json
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_boss_pirata import components  # noqa: E402
from extract_armas import grow  # noqa: E402

SRC = "imagens/decoracao_castelo.jpg"
OUT = "Assets/Art/Sprites/Environment/Decoracao"
ESCALA = 0.95            # px da folha -> px do jogo (64 por quadrado)
FUNDO = np.array([62, 69, 77])
LINHAS = [(0, 108), (108, 240), (240, 346), (346, 458), (458, 572)]
NOMES = [
    ["mesa_longa", "mesa_banquete", "cadeira_vermelha", "mesa_redonda", "cadeira_azul", "cadeira_estofada",
     "trono_vermelho", "trono_azul", "cadeira_ferro", "banco_madeira"],
    ["estatua_cavaleiro_1", "estatua_cavaleiro_2", "gargula_1", "gargula_2", "armadura_1", "armadura_penacho",
     "armadura_2", "estandarte_vermelho", "estandarte_leao", "estandarte_aguia", "estandarte_aguia_2", "bandeira_aguia"],
    ["lustre_1", "lustre_2", "lustre_3", "candelabro_ferro", "candelabro_cobre", "candelabro_prata", "candelabro_ouro",
     "candelabro_cobre_2", "tocheiro", "tocha_parede_1", "tocha_parede_2", "braseiro_1", "braseiro_2"],
    ["armario_1", "armario_2", "armario_3", "estante_livros_castelo", "estante_pergaminhos", "livros_banquinho",
     "bau_dourado", "bau_tesouro", "bau_aberto_vazio", "escrivaninha"],
    ["lareira", "tapecaria_batalha", "tapecaria_brasao", "escudo_espadas_1", "escudo_espadas_2", "barril_castelo",
     "barril_caixotes", "tapete_urso", "tapete_vermelho"],
]


def main():
    a = np.array(Image.open(SRC).convert("RGB")).astype(int)
    fg = np.abs(a - FUNDO).max(2) > 24
    log_path = "Tools/padronizar_64.json"
    log = json.load(open(log_path, encoding="utf-8")) if os.path.exists(log_path) else {}
    feitos = 0
    for (y0, y1), nomes in zip(LINHAS, NOMES):
        sub = np.zeros_like(fg)
        sub[y0:y1] = fg[y0:y1]
        comps = [c for c in components(grow(sub, 2)) if sub[c].sum() > 120]
        comps.sort(key=lambda c: c[1].min())
        if len(comps) != len(nomes):
            print(f"  aviso: linha {y0}: {len(comps)} peças para {len(nomes)} nomes")
        for (cy, cx), nome in zip(comps, nomes):
            keep = np.zeros_like(fg)
            keep[cy, cx] = True
            keep &= fg
            ys, xs = np.where(keep)
            yy0, yy1, xx0, xx1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
            rgb = a[yy0:yy1, xx0:xx1].astype(float)
            k = keep[yy0:yy1, xx0:xx1].astype(float)
            w, h = max(1, round((xx1 - xx0) * ESCALA)), max(1, round((yy1 - yy0) * ESCALA))
            pre = Image.fromarray((rgb * k[:, :, None]).astype(np.uint8)).resize((w, h), Image.LANCZOS)
            al = np.array(Image.fromarray((k * 255).astype(np.uint8)).resize((w, h), Image.LANCZOS)).astype(float)
            out = np.clip(np.array(pre).astype(float) * 255 / np.maximum(al, 1)[:, :, None], 0, 255)
            img = np.dstack([out, np.where(al >= 120, 255, 0)]).astype(np.uint8)
            path = f"{OUT}/{nome}.png"
            Image.fromarray(img, "RGBA").save(path)
            log[path] = {"regra": "castelo, já em 64", "antes": [w, h]}
            feitos += 1
    json.dump(log, open(log_path, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    print(feitos, "peças")


if __name__ == "__main__":
    main()
