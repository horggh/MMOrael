"""Deriva tiles de chão que ladrilham sem emenda a partir dos pisos desenhados à mão.

Os pisos originais têm tufos no topo e faixa de terra na base: são blocos com frente
e fundo, não se repetem. Aqui extraímos só a faixa interna de grama, medimos sua
paleta e a densidade de cada tom, e regeramos a textura com ruído que dá a volta nas
bordas — mesma aparência, mas ladrilhável.

Os elementos decorativos (pedras, flores, terra exposta) saem como sprites separados,
para serem espalhados por cima do chão.

Uso:  python3 Tools/make_tileable.py
"""
from PIL import Image
import numpy as np
import os

SRC = "Assets/Art/Sprites/Environment"
OUT = SRC
PREVIEW = os.environ.get("PREVIEW_DIR", "")

INTERIOR = (4, 31)   # faixa de grama, sem os tufos do topo nem a terra da base
TILE = 32            # um tufo da arte original tem ~12px: em 16x16 só cabe um, e repete demais
VARIANTS = 4

# Tons da grama, lidos da arte original.
GRASS = {
    "base": (0x2f, 0x62, 0x44),
    "dark": (0x16, 0x34, 0x33),
    "light": (0x4e, 0x90, 0x47),
    "bright": (0x7d, 0xbb, 0x52),
}


def interior_pixels(path):
    im = np.array(Image.open(path).convert("RGBA"))
    y0, y1 = INTERIOR
    region = im[y0:y1 + 1]
    return region[region[:, :, 3] > 0][:, :3]


def measure(sources=(1,)):
    """Mede que fração da grama cada tom ocupa.

    Só o piso liso entra por padrão: os que têm flores, pedras ou grama alta
    distorcem a proporção e produzem uma textura clara e agitada demais.
    """
    counts = {k: 0 for k in GRASS}
    total = 0
    for i in sources:
        p = f"{SRC}/piso_{i:02d}.png"
        if not os.path.exists(p):
            continue
        px = interior_pixels(p)
        for name, c in GRASS.items():
            counts[name] += int((np.abs(px - np.array(c)).sum(1) < 12).sum())
        total += len(px)
    return {k: v / max(total, 1) for k, v in counts.items()}, total


def extract_stamps(index=1):
    """Recorta os tufos que o artista realmente desenhou.

    Espalhar pixels soltos em frequência estatística produz chuvisco, não grama: na
    arte original os tufos são formas deliberadas de 50+ pixels, com silhueta própria.
    Reaproveitar essas formas preserva o traço de quem desenhou.
    """
    im = np.array(Image.open(f"{SRC}/piso_{index:02d}.png").convert("RGBA"))
    y0, y1 = INTERIOR
    reg = im[y0:y1 + 1]
    rgb = reg[:, :, :3].astype(int)
    mark = (reg[:, :, 3] > 0) & (np.abs(rgb - np.array(GRASS["base"])).sum(2) > 30)

    H, W = mark.shape
    seen = np.zeros((H, W), bool)
    stamps = []
    for sy in range(H):
        for sx in range(W):
            if not mark[sy, sx] or seen[sy, sx]:
                continue
            stack, comp = [(sy, sx)], []
            seen[sy, sx] = True
            while stack:
                cy, cx = stack.pop()
                comp.append((cy, cx))
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        ny, nx = cy + dy, cx + dx
                        if 0 <= ny < H and 0 <= nx < W and mark[ny, nx] and not seen[ny, nx]:
                            seen[ny, nx] = True
                            stack.append((ny, nx))

            ys = [p[0] for p in comp]
            xs = [p[1] for p in comp]
            h, w = max(ys) - min(ys) + 1, max(xs) - min(xs) + 1
            # descarta restos de borda do recorte (tiras longas e finas)
            if len(comp) < 3 or w > TILE - 4 or h > TILE - 4 or max(w, h) / min(w, h) > 5:
                continue
            stamps.append([(p[0] - min(ys), p[1] - min(xs), tuple(reg[p[0], p[1]])) for p in comp])
    return stamps


BIG = 20   # a partir daqui o agrupamento é vegetação desenhada, não textura de fundo


def make_tile(stamps, seed):
    """Chão é só a textura de fundo: as marcas miúdas que variam o tom do verde.

    Os tufos grandes ficam de fora de propósito — viram decoração solta, colocada por
    cima. Carimbá-los no próprio tile faria a mesma moita se repetir pelo mapa inteiro.
    Posições em módulo garantem que o tile encaixe consigo mesmo em qualquer direção.
    """
    rng = np.random.default_rng(seed)
    img = np.zeros((TILE, TILE, 4), np.uint8)
    img[:, :] = (*GRASS["base"], 255)

    small = [s for s in stamps if len(s) < BIG]
    if not small:
        return img

    # densidade medida na arte: fração do fundo ocupada por marcas miúdas
    total_small = sum(len(s) for s in small)
    coverage = total_small / 1008.0
    target = int(TILE * TILE * coverage)

    painted, guard = 0, 0
    while painted < target and guard < 600:
        guard += 1
        s = small[rng.integers(0, len(small))]
        oy, ox = rng.integers(0, TILE, 2)
        flip = rng.random() < 0.5
        wmax = max(dx for _, dx, _ in s)
        for dy, dx, color in s:
            px = (wmax - dx) if flip else dx
            img[(oy + dy) % TILE, (ox + px) % TILE] = color
        painted += len(s)

    return img


def export_tufts(stamps):
    """Os agrupamentos grandes são vegetação: saem como sprites soltos."""
    big = sorted((s for s in stamps if len(s) >= BIG), key=len, reverse=True)
    made = []
    for i, s in enumerate(big, 1):
        h = max(dy for dy, _, _ in s) + 1
        w = max(dx for _, dx, _ in s) + 1
        img = np.zeros((h, w, 4), np.uint8)
        for dy, dx, color in s:
            img[dy, dx] = color
        name = f"grama_alta_{i:02d}.png"
        Image.fromarray(img, "RGBA").save(f"{OUT}/{name}")
        made.append((name, w, h, len(s)))
    return made


def extract_decorations():
    """Tudo que não é grama nem terra dentro do piso é decoração: pedra, flor, folha."""
    grass_colors = np.array(list(GRASS.values()))
    made = []
    for i in range(1, 7):
        path = f"{SRC}/piso_{i:02d}.png"
        if not os.path.exists(path):
            continue
        im = np.array(Image.open(path).convert("RGBA"))
        y0, y1 = INTERIOR
        region = im[y0:y1 + 1].copy()
        rgb = region[:, :, :3].astype(int)

        is_grass = np.zeros(rgb.shape[:2], bool)
        for c in grass_colors:
            is_grass |= np.abs(rgb - c).sum(2) < 40
        deco = (region[:, :, 3] > 0) & ~is_grass
        if deco.sum() < 12:
            continue

        H, W = deco.shape
        seen = np.zeros((H, W), bool)
        idx = 0
        for sy in range(H):
            for sx in range(W):
                if not deco[sy, sx] or seen[sy, sx]:
                    continue
                stack, comp = [(sy, sx)], []
                seen[sy, sx] = True
                while stack:
                    cy, cx = stack.pop()
                    comp.append((cy, cx))
                    for dy in (-1, 0, 1):
                        for dx in (-1, 0, 1):
                            ny, nx = cy + dy, cx + dx
                            if 0 <= ny < H and 0 <= nx < W and deco[ny, nx] and not seen[ny, nx]:
                                seen[ny, nx] = True
                                stack.append((ny, nx))
                if len(comp) < 6:
                    continue
                ys = [c[0] for c in comp]
                xs = [c[1] for c in comp]
                bw, bh = max(xs) - min(xs) + 1, max(ys) - min(ys) + 1
                # Tiras de 1px são resto de borda recortada, não decoração.
                if bw < 3 or bh < 3 or max(bw, bh) / min(bw, bh) > 4:
                    continue
                out = np.zeros((max(ys) - min(ys) + 1, max(xs) - min(xs) + 1, 4), np.uint8)
                for cy, cx in comp:
                    out[cy - min(ys), cx - min(xs)] = region[cy, cx]
                idx += 1
                name = f"deco_{i:02d}_{idx:02d}.png"
                Image.fromarray(out, "RGBA").save(f"{OUT}/{name}")
                made.append((name, out.shape[1], out.shape[0]))
    return made


def main():
    freq, total = measure()
    print(f"grama analisada: {total} pixels")
    for k, v in freq.items():
        print(f"  {k:7s} {v*100:5.1f}%")

    stamps = extract_stamps()
    print(f"\nagrupamentos lidos da arte: {len(stamps)}")

    for v in range(VARIANTS):
        Image.fromarray(make_tile(stamps, 50 + v), "RGBA").save(f"{OUT}/grama_{v+1:02d}.png")
    print(f"{VARIANTS} tiles de chao {TILE}x{TILE} ladrilhaveis (so textura de fundo)")

    tufts = export_tufts(stamps)
    print(f"{len(tufts)} tufos exportados como vegetacao solta:")
    for n, w, h, px in tufts:
        print(f"  {n}: {w}x{h} ({px}px)")

    decos = extract_decorations()
    print(f"{len(decos)} decoracoes extraidas")
    for n, w, h in decos[:12]:
        print(f"  {n}: {w}x{h}")

    if PREVIEW:
        n = 6
        canvas = np.zeros((TILE * n, TILE * n, 4), np.uint8)
        rng = np.random.default_rng(3)
        tiles = [np.array(Image.open(f"{OUT}/grama_{v+1:02d}.png")) for v in range(VARIANTS)]
        for gy in range(n):
            for gx in range(n):
                canvas[gy*TILE:(gy+1)*TILE, gx*TILE:(gx+1)*TILE] = tiles[rng.integers(0, VARIANTS)]
        Image.fromarray(canvas, "RGBA").resize((TILE*n*6, TILE*n*6), Image.NEAREST) \
            .save(f"{PREVIEW}/tile_check.png")
        single = np.tile(tiles[0], (4, 4, 1))
        Image.fromarray(single, "RGBA").resize((TILE*4*6, TILE*4*6), Image.NEAREST) \
            .save(f"{PREVIEW}/tile_seam.png")
        print("previas: tile_check (4 variantes) e tile_seam (1 repetido, para achar emenda)")


if __name__ == "__main__":
    main()
