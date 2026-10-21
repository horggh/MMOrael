"""Monta a skin "Andarilho" (cabelo branco, cachecol) a partir das duas folhas:
imagens/skin_andarilho_movimento.jpg e imagens/skin_andarilho_ataque.jpg.

Separa os quadros (Tools/extract_frames.py) e junta cada animação numa tira horizontal em
Assets/Resources/Characters/Skins/andarilho/<animação>.png, no formato que o
SkinDB/KnightAnimator lê. Quadros da mesma folha têm o mesmo tamanho e os pés no mesmo
ponto, alinhados pelo tronco.

Uso: python Tools/build_skin_andarilho.py [--sem-efeito]
  --sem-efeito: tira o arco azul dos golpes (mantém a lâmina).
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_frames import extract, save_frames  # noqa: E402
from extract_boss_pirata import components  # noqa: E402

OUT = "Assets/Resources/Characters/Skins/andarilho"
TMP = os.path.join(OUT, "_quadros")

# Quadros por animação: (linha, número[, "flip"]) — números como na saída de extract_frames.
ANIMS = {
    "mov": {
        "idle_down": [(2, 1)],
        "walk_down": [(1, 1), (1, 2), (1, 3), (1, 4)],
        "walk_side": [(1, 5), (1, 6), (2, 5), (2, 6)],          # olha para a esquerda
        "walk_up": [(1, 8), (1, 9), (2, 8), (2, 9)],
        "run_side": [(3, 1), (3, 2), (3, 3), (3, 4), (4, 1), (4, 2), (4, 3), (4, 4)],  # direita
        "run_up": [(3, 8), (4, 7), (4, 8)],
    },
    "atk": {
        "attack_down": [(1, 2), (1, 3), (1, 4)],
        # Olha para a direita. Na folha, os quadros de preparo olham para a direita (espada
        # para trás) e o do golpe para a esquerda: o golpe entra espelhado, senão parece
        # que ele bate para os dois lados. Preparo agachado, golpe, golpe, volta à guarda.
        "attack_side": [(1, 7), (1, 6, "flip"), (1, 6, "flip"), (1, 5)],
        "attack_up": [(2, 1), (2, 2), (2, 3), (2, 4)],
        "death": [(3, 1), (3, 2), (3, 3), (3, 4), (3, 5), (3, 6)],
    },
}
SHEETS = {
    "mov": ("imagens/skin_andarilho_movimento.jpg", None),
    "atk": ("imagens/skin_andarilho_ataque.jpg", [8, 8, 0]),
}


def strip_slash(im):
    """Tira o arco do golpe de um quadro, mantendo a lâmina.

    Arco e lâmina têm quase a mesma cor (azul-claro) e se tocam, então a separação é pela
    forma: a lâmina é a reta que junta mais pixels azulados (RANSAC); o resto azulado é o
    arco. Depois saem os pedaços que ficaram soltos do corpo (o xadrez que aparecia
    dentro do arco)."""
    a = im.astype(int)
    alpha = a[:, :, 3] > 0
    rgb = a[:, :, :3]
    bluish = alpha & (rgb[:, :, 2] - rgb[:, :, 0] >= 12) & (rgb.mean(2) >= 105)
    ys, xs = np.where(bluish)
    if len(ys) < 30:
        return im
    pts = np.stack([xs, ys], 1).astype(float)
    rng = np.random.default_rng(7)
    best = None
    for _ in range(600):
        i, j = rng.choice(len(pts), 2, replace=False)
        d = pts[j] - pts[i]
        n = np.hypot(*d)
        if n < 6:
            continue
        normal = np.array([-d[1], d[0]]) / n
        inl = np.abs((pts - pts[i]) @ normal) < 1.6
        if best is None or inl.sum() > best.sum():
            best = inl
    blade = np.zeros_like(alpha)
    if best is not None and best.sum() >= 12:
        blade[ys[best], xs[best]] = True
        # Só o trecho contínuo maior da reta (o arco cruza a reta em outros pontos).
        comps = components(blade)
        blade[:] = False
        if comps:
            cy, cx = max(comps, key=lambda c: len(c[0]))
            blade[cy, cx] = True
    out = im.copy()
    out[bluish & ~blade, 3] = 0
    # Pedaços que ficaram soltos do corpo (xadrez de dentro do arco, pontas do arco).
    keep = out[:, :, 3] > 0
    comps = components(keep)
    if comps:
        main = max(comps, key=lambda c: len(c[0]))
        for cy, cx in comps:
            if len(cy) < 60 and cy is not main[0]:
                out[cy, cx, 3] = 0
    return out


NO_EFFECT = "--sem-efeito" in sys.argv

# Quadros de ataque que têm o arco do golpe.
SLASH = {("atk", 1, 3), ("atk", 1, 4), ("atk", 1, 6), ("atk", 2, 2), ("atk", 2, 4)}


def main():
    os.makedirs(OUT, exist_ok=True)
    for key, (src, cells) in SHEETS.items():
        grid, frames, alpha = extract(src, 862, cells, keep_effects=not NO_EFFECT)
        fw, fh, names = save_frames(grid, frames, TMP, key, alpha_map=alpha)
        print(f"{src}: {len(names)} quadros de {fw}x{fh}")
        for anim, picks in ANIMS[key].items():
            strip = np.zeros((fh, fw * len(picks), 4), np.uint8)
            for i, pick in enumerate(picks):
                li, n = pick[0], pick[1]
                im = np.array(Image.open(os.path.join(TMP, f"{key}_L{li}_{n:02d}.png")).convert("RGBA"))
                if "flip" in pick[2:]:
                    im = im[:, ::-1]
                if NO_EFFECT and (key, li, n) in SLASH:
                    im = strip_slash(im)
                strip[:, i * fw:(i + 1) * fw] = im
            Image.fromarray(strip, "RGBA").save(os.path.join(OUT, anim + ".png"))
            print(f"  {anim}.png: {len(picks)} quadros")
    # Os quadros soltos eram só para montar as tiras.
    for f in os.listdir(TMP):
        os.remove(os.path.join(TMP, f))
    os.rmdir(TMP)


if __name__ == "__main__":
    main()
