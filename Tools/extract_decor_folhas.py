"""Folhas de decoração de 20/10 (imagens/Gemini_Generated_Image_*.jpg, 2752x1536, fundo liso):
castelo 2 (tronos, armas, vitrais, túmulos, cozinha), vila (forno, horta, poço, alquimia, igreja)
e castelo 3 (tortura, quarto, jardim). Cada peça tem uma caixa marcada à mão na folha reduzida
a 1024 de largura; entram os grupos de pixels (detectados nessa escala) com o centro dentro da
caixa, recortados da imagem grande. Escala igual à de Tools/extract_castelo.py (64 px/quadrado).
Nome que já existe em Decoracao ganha sufixo _2, _3... (nada é sobrescrito).

Uso: python Tools/extract_decor_folhas.py
"""
import json
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_boss_pirata import components  # noqa: E402
from extract_armas import grow  # noqa: E402

OUT = "Assets/Art/Sprites/Environment/Decoracao"
REF_W = 1024
ESCALA = 0.95            # px da folha em 1024 -> px do jogo
# Peças coladas na vizinha (um grupo só de pixels): recorta pelo retângulo da caixa.
CORTE = {"trono_roxo", "trono_roxo_b", "tumulo", "tumulo_b", "mesa_alquimia", "caldeirao_verde",
         "estante_canto", "estante_livros_media", "estante_canto_b", "sacos_graos", "caixotes_madeira", "fardos_feno"}

FOLHAS = {
    "imagens/Gemini_Generated_Image_tiwjf5tiwjf5tiwj.jpg": {
        "estandarte_roxo": (10, 5, 80, 140), "trono_roxo": (85, 10, 150, 145),
        "trono_roxo_b": (150, 40, 215, 140), "estandarte_roxo_b": (220, 5, 285, 140),
        "biombo_tapecaria": (298, 20, 458, 140), "mesa_festa": (470, 15, 760, 140),
        "harpa": (765, 25, 830, 135), "piano": (835, 20, 940, 140), "relogio_pendulo": (950, 10, 1015, 140),
        "suporte_espadas": (10, 150, 105, 250), "suporte_arcos": (110, 155, 172, 245),
        "escudo_redondo": (185, 145, 237, 196), "escudo_vermelho": (238, 145, 286, 196),
        "escudo_espada": (287, 145, 338, 196), "escudo_branco": (185, 197, 237, 250),
        "escudo_leao": (238, 197, 286, 250), "escudo_redondo_b": (287, 197, 338, 250),
        "prateleira_elmos": (358, 140, 478, 252), "sino_grande": (490, 140, 622, 255),
        "canhao_barris": (632, 155, 812, 255), "besta_cerco": (815, 165, 910, 252),
        "suporte_lancas": (915, 145, 1018, 255),
        "castical_alto": (12, 280, 38, 368), "candelabro_triplo": (38, 255, 68, 358),
        "altar_biblia": (68, 250, 190, 368), "candelabro_triplo_b": (190, 255, 218, 358),
        "castical_alto_b": (218, 280, 245, 368), "dossel_roxo": (255, 245, 378, 372),
        "vitral_cavaleiro": (396, 258, 460, 360), "vitral_santo": (468, 252, 538, 360),
        "vitral_santa": (546, 258, 608, 360), "tumulo": (620, 258, 740, 358),
        "tumulo_b": (752, 272, 842, 382), "relicario": (842, 278, 900, 368),
        "calice_fogo": (900, 290, 936, 368), "fonte_parede": (936, 262, 1000, 368),
        "mesa_alquimia": (98, 362, 292, 462), "caldeirao_verde": (293, 372, 378, 458),
        "estante_canto": (392, 378, 470, 478), "estante_livros_media": (470, 362, 562, 478),
        "estante_canto_b": (562, 368, 648, 478), "globo_grande": (658, 368, 748, 478),
        "luneta": (748, 378, 806, 472), "mesinha_pergaminho": (806, 392, 862, 468),
        "mesa_mapa": (862, 372, 1008, 478), "forno_tijolo": (8, 432, 212, 572),
        "mesa_acougue": (222, 452, 422, 572), "prateleira_panelas": (442, 458, 568, 562),
        "barril_vinho": (580, 478, 671, 568), "barril_vinho_b": (671, 478, 762, 568),
        "sacos_graos": (778, 488, 864, 570), "caixotes_madeira": (864, 482, 934, 570),
        "fardos_feno": (934, 482, 1012, 565),
    },
    "imagens/Gemini_Generated_Image_3i6l2s3i6l2s3i6l.jpg": {
        "forno_barro": (8, 0, 194, 152), "tina_lavar": (203, 28, 324, 142), "varal_roupas": (333, 12, 518, 142),
        "moinho_mao": (528, 8, 632, 147), "caldeirao_sopa": (641, 28, 750, 147),
        "sacos_farinha": (756, 50, 867, 142), "varal_peixes": (876, 18, 1008, 150),
        "horta": (12, 150, 210, 275), "trelica_trepadeira": (216, 146, 334, 270),
        "poco_balde": (348, 150, 472, 274), "carrinho_mao": (480, 168, 630, 272),
        "picareta_pa": (628, 163, 722, 267), "regador": (722, 200, 800, 270),
        "vaso_planta": (804, 160, 861, 272), "vaso_lilas": (861, 158, 917, 272), "colmeia": (928, 170, 1002, 272),
        "alambique_cobre": (12, 260, 160, 372), "pilao_ervas": (176, 285, 292, 372),
        "balanca": (306, 270, 418, 372), "prensa_livros": (433, 270, 532, 374),
        "ervas_secas": (543, 268, 664, 360), "secador_ervas": (668, 276, 782, 370),
        "garrafao_vermelho": (791, 300, 845, 370), "frasco_verde": (834, 276, 860, 322),
        "garrafao_verde": (846, 294, 900, 370), "frasco_roxo": (900, 283, 952, 370),
        "garrafao_amarelo": (953, 283, 1003, 370), "caldeirao_engrenagem": (20, 372, 142, 478),
        "guindaste": (140, 372, 310, 564), "pedras_pilha": (336, 372, 437, 447),
        "caldeirao_preto": (480, 378, 552, 452), "escada_mao": (562, 368, 658, 458),
        "barreira_estacas": (676, 376, 797, 457), "orgao": (798, 398, 922, 572),
        "vitral_colorido": (932, 398, 1006, 567), "pia_pedra": (33, 478, 110, 570),
        "pia_pedra_b": (273, 472, 350, 567), "confessionario": (363, 438, 464, 572),
        "sino_portico": (480, 450, 587, 572), "altar_livro": (638, 453, 762, 572),
    },
    "imagens/Gemini_Generated_Image_bsiw5fbsiw5fbsiw.jpg": {
        "guilhotina": (16, 0, 132, 150), "donzela_ferro": (153, 8, 222, 137),
        "sarcofago_espinhos": (238, 10, 332, 137), "pelourinho": (348, 23, 462, 137),
        "correntes_parede": (478, 26, 584, 114), "gaiola_pendurada": (601, 0, 674, 132),
        "suporte_alabardas": (690, 3, 844, 137), "suporte_alabardas_b": (860, 3, 1014, 137),
        "javali_assado": (18, 150, 240, 232), "barril_torneira": (256, 138, 367, 252),
        "altar_velas": (393, 130, 547, 254), "confessionario_b": (563, 128, 692, 254),
        "atril_livro": (736, 133, 822, 254), "trono_ouro": (860, 136, 940, 254),
        "trono_ouro_b": (940, 138, 1012, 254), "mesa_alquimista": (136, 243, 374, 360),
        "astrolabio": (388, 266, 449, 344), "luneta_b": (456, 253, 545, 357),
        "estante_passagem": (546, 253, 724, 360), "quadro_mapas": (735, 260, 852, 354),
        "globo": (856, 260, 930, 354), "globo_b": (938, 260, 1012, 354),
        "cama_dossel": (10, 310, 167, 477), "banheira": (198, 360, 320, 462),
        "biombo_verde": (348, 361, 514, 464), "banquinho_verde": (538, 388, 582, 464),
        "penteadeira": (604, 356, 690, 464), "penteadeira_b": (716, 356, 802, 467),
        "bau_tecidos": (816, 366, 927, 467), "relogio_pendulo_b": (948, 348, 1012, 472),
        "vaso_arbusto": (13, 468, 80, 567), "vaso_laranjeira": (93, 488, 157, 567),
        "vaso_lavanda": (170, 468, 237, 567), "muro_hera": (253, 468, 352, 567),
        "muro_hera_b": (360, 468, 457, 567), "chafariz": (481, 463, 602, 567),
        "poco_manivela": (628, 463, 740, 570), "gongo": (766, 463, 857, 570),
        "relogio_sol": (878, 478, 1012, 567),
    },
}


def nome_livre(nome, usados):
    """'_b' marca a segunda peça igual na folha; nomes já existentes ganham _2, _3..."""
    raiz = nome[:-2] if nome.endswith("_b") else nome
    cands = ([] if nome.endswith("_b") else [raiz]) + [f"{raiz}_{i}" for i in range(2, 20)]
    n = next(c for c in cands if c not in usados and not os.path.exists(f"{OUT}/{c}.png"))
    usados.add(n)
    return n


def main():
    log_path = "Tools/padronizar_64.json"
    log = json.load(open(log_path, encoding="utf-8")) if os.path.exists(log_path) else {}
    usados, feitos = set(), []
    for src, caixas in FOLHAS.items():
        big = Image.open(src).convert("RGB")
        f = big.width / REF_W
        small = np.array(big.resize((REF_W, round(big.height / f)), Image.BOX)).astype(int)
        A = np.array(big).astype(int)
        bg = np.median(np.concatenate([small[:3].reshape(-1, 3), small[-3:].reshape(-1, 3)]), 0)
        fg_s = np.abs(small - bg).max(2) > 26
        fg_b = np.abs(A - bg).max(2) > 26
        comps = [c for c in components(grow(fg_s, 1)) if fg_s[c].sum() > 12]
        centros = [(c[1].mean(), c[0].mean()) for c in comps]
        for nome, (x0, y0, x1, y1) in caixas.items():
            m = np.zeros_like(fg_s)
            if nome in CORTE:
                m[y0:y1, x0:x1] = fg_s[y0:y1, x0:x1]
                # sem os pedacinhos de vizinhos que caem dentro do retângulo
                partes = components(grow(m, 1))
                maior = max(m[c].sum() for c in partes)
                for c in partes:
                    if m[c].sum() < maior * 0.08:
                        m[c] = False
            for c, (cx, cy) in zip(comps, centros):
                if nome not in CORTE and x0 <= cx < x1 and y0 <= cy < y1:
                    m[c] = True
            if not m.any():
                print("  vazio:", nome)
                continue
            # máscara pequena -> grande (vizinhança de 1 px na escala pequena) e pixels de verdade
            mb = np.array(Image.fromarray(grow(m, 1).astype(np.uint8) * 255).resize(big.size, Image.NEAREST)) > 0
            keep = mb & fg_b
            ys, xs = np.where(keep)
            yy0, yy1, xx0, xx1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
            rgb = A[yy0:yy1, xx0:xx1].astype(float)
            k = keep[yy0:yy1, xx0:xx1].astype(float)
            w = max(1, round((xx1 - xx0) / f * ESCALA))
            h = max(1, round((yy1 - yy0) / f * ESCALA))
            pre = Image.fromarray((rgb * k[:, :, None]).astype(np.uint8)).resize((w, h), Image.LANCZOS)
            al = np.array(Image.fromarray((k * 255).astype(np.uint8)).resize((w, h), Image.LANCZOS)).astype(float)
            out = np.clip(np.array(pre).astype(float) * 255 / np.maximum(al, 1)[:, :, None], 0, 255)
            img = np.dstack([out, np.where(al >= 120, 255, 0)]).astype(np.uint8)
            final = nome_livre(nome, usados)
            path = f"{OUT}/{final}.png"
            Image.fromarray(img, "RGBA").save(path)
            log[path] = {"regra": "folhas 20/10, já em 64", "antes": [w, h]}
            feitos.append(final)
    json.dump(log, open(log_path, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    print(len(feitos), "peças")
    print(" ".join(feitos))


if __name__ == "__main__":
    main()
