"""Folha da masmorra (imagens/masmorra.jpg).

Linha 1: 6 chãos de pedra -> texturas de 1 quadrado da grade (64x64), em
  Assets/Art/Sprites/Environment/Texturas/masmorra_piso_<n>.png (janela Pintar chão e paleta).
Linhas 2 a 4: paredes, cantos, estátua, grade, tochas, baús, barris... -> sprites em
  Assets/Art/Sprites/Environment/Masmorra/<nome>.png, na MESMA escala dos chãos (a largura de
  um ladrilho de chão da folha = 64 px = 1 quadrado), para as paredes encaixarem no piso.

O xadrez desta folha tem os mesmos cinzas das pedras, e o topo da imagem é todo chão: os
tons e a posição dos quadrados são medidos nas margens (só fundo), e o fundo sai por
inundação a partir das bordas, seguindo a posição dos quadrados. Pedaços de xadrez presos
(vãos da grade de ferro) saem pelo teste dos dois tons.

Uso:
  python Tools/extract_masmorra.py --listar <pasta>    (numera as peças das linhas 2-4)
  python Tools/extract_masmorra.py nome1 nome2 ...      (salva tudo; "-" pula a peça)
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_boss_pirata import components  # noqa: E402
from extract_frames import segments  # noqa: E402
import extract_decoracao  # noqa: E402

SRC = "imagens/masmorra.jpg"
TEX = "Assets/Art/Sprites/Environment/Texturas"
OUT = "Assets/Art/Sprites/Environment/Masmorra"
FLOOR_ROW = (0, 162)       # linha dos chãos
LO, HI = 82.0, 128.0       # tons do xadrez (medidos nas margens)


def fit_phase(lum, mask):
    ys, xs = np.where(mask)
    best = None
    for p in np.arange(16.4, 17.1, 0.04):
        for ox in np.arange(0, p, 1.0):
            for oy in np.arange(0, p, 1.0):
                par = (np.floor((xs + ox) / p) + np.floor((ys + oy) / p)) % 2
                err = np.abs(lum[ys, xs] - np.where(par == 0, LO, HI)).mean()
                if best is None or err < best[0]:
                    best = (err, p, ox, oy)
    return best[1:]


def flood(cand, seeds):
    H, W = cand.shape
    out = np.zeros_like(cand)
    stack = [s for s in seeds if cand[s]]
    for s in stack:
        out[s] = True
    while stack:
        y, x = stack.pop()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < H and 0 <= nx < W and cand[ny, nx] and not out[ny, nx]:
                out[ny, nx] = True
                stack.append((ny, nx))
    return out


def foreground(a):
    lum = a.mean(2)
    neutral = (a.max(2) - a.min(2)) < 10
    H, W = lum.shape
    margin = np.zeros((H, W), bool)
    margin[:, :12] = True
    margin[163:172, :] = True
    p, ox, oy = fit_phase(lum, margin & neutral)
    yy, xx = np.mgrid[0:H, 0:W]
    fx, fy = (xx + ox) / p, (yy + oy) / p
    expected = np.where((np.floor(fx) + np.floor(fy)) % 2 == 0, LO, HI)
    # Na divisa entre quadrados o JPEG mistura os tons: qualquer cinza entre os dois vale.
    edge = (np.minimum(fx % 1, 1 - fx % 1) * p < 1.6) | (np.minimum(fy % 1, 1 - fy % 1) * p < 1.6)
    cand = neutral & ((np.abs(lum - expected) < 13) | (edge & (lum > LO - 8) & (lum < HI + 8)))
    border = [(y, x) for y in range(H) for x in (0, W - 1)] + [(y, x) for x in range(W) for y in (0, H - 1)]
    bg = flood(cand, border)
    fg = ~bg
    # Xadrez preso no desenho: mancha cinza com os dois tons misturados.
    near_lo, near_hi = np.abs(lum - LO) < 12, np.abs(lum - HI) < 12
    for cy, cx in components(fg & neutral & (near_lo | near_hi)):
        if len(cy) >= 20 and min(near_lo[cy, cx].sum(), near_hi[cy, cx].sum()) >= 0.2 * len(cy):
            fg[cy, cx] = False
    # Quadrados de xadrez que a inundação não alcançou (o JPEG desta folha borra demais):
    # pedaço pequeno, cinza e quase todo num tom do xadrez.
    tone = neutral & ((np.abs(lum - LO) < 18) | (np.abs(lum - HI) < 18) | (edge & (lum > LO - 10) & (lum < HI + 10)))
    dropped = 0
    for cy, cx in components(fg):
        if len(cy) <= 700 and tone[cy, cx].mean() >= 0.8:
            fg[cy, cx] = False
            dropped += 1
    print(f"  xadrez: quadrado {p:.2f}px, tons {LO:.0f}/{HI:.0f}; {dropped} quadradinhos soltos removidos")
    return fg


def resample(tile, size):
    h, w, _ = tile.shape
    out = np.zeros((size, size, 3), np.uint8)
    for y in range(size):
        for x in range(size):
            cy, cx = (y + 0.5) * h / size, (x + 0.5) * w / size
            ry, rx = 0.3 * h / size, 0.3 * w / size
            blk = tile[int(cy - ry):int(cy + ry) + 1, int(cx - rx):int(cx + rx) + 1].reshape(-1, 3)
            out[y, x] = np.median(blk, axis=0)
    return out


def main():
    a = np.array(Image.open(SRC).convert("RGB")).astype(float)
    fg = foreground(a)

    # Chãos: 6 quadrados na primeira linha.
    y0, y1 = FLOOR_ROW
    row = fg[y0:y1 + 1]
    spans = segments(row.any(0), min_gap=3, min_width=40)
    widths = []
    os.makedirs(TEX, exist_ok=True)
    for i, (x0, x1) in enumerate(spans, 1):
        ys = np.where(row[:, x0:x1 + 1].any(1))[0]
        t0, t1 = y0 + ys.min(), y0 + ys.max()
        widths.append(x1 - x0 + 1)
        # Apara as beiradas claras (colunas/linhas do fundo que entraram no recorte: a pedra é
        # bem mais escura) e mais 3 px para dentro; senão vira uma linha entre ladrilhos.
        lumt = a[t0:t1 + 1, x0:x1 + 1].mean(2)
        cols, rows = lumt.mean(0), lumt.mean(1)
        cx0, cx1, cy0, cy1 = 0, len(cols) - 1, 0, len(rows) - 1
        while cols[cx0] > 85 and cx0 < cx1: cx0 += 1
        while cols[cx1] > 85 and cx1 > cx0: cx1 -= 1
        while rows[cy0] > 85 and cy0 < cy1: cy0 += 1
        while rows[cy1] > 85 and cy1 > cy0: cy1 -= 1
        m = 3
        tile = a[t0 + cy0 + m:t0 + cy1 + 1 - m, x0 + cx0 + m:x0 + cx1 + 1 - m]
        img = resample(tile.astype(np.uint8), 64)
        Image.fromarray(img, "RGB").convert("RGBA").save(os.path.join(TEX, f"masmorra_piso_{i}.png"))
    print(f"  chãos: {len(spans)} (largura média {np.mean(widths):.0f}px)")

    # Peças: mesma escala dos chãos (1 ladrilho da folha = 64 px).
    extract_decoracao.SCALE = 64 / float(np.mean(widths))
    rest = fg.copy()
    rest[:y1 + 1] = False
    from extract_decoracao_folha import clusters
    items = clusters(rest)
    args = sys.argv[1:]
    if args and args[0] == "--listar":
        extract_decoracao.OUT = args[1]
        os.makedirs(args[1], exist_ok=True)
        for i, it in enumerate(items, 1):
            W, H = extract_decoracao.save(a, it, f"{i:02d}")
            print(f"  {i:02d}: {W}x{H}  (x {it['xs'].min()}, base y {it['ys'].max()})")
        return
    extract_decoracao.OUT = OUT
    os.makedirs(OUT, exist_ok=True)
    if len(args) != len(items):
        print(f"  aviso: {len(items)} peças e {len(args)} nomes")
    for it, name in zip(items, args):
        if name != "-":
            W, H = extract_decoracao.save(a, it, name)
            print(f"  {name}.png {W}x{H}")


if __name__ == "__main__":
    main()
