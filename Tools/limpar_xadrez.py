"""Tira sobras do fundo xadrez em volta dos sprites (pontinhos cinza colados no contorno).

Descasca de fora para dentro, algumas vezes, os pixels cinza neutros (luminância média) que
encostam no transparente; o contorno de verdade é escuro e fica. Depois apaga pedacinhos
soltos. Guarda o original em Tools/_backup_xadrez/ na primeira vez.

Uso: python Tools/limpar_xadrez.py <pasta> [<pasta> ...]
"""
import os
import shutil
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_boss_pirata import components  # noqa: E402

BACKUP = "Tools/_backup_xadrez"
PASSES = 3


# Itens com xadrez preso dentro do desenho (a regra pega também metal e pano claros, então
# só vale para estes).
PRESO = {"alambique", "candelabro", "balanca_dourada"}


def sem_xadrez():
    """Peças recortadas de folhas de fundo liso (sem xadrez): não passam pela limpeza."""
    import extract_bancas
    import extract_mercado
    return set(extract_mercado.NAMES) | set(extract_bancas.NAMES) | {"portal_mapa"}


def clean(a, name=""):
    al = a[:, :, 3] > 0
    rgb = a[:, :, :3].astype(int)
    grey = ((rgb.max(2) - rgb.min(2)) <= 12) & (rgb.mean(2) >= 75) & (rgb.mean(2) <= 165)
    removed = 0
    for _ in range(PASSES):
        t = np.pad(~al, 1, constant_values=True)
        near = t[:-2, 1:-1] | t[2:, 1:-1] | t[1:-1, :-2] | t[1:-1, 2:]
        cut = al & grey & near
        if not cut.any():
            break
        al &= ~cut
        removed += int(cut.sum())
    # Xadrez preso dentro do desenho (ex.: entre o cano e o corpo do alambique): manchas
    # cinza neutras com os dois tons do xadrez misturados (metal liso tem um tom só).
    neutral = al & ((rgb.max(2) - rgb.min(2)) <= 14) & (rgb.mean(2) >= 90) & (rgb.mean(2) <= 225)
    for cy, cx in (components(neutral) if name in PRESO else []):
        if len(cy) < 30:
            continue
        lum = rgb[cy, cx].mean(1)
        hi = lum > (lum.min() + lum.max()) / 2
        if lum.max() - lum.min() >= 25 and 0.25 <= hi.mean() <= 0.75:
            al[cy, cx] = False
            removed += len(cy)
    comps = components(al)
    if comps:
        big = max(len(c[0]) for c in comps)
        for cy, cx in comps:
            if len(cy) < 6 and len(cy) < big:
                al[cy, cx] = False
                removed += len(cy)
    out = a.copy()
    out[:, :, 3] = np.where(al, a[:, :, 3], 0)
    return out, removed


def main():
    total = 0
    skip = sem_xadrez()
    for folder in sys.argv[1:]:
        for f in sorted(os.listdir(folder)):
            if not f.endswith(".png"):
                continue
            p = os.path.join(folder, f)
            bk = os.path.join(BACKUP, os.path.basename(folder), f)
            if not os.path.exists(bk):
                os.makedirs(os.path.dirname(bk), exist_ok=True)
                shutil.copy2(p, bk)
            a = np.array(Image.open(bk).convert("RGBA"))   # sempre do original
            if f[:-4] in skip:
                shutil.copy2(bk, p)
                continue
            out, n = clean(a, f[:-4])
            Image.fromarray(out, "RGBA").save(p)
            total += n
    print("pixels removidos:", total)


if __name__ == "__main__":
    main()
