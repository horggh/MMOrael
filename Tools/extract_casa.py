"""Recorta os 5 estágios da construção da casa (imagens/casa-construcao.jpeg).

Esta imagem não serve para o extract_pixelart.py: é pequena, não está alinhada a uma grade
de pixel exata, e as pedras cinzas da obra têm o mesmo tom do xadrez de fundo (a detecção
por faixa de cinza apagava as pedras). Aqui o xadrez é removido pela POSIÇÃO: ajusta-se a
grade dos quadrados, e em cada ponto só é fundo o cinza que bate com o tom esperado ali
(claro ou escuro), ligado à borda da imagem. A arte fica na resolução da imagem.

Saída: Assets/Resources/Buildings/casa_1.png .. casa_5.png (terreno marcado, andaime,
paredes, telhado em obra, casa pronta), todas com a mesma largura de tela e alinhadas pela
base, para trocar de estágio sem a construção "andar".

Uso:  python3 Tools/extract_casa.py
"""
import os
import numpy as np
from PIL import Image

SRC = "imagens/casa-construcao.jpeg"
OUT = "Assets/Resources/Buildings"
STAGES = 5
TOL = 16           # quanto o cinza pode fugir do tom esperado do quadrado


def checker_fit(a):
    """Acha tamanho e deslocamento dos quadrados do xadrez numa faixa só de fundo (topo)."""
    lum = a.mean(2)
    band = lum[:110]
    lo, hi = np.percentile(band, 20), np.percentile(band, 80)
    best = None
    ys, xs = np.mgrid[0:band.shape[0], 0:band.shape[1]]
    for p in np.arange(16.6, 17.6, 0.04):
        for ox in np.arange(0, p, 1.0):
            for oy in np.arange(0, p, 1.0):
                par = (np.floor((xs + ox) / p) + np.floor((ys + oy) / p)) % 2
                err = np.abs(band - np.where(par == 0, lo, hi)).mean()
                if best is None or err < best[0]:
                    best = (err, p, ox, oy)
    return best[1], best[2], best[3], lo, hi


def background(a):
    p, ox, oy, lo, hi = checker_fit(a)
    print(f"xadrez: quadrado {p:.2f}px, tons {lo:.0f}/{hi:.0f}")
    H, W, _ = a.shape
    lum = a.mean(2)
    neutral = (np.abs(a[:, :, 0] - a[:, :, 1]) < 12) & (np.abs(a[:, :, 1] - a[:, :, 2]) < 12)
    ys, xs = np.mgrid[0:H, 0:W]
    fx, fy = (xs + ox) / p, (ys + oy) / p
    expected = np.where((np.floor(fx) + np.floor(fy)) % 2 == 0, lo, hi)
    cand = neutral & (np.abs(lum - expected) < TOL)
    # Na divisa entre dois quadrados o JPEG mistura os tons: aceita qualquer cinza intermediário.
    edge = (np.minimum(fx % 1, 1 - fx % 1) * p < 1.6) | (np.minimum(fy % 1, 1 - fy % 1) * p < 1.6)
    cand |= neutral & edge & (lum > lo - TOL) & (lum < hi + TOL)

    # Só é fundo o que se liga à borda: cinzas dentro do desenho ficam.
    bg = np.zeros((H, W), bool)
    stack = [(y, x) for y in range(H) for x in (0, W - 1) if cand[y, x]]
    stack += [(y, x) for x in range(W) for y in (0, H - 1) if cand[y, x]]
    for y, x in stack:
        bg[y, x] = True
    while stack:
        y, x = stack.pop()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < H and 0 <= nx < W and cand[ny, nx] and not bg[ny, nx]:
                bg[ny, nx] = True
                stack.append((ny, nx))

    # Áreas de fundo fechadas pelo desenho (dentro do andaime, janelas, entre pernas de
    # mesa) não se ligam à borda. Uma região cinza fechada vira fundo se quase toda bater
    # com o tom do xadrez naquela posição e for cinza puro (as pedras puxam para o azul).
    # A divisa entre quadrados (borrada pelo JPEG) também conta como acerto.
    strict = neutral & ((np.abs(lum - expected) < 12) | edge)
    seen = bg.copy()
    for y0, x0 in zip(*np.where(cand & ~bg)):
        if seen[y0, x0]:
            continue
        comp, stack = [], [(y0, x0)]
        seen[y0, x0] = True
        while stack:
            y, x = stack.pop()
            comp.append((y, x))
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                ny, nx = y + dy, x + dx
                if 0 <= ny < H and 0 <= nx < W and cand[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        cy, cx = np.array(comp).T
        if len(comp) < 12 or strict[cy, cx].mean() < 0.9:
            continue
        # Pedras e chaminé são de um cinza levemente azulado; o xadrez é cinza puro.
        if (a[cy, cx, 2] - a[cy, cx, 0]).mean() >= 1.0:
            continue
        core = ~edge[cy, cx]
        if core.sum() == 0:
            continue
        l, e = lum[cy, cx][core], expected[cy, cx][core]
        dark = np.mean(e == lo)
        off = np.abs(l - e).mean()
        mixed = len(comp) >= 40 and 0.1 < dark < 0.9 and off < 9   # pega os dois tons do xadrez
        flat = l.std() < 5 and off < 6                 # buraco de um quadrado só (janela)
        if mixed or flat:
            bg[cy, cx] = True

    # Auréola: pixel de borda cinza-médio (mistura da arte com o fundo) também sai.
    for _ in range(2):
        near = np.zeros_like(bg)
        near[1:] |= bg[:-1]; near[:-1] |= bg[1:]; near[:, 1:] |= bg[:, :-1]; near[:, :-1] |= bg[:, 1:]
        halo = ~bg & near & neutral & (lum > lo - 10) & (lum < hi + 10)
        bg |= halo
    return bg


def drop_specks(fg, min_size=25):
    """Tira grupinhos soltos de pixels (ruído do JPEG no meio do fundo)."""
    H, W = fg.shape
    seen = np.zeros_like(fg)
    out = fg.copy()
    for y, x in zip(*np.where(fg)):
        if seen[y, x]:
            continue
        comp, stack = [], [(y, x)]
        seen[y, x] = True
        while stack:
            cy, cx = stack.pop()
            comp.append((cy, cx))
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    ny, nx = cy + dy, cx + dx
                    if 0 <= ny < H and 0 <= nx < W and fg[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
        if len(comp) < min_size:
            for cy, cx in comp:
                out[cy, cx] = False
    return out


def groups(fg):
    """Colunas com desenho, separadas por colunas vazias. Pedaços pequenos (pedras, toras
    soltas) juntam-se ao grupo grande mais próximo."""
    filled = fg.sum(0) > 1
    spans, start = [], None
    for x, f in enumerate(filled):
        if f and start is None:
            start = x
        elif not f and start is not None:
            spans.append([start, x - 1]); start = None
    if start is not None:
        spans.append([start, len(filled) - 1])
    big = sorted(spans, key=lambda s: -fg[:, s[0]:s[1] + 1].sum())[:STAGES]
    big = sorted(big)
    for s in spans:
        if s in big:
            continue
        mid = (s[0] + s[1]) / 2
        g = min(big, key=lambda b: abs((b[0] + b[1]) / 2 - mid))
        g[0], g[1] = min(g[0], s[0]), max(g[1], s[1])
    return sorted(big)


def main():
    a = np.array(Image.open(SRC).convert("RGB")).astype(float)
    bg = background(a)
    fg = drop_specks(~bg)
    spans = groups(fg)
    print(f"estágios encontrados: {len(spans)}")

    crops = []
    for x0, x1 in spans:
        sub = fg[:, x0:x1 + 1]
        rows = np.where(sub.sum(1) > 0)[0]
        crops.append((x0, x1, rows.min(), rows.max()))

    # Mesma largura e mesma base para todos: troca de estágio sem deslocar.
    width = max(x1 - x0 + 1 for x0, x1, _, _ in crops)
    height = max(y1 - y0 + 1 for _, _, y0, y1 in crops)
    os.makedirs(OUT, exist_ok=True)
    for i, (x0, x1, y0, y1) in enumerate(crops, 1):
        img = np.zeros((height, width, 4), np.uint8)
        rgb = a[y0:y1 + 1, x0:x1 + 1].astype(np.uint8)
        alpha = np.where(fg[y0:y1 + 1, x0:x1 + 1], 255, 0).astype(np.uint8)
        h, w = rgb.shape[:2]
        ox = (width - w) // 2
        img[height - h:, ox:ox + w, :3] = rgb
        img[height - h:, ox:ox + w, 3] = alpha
        path = os.path.join(OUT, f"casa_{i}.png")
        Image.fromarray(img, "RGBA").save(path)
        print(f"  {path}: {w}x{h} (tela {width}x{height})")


if __name__ == "__main__":
    main()
