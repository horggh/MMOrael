"""As 3 etapas do Portal de teleporte (imagens/portal.jpeg): inativo, ativando e ativo.

Pedra cinza sobre xadrez cinza e o vão do portal mostrando o fundo: o fundo sai pela
posição dos quadrados (extract_janelas.py), inclusive dentro do arco. Depois tudo é
reduzido à grade nativa. As 3 etapas ficam com a mesma tela e a mesma base, para trocar
de etapa sem o portal sair do lugar.
Saída: Assets/Resources/Props/portal_1.png .. portal_3.png.

Uso:  python3 Tools/extract_portal.py
"""
import sys
import numpy as np
from PIL import Image
sys.path.insert(0, "Tools")
import extract_pixelart as EP
import extract_janelas as J

SRC = "imagens/portal.jpeg"


def tinted_squares(a, bg):
    lum = a.mean(2)
    sat = a.max(2) - a.min(2)
    H, W = lum.shape
    flat = (J.local_std(lum) < 5) & (sat < 110) & ~bg
    squares = []
    seen = np.zeros_like(flat)
    for y, x in zip(*np.where(flat)):
        if seen[y, x]:
            continue
        comp = J.flood(flat & ~seen, [(y, x)])
        seen |= comp
        ys, xs = np.where(comp)
        h, w = ys.max() - ys.min() + 1, xs.max() - xs.min() + 1
        if 9 <= h <= 18 and 9 <= w <= 18 and len(ys) >= 0.7 * h * w:
            squares.append((ys.mean(), xs.mean(), lum[ys, xs].mean(), ys, xs))
    out = np.zeros((H, W), bool)
    for cy, cx, l, ys, xs in squares:
        n = 0
        for cy2, cx2, l2, _, _ in squares:
            if 13 <= np.hypot(cy - cy2, cx - cx2) <= 22 and abs(l - l2) > 10:
                n += 1
        if n >= 1:
            out[ys, xs] = True
    for _ in range(3):   # divisas borradas entre os quadradinhos
        g = out.copy()
        g[1:] |= out[:-1]; g[:-1] |= out[1:]; g[:, 1:] |= out[:, :-1]; g[:, :-1] |= out[:, 1:]
        out = g & ~bg & (sat < 110) & (J.local_std(lum) < 25)
    return out


def box_mean(v, r):
    """Média numa janela (2r+1)x(2r+1) em volta de cada pixel."""
    pad = np.pad(v, r + 1, mode="edge")
    c = pad.cumsum(0).cumsum(1)
    k = 2 * r + 1
    m = c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]
    return m[:v.shape[0], :v.shape[1]] / (k * k)


def clean_halo(rgb, mask, period):
    """Aura semitransparente (etapa ativa): o xadrez aparece tingido através do brilho.
    Ajusta a grade do xadrez neste recorte e tira o que segue o padrão claro/escuro,
    inclusive encostado na borda (a aura fica por fora do totem)."""
    a = rgb.astype(float)
    lum = a.mean(2)
    sat = a.max(2) - a.min(2)
    h, w = lum.shape
    flat = mask & (J.local_std(lum) < 6) & (sat < 90)
    ys, xs = np.where(flat)
    if len(ys) < 100:
        return mask
    l = lum[ys, xs]
    best = None
    for ox in np.arange(0, period, 0.5):
        for oy in np.arange(0, period, 0.5):
            par = ((np.floor((xs + ox) / period) + np.floor((ys + oy) / period)) % 2).astype(bool)
            if par.sum() < 30 or (~par).sum() < 30:
                continue
            t0, t1 = np.median(l[~par]), np.median(l[par])
            err = np.abs(l - np.where(par, t1, t0)).mean()
            if best is None or err < best[0]:
                best = (err, ox, oy, t0, t1)
    if best is None or abs(best[3] - best[4]) < 12:
        return mask
    _, ox, oy, t0, t1 = best
    yy, xx = np.mgrid[0:h, 0:w]
    par = ((np.floor((xx + ox) / period) + np.floor((yy + oy) / period)) % 2).astype(bool)
    expected = np.where(par, t1, t0)
    cand = mask & (sat < 90) & (np.abs(lum - expected) < 9)

    # Onde o tom da aura muda, os dois tons fixos falham: vale o padrão. Se o brilho sobe e
    # desce junto com a grade do xadrez (mais claro nos quadrados claros), é fundo.
    sign = np.where(par == (t1 > t0), 1.0, -1.0)
    r = int(round(period))
    d = lum - box_mean(lum, r)
    agree = box_mean(d * sign, r) / np.maximum(box_mean(np.abs(d), r), 1e-3)
    pattern = mask & (agree > 0.55) & (box_mean(np.abs(d), r) > 5) & (sat < 120)
    cand |= pattern
    out = mask.copy()
    seen = np.zeros_like(cand)
    for y, x in zip(*np.where(cand)):
        if seen[y, x]:
            continue
        comp = J.flood(cand & ~seen, [(y, x)])
        seen |= comp
        if comp.sum() >= 6:
            out &= ~comp
    return J.drop_specks(out, 4)


def main():
    a = np.array(Image.open(SRC).convert("RGB")).astype(float)
    bg, _ = J.background(a)
    # Aura semitransparente: xadrez tingido de azul/roxo por baixo do brilho. Na imagem
    # grande cada quadrado tem 17 px: quadradinhos lisos (mesmo coloridos) com vizinhos
    # alternando claro/escuro são fundo.
    bg |= tinted_squares(a, bg)
    fg = J.drop_specks(~bg, 12)          # faíscas pequenas ficam
    pitch = EP.detect_pitch(a)
    gw = int(round(a.shape[1] / pitch))
    print(f"  grade {gw} (passo {pitch:.2f}px)")
    grid = EP.resample(a, gw)
    mask = EP.resample(np.dstack([fg * 255.0] * 3), gw)[:, :, 0] > 127
    spans = sorted(EP.split_columns(mask), key=lambda s: -mask[:, s[0]:s[1] + 1].sum())[:3]
    spans.sort()
    crops = []
    for x0, x1 in spans:
        ys = np.where(mask[:, x0:x1 + 1].any(1))[0]
        crops.append((x0, x1, ys.min(), ys.max()))
    width = max(x1 - x0 + 1 for x0, x1, _, _ in crops)
    height = max(y1 - y0 + 1 for _, _, y0, y1 in crops)
    for i, (x0, x1, y0, y1) in enumerate(crops, 1):
        rgb = grid[y0:y1 + 1, x0:x1 + 1].astype(int)
        m = mask[y0:y1 + 1, x0:x1 + 1]
        if i == 3:
            m = clean_halo(rgb, m, 17.04 / pitch)
        alpha = np.where(m, 255, 0).astype(np.uint8)
        snapped, _ = EP.quantize(rgb, alpha, 16)
        img = np.zeros((height, width, 4), np.uint8)
        h, w = alpha.shape
        ox = (width - w) // 2
        img[height - h:, ox:ox + w, :3] = snapped
        img[height - h:, ox:ox + w, 3] = alpha
        path = f"Assets/Resources/Props/portal_{i}.png"
        Image.fromarray(img, "RGBA").save(path)
        print(f"  {path}: {w}x{h} (tela {width}x{height})")


if __name__ == "__main__":
    main()
