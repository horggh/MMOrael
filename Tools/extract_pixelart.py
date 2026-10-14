"""Extrai pixel art em resolução nativa de uma captura ampliada.

Resolve o problema de receber a arte como JPEG gigante com fundo xadrez: descobre
o tamanho real do pixel, reamostra, transforma o xadrez em transparência, separa
os sprites e limpa o ruído de compressão do JPEG.

Uso:
    python3 Tools/extract_pixelart.py imagens/Arvores.jpeg Assets/Art/Sprites/Environment arvore
    python3 Tools/extract_pixelart.py imagens/Cavaleiro.jpeg Assets/Art/Sprites/Characters cavaleiro --single
"""
import sys
import os
import numpy as np
from PIL import Image


def detect_pitch(a):
    """Descobre o tamanho real de cada pixel da arte.

    Medir o espaçamento entre mudanças de cor não funciona: pixels vizinhos de mesma
    cor se fundem, então os espaçamentos observados são múltiplos do valor verdadeiro
    (e o do xadrez de fundo costuma dominar). Em vez disso, testamos cada grade
    candidata e escolhemos a que deixa os blocos mais uniformes por dentro — na grade
    certa, cada bloco é de cor sólida e a variância interna cai para quase zero.
    """
    H, W, _ = a.shape
    scores = {}

    for gw in range(20, 641):
        pitch = W / gw
        if pitch < 3:
            break
        gh = int(round(H / pitch))
        if gh < 8:
            continue

        total, n = 0.0, 0
        for gy in range(2, gh - 2, max(1, gh // 24)):
            for gx in range(2, gw - 2, max(1, gw // 24)):
                x0, x1 = int(gx * pitch) + 1, int((gx + 1) * pitch) - 1
                y0, y1 = int(gy * pitch) + 1, int((gy + 1) * pitch) - 1
                if x1 <= x0 or y1 <= y0:
                    continue
                blk = a[y0:y1, x0:x1].reshape(-1, 3)
                if len(blk) < 4:
                    continue
                total += blk.std(axis=0).mean()
                n += 1

        if n >= 20:
            scores[gw] = total / n

    if not scores:
        raise SystemExit("nao consegui medir a grade de pixels")

    # Qualquer grade mais fina que a verdadeira também fica uniforme (cada pixel real
    # vira um bloco NxN sólido), então a de menor variância é sempre fina demais.
    # A resposta certa é a grade MAIS GROSSA que ainda tem variância quase nula.
    floor = min(scores.values())
    limit = max(floor * 1.8, floor + 1.2)
    return W / min(gw for gw, s in scores.items() if s <= limit)


def resample(a, gw):
    H, W, _ = a.shape
    pitch = W / gw
    gh = int(round(H / pitch))
    out = np.zeros((gh, gw, 3), int)
    for gy in range(gh):
        for gx in range(gw):
            cx, cy = (gx + 0.5) * pitch, (gy + 0.5) * pitch
            x0, x1 = int(cx - pitch * 0.3), int(cx + pitch * 0.3)
            y0, y1 = int(cy - pitch * 0.3), int(cy + pitch * 0.3)
            blk = a[max(0, y0):min(H, y1 + 1), max(0, x0):min(W, x1 + 1)].reshape(-1, 3)
            out[gy, gx] = np.median(blk, axis=0).astype(int)
    return out


def background_mask(grid):
    """Detecta o xadrez de transparência pelas cores reais da imagem.

    Faixa de brilho fixa não serve: cada editor desenha o xadrez num cinza diferente.
    Como ele cobre a maior parte da folha, os dois cinzas neutros mais frequentes são
    justamente os quadrados do xadrez.
    """
    r, g, b = grid[:, :, 0], grid[:, :, 1], grid[:, :, 2]
    neutral = (abs(r - g) < 10) & (abs(g - b) < 10) & (abs(r - b) < 10)
    if neutral.sum() < 20:
        return np.zeros(grid.shape[:2], bool)

    vals, counts = np.unique(grid[neutral].reshape(-1, 3), axis=0, return_counts=True)

    # Os dois tons precisam ser distantes entre si: o ruído do JPEG cria vários
    # vizinhos quase idênticos do mesmo cinza, e pegar os dois mais frequentes
    # devolveria o mesmo quadrado duas vezes, deixando metade do xadrez de fora.
    tones = []
    for i in np.argsort(-counts):
        c = vals[i]
        if all(np.abs(c - t).sum() > 40 for t in tones):
            tones.append(c)
        if len(tones) == 2:
            break

    if len(tones) < 2:
        return np.zeros(grid.shape[:2], bool)

    # O JPEG borra a fronteira entre quadrados claro e escuro, criando cinzas
    # intermediários que não batem com nenhum dos dois tons. Aceita toda a faixa
    # entre eles — e depois preenche a partir das bordas, porque o xadrez é
    # conectado e encosta nos limites da imagem, enquanto a arte fica ilhada no
    # meio. Sem isso, cinzas legítimos da arte (pedras, metal) seriam apagados.
    lo = min(t.min() for t in tones) - 10
    hi = max(t.max() for t in tones) + 10
    cand = neutral & (grid.mean(2) >= lo) & (grid.mean(2) <= hi)

    H, W = cand.shape
    bg = np.zeros((H, W), bool)
    stack = []
    for x in range(W):
        for y in (0, H - 1):
            if cand[y, x] and not bg[y, x]:
                bg[y, x] = True
                stack.append((y, x))
    for y in range(H):
        for x in (0, W - 1):
            if cand[y, x] and not bg[y, x]:
                bg[y, x] = True
                stack.append((y, x))

    while stack:
        cy, cx = stack.pop()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = cy + dy, cx + dx
            if 0 <= ny < H and 0 <= nx < W and cand[ny, nx] and not bg[ny, nx]:
                bg[ny, nx] = True
                stack.append((ny, nx))
    return bg


def split_columns(fg, min_width=5):
    fill = fg.sum(0)
    groups, start = [], None
    for x in range(fg.shape[1]):
        if fill[x] > 0 and start is None:
            start = x
        elif fill[x] == 0 and start is not None:
            groups.append((start, x - 1))
            start = None
    if start is not None:
        groups.append((start, fg.shape[1] - 1))
    return [g for g in groups if g[1] - g[0] >= min_width]


def quantize(rgb, alpha, dist=34):
    """Agrupa cores próximas, começando pelas mais frequentes.
    O JPEG cria centenas de tons quase iguais; pixel art real tem poucas dezenas."""
    px = rgb.reshape(-1, 3)[alpha.reshape(-1) > 0]
    cols, cnts = np.unique(px, axis=0, return_counts=True)
    reps = []
    for i in np.argsort(-cnts):
        c = cols[i]
        if all(((c - np.array(r)) ** 2).sum() > dist ** 2 for r in reps):
            reps.append(tuple(c))
    reps = np.array(reps)
    d = ((rgb.reshape(-1, 1, 3) - reps.reshape(1, -1, 3)) ** 2).sum(2)
    return reps[d.argmin(1)].reshape(rgb.shape).astype(np.uint8), len(reps)


def main():
    if len(sys.argv) < 4:
        print(__doc__)
        raise SystemExit(1)
    src, outdir, name = sys.argv[1], sys.argv[2], sys.argv[3]
    single = "--single" in sys.argv

    # A detecção automática é só um palpite: ruído de JPEG faz a variância cair suave,
    # sem degrau nítido, e grades 2x finas demais pontuam quase igual. Quando o palpite
    # erra, passe a largura correta com --grid N (confira o resultado visualmente).
    forced = None
    if "--grid" in sys.argv:
        forced = int(sys.argv[sys.argv.index("--grid") + 1])

    a = np.array(Image.open(src).convert("RGB")).astype(float)
    if forced:
        gw = forced
        pitch = a.shape[1] / gw
        print(f"grade informada: {gw} (passo {pitch:.2f}px)")
    else:
        pitch = detect_pitch(a)
        gw = int(round(a.shape[1] / pitch))
        print(f"grade estimada: {gw} (passo {pitch:.2f}px) - confira, use --grid N se errado")
    grid = resample(a, gw)

    bg = background_mask(grid)
    fg = ~bg

    if single:
        groups = [(0, gw - 1)]
    elif "--split" in sys.argv:
        # Folhas de tile vêm coladas, sem coluna vazia entre elas: corta em N partes iguais.
        n = int(sys.argv[sys.argv.index("--split") + 1])
        xs = np.where(fg.any(0))[0]
        x0, x1 = int(xs.min()), int(xs.max())
        step = (x1 - x0 + 1) / n
        groups = [(int(round(x0 + i * step)), int(round(x0 + (i + 1) * step)) - 1) for i in range(n)]
    else:
        groups = split_columns(fg)
    print(f"sprites encontrados: {len(groups)}")

    os.makedirs(outdir, exist_ok=True)
    for i, (x0, x1) in enumerate(groups):
        sub_fg = fg[:, x0:x1 + 1]
        ys = np.where(sub_fg.any(1))[0]
        xs = np.where(sub_fg.any(0))[0]
        y0, y1 = ys.min(), ys.max()
        rgb = grid[y0:y1 + 1, x0 + xs.min():x0 + xs.max() + 1].astype(int)
        alpha = np.where(bg[y0:y1 + 1, x0 + xs.min():x0 + xs.max() + 1], 0, 255).astype(np.uint8)

        snapped, ncols = quantize(rgb, alpha)
        out = np.dstack([snapped, alpha]).astype(np.uint8)

        fname = f"{name}.png" if single else f"{name}_{i + 1:02d}.png"
        Image.fromarray(out, "RGBA").save(os.path.join(outdir, fname))
        print(f"  {fname}: {out.shape[1]}x{out.shape[0]}, {ncols} cores")


if __name__ == "__main__":
    main()
