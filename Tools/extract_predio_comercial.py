"""Prédio comercial (loja) de imagens/predio-comercial.jpg, na mesma escala da casa.

O fundo xadrez sai pela posição dos quadrados (extract_casa.background), a fumaça da
chaminé e os grãos soltos saem por tamanho. A arte é reduzida para a largura da casa
(192 px = 6 quadrados da grade, Resources/Buildings/casa_5.png), mantendo a proporção:
assim fica na mesma escala no mapa (32 px por unidade, base nos pés do prédio).
Saída: Assets/Resources/Buildings/loja.png e Assets/Art/Sprites/Environment/Predios/loja.png
(esta é a que vai na cena: RaelMMO > Mapa > Colocar prédio comercial)
Uso: python Tools/extract_predio_comercial.py
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_casa import background, drop_specks  # noqa: E402

SRC = "imagens/predio-comercial.jpg"
OUT = "Assets/Resources/Buildings/loja.png"
OUT_SCENE = "Assets/Art/Sprites/Environment/Predios/loja.png"
WIDTH = 192   # largura da casa


def main():
    a = np.array(Image.open(SRC).convert("RGB")).astype(float)
    fg = drop_specks(~background(a), 400)
    ys, xs = np.where(fg)
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
    rgb = a[y0:y1 + 1, x0:x1 + 1]
    alpha = fg[y0:y1 + 1, x0:x1 + 1].astype(float)
    h, w = alpha.shape
    H = round(h * WIDTH / w)
    # Redução pré-multiplicada (a borda não escurece) e alfa sem meio-termo (pixel art).
    pre = Image.fromarray((rgb * alpha[:, :, None]).clip(0, 255).astype(np.uint8)).resize((WIDTH, H), Image.LANCZOS)
    al = np.array(Image.fromarray((alpha * 255).astype(np.uint8)).resize((WIDTH, H), Image.LANCZOS)).astype(float) / 255
    col = np.array(pre).astype(float) / np.maximum(al[:, :, None], 1e-3)
    out = np.dstack([col.clip(0, 255).astype(np.uint8), np.where(al > 0.5, 255, 0).astype(np.uint8)])
    Image.fromarray(out, "RGBA").save(OUT)
    os.makedirs(os.path.dirname(OUT_SCENE), exist_ok=True)
    Image.fromarray(out, "RGBA").save(OUT_SCENE)
    print(f"  recorte {w}x{h} -> {OUT}: {WIDTH}x{H}")


if __name__ == "__main__":
    main()
