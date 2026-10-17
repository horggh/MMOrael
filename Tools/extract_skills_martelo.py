"""Ícones das habilidades Mover e Recolher (imagens/skills-martelo.jpeg).

Cada ícone já vem com a própria moldura e o xadrez também DENTRO dela (área fechada), que o
extract_pixelart.py não alcança. O fundo é tirado na imagem grande com a mesma remoção das
janelas (posição do xadrez + forma dos quadradinhos) e depois tudo é reduzido para a grade
nativa da arte (257 colunas, ~4 px por pixel). Saída: Assets/Resources/Skills/mover.png e
recolher.png (esquerda e direita).

O interior da moldura recebe um fundo próprio no estilo dos ícones de habilidade do jogo
(degradê radial escuro com um anel sutil), em marrom quente para combinar com a arte;
no Recolher, um brilho dourado fraco no centro acompanha a aura do desenho.

Uso:  python3 Tools/extract_skills_martelo.py
"""
import sys
import numpy as np
from PIL import Image
sys.path.insert(0, "Tools")
import extract_pixelart as EP
import extract_janelas as J

SRC = "imagens/skills-martelo.jpeg"
GRID = 257
NAMES = ["mover", "recolher"]

# Faixas do degradê (do centro para a borda) e brilho dourado opcional.
BANDS = [(84, 62, 46), (70, 51, 39), (58, 42, 33), (47, 34, 28), (37, 27, 23)]
GLOW = {"recolher": (118, 92, 50)}
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0


def background(h, w, name):
    """Fundo em pixel art: degradê radial em poucas cores, com pontilhado nas transições."""
    yy, xx = np.mgrid[0:h, 0:w]
    cy, cx = (h - 1) / 2, (w - 1) / 2
    r = np.hypot(yy - cy, xx - cx) / (min(h, w) / 2)          # 0 no centro, ~1 na borda
    t = np.clip(r, 0, 1) * (len(BANDS) - 1)
    t = t + (BAYER[yy % 4, xx % 4] - 0.5) * 0.9                # pontilhado entre faixas
    idx = np.clip(np.round(t), 0, len(BANDS) - 1).astype(int)
    rgb = np.array(BANDS)[idx].astype(float)
    # Anel sutil, como na moldura padrão dos ícones de habilidade.
    ring = np.abs(r - 0.62) < 0.035
    rgb[ring] = rgb[ring] * 1.18
    if name in GLOW:
        g = np.clip(1 - r / 0.55, 0, 1) ** 1.5
        dither = BAYER[yy % 4, xx % 4]
        mix = (g > dither * 0.9)[:, :, None] * np.minimum(g, 0.6)[:, :, None]
        rgb = rgb * (1 - mix) + np.array(GLOW[name]) * mix
    return np.clip(rgb, 0, 255).astype(np.uint8)


def fill_inside(out, name):
    """Pinta o fundo só nos pixels transparentes DENTRO da moldura (fora fica transparente)."""
    alpha = out[:, :, 3] > 0
    h, w = alpha.shape
    border = [(y, x) for y in range(h) for x in (0, w - 1)] + [(y, x) for x in range(w) for y in (0, h - 1)]
    outside = J.flood(~alpha, border)
    inside = ~alpha & ~outside
    bg = background(h, w, name)
    out[inside, :3] = bg[inside]
    out[inside, 3] = 255
    return out


def main():
    a = np.array(Image.open(SRC).convert("RGB")).astype(float)
    bg, _ = J.background(a)
    fg = J.drop_specks(~bg)
    grid = EP.resample(a, GRID)
    # Máscara reduzida à grade: o pixel da arte é desenho se a maioria do bloco for desenho.
    mask = EP.resample(np.dstack([fg * 255.0] * 3), GRID)[:, :, 0] > 127
    for (x0, x1), name in zip(EP.split_columns(mask), NAMES):
        sub = mask[:, x0:x1 + 1]
        ys = np.where(sub.any(1))[0]
        y0, y1 = ys.min(), ys.max()
        rgb = grid[y0:y1 + 1, x0:x1 + 1].astype(int)
        alpha = np.where(mask[y0:y1 + 1, x0:x1 + 1], 255, 0).astype(np.uint8)
        snapped, _ = EP.quantize(rgb, alpha, 20)
        out = np.dstack([snapped, alpha]).astype(np.uint8)
        out = fill_inside(out, name)
        path = f"Assets/Resources/Skills/{name}.png"
        Image.fromarray(out, "RGBA").save(path)
        print(f"  {path}: {out.shape[1]}x{out.shape[0]}")


if __name__ == "__main__":
    main()
