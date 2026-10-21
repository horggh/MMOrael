"""MVP cavaleiro montado de sangue (imagens/mvp_cavaleiro_sangue.webp: parado e andando em
8 direções, ataque em 6 tempos, habilidade em 4, dano em 3, morte e 4 efeitos; fundo
transparente com os nomes embaixo e sombras pretas no chão).
Saída: Assets/Art/Sprites/Monsters/MVP/Resources/CavaleiroSangue/<animação>.png (tiras de
quadros do mesmo tamanho, pés na base).

Os quadros encostam uns nos outros (lança, efeitos): cada pixel vai para o quadro do nome
mais perto, dentro do grupo da linha. A sombra preta no chão sai.
Escala: ~80 px de altura (32 PPU: maior que o personagem, é montado).

Uso: python Tools/extract_mvp_cavaleiro.py
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_boss_pirata import components  # noqa: E402

SRC = "imagens/mvp_cavaleiro_sangue.webp"
OUT = "Assets/Art/Sprites/Monsters/MVP/Resources/CavaleiroSangue"
SCALE = 0.62
DIRS = ["frente", "frente_direita", "direita", "costas_direita", "costas", "costas_esquerda", "esquerda", "frente_esquerda"]
# (linha y0, y1, x0, x1, meios dos quadros, nomes)
GROUPS = [
    (28, 159, 0, 1125, [50, 195, 330, 480, 622, 770, 915, 1060], ["parado_" + d for d in DIRS]),
    (218, 338, 0, 1125, [63, 195, 335, 480, 625, 770, 915, 1060], ["andar_" + d for d in DIRS]),
    (402, 534, 0, 662, [50, 163, 285, 405, 513, 615], ["ataque_%d" % i for i in range(1, 7)]),
    (402, 534, 662, 1125, [725, 840, 945, 1065], ["habilidade_%d" % i for i in range(1, 5)]),
    (596, 715, 0, 350, [60, 165, 275], ["dano_%d" % i for i in range(1, 4)]),
    (596, 715, 350, 670, [510], ["morte"]),
    (596, 715, 670, 1125, [735, 840, 935, 1050], ["efeito_%d" % i for i in range(1, 5)]),
]
STRIPS = {
    "ataque": ["ataque_%d" % i for i in range(1, 7)],
    "habilidade": ["habilidade_%d" % i for i in range(1, 5)],
    "dano": ["dano_%d" % i for i in range(1, 4)],
}


def scaled(rgba, keep):
    ys, xs = np.where(keep)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    rgb, k = rgba[y0:y1, x0:x1, :3].astype(float), keep[y0:y1, x0:x1].astype(float)
    w, h = max(1, round((x1 - x0) * SCALE)), max(1, round((y1 - y0) * SCALE))
    pre = Image.fromarray((rgb * k[:, :, None]).astype(np.uint8)).resize((w, h), Image.LANCZOS)
    al = np.array(Image.fromarray((k * 255).astype(np.uint8)).resize((w, h), Image.LANCZOS)).astype(float)
    out = np.clip(np.array(pre).astype(float) * 255 / np.maximum(al, 1)[:, :, None], 0, 255)
    return np.dstack([out, np.where(al >= 128, 255, 0)]).astype(np.uint8)


def strip(imgs):
    fw = max(i.shape[1] for i in imgs) + 2
    fh = max(i.shape[0] for i in imgs) + 2
    s = np.zeros((fh, fw * len(imgs), 4), np.uint8)
    for k, im in enumerate(imgs):
        h, w = im.shape[:2]
        ox, oy = k * fw + (fw - w) // 2, fh - 1 - h
        s[oy:oy + h, ox:ox + w] = im
    return s


def main():
    os.makedirs(OUT, exist_ok=True)
    a = np.array(Image.open(SRC).convert("RGBA")).astype(int)
    rgb = a[:, :, :3]
    shadow = (rgb.max(2) <= 75) & ((rgb.max(2) - rgb.min(2)) <= 18)
    fg = (a[:, :, 3] >= 128) & ~shadow
    frames = {}
    for y0, y1, x0, x1, centers, names in GROUPS:
        sub = np.zeros_like(fg)
        sub[y0:y1, x0:x1] = fg[y0:y1, x0:x1]
        cs = np.array(centers)
        owner = np.argmin(np.abs(np.arange(fg.shape[1])[:, None] - cs[None, :]), 1)
        # Pedaço solto (arco do golpe, efeito) vai inteiro para o quadro mais perto do meio
        # dele; só os grudados em mais de um quadro (dois corpos encostados) são cortados.
        label = np.full(fg.shape, -1)
        for cy, cx in components(sub):
            inside = [k for k, c in enumerate(cs) if cx.min() <= c <= cx.max()]
            if len(inside) <= 1:
                label[cy, cx] = int(np.argmin(np.abs(cs - cx.mean())))
            else:
                label[cy, cx] = owner[cx]
        for k, name in enumerate(names):
            m = label == k
            comps = components(m)
            big = max(len(c[0]) for c in comps)
            keep = np.zeros_like(m)
            for cy, cx in comps:
                if len(cy) >= 0.02 * big:
                    keep[cy, cx] = True
            frames[name] = scaled(a, keep)
    # Parado e andar: 1 quadro cada direção; andar = parado + passo (2 quadros).
    for d in DIRS:
        Image.fromarray(strip([frames["parado_" + d]]), "RGBA").save(f"{OUT}/parado_{d}.png")
        Image.fromarray(strip([frames["parado_" + d], frames["andar_" + d]]), "RGBA").save(f"{OUT}/andar_{d}.png")
    for name, parts in STRIPS.items():
        Image.fromarray(strip([frames[p] for p in parts]), "RGBA").save(f"{OUT}/{name}.png")
    Image.fromarray(strip([frames["morte"]]), "RGBA").save(f"{OUT}/morte.png")
    for i in range(1, 5):
        Image.fromarray(strip([frames["efeito_%d" % i]]), "RGBA").save(f"{OUT}/efeito_{i}.png")
    print(len(os.listdir(OUT)), "arquivos")


if __name__ == "__main__":
    main()
