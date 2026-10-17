"""Separa todos os quadros de uma folha de sprites (fundo xadrez) em arquivos numerados.

Linha a linha (faixas com desenho separadas por linhas vazias), cada quadro é um grupo de
colunas com desenho; efeitos soltos (arco do golpe, fumaça) ficam com o quadro mais
próximo. Os quadros saem já na grade nativa e alinhados pelo tronco (como o Drake), com
os pés na base da linha.

Uso: python Tools/extract_frames.py <imagem> <pasta de saída> <prefixo> [--grid N] [--cells 8,8,0]
Saída: <prefixo>_L<linha>_<n>.png (para conferir e escolher as animações).
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_pixelart import resample, background_mask, drop_specks, quantize, detect_pitch  # noqa: E402
from extract_boss_pirata import components, row_bands  # noqa: E402


def segments(fill, min_gap=3, min_width=6):
    segs, start, gap = [], None, 0
    for x, f in enumerate(fill):
        if f:
            if start is None:
                start = x
            gap = 0
        elif start is not None:
            gap += 1
            if gap >= min_gap:
                segs.append((start, x - gap))
                start, gap = None, 0
    if start is not None:
        segs.append((start, len(fill) - 1))
    return [s for s in segs if s[1] - s[0] >= min_width]


def extract(src, grid_w=None, cells=None, keep_effects=False):
    a = np.array(Image.open(src).convert("RGB")).astype(float)
    if grid_w is None:
        grid_w = int(round(a.shape[1] / detect_pitch(a)))
    grid = resample(a, grid_w)
    fg = drop_specks(~background_mask(grid))
    alpha = fg.astype(float) if keep_effects else remove_effects(grid, fg)
    frames = []   # (linha, índice, máscara, base da linha)
    for li, (b0, b1) in enumerate(row_bands(fg)):
        band = np.zeros_like(fg)
        band[b0:b1 + 1] = fg[b0:b1 + 1]
        # Grupos grandes (personagens) definem os quadros; os pequenos (efeitos) vão para
        # o quadro cujo centro está mais perto.
        comps = components(band)
        # O arco azul-claro do golpe é grande mas não é personagem: não define quadro
        # (senão emenda dois quadros vizinhos).
        big = [c for c in comps if len(c[0]) >= 150 and not is_effect_px(grid[c[0], c[1]])]
        cols = np.zeros(fg.shape[1], bool)
        for ys, xs in big:
            cols[xs.min():xs.max() + 1] = True
        segs = segments(cols)
        n = cells[li] if cells and li < len(cells) else 0
        if n:
            # Grade regular: quadros encostados (espada e capa invadem o vizinho) são
            # cortados na divisa das células.
            cw = fg.shape[1] / n
            segs = [(int(round(k * cw)), int(round((k + 1) * cw)) - 1) for k in range(n)]
            masks = []
            for s0, s1 in segs:
                m = np.zeros_like(fg)
                m[:, s0:s1 + 1] = band[:, s0:s1 + 1]
                # Pedaços do quadro vizinho (ponta de capa, pé) entram cortados na divisa:
                # fora o corpo principal, sai tudo que encosta na borda da célula.
                parts = components(m)
                if parts:
                    main = max(parts, key=lambda c: len(c[0]))
                    for cy, cx in parts:
                        if cy is not main[0] and (cx.min() <= s0 or cx.max() >= s1):
                            m[cy, cx] = False
                masks.append(m if m.any() else None)
            for i, m in enumerate(masks):
                if m is not None:
                    frames.append((li, i, m, b1))
            continue
        masks = [np.zeros_like(fg) for _ in segs]
        centers = [(s0 + s1) / 2 for s0, s1 in segs]
        for ys, xs in comps:
            cx = xs.mean()
            inside = [i for i, (s0, s1) in enumerate(segs) if s0 <= cx <= s1]
            i = inside[0] if inside else int(np.argmin([abs(cx - c) for c in centers]))
            masks[i][ys, xs] = True
        for i, m in enumerate(masks):
            frames.append((li, i, m, b1))
    return grid, frames, alpha


def is_effect_px(px):
    """Arco azul-claro do golpe (efeito da espada)."""
    return (px[:, 2] - px[:, 0]).mean() > 25 and px.mean() > 140


def remove_effects(grid, fg):
    """Tira os efeitos da espada (arcos azul-claros): o jogo desenha só o personagem.
    Sai o arco inteiro (com o xadrez que aparece dentro dele) e os pixels bem azulados
    soltos na borda. Devolve o alfa (0 ou 1) da imagem toda."""
    alpha = fg.astype(float)
    removed = 0
    for ys, xs in components(fg):
        if len(ys) >= 20 and is_effect_px(grid[ys, xs]):
            alpha[ys, xs] = 0
            removed += len(ys)
    px = grid.astype(int)
    cyan = (px[:, :, 2] - px[:, :, 0] > 45) & (px[:, :, 1] - px[:, :, 0] > 20) & (px.mean(2) > 150)
    removed += int((cyan & (alpha > 0)).sum())
    alpha[cyan] = 0
    print(f"  efeitos da espada removidos: {removed} pixels")
    return alpha


def save_frames(grid, frames, outdir, prefix, half_w=None, height=None, alpha_map=None):
    os.makedirs(outdir, exist_ok=True)
    anchors, boxes = [], []
    for li, i, m, base in frames:
        ys, xs = np.where(m)
        h = ys.max() - ys.min()
        band = (ys >= ys.min() + 0.25 * h) & (ys <= ys.min() + 0.55 * h)
        anchors.append(int(round(np.median(xs[band]))))
        boxes.append((ys.min(), xs.min(), xs.max(), base))
    if half_w is None:
        half_w = max(max(ax - x0, x1 - ax) for ax, (y0, x0, x1, b) in zip(anchors, boxes)) + 2
    if height is None:
        height = max(b - y0 for (y0, x0, x1, b) in boxes) + 3
    fw, fh = half_w * 2, height
    names = []
    for (li, i, m, base), ax in zip(frames, anchors):
        rgb = np.zeros((fh, fw, 3), int)
        alpha = np.zeros((fh, fw), np.uint8)
        ys, xs = np.where(m)
        ty, tx = ys - base + fh - 2, xs - ax + half_w
        ok = (ty >= 0) & (ty < fh) & (tx >= 0) & (tx < fw)
        rgb[ty[ok], tx[ok]] = grid[ys[ok], xs[ok]]
        alpha[ty[ok], tx[ok]] = 255 if alpha_map is None else             np.clip(alpha_map[ys[ok], xs[ok]] * 255, 1, 255).astype(np.uint8)
        # Pontinhas soltas (a espada do quadro vizinho cortada na divisa) saem.
        for cy, cx in components(alpha > 0):
            if len(cy) < 25:
                alpha[cy, cx] = 0
        snapped, _ = quantize(rgb, alpha, dist=20)
        name = f"{prefix}_L{li + 1}_{i + 1:02d}.png"
        Image.fromarray(np.dstack([snapped, alpha]).astype(np.uint8), "RGBA").save(os.path.join(outdir, name))
        names.append(name)
    return fw, fh, names


def main():
    src, outdir, prefix = sys.argv[1], sys.argv[2], sys.argv[3]
    gw = int(sys.argv[sys.argv.index("--grid") + 1]) if "--grid" in sys.argv else None
    # --cells 8,8,0: quadros por linha numa grade regular (0 = separar pelos vãos).
    cells = [int(v) for v in sys.argv[sys.argv.index("--cells") + 1].split(",")] if "--cells" in sys.argv else None
    grid, frames, alpha = extract(src, gw, cells)
    fw, fh, names = save_frames(grid, frames, outdir, prefix, alpha_map=alpha)
    print(f"{len(names)} quadros de {fw}x{fh}")
    for li in sorted({f[0] for f in frames}):
        print(f"  linha {li + 1}: {sum(1 for f in frames if f[0] == li)} quadros")


if __name__ == "__main__":
    main()
