"""Skin "Cavaleiro montado" (imagens/skin_montaria.png): grade de 8 direções.

Linhas: IDLE (1 quadro), WALK (3), RUN (3), ATTACK (3, com a espada e o arco desenhados —
esta skin não usa as camadas de arma/escudo), DEATH (4 quadros, só de frente).
Colunas: down, downleft, left, upleft, up, upright, right, downright.

Cada célula: o fundo escuro sai por inundação a partir da borda da célula; fica o desenho
maior e o que estiver colado nele. Os quadros de cada animação/direção são alinhados pelo
tronco e pelos pés. Na prancha, IDLE e DEATH estão desenhados maiores que WALK/RUN/ATTACK:
são reduzidos para a mesma altura do andar.
Saída: Assets/Resources/Characters/Skins/montaria/<anim>_<direção>.png (death.png sem direção).
Uso: python Tools/extract_montaria.py
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_boss_pirata import components  # noqa: E402
from extract_frames import segments  # noqa: E402

SRC = "imagens/skin_montaria.png"
OUT = "Assets/Resources/Characters/Skins/montaria"
COLS = [110, 255, 399, 532, 670, 802, 943, 1101, 1248]
DIRS = ["down", "downleft", "left", "upleft", "up", "upright", "right", "downright"]
ROWS = {
    "idle": [(40, 175)],
    "walk": [(180, 267), (269, 360), (362, 455)],
    "run": [(461, 552), (554, 646), (648, 740)],
    "attack": [(746, 836), (838, 928), (929, 1032)],
}
DEATH_ROW = (1037, 1220)
INSET = 4


def cut(a, y0, y1, x0, x1):
    """Desenho de uma célula: (rgb, máscara) recortados na caixa do desenho."""
    cell = a[y0 + INSET:y1 - INSET, x0 + INSET:x1 - INSET].astype(int)
    H, W, _ = cell.shape
    border = np.concatenate([cell[0], cell[-1], cell[:, 0], cell[:, -1]])
    bg = np.median(border, 0)
    cand = np.abs(cell - bg).max(2) <= 12
    filled = np.zeros((H, W), bool)
    stack = [(y, x) for y in range(H) for x in (0, W - 1) if cand[y, x]]
    stack += [(y, x) for x in range(W) for y in (0, H - 1) if cand[y, x]]
    for s in stack:
        filled[s] = True
    while stack:
        y, x = stack.pop()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < H and 0 <= nx < W and cand[ny, nx] and not filled[ny, nx]:
                filled[ny, nx] = True
                stack.append((ny, nx))
    fg = ~filled
    parts = components(fg)
    if not parts:
        return None
    main = max(parts, key=lambda c: len(c[0]))
    keep = np.zeros_like(fg)
    my0, my1, mx0, mx1 = main[0].min() - 3, main[0].max() + 3, main[1].min() - 3, main[1].max() + 3
    for ys, xs in parts:
        near = ys.max() >= my0 and ys.min() <= my1 and xs.max() >= mx0 and xs.min() <= mx1
        if ys is main[0] or (near and len(ys) >= 6):
            keep[ys, xs] = True
    ys, xs = np.where(keep)
    by0, by1, bx0, bx1 = ys.min(), ys.max(), xs.min(), xs.max()
    return cell[by0:by1 + 1, bx0:bx1 + 1].astype(np.uint8), keep[by0:by1 + 1, bx0:bx1 + 1]


def mirror_effect(rgb, mask, front):
    """Passa o arco/lâmina (claros: azul-claro ou cinza-claro) para o lado `front` do corpo
    (-1 esquerda, 1 direita), espelhando em volta do meio do corpo; onde o efeito cobria o
    corpo, usa a cor dos vizinhos. Devolve (rgb, máscara) num quadro maior se precisar."""
    a = rgb.astype(int)
    lum, sat = a.mean(2), a.max(2) - a.min(2)
    # Só o arco azul-claro (o peito branco e as penas da ave não entram): azul bem mais
    # forte que o vermelho, claro, e em pedaço grande.
    effect = mask & (lum > 140) & (a[:, :, 2] - a[:, :, 0] > 35)
    keep = np.zeros_like(effect)
    for cy, cx_ in components(effect):
        if len(cy) >= 40:
            keep[cy, cx_] = True
    # O contorno do arco é mais apagado (cinza-azulado): entra o que estiver a até 3 px do
    # arco e for azulado ou neutro, claro ou médio.
    near = keep.copy()
    for _ in range(3):
        g = near.copy()
        g[1:] |= near[:-1]; g[:-1] |= near[1:]; g[:, 1:] |= near[:, :-1]; g[:, :-1] |= near[:, 1:]
        near = g
    effect = keep | (near & mask & (lum > 90) & (a[:, :, 2] >= a[:, :, 0] - 5) & (sat < 110))
    body = mask & ~effect
    ys, xs = np.where(body)
    cx = float(np.median(xs))
    ey, ex = np.where(effect)
    if len(ex) == 0 or np.sign(ex.mean() - cx) == front:
        return rgb, mask
    nx = np.round(2 * cx - ex).astype(int)
    pad_l = max(0, -nx.min()); pad_r = max(0, nx.max() - (mask.shape[1] - 1))
    H, W = mask.shape
    out = np.zeros((H, W + pad_l + pad_r, 3), np.uint8)
    om = np.zeros((H, W + pad_l + pad_r), bool)
    out[:, pad_l:pad_l + W] = rgb
    om[:, pad_l:pad_l + W] = body
    # Onde o efeito cobria o corpo: cor dos vizinhos do corpo.
    hole = np.zeros_like(om)
    hole[:, pad_l:pad_l + W] = effect
    for _ in range(12):
        pad = np.pad(om, 1)
        cnt = sum(pad[1 + dy:1 + dy + H, 1 + dx:1 + dx + om.shape[1]].astype(int)
                  for dy in (-1, 0, 1) for dx in (-1, 0, 1) if dy or dx)
        grow = hole & ~om & (cnt >= 5)
        if not grow.any():
            break
        for y, x in zip(*np.where(grow)):
            nb = om[max(0, y - 1):y + 2, max(0, x - 1):x + 2]
            out[y, x] = out[max(0, y - 1):y + 2, max(0, x - 1):x + 2][nb].mean(0)
            om[y, x] = True
    # Fios que sobram do contorno do arco antigo (finos, na região dele): saem de fora para
    # dentro (pixel com até 2 vizinhos), sem mexer no resto do corpo.
    region = np.zeros_like(om)
    region[:, pad_l:pad_l + W] = near
    for _ in range(10):
        pad = np.pad(om, 1)
        n8 = sum(pad[1 + dy:1 + dy + H, 1 + dx:1 + dx + om.shape[1]].astype(int)
                 for dy in (-1, 0, 1) for dx in (-1, 0, 1) if dy or dx)
        thin = om & region & (n8 <= 2)
        if not thin.any():
            break
        om &= ~thin
    for cy, cx_ in components(om & region):
        if len(cy) < 25 and not (om & ~region)[np.clip(cy, 0, H - 1), np.clip(cx_, 0, om.shape[1] - 1)].any():
            om[cy, cx_] = False
    out[ey, nx + pad_l] = rgb[ey, ex]
    om[ey, nx + pad_l] = True
    return out, om


def arc_in_front(base, src, front):
    """Quadro do meio do golpe: o quadro 1 (sem arco, limpo) com o arco do quadro 2
    espelhado para o lado `front` (-1 esquerda, 1 direita). Os dois quadros são alinhados pelo
    tronco (x) e pelos pés (base)."""
    rgb2, m2 = src
    a = rgb2.astype(int)
    lum = a.mean(2)
    arc = m2 & (lum > 140) & (a[:, :, 2] - a[:, :, 0] > 35)
    keep = np.zeros_like(arc)
    for cy, cx_ in components(arc):
        if len(cy) >= 40:
            keep[cy, cx_] = True
    # Com o contorno azul-escuro do arco (até 3 px dele, azulado).
    near = keep.copy()
    for _ in range(3):
        g = near.copy()
        g[1:] |= near[:-1]; g[:-1] |= near[1:]; g[:, 1:] |= near[:, :-1]; g[:, :-1] |= near[:, 1:]
        near = g
    arc = keep | (near & m2 & (a[:, :, 2] - a[:, :, 0] > 20))

    def anchor(m):
        ys, xs = np.where(m)
        h = ys.max() - ys.min()
        band = (ys >= ys.min() + 0.2 * h) & (ys <= ys.min() + 0.5 * h)
        return float(np.median(xs[band])), ys.max()

    rgb1, m1 = base
    ax2, by2 = anchor(m2 & ~arc)
    ax1, by1 = anchor(m1)
    ey, ex = np.where(arc)
    if len(ex) == 0:
        return base
    side = np.sign(ex.mean() - ax2)
    rel_x = (ex - ax2) * (side * front)        # espelha se estiver do lado de trás
    tx = np.round(ax1 + rel_x).astype(int)
    ty = (ey - by2 + by1).astype(int)
    H, W = m1.shape
    pl, pr = max(0, -tx.min()), max(0, tx.max() - (W - 1))
    pt, pb = max(0, -ty.min()), max(0, ty.max() - (H - 1))
    out = np.zeros((H + pt + pb, W + pl + pr, 3), np.uint8)
    om = np.zeros(out.shape[:2], bool)
    out[pt:pt + H, pl:pl + W] = rgb1
    om[pt:pt + H, pl:pl + W] = m1
    out[ty + pt, tx + pl] = rgb2[ey, ex]
    om[ty + pt, tx + pl] = True
    return out, om


def scaled(rgb, mask, s):
    if abs(s - 1) < 0.02:
        return rgb, mask
    h, w = mask.shape
    W, H = max(1, round(w * s)), max(1, round(h * s))
    pre = Image.fromarray((rgb * mask[:, :, None]).astype(np.uint8)).resize((W, H), Image.LANCZOS)
    al = np.array(Image.fromarray((mask * 255).astype(np.uint8)).resize((W, H), Image.LANCZOS)) / 255.0
    col = np.array(pre).astype(float) / np.maximum(al[:, :, None], 1e-3)
    return col.clip(0, 255).astype(np.uint8), al > 0.5


FINAL = 106 / 85


def save_strip(frames, name):
    """Quadros alinhados pelo tronco e pelos pés numa tira."""
    anchors = []
    for rgb, m in frames:
        ys, xs = np.where(m)
        h = ys.max() - ys.min()
        band = (ys >= ys.min() + 0.2 * h) & (ys <= ys.min() + 0.5 * h)
        anchors.append(int(round(np.median(xs[band]))))
    half = max(max(ax, m.shape[1] - ax) for (rgb, m), ax in zip(frames, anchors)) + 2
    fh = max(m.shape[0] for _, m in frames) + 3
    fw = half * 2
    strip = np.zeros((fh, fw * len(frames), 4), np.uint8)
    for i, ((rgb, m), ax) in enumerate(zip(frames, anchors)):
        h, w = m.shape
        oy, ox = fh - 2 - h, i * fw + half - ax
        region = strip[oy:oy + h, ox:ox + w]
        region[m, :3] = rgb[m]
        region[m, 3] = 255
    # Na altura do "Cavaleiro (camadas)" (PPU 64): a arte de origem tem ~85px, sobe para ~106.
    H, W = strip.shape[0], strip.shape[1] // len(frames)
    nw, nh = round(W * FINAL), round(H * FINAL)
    a = strip[:, :, 3:].astype(float) / 255
    pre = Image.fromarray((strip[:, :, :3] * a).astype(np.uint8)).resize((nw * len(frames), nh), Image.LANCZOS)
    al = np.array(Image.fromarray(strip[:, :, 3]).resize((nw * len(frames), nh), Image.LANCZOS)).astype(float)
    rgb = np.clip(np.array(pre).astype(float) * 255 / np.maximum(al, 1)[:, :, None], 0, 255)
    strip = np.dstack([rgb, np.where(al >= 128, 255, 0)]).astype(np.uint8)
    Image.fromarray(strip, "RGBA").save(os.path.join(OUT, name + ".png"))
    return fw, fh


def main():
    a = np.array(Image.open(SRC).convert("RGB"))
    os.makedirs(OUT, exist_ok=True)
    cells = {}
    for anim, rows in ROWS.items():
        for d, direction in enumerate(DIRS):
            cells[(anim, direction)] = [cut(a, y0, y1, COLS[d], COLS[d + 1]) for y0, y1 in rows]
    # Golpe para a frente. Na prancha, o golpe para a esquerda e para a direita foi
    # desenhado com a espada e o arco ATRÁS do personagem (a ave olha para um lado e o corte
    # vai para o outro). As diagonais de baixo cortam para a frente, com quase a mesma pose:
    # esquerda/direita usam esses golpes.
    cells[("attack", "left")] = cells[("attack", "downleft")]
    cells[("attack", "right")] = cells[("attack", "downright")]
    # Diagonais de cima: só o quadro do meio (o do arco) corta para trás -> o arco e a lâmina
    # são espelhados para o lado da frente (esquerda em upleft, direita em upright).
    up_right = cells[("attack", "upright")]
    up_right[1] = arc_in_front(up_right[0], up_right[1], 1)
    # Cima-esquerda: o mesmo golpe espelhado (a pose da prancha é o espelho da direita).
    rgb, m = up_right[1]
    cells[("attack", "upleft")][1] = (rgb[:, ::-1].copy(), m[:, ::-1].copy())

    # Altura de referência: a do andar (mediana).
    walk_h = np.median([c[1].shape[0] for d in DIRS for c in cells[("walk", d)] if c is not None])
    idle_h = np.median([c[1].shape[0] for d in DIRS for c in cells[("idle", d)] if c is not None])
    idle_s = walk_h / idle_h
    print(f"  altura do andar {walk_h:.0f}px, parado {idle_h:.0f}px -> parado x{idle_s:.2f}")
    for (anim, direction), frames in cells.items():
        frames = [f for f in frames if f is not None]
        if anim == "idle":
            frames = [scaled(rgb, m, idle_s) for rgb, m in frames]
        fw, fh = save_strip(frames, f"{anim}_{direction}")
    # Morte: só de frente, 4 desenhos lado a lado (largos), na escala do parado.
    y0, y1 = DEATH_ROW
    band = a[y0 + INSET:y1 - INSET, 110:1248].astype(int)
    border = np.concatenate([band[0], band[-1]])
    bgc = np.median(border, 0)
    fgcols = (np.abs(band - bgc).max(2) > 12).any(0)
    spans = segments(fgcols, min_gap=6, min_width=30)
    death = []
    for s0, s1 in spans[:4]:
        c = cut(a, y0, y1, 110 + s0 - INSET - 2, 110 + s1 + INSET + 3)
        if c is not None:
            death.append(scaled(*c, idle_s))
    save_strip(death, "death")
    print(f"  {len(cells)} tiras + morte ({len(death)} quadros)")


if __name__ == "__main__":
    main()
