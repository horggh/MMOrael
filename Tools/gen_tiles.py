"""Gera tiles e props de cenário em pixel art (placeholder).

Tiles de 16x16 para chão; árvores como sprites maiores.
Paleta quente, no espírito de um campo aberto acolhedor.

Uso:  python3 Tools/gen_tiles.py
"""
from PIL import Image
import numpy as np
import os

OUT = "Assets/Art/Sprites/Environment"
PREVIEW = os.environ.get("PREVIEW_DIR", "")
T = 16

GRASS = [(74, 122, 54), (88, 140, 62), (102, 158, 74), (120, 176, 88)]
DIRT = [(132, 102, 68), (150, 120, 84), (112, 86, 56), (166, 138, 100)]
BARK = [(92, 62, 40), (68, 44, 28), (116, 82, 54)]
LEAF = [(58, 110, 48), (74, 132, 58), (92, 154, 72), (44, 88, 38)]


def noise_tile(palette, seed, speckle=0.18, accent=None):
    rng = np.random.default_rng(seed)
    img = np.zeros((T, T, 4), np.uint8)
    base = np.array(palette[0])
    for y in range(T):
        for x in range(T):
            r = rng.random()
            c = palette[1] if r < 0.34 else (palette[2] if r < 0.5 else base)
            img[y, x] = (*c, 255)
    for _ in range(int(T * T * speckle)):
        x, y = rng.integers(0, T, 2)
        img[y, x] = (*palette[3], 255)
    if accent:
        for _ in range(accent[0]):
            x, y = rng.integers(1, T - 1, 2)
            img[y, x] = (*accent[1], 255)
            img[y - 1, x] = (*accent[1], 255)
    return img


def tree(seed, w=32, h=48):
    """Copa em blocos sobrepostos + tronco. Contorno escuro para separar do fundo."""
    rng = np.random.default_rng(seed)
    img = np.zeros((h, w, 4), np.uint8)
    cx = w // 2

    for y in range(h - 14, h - 1):
        for x in range(cx - 3, cx + 3):
            img[y, x] = (*BARK[0], 255)
        img[y, cx - 3] = (*BARK[1], 255)
        img[y, cx + 2] = (*BARK[1], 255)
        if rng.random() < 0.3:
            img[y, cx + rng.integers(-2, 2)] = (*BARK[2], 255)

    blobs = [(cx, 14, 13), (cx - 8, 20, 10), (cx + 8, 20, 10), (cx, 25, 11), (cx, 9, 9)]
    for bx, by, rad in blobs:
        for y in range(by - rad, by + rad):
            for x in range(bx - rad, bx + rad):
                if 0 <= x < w and 0 <= y < h:
                    d = ((x - bx) / rad) ** 2 + ((y - by) / (rad * 0.85)) ** 2
                    if d <= 1.0:
                        sh = LEAF[0] if y > by else (LEAF[2] if rng.random() < 0.45 else LEAF[1])
                        img[y, x] = (*sh, 255)

    opaque = img[:, :, 3] > 0
    out = img.copy()
    for y in range(h):
        for x in range(w):
            if not opaque[y, x]:
                near = False
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        ny, nx = y + dy, x + dx
                        if 0 <= ny < h and 0 <= nx < w and opaque[ny, nx]:
                            near = True
                if near:
                    out[y, x] = (26, 20, 16, 255)
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    made = []

    for i in range(4):
        acc = (3, (150, 190, 96)) if i == 3 else None
        Image.fromarray(noise_tile(GRASS, 100 + i, accent=acc), "RGBA").save(f"{OUT}/grass_{i:02d}.png")
        made.append(f"grass_{i:02d}")
    for i in range(3):
        Image.fromarray(noise_tile(DIRT, 200 + i, speckle=0.12), "RGBA").save(f"{OUT}/dirt_{i:02d}.png")
        made.append(f"dirt_{i:02d}")
    for i in range(2):
        Image.fromarray(tree(300 + i), "RGBA").save(f"{OUT}/tree_{i:02d}.png")
        made.append(f"tree_{i:02d}")

    print("gerados:", ", ".join(made))

    if PREVIEW:
        scene = np.zeros((16 * 10, 16 * 16, 4), np.uint8)
        rng = np.random.default_rng(7)
        grass = [np.array(Image.open(f"{OUT}/grass_{i:02d}.png")) for i in range(4)]
        dirt = [np.array(Image.open(f"{OUT}/dirt_{i:02d}.png")) for i in range(3)]
        for gy in range(10):
            for gx in range(16):
                path = abs(gx - (7 + int(2.5 * np.sin(gy * 0.7)))) <= 1
                src = dirt[rng.integers(0, 3)] if path else grass[rng.integers(0, 4)]
                scene[gy * 16:(gy + 1) * 16, gx * 16:(gx + 1) * 16] = src
        for tx, ty in ((1, 1), (12, 2), (3, 6), (13, 6)):
            tr = np.array(Image.open(f"{OUT}/tree_{(tx + ty) % 2:02d}.png"))
            py, px = ty * 16 - 32, tx * 16 - 8
            for y in range(tr.shape[0]):
                for x in range(tr.shape[1]):
                    sy, sx = py + y, px + x
                    if 0 <= sy < scene.shape[0] and 0 <= sx < scene.shape[1] and tr[y, x, 3] > 0:
                        scene[sy, sx] = tr[y, x]
        kn = np.array(Image.open("Assets/Art/Sprites/Characters/cavaleiro.png"))
        py, px = 10 * 16 - 48 - 2, 7 * 16 - 24
        for y in range(48):
            for x in range(48):
                sy, sx = py + y, px + x
                if 0 <= sy < scene.shape[0] and 0 <= sx < scene.shape[1] and kn[y, x, 3] > 0:
                    scene[sy, sx] = kn[y, x]
        Image.fromarray(scene, "RGBA").resize((scene.shape[1] * 4, scene.shape[0] * 4), Image.NEAREST) \
            .save(f"{PREVIEW}/scene_preview.png")
        print("previa salva")


if __name__ == "__main__":
    main()
