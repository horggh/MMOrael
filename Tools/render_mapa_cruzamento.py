"""Redesenha o fundo do mapa "Cruzamento" na resolução do jogo (32 px por unidade).

A referência (mapa do meio de imagens/mapas-referencia.jpg) é pequena: ampliar só borra.
Aqui ela vira só a PLANTA: cada ponto é classificado (grama, caminho, água, floresta,
penhasco) e o mapa é pintado de novo com texturas de pixel art na escala certa:
  - grama: a mesma textura do chão da cena (grama_base.png), com os tons claros/escuros
    da referência;
  - caminho: areia com pedrinhas; água: azul com ondinhas e espuma na margem;
  - penhascos: textura de pedra (dos paredões da borda), com luz em cima e sombra embaixo;
  - florestas: os sprites de árvore e arbusto do jogo, carimbados na imagem (nítidos, e
    no jogo continua sendo uma imagem só);
  - pedras soltas: o sprite de pedra; pontes: a arte da referência, ampliada.

Saída: Assets/Art/Sprites/Environment/Mapas/cruzamento_fundo.png (o menu
RaelMMO > Mapa > Colocar fundo do Cruzamento põe na cena).
Uso: python Tools/render_mapa_cruzamento.py [largura_unidades altura_unidades]
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

sys.path.insert(0, os.path.dirname(__file__))
from gen_fundo_cruzamento import remove_red_dots, BOX, SRC  # noqa: E402

OUT = "Assets/Art/Sprites/Environment/Mapas/cruzamento_fundo.png"
ENV = "Assets/Art/Sprites/Environment/"
PPU = 32
# Pontes na planta (coordenadas do recorte): pedra (diagonal) e madeira.
BRIDGES = [(102, 97, 142, 132), (200, 74, 215, 104)]
GRASS, SAND, WATER, FOREST, ROCK, EDGE = range(6)
rng = np.random.default_rng(2026)


# ── utilidades ──

def shift_or(m, r):
    """Dilatação simples (4 vizinhos), r vezes."""
    out = m.copy()
    for _ in range(r):
        g = out.copy()
        g[1:] |= out[:-1]; g[:-1] |= out[1:]
        g[:, 1:] |= out[:, :-1]; g[:, :-1] |= out[:, 1:]
        out = g
    return out


def erode(m, r):
    return ~shift_or(~m, r)


def tile(tex, W, H):
    th, tw = tex.shape[:2]
    return np.tile(tex, (H // th + 1, W // tw + 1, 1))[:H, :W].astype(float)


def load(name):
    return np.array(Image.open(ENV + name).convert("RGBA"))


def up2(img):
    return np.kron(img, np.ones((2, 2, 1), np.uint8))


def wrap_blur(a, r):
    out = a.copy()
    for _ in range(r):
        out = (out + np.roll(out, 1, 0) + np.roll(out, -1, 0) + np.roll(out, 1, 1) + np.roll(out, -1, 1)) / 5
    return out


def shades_texture(size, colors, blur, specks=0, speck_color=None):
    """Textura que emenda: ruído suavizado (com volta nas bordas) em poucos tons."""
    n = wrap_blur(rng.random((size, size)), blur)
    q = np.digitize(n, np.quantile(n, np.linspace(0, 1, len(colors) + 1)[1:-1]))
    tex = np.array(colors, float)[q]
    for _ in range(specks):
        y, x = rng.integers(0, size, 2)
        tex[y:y + 2, x:x + 2] = speck_color
    return tex


# ── planta ──

def classify(c):
    a = c.astype(int)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    lum, sat = a.mean(2), a.max(2) - a.min(2)
    water = (b > r + 25) & (b > g - 10) & (b > 80)
    sand = (r > 180) & (g > 160) & (r - b > 60) & (r >= g - 5)
    dark = (g < 105) & (g > r) & (lum < 85) & ~water
    rock = (np.abs(r - g) < 18) & (b < r) & (lum < 125) & (r > 55) & ~water & ~dark & (sat < 45)
    lab = np.full(r.shape, GRASS)
    lab[sand] = SAND
    lab[water] = WATER
    # Floresta = verde escuro em mancha; as linhas finas escuras são o contorno dos platôs.
    forest = shift_or(erode(dark, 1), 1) & dark
    lab[forest] = FOREST
    lab[dark & ~forest] = EDGE
    lab[rock] = ROCK
    return lab


def upscale_labels(lab, W, H, wobble):
    """Rótulos ampliados com borda orgânica: cada classe vira um campo suave (desfocado e
    ampliado), com um pouco de ruído na areia e na água, e vence a maior."""
    fields = []
    noise = np.array(Image.fromarray((rng.random((H // 24 + 2, W // 24 + 2)) * 255).astype(np.uint8))
                     .resize((W, H), Image.BICUBIC)).astype(float) - 128
    for k in range(6):
        m = Image.fromarray(((lab == k) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.7))
        f = np.array(m.resize((W, H), Image.BILINEAR)).astype(float)
        if k in (SAND, WATER):
            f += noise * wobble
        fields.append(f)
    return np.argmax(np.stack(fields), 0)


# ── pintura ──

def main():
    wu = float(sys.argv[1]) if len(sys.argv) > 2 else 74.18
    hu = float(sys.argv[2]) if len(sys.argv) > 2 else 60.91
    W, H = int(round(wu * PPU)), int(round(hu * PPU))

    src = np.array(Image.open(SRC).convert("RGB").crop(BOX))
    clean, _ = remove_red_dots(src)
    ch, cw = clean.shape[:2]
    sx, sy = W / cw, H / ch
    lab_small = classify(clean)

    # Pedras soltas (manchas pequenas de pedra no meio da grama) viram o sprite de pedra.
    rocks = []
    from extract_boss_pirata import components
    for ys, xs in components(lab_small == ROCK):
        if len(ys) < 40:
            lab_small[ys, xs] = GRASS
            rocks.append(((xs.mean() + 0.5) * sx, (ys.mean() + 0.5) * sy, len(ys)))

    lab = upscale_labels(lab_small, W, H, 0.35)
    print(f"  planta {cw}x{ch} -> {W}x{H}; pedras soltas: {len(rocks)}")

    # Tons da referência (bem desfocados) para a grama não ficar de um verde só.
    ref = np.array(Image.fromarray(clean).resize((W, H), Image.BILINEAR)
                   .filter(ImageFilter.GaussianBlur(14))).astype(float)
    # Só a claridade da referência (não a cor), senão a grama fica verde-neon.
    grass_ref = clean[lab_small == GRASS].mean()
    tone = np.clip(ref.mean(2, keepdims=True) / grass_ref, 0.85, 1.12)

    grass = tile(load("grama_base.png")[:, :, :3], W, H) * tone
    sand = tile(shades_texture(64, [(206, 176, 104), (222, 194, 118), (233, 207, 130), (242, 221, 150)],
                               3, 14, (176, 146, 92)), W, H)
    water = tile(shades_texture(64, [(30, 60, 118), (38, 74, 134), (46, 86, 148)], 4), W, H)
    # Ondinhas: traços claros curtos, espalhados.
    for _ in range(W * H // 900):
        y, x = rng.integers(0, H), rng.integers(0, W - 8)
        if lab[y, x] == WATER:
            water[y, x:x + rng.integers(3, 8)] = (86, 128, 186)
    pared = load("Borda/paredao_2.png")[14:44, 18:56, :3]
    top = np.concatenate([pared, pared[:, ::-1]], 1)
    rock_tex = np.concatenate([top, top[::-1]], 0)
    rock = tile(rock_tex, W, H) * 0.95

    canvas = grass.copy()
    canvas[lab == SAND] = sand[lab == SAND]
    canvas[lab == WATER] = water[lab == WATER]
    canvas[lab == FOREST] = grass[lab == FOREST] * 0.78
    canvas[lab == ROCK] = rock[lab == ROCK]
    canvas[lab == EDGE] = (38, 44, 30)

    land = lab != WATER
    is_sand, is_rock = lab == SAND, lab == ROCK
    # Beira do caminho: grama mais escura colada na areia, areia um pouco mais escura na borda.
    canvas[shift_or(is_sand, 2) & (lab == GRASS)] *= 0.86
    canvas[is_sand & shift_or(~is_sand & land, 1)] *= 0.93
    # Margem do rio: espuma clara na água, faixa escura logo depois, barranco escuro na terra.
    foam = (lab == WATER) & shift_or(land, 2)
    deep = (lab == WATER) & shift_or(land, 6) & ~foam
    canvas[foam] = canvas[foam] * 0.4 + np.array([196, 222, 236]) * 0.6
    canvas[deep] *= 0.85
    canvas[land & shift_or(lab == WATER, 3)] *= 0.78
    # Penhasco: luz no alto da pedra, sombra no pé.
    above = np.zeros_like(is_rock); above[1:] = ~is_rock[:-1]
    canvas[is_rock & above] *= 1.25
    below_shadow = np.zeros_like(is_rock)
    for d in range(1, 5):
        below_shadow[d:] |= is_rock[:-d]
    canvas[below_shadow & ~is_rock & land] *= 0.72

    # Pontes: a arte da referência ampliada e com nitidez, só onde não é água.
    sharp = np.array(Image.fromarray(clean).resize((W, H), Image.LANCZOS)
                     .filter(ImageFilter.UnsharpMask(3, 120, 2))).astype(float)
    big_clean = np.array(Image.fromarray(clean).resize((W, H), Image.NEAREST)).astype(int)
    not_blue = ~((big_clean[..., 2] > big_clean[..., 0] + 25) & (big_clean[..., 2] > 80))
    for x0, y0, x1, y1 in BRIDGES:
        X0, Y0, X1, Y1 = int(x0 * sx), int(y0 * sy), int(x1 * sx), int(y1 * sy)
        m = np.zeros((H, W), bool)
        m[Y0:Y1, X0:X1] = True
        m &= not_blue
        canvas[m] = sharp[m]

    # Floresta: árvores (2x, como as da cena) e arbustos, de trás para a frente.
    # Só as árvores verdes (a de outono e o salgueiro destoam da referência): a redonda
    # na maior parte, o pinheiro e a retorcida de vez em quando.
    trees = [up2(load(f"arvore_{i:02d}.png")) for i in (1, 2, 3)]
    tree_w = [0.62, 0.28, 0.10]
    bushes = [up2(load(f"arbusto_{i:02d}.png")) for i in (1, 2, 3, 6)]
    forest = lab == FOREST
    stamps = []
    for y in range(0, H, 18):
        for x in range(0, W, 26):
            px = int(x + rng.integers(-9, 10))
            py = int(y + rng.integers(-7, 8))
            if 0 <= px < W and 30 <= py < H and forest[py, px] and forest[py - 30, px]:
                t = trees[rng.choice(3, p=tree_w)].copy()
                t[:, :, :3] = (t[:, :, :3] * rng.uniform(0.88, 1.06)).clip(0, 255).astype(np.uint8)
                stamps.append((py, px, t))
    edge = shift_or(forest, 10) & ~forest & (lab == GRASS)
    for _ in range(W * H // 6000):
        py, px = int(rng.integers(20, H)), int(rng.integers(0, W))
        if edge[py, px]:
            stamps.append((py, px, bushes[rng.integers(0, len(bushes))]))
    pedra = load("Borda/pedra.png")
    near_bridge = np.zeros((H, W), bool)
    for x0, y0, x1, y1 in BRIDGES:
        near_bridge[max(0, int((y0 - 6) * sy)):int((y1 + 6) * sy), max(0, int((x0 - 6) * sx)):int((x1 + 6) * sx)] = True
    for x, y, area in rocks:
        # Só no chão firme: nada de pedra em cima da ponte, na água ou no penhasco.
        xi, yi = min(W - 1, int(x)), min(H - 1, int(y))
        if area < 6 or near_bridge[yi, xi] or lab[yi, xi] not in (GRASS, SAND):
            continue
        img = pedra if area > 14 else pedra[::2, ::2]
        stamps.append((int(y + img.shape[0] / 3), int(x), img))
    stamps.sort(key=lambda s: s[0])
    for py, px, img in stamps:
        h, w = img.shape[:2]
        y0, x0 = py - h, px - w // 2
        # Sombra no chão, embaixo do pé.
        sy0, sy1 = max(0, py - 5), min(H, py + 3)
        sx0, sx1 = max(0, px - w // 3), min(W, px + w // 3)
        canvas[sy0:sy1, sx0:sx1] *= 0.8
        ys0, xs0 = max(0, y0), max(0, x0)
        ys1, xs1 = min(H, y0 + h), min(W, x0 + w)
        if ys1 <= ys0 or xs1 <= xs0:
            continue
        part = img[ys0 - y0:ys1 - y0, xs0 - x0:xs1 - x0]
        a = part[:, :, 3:4] > 0
        canvas[ys0:ys1, xs0:xs1] = np.where(a, part[:, :, :3], canvas[ys0:ys1, xs0:xs1])
    print(f"  árvores, arbustos e pedras: {len(stamps)}")

    out = np.dstack([canvas.clip(0, 255).round().astype(np.uint8), np.full((H, W), 255, np.uint8)])
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    Image.fromarray(out, "RGBA").save(OUT)
    print(f"  {OUT}: {W}x{H}")


if __name__ == "__main__":
    main()
