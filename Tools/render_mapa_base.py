"""Fundo do mapa só com a BASE: grama, trilhas de terra, rio e as duas pontes, sem morros
nem árvores (essas entram como objetos de verdade na cena, por cima).

Planta: imagens/mapa-base-cruzamento.jpg. Cada ponto é classificado (grama, trilha, água,
ponte) e o mapa é pintado de novo a 32 px por unidade, com as mesmas texturas do
render_mapa_cruzamento.py (grama da cena, areia, água com espuma na margem). Pedras e
tufos da planta são ignorados. As pontes vêm da planta, ampliadas com nitidez.

Saída: Assets/Art/Sprites/Environment/Mapas/cruzamento_fundo.png (o menu
RaelMMO > Mapa > Colocar fundo do Cruzamento põe na cena, do tamanho do chão).
Uso: python Tools/render_mapa_base.py [largura_unidades altura_unidades] [--grama-transparente]
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

sys.path.insert(0, os.path.dirname(__file__))
from gen_fundo_cruzamento import remove_red_dots  # noqa: E402
from render_mapa_cruzamento import (shift_or, tile, load, shades_texture, upscale_labels,  # noqa: E402
                                    GRASS, SAND, WATER, rng)

SRC = "imagens/mapa-base-cruzamento.jpg"
BOX = (68, 7, 956, 572)          # o mapa, sem o xadrez em volta
OUT = "Assets/Art/Sprites/Environment/Mapas/cruzamento_fundo.png"
PPU = 32
# Pontes na planta (coordenadas do recorte): pedra (diagonal) e madeira.
BRIDGES = [(352, 150, 430, 228), (512, 118, 545, 172)]


def classify(c):
    a = c.astype(int)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    water = (b > r + 40) & (b > 90)
    sand = (r > 175) & (g > 140) & (r - b > 55) & (r > g)
    lab = np.full(r.shape, GRASS)
    lab[sand] = SAND
    lab[water] = WATER
    # O resto (contorno do rio, pedras, tufos, pontes): vira o que mais tem em volta.
    other = ~water & ~sand & ~((g > r + 15) & (g > b + 30))
    for _ in range(4):
        votes = [np.zeros(lab.shape) for _ in range(3)]
        for dy in (-2, -1, 0, 1, 2):
            for dx in (-2, -1, 0, 1, 2):
                sh = np.roll(np.roll(lab, dy, 0), dx, 1)
                ok = ~np.roll(np.roll(other, dy, 0), dx, 1)
                for k in range(3):
                    votes[k] += (sh == k) & ok
        best = np.argmax(np.stack(votes), 0)
        lab[other] = best[other]
    bridge = np.zeros(lab.shape, bool)
    for x0, y0, x1, y1 in BRIDGES:
        bridge[y0:y1, x0:x1] = other[y0:y1, x0:x1]
    return lab, bridge


def main():
    nums = [a for a in sys.argv[1:] if not a.startswith("--")]
    wu = float(nums[0]) if len(nums) > 1 else 74.18
    hu = float(nums[1]) if len(nums) > 1 else 60.91
    W, H = int(round(wu * PPU)), int(round(hu * PPU))

    src = np.array(Image.open(SRC).convert("RGB").crop(BOX))
    clean, dots = remove_red_dots(src)
    ch, cw = clean.shape[:2]
    lab_small, bridge_small = classify(clean)
    lab = upscale_labels(lab_small, W, H, 0.3)
    print(f"  planta {cw}x{ch} -> {W}x{H}")

    # Claridade da planta (manchas mais claras e mais escuras na grama).
    ref = np.array(Image.fromarray(clean).resize((W, H), Image.BILINEAR)
                   .filter(ImageFilter.GaussianBlur(18))).astype(float)
    grass_ref = clean[lab_small == GRASS].mean()
    tone = np.clip(ref.mean(2, keepdims=True) / grass_ref, 0.86, 1.12)

    grass = tile(load("grama_base.png")[:, :, :3], W, H) * tone
    sand = tile(shades_texture(64, [(206, 176, 104), (222, 194, 118), (233, 207, 130), (242, 221, 150)],
                               3, 14, (176, 146, 92)), W, H)
    water = tile(shades_texture(64, [(30, 60, 118), (38, 74, 134), (46, 86, 148)], 4), W, H)
    for _ in range(W * H // 900):
        y, x = rng.integers(0, H), rng.integers(0, W - 8)
        if lab[y, x] == WATER:
            water[y, x:x + rng.integers(3, 8)] = (86, 128, 186)

    canvas = grass.copy()
    canvas[lab == SAND] = sand[lab == SAND]
    canvas[lab == WATER] = water[lab == WATER]
    # Com --grama-transparente, onde é grama o fundo fica transparente (aparece o chão da
    # cena por baixo) e na grama só fica a sombra da beira (preta, translúcida). Sem ela, a
    # grama vem pintada no próprio fundo.
    clear_grass = "--grama-transparente" in sys.argv
    alpha = np.where((lab == GRASS) & clear_grass, 0.0, 1.0)
    shade = np.zeros((H, W))

    land = lab != WATER
    is_sand = lab == SAND
    shade[shift_or(is_sand, 2) & (lab == GRASS)] = 0.16
    canvas[shift_or(is_sand, 2) & (lab == GRASS)] *= 0.86
    canvas[is_sand & shift_or(~is_sand & land, 1)] *= 0.93
    foam = (lab == WATER) & shift_or(land, 2)
    deep = (lab == WATER) & shift_or(land, 6) & ~foam
    canvas[foam] = canvas[foam] * 0.4 + np.array([196, 222, 236]) * 0.6
    canvas[deep] *= 0.85
    bank = land & shift_or(lab == WATER, 3)
    canvas[bank & (lab != GRASS)] *= 0.78
    shade[bank & (lab == GRASS)] = 0.24
    canvas[bank & (lab == GRASS)] *= 0.78

    # Pontes: a arte da planta ampliada com nitidez, com sombra na água embaixo.
    sharp = np.array(Image.fromarray(clean).resize((W, H), Image.LANCZOS)
                     .filter(ImageFilter.UnsharpMask(2, 140, 2))).astype(float)
    bridge = np.array(Image.fromarray((bridge_small * 255).astype(np.uint8)).resize((W, H), Image.NEAREST)) > 0
    shadow = np.zeros_like(bridge)
    shadow[6:] = bridge[:-6]
    canvas[shadow & ~bridge & (lab == WATER)] *= 0.7
    canvas[bridge] = sharp[bridge]
    alpha[bridge] = 1.0

    # Grama: só a sombra translúcida (cor preta); o resto, opaco.
    grass_px = alpha == 0
    canvas[grass_px] = 0
    alpha[grass_px] = shade[grass_px]
    out = np.dstack([canvas.clip(0, 255).round().astype(np.uint8), (alpha * 255).round().astype(np.uint8)])
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    Image.fromarray(out, "RGBA").save(OUT)
    print(f"  {OUT}: {W}x{H}")
    if dots:
        print("  saídas (bolinhas vermelhas), em unidades a partir do canto de baixo à esquerda:")
        for cy, cx in dots:
            print(f"    x {cx / cw * wu:5.1f}   y {(1 - cy / ch) * hu:5.1f}")


if __name__ == "__main__":
    main()
