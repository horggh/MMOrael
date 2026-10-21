"""Separa as peças de borda do mapa (imagens/bordas.jpg) e monta as versões longas.

Folha (5 linhas):
  1: gramados (barranco com grama em cima, em V, visão 3/4)       -> gramado_1..4
  2: paredões de pedra (em V)                                       -> paredao_1..4
  3: cachoeiras (3) e pedras molhadas sem água (1)                  -> cachoeira_1..4
  4: barrancos escuros com raízes (retos, a beira de baixo do mapa) -> barranco_1..4
  5: pedra, 3 troncos, 2 muretas de pedra, 2 cercas de madeira

Cada peça sai recortada (sem sobra) em Assets/Art/Sprites/Environment/Borda/.
As retas viram também ladrilhos que emendam (pedaço que repete) e versões longas prontas:
  barranco_ladrilho / barranco_longo, cerca_ladrilho / cerca_longa,
  mureta_ladrilho / mureta_longa.
Os ladrilhos servem para o SpriteRenderer em modo "Tiled": um objeto só, do comprimento
que quiser (RaelMMO > Borda no menu do Unity).

Uso: python Tools/extract_bordas.py
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_pixelart import resample, background_mask, drop_specks, quantize  # noqa: E402
from extract_boss_pirata import components  # noqa: E402
from extract_frames import segments  # noqa: E402

SRC = "imagens/bordas.jpg"
OUT = "Assets/Art/Sprites/Environment/Borda"
GRID = 512
# Linhas na grade nativa (a 3 e a 4 se encostam: a água cai em cima do barranco).
ROWS = [(0, 61), (62, 119), (120, 178), (179, 233), (234, 285)]
ROW_NAMES = [
    ["gramado_1", "gramado_2", "gramado_3", "gramado_4"],
    ["paredao_1", "paredao_2", "paredao_3", "paredao_4"],
    ["cachoeira_1", "cachoeira_2", "cachoeira_3", "cachoeira_4"],
    ["barranco_1", "barranco_2", "barranco_3", "barranco_4"],
    ["pedra", "tronco_1", "tronco_2", "tronco_3", "mureta_1", "mureta_2", "cerca_1", "cerca_2"],
]


def save(rgba, name):
    alpha = rgba[:, :, 3]
    ys, xs = np.where(alpha > 0)
    rgba = rgba[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    Image.fromarray(rgba.astype(np.uint8), "RGBA").save(os.path.join(OUT, name + ".png"))
    return rgba


def piece(grid, mask):
    rgb = np.where(mask[:, :, None], grid, 0).astype(int)
    alpha = np.where(mask, 255, 0).astype(np.uint8)
    snapped, _ = quantize(rgb, alpha, dist=16)
    return np.dstack([snapped, alpha])


def seam_tile(img, overlap=8):
    """Faz um pedaço que emenda com ele mesmo na horizontal: a ponta direita (overlap px)
    se funde com a esquerda, então ladrilho + ladrilho não mostra corte."""
    h, w = img.shape[:2]
    core = img[:, :w - overlap].astype(float)
    tail = img[:, w - overlap:].astype(float)
    for i in range(overlap):
        t = (i + 0.5) / overlap
        a_core, a_tail = core[:, i, 3:4] / 255, tail[:, i, 3:4] / 255
        mix_a = a_core * t + a_tail * (1 - t)
        rgb = np.where(mix_a > 0, (core[:, i, :3] * a_core * t + tail[:, i, :3] * a_tail * (1 - t)) /
                       np.maximum(mix_a, 1e-6), 0)
        core[:, i, :3] = rgb
        core[:, i, 3] = np.where(mix_a[:, 0] > 0.5, 255, 0)
    return core.round().astype(np.uint8)


def repeat(tile, n):
    return np.concatenate([tile] * n, axis=1)


def bottom_align(imgs):
    """Mesma altura para todos, alinhados pela base (as peças saem recortadas)."""
    h = max(i.shape[0] for i in imgs)
    out = []
    for i in imgs:
        pad = np.zeros((h, i.shape[1], 4), np.uint8)
        pad[h - i.shape[0]:] = i
        out.append(pad)
    return out


def join_blend(imgs, overlap):
    """Junta as peças lado a lado, cada uma entrando `overlap` px por cima da anterior
    com transição suave: some a linha reta que aparecia na emenda."""
    imgs = bottom_align(imgs)
    out = imgs[0].astype(float)
    for nxt in imgs[1:]:
        nxt = nxt.astype(float)
        a, b = out[:, -overlap:], nxt[:, :overlap]
        mixed = np.zeros_like(a)
        for i in range(overlap):
            t = (i + 0.5) / overlap
            aa, ab = a[:, i, 3:4] / 255 * (1 - t), b[:, i, 3:4] / 255 * t
            al = aa + ab
            mixed[:, i, :3] = np.where(al > 0, (a[:, i, :3] * aa + b[:, i, :3] * ab) / np.maximum(al, 1e-6), 0)
            mixed[:, i, 3] = np.where(al[:, 0] > 0.5, 255, 0)
        out = np.concatenate([out[:, :-overlap], mixed, nxt[:, overlap:]], axis=1)
    return out.round().astype(np.uint8)


def main():
    os.makedirs(OUT, exist_ok=True)
    a = np.array(Image.open(SRC).convert("RGB")).astype(float)
    grid = resample(a, GRID)
    fg = drop_specks(~background_mask(grid))
    H, W = fg.shape
    pieces = {}
    for (y0, y1), names in zip(ROWS, ROW_NAMES):
        band = np.zeros_like(fg)
        band[y0:y1 + 1] = fg[y0:y1 + 1]
        if len(names) == 4:
            cw = W / 4
            cells = [(int(round(k * cw)), int(round((k + 1) * cw)) - 1) for k in range(4)]
        else:
            cells = segments(band.any(0), min_gap=2)
        if len(cells) != len(names):
            print(f"  aviso: linha {names[0]}: {len(cells)} peças, esperava {len(names)}")
        for (s0, s1), name in zip(cells, names):
            m = np.zeros_like(fg)
            m[:, s0:s1 + 1] = band[:, s0:s1 + 1]
            # Só o maior grupo e o que está colado nele (some o que vazou da vizinha).
            parts = components(m)
            main_part = max(parts, key=lambda c: len(c[0]))
            for cy, cx in parts:
                if cy is not main_part[0] and len(cy) < 40:
                    m[cy, cx] = False
            pieces[name] = save(piece(grid, m), name)
            print(f"  {name}.png {pieces[name].shape[1]}x{pieces[name].shape[0]}")

    # Barranco: os 4 emendados com transição (varia sem repetir a cada peça); o ladrilho
    # também emenda com ele mesmo nas pontas. A versão longa repete o ladrilho 3 vezes.
    strip = join_blend([pieces[f"barranco_{i}"] for i in range(1, 5)], 8)
    tile = seam_tile(strip, 10)
    save(tile, "barranco_ladrilho")
    save(repeat(tile, 3), "barranco_longo")

    # Cerca: mourões a cada 17 px. Ladrilho = de um mourão até antes do próximo (repete
    # sem emenda). Longa = ponta esquerda (pilar alto) + ladrilhos + ponta direita.
    c1, c2 = bottom_align([pieces["cerca_1"], pieces["cerca_2"]])
    unit = c2[:, 18:35]
    rail = unit[-22:]
    Image.fromarray(rail, "RGBA").save(os.path.join(OUT, "cerca_ladrilho.png"))
    save(np.concatenate([c1[:, :28]] + [unit] * 8 + [c2[:, 35:]], axis=1), "cerca_longa")
    print(f"  cerca_ladrilho.png {rail.shape[1]}x{rail.shape[0]}")

    # Mureta: pedras sem padrão fixo; o trecho reto vira ladrilho emendado nas pontas.
    m1, m2 = bottom_align([pieces["mureta_1"], pieces["mureta_2"]])
    straight = m2[:, 6:44]
    mtile = seam_tile(straight[-20:], 8)
    Image.fromarray(mtile, "RGBA").save(os.path.join(OUT, "mureta_ladrilho.png"))
    middle = seam_tile(straight, 8)
    save(join_blend([m1[:, :30]] + [middle] * 6 + [m2[:, 20:]], 4), "mureta_longa")
    print(f"  mureta_ladrilho.png {mtile.shape[1]}x{mtile.shape[0]}")


if __name__ == "__main__":
    main()
