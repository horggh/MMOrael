"""Recorta um prédio de uma imagem com fundo xadrez, na escala dos outros prédios.

Fundo pela posição dos quadrados (extract_casa.background), inclusive o que fica preso
dentro do desenho; fumaça e grãos soltos saem por tamanho. A redução é a mesma do prédio comercial e da decoração (0,384 da imagem), para
todos ficarem na mesma escala no mapa (32 px por unidade).
Saída: Assets/Art/Sprites/Environment/Predios/<nome>.png (janela RaelMMO > Mapa > Decoração,
aba Prédios).
Uso: python Tools/extract_predio.py <imagem> <nome> [escala]
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_casa import background, drop_specks, checker_fit  # noqa: E402
from extract_boss_pirata import components  # noqa: E402

OUT = "Assets/Art/Sprites/Environment/Predios"


def main():
    src, name = sys.argv[1], sys.argv[2]
    scale = float(sys.argv[3]) if len(sys.argv) > 3 else 192 / 500
    a = np.array(Image.open(src).convert("RGB")).astype(float)
    fg = drop_specks(~background(a), 400)
    # Fundo preso dentro do desenho (entre postes, debaixo de telhadinho): mancha cinza com
    # OS DOIS tons do xadrez misturados vira transparente. Não dá para usar a posição dos
    # quadrados: em imagem gerada o xadrez "escorrega" ao longo da folha. Pedra cinza tem um
    # tom só e fica.
    p, ox, oy, lo, hi = checker_fit(a)
    lum = a.mean(2)
    neutral = (a.max(2) - a.min(2)) < 18
    near_lo, near_hi = np.abs(lum - lo) < 16, np.abs(lum - hi) < 16
    trapped = fg & neutral & (near_lo | near_hi)
    freed = 0
    for cy, cx in components(trapped):
        if len(cy) < 25:
            continue
        n_lo, n_hi = near_lo[cy, cx].sum(), near_hi[cy, cx].sum()
        if min(n_lo, n_hi) >= 0.15 * len(cy):
            fg[cy, cx] = False
            freed += len(cy)
    # Lasquinhas de quadrado grudadas no contorno (claras e cinza): o que sobrar solto ou
    # fino demais sai no drop_specks abaixo.
    fg = drop_specks(fg, 400)
    print(f"  fundo preso no desenho removido: {freed} px")
    ys, xs = np.where(fg)
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
    rgb = a[y0:y1 + 1, x0:x1 + 1]
    alpha = fg[y0:y1 + 1, x0:x1 + 1].astype(float)
    h, w = alpha.shape
    W, H = round(w * scale), round(h * scale)
    pre = Image.fromarray((rgb * alpha[:, :, None]).clip(0, 255).astype(np.uint8)).resize((W, H), Image.LANCZOS)
    al = np.array(Image.fromarray((alpha * 255).astype(np.uint8)).resize((W, H), Image.LANCZOS)).astype(float) / 255
    col = np.array(pre).astype(float) / np.maximum(al[:, :, None], 1e-3)
    out = np.dstack([col.clip(0, 255).astype(np.uint8), np.where(al > 0.5, 255, 0).astype(np.uint8)])
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray(out, "RGBA").save(os.path.join(OUT, name + ".png"))
    print(f"  recorte {w}x{h} -> {OUT}/{name}.png: {W}x{H} ({W / 32:.1f} x {H / 32:.1f} unidades)")


if __name__ == "__main__":
    main()
