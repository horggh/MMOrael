"""Monstro "Serial Killer" (imagens/serial_killer.webp: andar de frente/direita/esquerda,
ataque com espada em 5 tempos e morte; fundo azul-escuro com os nomes embaixo de cada quadro).
Saída: Assets/Art/Sprites/Monsters/SerialKiller/Resources/SerialKiller/<animação>.png, tiras
horizontais com quadros do mesmo tamanho (pés na base, meio do corpo no meio).

Os quadros se separam pela posição dos nomes (o sangue do ataque encosta no vizinho): cada
pixel vai para o quadro do nome mais perto, na linha dele.
Escala: ~70 px de altura parado (32 PPU: um pouco maior que o personagem).

Uso: python Tools/extract_serial_killer.py
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_boss_pirata import components  # noqa: E402

SRC = "imagens/serial_killer.webp"
OUT = "Assets/Art/Sprites/Monsters/SerialKiller/Resources/SerialKiller"
SCALE = 0.37
ROWS = {   # faixa da linha (sem título e nomes) e meio de cada nome
    "andar": ((68, 262), [120, 295, 472, 685, 885, 1085, 1290, 1474, 1660]),
    "ataque": ((338, 590), [170, 532, 882, 1226, 1588]),
    "morte": ((650, 845), [885]),
}
ANIMS = {"andar_frente": ("andar", [0, 1, 2]), "andar_direita": ("andar", [3, 4, 5]),
         "andar_esquerda": ("andar", [6, 7, 8]), "ataque": ("ataque", [0, 1, 2, 3, 4]),
         "morte": ("morte", [0])}


def frames(a):
    bg = ((a.max(2) - a.min(2)) <= 14) & (a.max(2) <= 70)   # fundo e sombra no chão
    out = {}
    for row, ((y0, y1), centers) in ROWS.items():
        fg = ~bg[y0:y1]
        fg[:, :20] = False
        fg[:, -20:] = False
        xs = np.arange(fg.shape[1])
        owner = np.argmin(np.abs(xs[:, None] - np.array(centers)[None, :]), 1)
        out[row] = []
        for k in range(len(centers)):
            m = fg & (owner == k)[None, :]
            comps = components(m)
            big = max(len(c[0]) for c in comps)
            keep = np.zeros_like(m)
            for cy, cx in comps:
                if len(cy) >= 0.03 * big:
                    keep[cy, cx] = True
            out[row].append((a[y0:y1], keep))
    return out


def scaled(rgb, keep):
    ys, xs = np.where(keep)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    rgb, keep = rgb[y0:y1, x0:x1].astype(float), keep[y0:y1, x0:x1].astype(float)
    w, h = max(1, round((x1 - x0) * SCALE)), max(1, round((y1 - y0) * SCALE))
    pre = Image.fromarray((rgb * keep[:, :, None]).astype(np.uint8)).resize((w, h), Image.LANCZOS)
    al = np.array(Image.fromarray((keep * 255).astype(np.uint8)).resize((w, h), Image.LANCZOS)).astype(float)
    out = np.clip(np.array(pre).astype(float) * 255 / np.maximum(al, 1)[:, :, None], 0, 255)
    return np.dstack([out, np.where(al >= 128, 255, 0)]).astype(np.uint8)


def main():
    os.makedirs(OUT, exist_ok=True)
    a = np.array(Image.open(SRC).convert("RGB")).astype(int)
    fr = frames(a)
    for name, (row, idx) in ANIMS.items():
        imgs = [scaled(*fr[row][i]) for i in idx]
        fw = max(i.shape[1] for i in imgs) + 2
        fh = max(i.shape[0] for i in imgs) + 2
        strip = np.zeros((fh, fw * len(imgs), 4), np.uint8)
        for k, im in enumerate(imgs):
            h, w = im.shape[:2]
            ox, oy = k * fw + (fw - w) // 2, fh - 1 - h
            strip[oy:oy + h, ox:ox + w] = im
        Image.fromarray(strip, "RGBA").save(f"{OUT}/{name}.png")
        print(f"  {name}: {len(imgs)} quadros de {fw}x{fh}")


if __name__ == "__main__":
    main()
