"""Texturas próprias de chão no estilo "fazenda aconchegante" (cores vivas, tufos de grama,
terra alaranjada): desenhadas por código, sem copiar arte de nenhum jogo.

- grama_campo: verde em 3 tons com manchas suaves + tufos (2 a 4 folhinhas com ponta
  clara e base escura) espalhados ao acaso.
- areia_campo: terra/areia alaranjada em manchas suaves + pedrinhas e grãos escuros.
- agua_campo: azul em manchas suaves com ondinhas claras (rio, lago).
Tudo 64x64 (1 quadrado da grade) e emendando: o ruído e os enfeites dão a volta na borda.
Saída: Assets/Art/Sprites/Environment/Texturas/<nome>.png
Uso: python Tools/gen_texturas_campo.py
"""
import numpy as np
from PIL import Image

TEX = "Assets/Art/Sprites/Environment/Texturas/"
N = 64
rng = np.random.default_rng(1977)


def wrap_blur(a, r):
    for _ in range(r):
        a = (a + np.roll(a, 1, 0) + np.roll(a, -1, 0) + np.roll(a, 1, 1) + np.roll(a, -1, 1)) / 5
    return a


def tones(colors, blur):
    """Manchas suaves que emendam, em poucos tons (sem degradê: pixel art)."""
    n = wrap_blur(rng.random((N, N)), blur)
    cuts = np.quantile(n, np.linspace(0, 1, len(colors) + 1)[1:-1])
    return np.array(colors, float)[np.digitize(n, cuts)]


def put(img, y, x, color):
    img[y % N, x % N] = color


def grama():
    img = tones([(70, 150, 40), (84, 168, 44), (98, 182, 50)], 6)
    dark, mid, light, tip = (52, 122, 36), (110, 196, 54), (140, 214, 70), (176, 232, 96)
    for _ in range(70):
        y, x = rng.integers(0, N, 2)
        for b in range(rng.integers(2, 5)):
            bx = x + b * 2 - 2 + rng.integers(-1, 2)
            h = rng.integers(2, 5)
            lean = rng.choice([-1, 0, 0, 1])
            put(img, y + 1, bx, dark)                      # sombra na base
            for k in range(h):
                c = tip if k == h - 1 else (light if k >= h - 2 else mid)
                put(img, y - k, bx + (lean if k == h - 1 else 0), c)
    # Pontinhos claros e escuros soltos.
    for _ in range(40):
        y, x = rng.integers(0, N, 2)
        put(img, y, x, dark if rng.random() < 0.5 else light)
    return img


def areia():
    img = tones([(206, 142, 52), (218, 158, 64), (230, 174, 78)], 5)
    grain, pebble, pebble_hi, dent = (184, 120, 44), (164, 150, 128), (206, 196, 176), (196, 132, 50)
    for _ in range(90):
        y, x = rng.integers(0, N, 2)
        put(img, y, x, grain)
    for _ in range(10):
        y, x = rng.integers(0, N, 2)
        put(img, y, x, pebble)
        put(img, y, x + 1, pebble)
        put(img, y - 1, x, pebble_hi)
        put(img, y + 1, x, grain)
        put(img, y + 1, x + 1, grain)
    for _ in range(4):
        # Marquinha curva (pegada/sulco), bem discreta.
        y, x = rng.integers(0, N, 2)
        for k in range(4):
            put(img, y + (k in (0, 3)), x + k, dent)
    return img


def agua():
    img = tones([(46, 104, 170), (54, 118, 186), (64, 132, 198)], 6)
    ripple, foam = (98, 164, 220), (150, 204, 238)
    for _ in range(26):
        # Ondinha: traço claro curto, com um pixel mais claro no meio.
        y, x = rng.integers(0, N, 2)
        n = rng.integers(3, 7)
        for k in range(n):
            put(img, y, x + k, foam if k == n // 2 else ripple)
    return img


def main():
    for name, fn in (("grama_campo", grama), ("areia_campo", areia), ("agua_campo", agua)):
        img = fn().clip(0, 255).astype(np.uint8)
        Image.fromarray(img, "RGB").convert("RGBA").save(TEX + name + ".png")
        print(f"  {TEX}{name}.png {N}x{N}")


if __name__ == "__main__":
    main()
