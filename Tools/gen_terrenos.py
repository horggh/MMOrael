"""Peças de terreno para o pincel de terrenos (grade dupla / "dual grid").

Cada terreno tem 16 peças de 64x64. A grade de desenho fica deslocada meio quadrado da grade
de dados: cada peça cobre os 4 quadrados de dados que se encontram nos cantos dela, e o
número da peça diz quais desses cantos são deste terreno:
    índice = BL*1 + BR*2 + TL*4 + TR*8   (B = baixo, T = cima, L = esquerda, R = direita)
A peça 15 é cheia; a 0 é vazia. Cada terreno é uma camada: os de cima cobrem a borda dos de
baixo (a borda tem uma folga, então não sobra fresta entre terrenos vizinhos).

A borda é a curva do valor dos cantos interpolado, mais um ruído que some na beirada da peça
(assim peças vizinhas sempre se encaixam). Contorno escuro de 2 px na borda; a água tem
espuma clara. A textura vem das texturas de chão (Environment/Texturas), deslocada meio
quadrado para continuar sem emenda de uma peça para a outra.

Saída: Assets/Art/Sprites/Environment/Terrenos/<terreno>/<terreno>_NN.png
       e uma prévia em Tools/terrenos_previa.png (com --previa).
Uso: python Tools/gen_terrenos.py [--previa]
"""
import math
import os
import sys

import numpy as np
from PIL import Image

SRC = "Assets/Art/Sprites/Environment/Texturas"
OUT = "Assets/Art/Sprites/Environment/Terrenos"
N = 64
LIMIAR = 0.45            # < 0,5: cada terreno avança um pouco por baixo do vizinho de cima
RUIDO = 0.16

# (id, textura, estilo da borda) na ordem das camadas: o primeiro fica embaixo.
TERRENOS = [
    ("agua", "agua_campo", "espuma"),
    ("areia", "areia_campo", "contorno"),
    ("lama", "barro", "contorno"),
    ("terra", "terra_batida", "contorno"),
    ("grama", "grama_campo", "grama"),
    ("grama_flores", "grama_flores", "grama"),
    ("mato", "mato_seco", "grama"),
    ("trilha", "cascalho_terra", "contorno"),
    ("cascalho", "cascalho", "contorno"),
    ("pedras", "pedras_redondas", "contorno"),
    ("calcamento", "lajotas", "contorno"),
    ("tabuas", "tabuas", "contorno"),
    ("masmorra", "masmorra_piso_1", "contorno"),
]


def noise(seed):
    """Ruído suave (soma de senos) em [−1, 1], igual para todas as peças do terreno."""
    rng = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:N, 0:N] / N
    n = np.zeros((N, N))
    for k in range(6):
        fx, fy = rng.uniform(2, 7, 2)
        ph = rng.uniform(0, 2 * math.pi, 2)
        n += np.sin(xx * fx * 2 * math.pi + ph[0]) * np.sin(yy * fy * 2 * math.pi + ph[1]) / (k + 1)
    return n / np.abs(n).max()


def mask(idx, nz, dentro=LIMIAR):
    """Cada canto da peça é um quarto (32x32) do quadrado de dados dele: pintar um quadrado
    preenche o quadrado inteiro. Um desfoque arredonda as quinas e o ruído deixa a borda
    natural; os dois respeitam a beirada da peça (a peça vizinha continua igual)."""
    bl, br, tl, tr = idx & 1, (idx >> 1) & 1, (idx >> 2) & 1, (idx >> 3) & 1
    h = N // 2
    q = np.zeros((N, N))
    q[h:, :h] = bl          # linha 0 da imagem = em cima
    q[h:, h:] = br
    q[:h, :h] = tl
    q[:h, h:] = tr
    # Desfoque (caixa 3x, ~gaussiano); a borda repete a coluna/linha da beirada, que é
    # exatamente o que a peça vizinha tem ali (os mesmos dois quadrados de dados).
    r = 7
    v = q
    for _ in range(3):
        pad = np.pad(v, r, mode="edge")
        c = np.cumsum(np.cumsum(pad, 0), 1)
        c = np.pad(c, ((1, 0), (1, 0)))
        k = 2 * r + 1
        v = (c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]) / (k * k)
    u = (np.arange(N) + 0.5) / N
    U, Vv = np.meshgrid(u, u)
    fall = np.sin(math.pi * U) * np.sin(math.pi * Vv)
    return v + RUIDO * nz * fall > dentro


def border(m, w):
    """Pixels do terreno a até w px do lado de fora (a beirada da peça conta como continuação)."""
    pad = np.pad(m, w, mode="edge")
    out = np.zeros_like(m)
    for dy in range(-w, w + 1):
        for dx in range(-w, w + 1):
            if dx * dx + dy * dy > w * w:
                continue
            out |= ~pad[w + dy:w + dy + N, w + dx:w + dx + N]
    return m & out


def textura(nome):
    im = np.array(Image.open(os.path.join(SRC, nome + ".png")).convert("RGB"))[:N, :N].astype(float)
    return np.roll(np.roll(im, N // 2, 0), N // 2, 1)   # meio quadrado: continua entre as peças


def gerar(tid, tex_name, estilo, seed):
    tex = textura(tex_name)
    nz = noise(seed)
    media = tex.reshape(-1, 3).mean(0)
    tiles = []
    for idx in range(16):
        m = mask(idx, nz)
        rgb = tex.copy()
        if idx not in (0, 15):
            if estilo == "espuma":
                b = border(m, 2)
                rgb[b] = rgb[b] * 0.35 + np.array([235, 245, 250]) * 0.65
            elif estilo == "grama":
                b = border(m, 2)
                rgb[b] = rgb[b] * 0.62 + media * 0.0
            else:
                b = border(m, 2)
                rgb[b] = rgb[b] * 0.6
        a = np.where(m, 255, 0).astype(np.uint8)
        img = np.dstack([np.clip(rgb, 0, 255).astype(np.uint8), a])
        tiles.append(img)
    d = os.path.join(OUT, tid)
    os.makedirs(d, exist_ok=True)
    for idx, img in enumerate(tiles):
        Image.fromarray(img, "RGBA").save(os.path.join(d, f"{tid}_{idx:02d}.png"))
    return tiles


def previa(all_tiles):
    """Cada terreno numa ilha irregular sobre a grama, para conferir bordas e cantos."""
    W, H = 10, 7
    rng = np.random.default_rng(3)
    out = []
    for tid, tiles in all_tiles:
        data = np.zeros((H + 1, W + 1), int)
        data[2:5, 2:8] = 1
        data[1, 4:6] = 1
        data[5, 3] = 1
        data[3, 8] = 1
        img = Image.new("RGBA", (W * N, H * N), (70, 120, 60, 255))
        base = [t for t2, t in all_tiles if t2 == "grama"][0][15]
        for y in range(H):
            for x in range(W):
                img.alpha_composite(Image.fromarray(base, "RGBA"), (x * N, y * N))
        for y in range(H):
            for x in range(W):
                # display (x, y) com y para baixo: cantos = dados (x, y+1) BL ... (x+1, y) TR
                bl, br = data[y + 1, x], data[y + 1, x + 1]
                tl, tr = data[y, x], data[y, x + 1]
                idx = bl + br * 2 + tl * 4 + tr * 8
                if idx:
                    img.alpha_composite(Image.fromarray(tiles[idx], "RGBA"), (x * N, y * N))
        out.append(img.resize((W * N // 2, H * N // 2), Image.NEAREST))
    cols = 4
    sheet = Image.new("RGBA", (cols * W * N // 2, ((len(out) + cols - 1) // cols) * H * N // 2))
    for i, im in enumerate(out):
        sheet.alpha_composite(im, ((i % cols) * W * N // 2, (i // cols) * H * N // 2))
    sheet.save("Tools/terrenos_previa.png")


def main():
    all_tiles = []
    for i, (tid, tex, estilo) in enumerate(TERRENOS):
        all_tiles.append((tid, gerar(tid, tex, estilo, 17 + i * 31)))
    # Ordem das camadas para o pincel do Unity (TerrenoPincel): de baixo para cima.
    with open(os.path.join(OUT, "ordem.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(t[0] for t in TERRENOS) + "\n")
    print(len(all_tiles), "terrenos x 16 peças")
    if "--previa" in sys.argv:
        previa(all_tiles)


if __name__ == "__main__":
    main()
