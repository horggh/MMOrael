"""Separa uma folha de decoração (fundo xadrez) em peças, na escala dos prédios.

Genérico: cada peça é um grupo de pedaços a até 2 px uns dos outros — a fumaça do
caldeirão, os pratos da balança e a chama da vela ficam com a peça. O xadrez que o recorte
deixa (preso no desenho ou em lasquinhas no contorno) é limpo antes.
A ordem é de cima para baixo e da esquerda para a direita (linhas pela altura da base).
Saída: Assets/Art/Sprites/Environment/Decoracao/<nome>.png

Uso:
  python Tools/extract_decoracao_folha.py <imagem> --listar <pasta>   (numera, para escolher nomes)
  python Tools/extract_decoracao_folha.py <imagem> nome1 nome2 ... [--pasta <pasta>]
      (salva com os nomes, na ordem; "-" pula a peça; NPCs: --pasta Assets/Art/Sprites/NPCs)
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_casa import background, drop_specks, checker_fit  # noqa: E402
from extract_boss_pirata import components  # noqa: E402
from extract_decoracao import save, SCALE  # noqa: E402

OUT = "Assets/Art/Sprites/Environment/Decoracao"


def clean_checker(a, fg):
    """Xadrez que sobra: manchas cinza com os dois tons misturados (presas no desenho) e
    lasquinhas de quadrado grudadas no contorno (cinza no tom do xadrez, encostadas no fundo)."""
    _, _, _, lo, hi = checker_fit(a)
    lum = a.mean(2)
    neutral = (a.max(2) - a.min(2)) < 18
    near_lo, near_hi = np.abs(lum - lo) < 16, np.abs(lum - hi) < 16
    tone = neutral & (near_lo | near_hi)
    for cy, cx in components(fg & tone):
        if len(cy) >= 25 and min(near_lo[cy, cx].sum(), near_hi[cy, cx].sum()) >= 0.15 * len(cy):
            fg[cy, cx] = False
    for _ in range(4):
        bg = ~fg
        touch = np.zeros_like(fg)
        touch[1:] |= bg[:-1]; touch[:-1] |= bg[1:]; touch[:, 1:] |= bg[:, :-1]; touch[:, :-1] |= bg[:, 1:]
        fg &= ~(tone & touch)
    return drop_specks(fg, 150)


def clusters(fg):
    """Peças = grupos de pixels a até 2 px uns dos outros (pedaços quase encostados, como a
    chama da vela e os pratos da balança, ficam juntos; objetos vizinhos, não)."""
    grown = fg.copy()
    grown[1:] |= fg[:-1]; grown[:-1] |= fg[1:]; grown[:, 1:] |= fg[:, :-1]; grown[:, :-1] |= fg[:, 1:]
    items = []
    for ys, xs in components(grown):
        keep = fg[ys, xs]
        if keep.sum() < 40:
            continue
        items.append({"ys": ys[keep], "xs": xs[keep]})
    # Ordem de leitura: linha pela base (arredondada a 40 px), depois esquerda -> direita.
    items.sort(key=lambda it: (round(it["ys"].max() / 40), it["xs"].min()))
    return items


def main():
    src = sys.argv[1]
    a = np.array(Image.open(src).convert("RGB")).astype(float)
    fg = clean_checker(a, drop_specks(~background(a), 150))
    items = clusters(fg)
    if sys.argv[2] == "--listar":
        out = sys.argv[3]
        os.makedirs(out, exist_ok=True)
        import extract_decoracao
        extract_decoracao.OUT = out
        for i, it in enumerate(items, 1):
            W, H = save(a, it, f"{i:02d}")
            print(f"  {i:02d}: {W}x{H}  (x {it['xs'].min()}, base y {it['ys'].max()})")
        return
    names = sys.argv[2:]
    global OUT
    if "--pasta" in names:
        i = names.index("--pasta")
        OUT = names[i + 1]
        names = names[:i] + names[i + 2:]
    if len(names) != len(items):
        print(f"  aviso: {len(items)} peças e {len(names)} nomes")
    import extract_decoracao
    extract_decoracao.OUT = OUT
    os.makedirs(OUT, exist_ok=True)
    for it, name in zip(items, names):
        if name == "-":
            continue
        W, H = save(a, it, name)
        print(f"  {name}.png {W}x{H}")


if __name__ == "__main__":
    main()
