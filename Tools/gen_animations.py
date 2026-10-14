"""Gera tiras de animação a partir do sprite estático do Cavaleiro.

Separa o sprite em camadas (espada, capa, pernas, corpo) e recompõe cada quadro
com deslocamentos diferentes. É animação por recorte: serve de placeholder até a
arte final ser desenhada quadro a quadro.

Uso:  python3 Tools/gen_animations.py
"""
from PIL import Image
import numpy as np
import os

SRC = "Assets/Art/Sprites/Characters/cavaleiro.png"
OUT = "Assets/Art/Sprites/Characters"
PREVIEW = os.environ.get("PREVIEW_DIR", "")
W = H = 48

BLADE = ("f3feff", "9cdfff", "b4d4e9", "80afcb")
CAPE = ("ba3a33", "752131", "50162c")


def hexc(s):
    return np.array([int(s[i:i + 2], 16) for i in (0, 2, 4)])


def load():
    return np.array(Image.open(SRC).convert("RGBA"))


def mask_of(base, alpha, hexes):
    m = np.zeros((H, W), bool)
    for h in hexes:
        c = hexc(h)
        m |= (base[:, :, 0] == c[0]) & (base[:, :, 1] == c[1]) & (base[:, :, 2] == c[2]) & alpha
    return m


def largest_component(m):
    """A armadura compartilha tons com a lâmina, então a máscara por cor vem suja.
    Só o maior grupo conectado é a espada de verdade."""
    lab = np.zeros((H, W), int)
    cur = 0
    for y in range(H):
        for x in range(W):
            if m[y, x] and lab[y, x] == 0:
                cur += 1
                stack = [(y, x)]
                lab[y, x] = cur
                while stack:
                    cy, cx = stack.pop()
                    for dy in (-1, 0, 1):
                        for dx in (-1, 0, 1):
                            ny, nx = cy + dy, cx + dx
                            if 0 <= ny < H and 0 <= nx < W and m[ny, nx] and lab[ny, nx] == 0:
                                lab[ny, nx] = cur
                                stack.append((ny, nx))
    if cur == 0:
        return m
    sizes = [(lab == i).sum() for i in range(1, cur + 1)]
    return lab == (int(np.argmax(sizes)) + 1)


def dilate(m, alpha, n=1):
    out = m.copy()
    for _ in range(n):
        g = out.copy()
        g[1:, :] |= out[:-1, :]
        g[:-1, :] |= out[1:, :]
        g[:, 1:] |= out[:, :-1]
        g[:, :-1] |= out[:, 1:]
        out = g
    return out & alpha


def build_layers(base):
    alpha = base[:, :, 3] > 0
    sword = dilate(largest_component(mask_of(base, alpha, BLADE)), alpha)
    cape = dilate(mask_of(base, alpha, CAPE), alpha) & ~sword
    rows = np.arange(H)[:, None]
    cols = np.arange(W)[None, :]
    legs = alpha & ~sword & ~cape & (rows >= 40)
    return {
        "sword": sword,
        "cape": cape,
        "legL": legs & (cols < 21),
        "legR": legs & (cols >= 21),
        "body": alpha & ~sword & ~cape & ~legs,
    }


def sword_pivot(sword):
    """A mão fica na ponta de baixo da lâmina: linha mais baixa, coluna mais à esquerda."""
    ys, xs = np.where(sword)
    bottom = ys.max()
    near = xs[ys >= bottom - 1]
    return float(near.min()), float(bottom)


class Composer:
    def __init__(self, base, layers, pivot):
        self.base, self.L, self.pivot = base, layers, pivot

    def _blit(self, dst, mask, dx=0, dy=0, over=False):
        ys, xs = np.where(mask)
        for y, x in zip(ys, xs):
            ny, nx = y + dy, x + dx
            if 0 <= ny < H and 0 <= nx < W and (over or dst[ny, nx, 3] == 0):
                dst[ny, nx] = self.base[y, x]

    def _blit_rot(self, dst, mask, ang, px, py):
        t = np.radians(ang)
        ct, st = np.cos(t), np.sin(t)
        pts = {}
        ys, xs = np.where(mask)
        for y, x in zip(ys, xs):
            rx, ry = x - px, y - py
            nx, ny = px + rx * ct - ry * st, py + rx * st + ry * ct
            key = (int(round(ny)), int(round(nx)))
            pts[key] = self.base[y, x]
            # tapa buracos que a rotação abre entre pixels vizinhos
            for oy, ox in ((0, 1), (1, 0)):
                pts.setdefault((key[0] + oy, key[1] + ox), self.base[y, x])
        for (y, x), c in pts.items():
            if 0 <= y < H and 0 <= x < W:
                dst[y, x] = c

    def frame(self, body_dy=0, legL=(0, 0), legR=(0, 0), cape=(0, 0),
              sword_ang=0, sword_d=(0, 0), lean=0):
        f = np.zeros((H, W, 4), np.uint8)
        self._blit(f, self.L["cape"], cape[0] + lean, cape[1])
        self._blit(f, self.L["legL"], legL[0], legL[1])
        self._blit(f, self.L["legR"], legR[0], legR[1])
        self._blit(f, self.L["body"], lean, body_dy)
        if sword_ang:
            self._blit_rot(f, self.L["sword"], sword_ang,
                           self.pivot[0] + lean, self.pivot[1] + body_dy)
        else:
            self._blit(f, self.L["sword"], sword_d[0] + lean, sword_d[1] + body_dy, over=True)
        return f


def main():
    base = load()
    layers = build_layers(base)
    pivot = sword_pivot(layers["sword"])
    print("camadas:", {k: int(v.sum()) for k, v in layers.items()})
    print("pivo da espada (mao):", pivot)

    c = Composer(base, layers, pivot)
    anims = {
        "idle": [c.frame(), c.frame(body_dy=-1, cape=(0, -1))],
        "walk": [c.frame(legL=(1, 0), legR=(-1, 0)),
                 c.frame(body_dy=-1, cape=(0, -1)),
                 c.frame(legL=(-1, 0), legR=(1, 0)),
                 c.frame(body_dy=-1, cape=(0, -1))],
        "run": [c.frame(body_dy=-1, legL=(2, 0), legR=(-2, 0), cape=(-2, -1), lean=1),
                c.frame(body_dy=-2, legL=(1, -1), legR=(-1, 0), cape=(-3, -2), lean=1),
                c.frame(body_dy=-1, legL=(-2, 0), legR=(2, 0), cape=(-2, -1), lean=1),
                c.frame(body_dy=-2, legL=(-1, 0), legR=(1, -1), cape=(-3, -2), lean=1)],
        "attack": [c.frame(sword_ang=-30, cape=(1, 0), lean=-1),
                   c.frame(sword_ang=-10, body_dy=-1),
                   c.frame(sword_ang=35, cape=(-2, 0), lean=1),
                   c.frame(sword_ang=15, lean=1),
                   c.frame(sword_ang=0)],
    }

    for name, frames in anims.items():
        sheet = np.zeros((H, W * len(frames), 4), np.uint8)
        for i, f in enumerate(frames):
            sheet[:, i * W:(i + 1) * W] = f
        Image.fromarray(sheet, "RGBA").save(f"{OUT}/cavaleiro_{name}.png")
        print(f"  {name}: {len(frames)} quadros")

        if PREVIEW:
            gif = [Image.fromarray(f, "RGBA").resize((W * 6, H * 6), Image.NEAREST)
                   .convert("P", palette=Image.ADAPTIVE) for f in frames]
            gif[0].save(f"{PREVIEW}/prev_{name}.gif", save_all=True, append_images=gif[1:],
                        duration=130, loop=0, disposal=2)


if __name__ == "__main__":
    main()
