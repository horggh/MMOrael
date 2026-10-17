"""Separa as 60 janelas (imagens/janelas-1.jpeg e janelas-2.jpeg, grade 6x5 cada).

O fundo xadrez sai pela POSIÇÃO de cada quadrado (como no extract_casa.py), inclusive
dentro dos vidros: esses vãos viram transparência. Para cada janela com vão, gera também
<id>_ceu.png — o céu claro só no formato dos vãos — que o jogo põe por trás da janela,
como se lá fora fosse dia. E uma textura comum de raios de sol (luz_raios.png).

Saída: Assets/Resources/Windows/. A janela fica na resolução da imagem (64 px = 1 quadrado
da parede no jogo).

Uso:  python3 Tools/extract_janelas.py
"""
import os
import numpy as np
from PIL import Image

OUT = "Assets/Resources/Windows"
SOURCES = ["imagens/janelas-2.jpeg", "imagens/janelas-1.jpeg"]   # clássicas primeiro
COLS, ROWS = 6, 5
TOL = 16

# Nomes na ordem da grade de cada imagem (linha a linha).
NAMES = {
    "imagens/janelas-2.jpeg": [
        ("janela_simples", "Simples"), ("janela_venezianas_abertas", "Venezianas abertas"),
        ("janela_arco_madeira", "Arco de madeira"), ("janela_floreira", "Com floreira"),
        ("janela_cortinas_vermelhas", "Cortinas vermelhas"), ("janela_portinhola", "Portinhola fechada"),
        ("janela_grade_ferro", "Grade de ferro"), ("janela_cela", "Cela"),
        ("janela_quebrada", "Quebrada com tábuas"), ("janela_porao", "Porão"),
        ("janela_escotilha_metal", "Escotilha de metal"), ("janela_seteira", "Seteira"),
        ("janela_vitral_gotico", "Vitral gótico"), ("janela_rosacea", "Rosácea"),
        ("janela_elfica", "Élfica"), ("janela_runas_brilhantes", "Runas brilhantes"),
        ("janela_arcos_pedra", "Arcos de pedra"), ("janela_lua_estrela", "Lua e estrela"),
        ("janela_cortinas_brancas", "Cortinas brancas"), ("janela_cortinas_azuis", "Cortinas azuis"),
        ("janela_sacada_telhado", "Sacada com telhado"), ("janela_triangular", "Triangular"),
        ("janela_moderna", "Moderna"), ("janela_shoji", "Shoji"),
        ("janela_hera_redonda", "Redonda com hera"), ("janela_congelada", "Congelada"),
        ("janela_nave", "Nave espacial"), ("janela_ferro_forjado", "Ferro forjado"),
        ("janela_tabuas", "Tábuas pregadas"), ("janela_tijolo_redonda", "Redonda de tijolo"),
    ],
    "imagens/janelas-1.jpeg": [
        ("janela_vitral_floresta", "Vitral da floresta"), ("janela_tela_futurista", "Tela futurista"),
        ("janela_engrenagem", "Escotilha de engrenagem"), ("janela_vitral_morcego", "Vitral do morcego"),
        ("janela_lua_sol", "Lua e sol"), ("janela_quadrada_entalhada", "Quadrada entalhada"),
        ("janela_doze_vidros", "Doze vidros"), ("janela_sacada_pedra", "Sacada de pedra"),
        ("janela_art_deco", "Art déco"), ("janela_losangos", "Losangos de chumbo"),
        ("janela_dupla", "Dupla"), ("janela_cruz_pedra", "Cruz de pedra"),
        ("janela_hera", "Coberta de hera"), ("janela_arco_cortina", "Arco com pano"),
        ("janela_aquario", "Aquário"), ("janela_lava", "Lava"),
        ("janela_teia", "Teia"), ("janela_musgo", "Musgo"),
        ("janela_sacada_cortinas", "Sacada com cortinas"), ("janela_estufa", "Estufa"),
        ("janela_venezianas_floreira", "Venezianas com floreira"), ("janela_claraboia", "Claraboia"),
        ("janela_vitral_fenix", "Vitral da fênix"), ("janela_meia_lua", "Meia-lua"),
        ("janela_lapide", "Lápide de caveira"), ("janela_gaiola", "Gaiola suspensa"),
        ("janela_tela_metal", "Tela de metal"), ("janela_escotilha_mar", "Escotilha do mar"),
        ("janela_casinha_doce", "Casinha de doce"), ("janela_moldura_barroca", "Moldura barroca"),
    ],
}


def checker_fit(a):
    """Tamanho e deslocamento dos quadrados, ajustados só em pixels cinzas (as janelas
    encostam no topo da imagem, então não dá para usar uma faixa só de fundo)."""
    lum = a.mean(2)
    neutral = (np.abs(a[:, :, 0] - a[:, :, 1]) < 6) & (np.abs(a[:, :, 1] - a[:, :, 2]) < 6)
    lo = np.median(lum[neutral & (lum < 110)])
    hi = np.median(lum[neutral & (lum >= 110) & (lum < 170)])
    ys, xs = np.where(neutral & ((np.abs(lum - lo) < 6) | (np.abs(lum - hi) < 6)))
    rng = np.random.default_rng(1)
    pick = rng.choice(len(ys), min(30000, len(ys)), replace=False)
    ys, xs, l = ys[pick], xs[pick], lum[ys[pick], xs[pick]]
    best = None
    for p in np.arange(16.8, 17.4, 0.02):
        for ox in np.arange(0, p, 0.5):
            for oy in np.arange(0, p, 0.5):
                par = (np.floor((xs + ox) / p) + np.floor((ys + oy) / p)) % 2
                err = np.abs(l - np.where(par == 0, lo, hi)).mean()
                if best is None or err < best[0]:
                    best = (err, p, ox, oy)
    return best[1], best[2], best[3], lo, hi


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


def local_std(lum, r=2):
    """Desvio do brilho numa janela (2r+1)x(2r+1) em volta de cada pixel."""
    pad = np.pad(lum, r + 1, mode="edge")
    s1 = pad.cumsum(0).cumsum(1)
    s2 = (pad ** 2).cumsum(0).cumsum(1)
    k = 2 * r + 1

    def box(s):
        return s[k:, k:] - s[:-k, k:] - s[k:, :-k] + s[:-k, :-k]
    n = k * k
    m = box(s1)[:lum.shape[0], :lum.shape[1]] / n
    v = box(s2)[:lum.shape[0], :lum.shape[1]] / n - m ** 2
    return np.sqrt(np.maximum(v, 0))


def background(a):
    p, ox, oy, lo, hi = checker_fit(a)
    print(f"  xadrez: quadrado {p:.2f}px, tons {lo:.0f}/{hi:.0f}")
    H, W, _ = a.shape
    lum = a.mean(2)
    neutral = (np.abs(a[:, :, 0] - a[:, :, 1]) < 12) & (np.abs(a[:, :, 1] - a[:, :, 2]) < 12)
    yy, xx = np.mgrid[0:H, 0:W]
    fx, fy = (xx + ox) / p, (yy + oy) / p
    expected = np.where((np.floor(fx) + np.floor(fy)) % 2 == 0, lo, hi)
    edge = (np.minimum(fx % 1, 1 - fx % 1) * p < 1.6) | (np.minimum(fy % 1, 1 - fy % 1) * p < 1.6)
    cand = neutral & (np.abs(lum - expected) < TOL)
    cand |= neutral & edge & (lum > lo - TOL) & (lum < hi + TOL)
    # Sem depender da posição: bloco liso de cinza puro num dos dois tons é quadrado do xadrez
    # (em parte das imagens a grade desalinha um pouco e o "tom esperado" erra).
    flat = neutral & (local_std(lum) < 4) & ((np.abs(lum - lo) < 12) | (np.abs(lum - hi) < 12))
    cand |= flat

    border = [(y, x) for y in range(H) for x in (0, W - 1)] + [(y, x) for x in range(W) for y in (0, H - 1)]
    bg = flood(cand, border)

    # Vãos fechados (vidros): região cinza que segue o xadrez e é cinza puro vira transparente.
    strict = neutral & ((np.abs(lum - expected) < 12) | edge)
    seen = bg.copy()
    for y0, x0 in zip(*np.where(cand & ~bg)):
        if seen[y0, x0]:
            continue
        comp = flood(cand & ~seen, [(y0, x0)])
        seen |= comp
        cy, cx = np.where(comp)
        core = ~edge[cy, cx]
        if len(cy) < 12 or core.sum() == 0:
            continue
        l, e = lum[cy, cx][core], expected[cy, cx][core]
        dark = np.mean(e == lo)
        off = np.abs(l - e).mean()
        hue = (a[cy, cx, 2] - a[cy, cx, 0]).mean()
        match = strict[cy, cx].mean()
        # Cinza puro seguindo o xadrez (pedra e metal puxam para outra cor ou ficam num tom só).
        plain = match >= 0.9 and hue < 1.0 and (
            (len(cy) >= 40 and 0.1 < dark < 0.9 and off < 9) or (l.std() < 5 and off < 6))
        # Vidro levemente colorido: área grande com os dois tons do xadrez, cor fraca.
        glass = len(cy) >= 150 and 0.15 < dark < 0.85 and off < 10 and match >= 0.8 and abs(hue) < 15
        # Vidro onde a grade desalinhou: quase todo em blocos lisos, com os dois tons presentes.
        lc = lum[cy, cx]
        both = np.mean(np.abs(lc - lo) < 14) > 0.15 and np.mean(np.abs(lc - hi) < 14) > 0.15
        glass |= len(cy) >= 120 and flat[cy, cx].mean() > 0.6 and both and abs(hue) < 15
        if plain or glass:
            bg[cy, cx] = True

    bg |= tinted_glass(a, lum, bg)

    for _ in range(2):   # auréola do JPEG em volta do desenho
        near = np.zeros_like(bg)
        near[1:] |= bg[:-1]; near[:-1] |= bg[1:]; near[:, 1:] |= bg[:, :-1]; near[:, :-1] |= bg[:, 1:]
        bg |= ~bg & near & neutral & (lum > lo - 10) & (lum < hi + 10)
    return bg, p


def tinted_glass(a, lum, bg):
    """Vidro colorido e semitransparente: o xadrez aparece em outros tons (mais escuro,
    esverdeado...). Procura a forma dele em vez da cor: quadradinhos lisos de 9 a 18 px
    vizinhos de outro quadradinho com brilho bem diferente (claro/escuro alternando)."""
    H, W = lum.shape
    sat = a.max(2) - a.min(2)
    flat = (local_std(lum) < 5) & (sat < 45) & ~bg
    squares = []
    seen = np.zeros_like(flat)
    for y, x in zip(*np.where(flat)):
        if seen[y, x]:
            continue
        comp = flood(flat & ~seen, [(y, x)])
        seen |= comp
        ys, xs = np.where(comp)
        h, w = ys.max() - ys.min() + 1, xs.max() - xs.min() + 1
        if 9 <= h <= 18 and 9 <= w <= 18 and len(ys) >= 0.7 * h * w:
            squares.append((ys.mean(), xs.mean(), lum[ys, xs].mean(), ys, xs))
    glass = np.zeros((H, W), bool)
    for i, (cy, cx, l, ys, xs) in enumerate(squares):
        for cy2, cx2, l2, _, _ in squares:
            d = np.hypot(cy - cy2, cx - cx2)
            if 13 <= d <= 22 and abs(l - l2) > 15:
                glass[ys, xs] = True
                break
    # As divisas entre os quadradinhos (borradas) entram também.
    for _ in range(3):
        g = glass.copy()
        g[1:] |= glass[:-1]; g[:-1] |= glass[1:]; g[:, 1:] |= glass[:, :-1]; g[:, :-1] |= glass[:, 1:]
        glass = g & ~bg & (sat < 45)
    return glass


def refine_glass(rgb, mask, period):
    """Por janela: ajusta a grade do xadrez só nos pixels lisos dentro dela e mede os dois
    tons do vidro. Se os tons alternam (há xadrez visto pelo vidro), o que bate com a grade
    e com o tom do quadrado vira transparente. Só mexe em vãos fechados (longe da borda)."""
    a = rgb.astype(float)
    lum = a.mean(2)
    sat = a.max(2) - a.min(2)
    h, w = lum.shape
    flat = mask & (local_std(lum) < 5) & (sat < 45)
    ys, xs = np.where(flat)
    if len(ys) < 150:
        return mask
    l = lum[ys, xs]
    best = None
    for ox in np.arange(0, period, 1.0):
        for oy in np.arange(0, period, 1.0):
            par = ((np.floor((xs + ox) / period) + np.floor((ys + oy) / period)) % 2).astype(bool)
            if par.sum() < 40 or (~par).sum() < 40:
                continue
            t0, t1 = np.median(l[~par]), np.median(l[par])
            err = np.abs(l - np.where(par, t1, t0)).mean()
            if best is None or err < best[0]:
                best = (err, ox, oy, t0, t1)
    if best is None:
        return mask
    err, ox, oy, t0, t1 = best
    if abs(t0 - t1) < 15 or err > 7:
        return mask   # sem xadrez no vidro
    yy, xx = np.mgrid[0:h, 0:w]
    fx, fy = (xx + ox) / period, (yy + oy) / period
    par = ((np.floor(fx) + np.floor(fy)) % 2).astype(bool)
    expected = np.where(par, t1, t0)
    edge = (np.minimum(fx % 1, 1 - fx % 1) * period < 1.6) | (np.minimum(fy % 1, 1 - fy % 1) * period < 1.6)
    lo, hi = min(t0, t1), max(t0, t1)
    cand = mask & (sat < 45) & ((np.abs(lum - expected) < 10) | (edge & (lum > lo - 10) & (lum < hi + 10)))
    out = mask.copy()
    seen = np.zeros_like(cand)
    for y, x in zip(*np.where(cand)):
        if seen[y, x]:
            continue
        comp = flood(cand & ~seen, [(y, x)])
        seen |= comp
        cy, cx = np.where(comp)
        # Grande o bastante e longe da borda do recorte (vão de vidro, não contorno).
        if len(cy) >= 30 and cy.min() > 1 and cx.min() > 1 and cy.max() < h - 2 and cx.max() < w - 2:
            out[cy, cx] = False
    return out


def drop_specks(fg, min_size=25):
    seen = np.zeros_like(fg)
    out = fg.copy()
    for y, x in zip(*np.where(fg)):
        if seen[y, x]:
            continue
        comp = flood(fg & ~seen, [(y, x)])
        seen |= comp
        if comp.sum() < min_size:
            out &= ~comp
    return out


def group_by_cell(fg):
    """Cada bloco de desenho conectado vai para a célula da grade onde fica o centro dele
    (as janelas largas passam da divisão em partes iguais). Pedacinhos perto um do outro
    (gelo, folhas) contam como o mesmo bloco. Devolve o índice da célula por pixel (-1 = fundo)."""
    H, W = fg.shape
    near = fg.copy()
    for _ in range(1):   # junta pedaços a até 1 px (mais que isso cola janelas vizinhas)
        g = near.copy()
        g[1:] |= near[:-1]; g[:-1] |= near[1:]; g[:, 1:] |= near[:, :-1]; g[:, :-1] |= near[:, 1:]
        near = g
    cells = np.full((H, W), -1)
    seen = np.zeros_like(near)
    blocks = {}
    for y, x in zip(*np.where(near)):
        if seen[y, x]:
            continue
        comp = flood(near & ~seen, [(y, x)])
        seen |= comp
        ys, xs = np.where(comp & fg)
        if len(ys) == 0:
            continue
        r = min(ROWS - 1, int(ys.mean() * ROWS / H))
        c = min(COLS - 1, int(xs.mean() * COLS / W))
        blocks.setdefault(r * COLS + c, []).append((ys, xs))
    # Em cada célula, pedaço muito menor que a janela é sobra do fundo (quadradinho solto).
    for cell, parts in blocks.items():
        biggest = max(len(ys) for ys, _ in parts)
        for ys, xs in parts:
            if len(ys) >= 0.04 * biggest:
                cells[ys, xs] = cell
    return cells


def sky(holes):
    """Céu de dia só nos vãos: azul claro em cima, quase branco quente embaixo."""
    h, w = holes.shape
    t = np.linspace(0, 1, h)[:, None]
    top, bottom = np.array([150, 205, 245]), np.array([255, 246, 220])
    rgb = (top * (1 - t) + bottom * t)[:, :, None].repeat(w, 1).reshape(h, w, 3)
    rgb = np.broadcast_to(top * (1 - t[:, :, None]) + bottom * t[:, :, None], (h, w, 3))
    # Vão um pixel maior por baixo da moldura, para não sobrar fresta.
    grow = holes.copy()
    grow[1:] |= holes[:-1]; grow[:-1] |= holes[1:]; grow[:, 1:] |= holes[:, :-1]; grow[:, :-1] |= holes[:, 1:]
    return np.dstack([rgb.astype(np.uint8), np.where(grow, 255, 0).astype(np.uint8)])


def rays():
    """Raios de sol: faixas claras que somem para baixo (o jogo estica e inclina)."""
    w, h = 64, 256
    y = np.linspace(0, 1, h)[:, None]
    x = np.arange(w)[None, :]
    streaks = 0.65 + 0.35 * np.sin(x / w * np.pi * 5 + 0.7) ** 2
    alpha = (1 - y) ** 1.6 * streaks * 150
    rgb = np.zeros((h, w, 3)) + np.array([255, 238, 180])
    return np.dstack([rgb, alpha]).astype(np.uint8)


def main():
    os.makedirs(OUT, exist_ok=True)
    for src in SOURCES:
        print(src)
        a = np.array(Image.open(src).convert("RGB")).astype(float)
        bg, period = background(a)
        fg = drop_specks(~bg)
        H, W = fg.shape
        cells = group_by_cell(fg)
        for i, (wid, name) in enumerate(NAMES[src]):
            cell_mask = cells == i
            ys, xs = np.where(cell_mask)
            cy0, cy1, cx0, cx1 = ys.min(), ys.max(), xs.min(), xs.max()
            mask = cell_mask[cy0:cy1 + 1, cx0:cx1 + 1]
            rgb = a[cy0:cy1 + 1, cx0:cx1 + 1].astype(np.uint8)
            mask = refine_glass(rgb, mask, period)
            img = np.dstack([rgb, np.where(mask, 255, 0).astype(np.uint8)])
            Image.fromarray(img, "RGBA").save(os.path.join(OUT, wid + ".png"))

            # Vãos: transparência que não se liga à borda do recorte.
            h, w = mask.shape
            border = [(y, x) for y in range(h) for x in (0, w - 1)] + [(y, x) for x in range(w) for y in (0, h - 1)]
            outside = flood(~mask, border)
            holes = ~mask & ~outside
            glass = holes.sum() / mask.size
            if glass > 0.02:
                Image.fromarray(sky(holes), "RGBA").save(os.path.join(OUT, wid + "_ceu.png"))
            elif os.path.exists(os.path.join(OUT, wid + "_ceu.png")):
                os.remove(os.path.join(OUT, wid + "_ceu.png"))
            print(f"    {wid}: {w}x{h}" + (f"  vidro {glass:.0%}" if glass > 0.02 else ""))
    Image.fromarray(rays(), "RGBA").save(os.path.join(OUT, "luz_raios.png"))


if __name__ == "__main__":
    main()
