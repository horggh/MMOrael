"""Decoração de loja/casa (imagens/decoracao-loja.jpg): cada peça num sprite, na mesma escala
do prédio comercial e da casa (a folha é do mesmo estilo: 0,384 da imagem = 32 px/unidade).

Fundo xadrez pela posição dos quadrados (extract_casa.background). As peças são os grupos
de pixels, juntados por linha (pelo centro) e por coluna (os que se sobrepõem na horizontal
são a mesma peça: a balança tem os pratos soltos, a mesa vem com a cadeira).
Saída: Assets/Art/Sprites/Environment/Decoracao/<nome>.png
Uso: python Tools/extract_decoracao.py
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_casa import background, drop_specks  # noqa: E402
from extract_boss_pirata import components  # noqa: E402

SRC = "imagens/decoracao-loja.jpg"
OUT = "Assets/Art/Sprites/Environment/Decoracao"
SCALE = 192 / 500        # a mesma redução do prédio comercial
ROW_CENTERS = [110, 260, 380, 505]
NAMES = [
    ["balcao", "estante_mantimentos", "estante_pocoes", "mesa_cadeira", "estante_livros", "cama", "guarda_roupa"],
    ["caixa_macas", "caixa_legumes", "caixote_1", "caixote_2", "caixotes_empilhados", "bau_ferro",
     "caixote_3", "caixote_4", "caixote_5"],
    ["barril_1", "barril_2", "barril_3", "barril_4", "barril_5", "barril_6", "barril_aberto", "balde_agua",
     "lenha_pilha", "lenha"],
    ["saco_1", "saco_2", "saco_graos", "sacos", "cesto_tomates", "cesto_batatas", "cesto_legumes", "balanca",
     "presunto", "linguicas", "linguica"],
]


def items_of_row(comps):
    """Junta os grupos que se sobrepõem na horizontal (folga de 2 px)."""
    comps = sorted(comps, key=lambda c: c[1].min())
    items = []
    for ys, xs in comps:
        if items and xs.min() <= items[-1]["x1"] + 2:
            it = items[-1]
            it["ys"] = np.concatenate([it["ys"], ys]); it["xs"] = np.concatenate([it["xs"], xs])
            it["x1"] = max(it["x1"], xs.max())
        else:
            items.append({"ys": ys, "xs": xs, "x1": xs.max()})
    return items


def split_at_gap(item):
    """As duas estantes encostadas viram duas peças, cortadas na coluna mais vazia do meio."""
    ys, xs = item["ys"], item["xs"]
    x0, x1 = xs.min(), xs.max()
    cols = np.bincount(xs - x0, minlength=x1 - x0 + 1)
    w = x1 - x0
    cut = x0 + w // 3 + int(np.argmin(cols[w // 3: 2 * w // 3]))
    left, right = xs < cut, xs >= cut
    return [{"ys": ys[left], "xs": xs[left]}, {"ys": ys[right], "xs": xs[right]}]


def save(a, it, name):
    ys, xs = it["ys"], it["xs"]
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
    h, w = y1 - y0 + 1, x1 - x0 + 1
    rgb = a[y0:y1 + 1, x0:x1 + 1]
    alpha = np.zeros((h, w))
    alpha[ys - y0, xs - x0] = 1
    W, H = max(1, round(w * SCALE)), max(1, round(h * SCALE))
    pre = Image.fromarray((rgb * alpha[:, :, None]).clip(0, 255).astype(np.uint8)).resize((W, H), Image.LANCZOS)
    al = np.array(Image.fromarray((alpha * 255).astype(np.uint8)).resize((W, H), Image.LANCZOS)).astype(float) / 255
    col = np.array(pre).astype(float) / np.maximum(al[:, :, None], 1e-3)
    opaque = al > 0.5
    # Pixel solto (sobra do xadrez): sem nenhum vizinho opaco em volta, sai.
    n = np.zeros(opaque.shape, int)
    pad = np.pad(opaque, 1)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            if dy or dx:
                n += pad[1 + dy:1 + dy + H, 1 + dx:1 + dx + W]
    opaque &= n >= 2
    out = np.dstack([col.clip(0, 255).astype(np.uint8), np.where(opaque, 255, 0).astype(np.uint8)])
    Image.fromarray(out, "RGBA").save(os.path.join(OUT, name + ".png"))
    return W, H


def main():
    a = np.array(Image.open(SRC).convert("RGB")).astype(float)
    fg = drop_specks(~background(a), 150)
    rows = [[] for _ in ROW_CENTERS]
    for ys, xs in components(fg):
        if len(ys) < 60:
            continue
        r = int(np.argmin([abs(ys.mean() - c) for c in ROW_CENTERS]))
        rows[r].append((ys, xs))
    os.makedirs(OUT, exist_ok=True)
    for r, names in enumerate(NAMES):
        items = items_of_row(rows[r])
        if r == 0 and len(items) == len(names) - 1:
            items = items[:1] + split_at_gap(items[1]) + items[2:]
        if len(items) != len(names):
            print(f"  aviso: linha {r + 1}: {len(items)} peças, esperava {len(names)}")
        for it, name in zip(items, names):
            W, H = save(a, it, name)
            print(f"  {name}.png {W}x{H}")


if __name__ == "__main__":
    main()
