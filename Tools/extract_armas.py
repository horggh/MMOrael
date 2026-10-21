"""Recorta as folhas de armas (imagens/armas/<n>.webp, fundo escuro, números embaixo ou no
canto de cada item) em ícones de Assets/Resources/Items/<id>.png (lado maior 64 px, como os
outros ícones). A ordem é a da folha (linha a linha); os ids são <prefixo>_NN.

Cada pedaço de desenho vai para a célula da grade onde fica o meio dele. Em cada célula fica
o desenho principal e o que está colado nele (brilhos, faíscas); os números e as molduras das
células (folhas de maças e cajados) saem.

Uso: python Tools/extract_armas.py [--previa <pasta>]
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_boss_pirata import components  # noqa: E402

OUT = "Assets/Resources/Items"
ICON = 64
# folha: (prefixo, linhas, colunas)
SHEETS = {
    99: ("espada_magica", 5, 10),
    100: ("espada", 3, 10),
    101: ("lanca", 2, (7, 8)),   # 7 na primeira linha, 8 na segunda
    102: ("machado", 3, 5),
    103: ("aljava", 4, 5),
    104: ("alabarda", 4, 5),
    105: ("martelo_guerra", 4, 5),
    106: ("foice", 4, 5),
    107: ("escudo", 5, 6),
    108: ("shuriken", 4, 5),
    109: ("livro", 5, 6),
    110: ("instrumento", 5, 6),
    111: ("chicote", 5, 6),
    112: ("soqueira", 4, 5),
    113: ("katar", 5, 6),
    114: ("cajado", 5, 6),
    115: ("maca", 5, 6),
}


# Distância (px) para juntar pedaços do mesmo item; menor onde os itens quase se encostam.
JOIN = {"machado": 0}
# Comprimento mínimo de um trecho reto para ser linha de moldura (maças: molduras por célula).
LINE = {}


def grow(m, n):
    for _ in range(n):
        g = m.copy()
        g[1:] |= m[:-1]; g[:-1] |= m[1:]; g[:, 1:] |= m[:, :-1]; g[:, :-1] |= m[:, 1:]
        m = g
    return m


# Folhas recortadas célula por célula (tudo o que há na célula é o item):
# cajados: grade regular com molduras (entra INSET px para dentro); machados: linhas de altura
# própria, e as colunas pelos vãos entre os itens.
CELLS = {"cajado": {"xs": [(26, 231), (253, 465), (485, 675), (694, 871), (890, 1067), (1086, 1235)],
                     "ys": [(16, 226), (243, 463), (482, 705), (723, 938), (957, 1228)]}, "machado": {"bands": [(0, 340), (340, 690), (690, 1254)]}}


def spans(v, gap=10, minw=12):
    out, s0, last = [], None, -99
    for i, x in enumerate(v):
        if x:
            if s0 is None:
                s0 = i
            last = i
        elif s0 is not None and i - last > gap:
            if last - s0 >= minw:
                out.append((s0, last + 1))
            s0 = None
    if s0 is not None:
        out.append((s0, last + 1))
    return out


def by_cells(rgb, fg, sat, prefix, rows, per_row):
    H, W = fg.shape
    cfg = CELLS[prefix]
    boxes = []
    if "bands" in cfg:
        for r, (y0, y1) in enumerate(cfg["bands"]):
            cols = spans(fg[y0:y1].any(0))
            cols = sorted(cols, key=lambda c: -(c[1] - c[0]))[:per_row[r]]
            boxes += [(y0, y1, x0, x1) for x0, x1 in sorted(cols)]
    elif "xs" in cfg:
        boxes = [(y0, y1, x0, x1) for y0, y1 in cfg["ys"] for x0, x1 in cfg["xs"]]
    else:
        ins = cfg["inset"]
        cols = per_row[0]
        for r in range(rows):
            for c in range(cols):
                boxes.append((int(r * H / rows) + ins, int((r + 1) * H / rows) - ins,
                              int(c * W / cols) + ins, int((c + 1) * W / cols) - ins))
    out = []
    for y0, y1, x0, x1 in boxes:
        keep = np.zeros_like(fg)
        keep[y0:y1, x0:x1] = fg[y0:y1, x0:x1]
        # Pedaços muito pequenos e soltos (poeira da folha) saem.
        comps = components(keep)
        big = max(len(c[0]) for c in comps)
        for cy, cx in comps:
            h, w = cy.max() - cy.min() + 1, cx.max() - cx.min() + 1
            if len(cy) < 0.01 * big:
                keep[cy, cx] = False
        # Enfeites da moldura nos cantos (soltos, pequenos) saem; fica o item e o que está perto.
        comps = components(keep)
        if comps:
            main = max(comps, key=lambda c: len(c[0]))
            near = np.zeros_like(keep)
            near[main] = True
            near = grow(near, 10)
            for cy, cx in comps:
                if not near[cy, cx].any() and len(cy) < 0.3 * len(main[0]):
                    keep[cy, cx] = False
        out.append(icon(rgb, keep))
    return out


def icon(rgb, keep):
    ys, xs = np.where(keep)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    c, k = rgb[y0:y1, x0:x1].astype(float), keep[y0:y1, x0:x1].astype(float)
    s = ICON / max(y1 - y0, x1 - x0)
    w, h = max(1, round((x1 - x0) * s)), max(1, round((y1 - y0) * s))
    pre = Image.fromarray((c * k[:, :, None]).astype(np.uint8)).resize((w, h), Image.LANCZOS)
    al = np.array(Image.fromarray((k * 255).astype(np.uint8)).resize((w, h), Image.LANCZOS)).astype(float)
    out = np.clip(np.array(pre).astype(float) * 255 / np.maximum(al, 1)[:, :, None], 0, 255)
    return Image.fromarray(np.dstack([out, np.where(al >= 110, 255, 0)]).astype(np.uint8), "RGBA")


def extract(n, prefix, rows, cols):
    rgb = np.array(Image.open(f"imagens/armas/{n}.webp").convert("RGB")).astype(int)
    H, W = rgb.shape[:2]
    border = np.concatenate([rgb[:4].reshape(-1, 3), rgb[-4:].reshape(-1, 3), rgb[:, :4].reshape(-1, 3), rgb[:, -4:].reshape(-1, 3)])
    bg = np.median(border, 0)
    sat = rgb.max(2) - rgb.min(2)
    fg = (np.abs(rgb - bg).max(2) > 22) | (sat > 40)
    # Folhas com quadros por item (maças): a cor do quadro também é fundo.
    dark = (sat < 14) & (rgb.mean(2) < 80) & ~(np.abs(rgb - bg).max(2) <= 6)
    if dark.mean() > 0.15:
        q = (rgb[dark] // 4).astype(int)
        vals, cnt = np.unique(q, axis=0, return_counts=True)
        panel = vals[cnt.argmax()] * 4 + 2
        fg &= ~((np.abs(rgb - panel).max(2) <= 10) & (sat < 14))
    per_row = list(cols) if isinstance(cols, tuple) else [cols] * rows
    cols = max(per_row)
    ch, cw = H / rows, W / cols
    # Linhas pela grade; dentro da linha, os itens são os `cols` pedaços maiores (da esquerda
    # para a direita) e cada pedaço menor vai para o item mais perto.
    rows_parts = [[] for _ in range(rows)]
    # Números da folha (texto claro e sem cor, pequeno): saem antes de juntar os pedaços,
    # senão grudam no item.
    lum = rgb.mean(2)
    for cy, cx in components(fg):
        h, w = cy.max() - cy.min() + 1, cx.max() - cx.min() + 1
        if len(cy) < 1500 and h <= 48 and w <= 70 and lum[cy, cx].mean() > 140 and (sat[cy, cx] < 50).mean() > 0.8:
            fg[cy, cx] = False
    # Linhas da grade (folhas de maças e cajados): trechos retos muito longos, que atravessam
    # várias células; saem antes de juntar os pedaços.
    def long_runs(m):
        out = np.zeros_like(m)
        for i in range(m.shape[0]):
            row = m[i]
            d = np.diff(np.concatenate([[0], row.astype(int), [0]]))
            for a0, a1 in zip(np.where(d == 1)[0], np.where(d == -1)[0]):
                if a1 - a0 >= LINE.get(prefix, 500):
                    out[i, a0:a1] = True
        return out
    lines = long_runs(fg) | long_runs(fg.T).T
    fg &= ~grow(lines, 1)
    if prefix in CELLS:
        out = by_cells(rgb, fg, sat, prefix, rows, per_row)
        for i, im in enumerate(out, 1):
            im.save(f"{OUT}/{prefix}_{i:02d}.png")
        print(f"  {prefix}: {len(out)}")
        return out
    # Partes escuras (cabo, ferro escuro) quase somem no fundo e partem o item: os pedaços a
    # até 4 px uns dos outros contam como um só; o recorte usa só os pixels de desenho.
    joined = grow(fg, JOIN.get(prefix, 4))
    for jy, jx in components(joined):
        sel = fg[jy, jx]
        cy, cx = jy[sel], jx[sel]
        if len(cy) == 0:
            continue
        # Mancha do próprio fundo (escura e sem cor): não é item.
        bh, bw = cy.max() - cy.min() + 1, cx.max() - cx.min() + 1
        if (rgb[cy, cx].mean(1) < 50).mean() > 0.8 and (sat[cy, cx] < 20).mean() > 0.8 and len(cy) > 0.6 * bh * bw:
            # Item escuro com ruído do fundo em volta (vira um bloco): fica só o que se destaca bem.
            strong = np.abs(rgb[cy, cx] - bg).max(1) > 40
            if strong.sum() < 50:
                continue
            cy, cx = cy[strong], cx[strong]
        h, w = cy.max() - cy.min() + 1, cx.max() - cx.min() + 1
        rows_parts[min(rows - 1, int(cy.mean() / ch))].append((cy, cx))
    out = []
    for r in range(rows):
        parts = sorted(rows_parts[r], key=lambda p: -len(p[0]))
        n = per_row[r]
        mains = sorted(parts[:n], key=lambda p: p[1].mean())
        if len(mains) < n:
            print(f"  {prefix} linha {r + 1}: {len(mains)} itens")
        keeps = []
        for m in mains:
            k = np.zeros(fg.shape, bool)
            k[m] = True
            keeps.append([k, grow(k, 6), len(m[0])])
        centers = np.array([[m[0].mean(), m[1].mean()] for m in mains])
        for cy, cx in parts[n:]:
            d = np.hypot(centers[:, 0] - cy.mean(), centers[:, 1] - cx.mean())
            k = int(np.argmin(d))
            keep, near, size = keeps[k]
            colorful = (sat[cy, cx] > 45).mean() > 0.3
            # Brilhos colados ficam; números (soltos, pequenos, sem cor) saem.
            if near[cy, cx].any() or (len(cy) >= 0.04 * size and colorful):
                keep[cy, cx] = True
        out += [icon(rgb, k[0]) for k in keeps]
    for i, im in enumerate(out, 1):
        im.save(f"{OUT}/{prefix}_{i:02d}.png")
    print(f"  {prefix}: {len(out)}")
    return out


def main():
    previa = sys.argv[sys.argv.index("--previa") + 1] if "--previa" in sys.argv else None
    for n, (prefix, rows, cols) in SHEETS.items():
        ims = extract(n, prefix, rows, cols)
        if previa:
            os.makedirs(previa, exist_ok=True)
            cols = max(cols) if isinstance(cols, tuple) else cols
            sheet = Image.new("RGBA", (cols * 70, rows * 70), (60, 120, 60, 255))
            for i, im in enumerate(ims):
                sheet.alpha_composite(im, ((i % cols) * 70 + (70 - im.width) // 2, (i // cols) * 70 + (70 - im.height) // 2))
            sheet.save(f"{previa}/{prefix}.png")


if __name__ == "__main__":
    main()
