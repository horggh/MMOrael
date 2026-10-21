"""Gera o andar do Drake a partir dos quadros parados de cada direção.

A arte só tem uma pose por direção, então as pernas (calça e bota, abaixo da barra do
casaco) são recortadas e recompostas a cada passo, sempre por TRÁS do corpo: a barra do
casaco cobre o topo da perna, como se a coxa entrasse por baixo dela.

- De frente e de costas as duas pernas aparecem separadas: a do passo sobe INTEIRA
  (sem encolher, o pé mantém o desenho) e a outra fica apoiada; o corpo sobe 1 px.
- De perfil as pernas aparecem como um bloco só: viram duas pernas em tesoura que giram a
  partir do quadril (o pé anda mais que a coxa). A de trás é a mesma perna, um pouco mais
  escura, como a perna do outro lado do corpo.

Saída: Assets/Art/Sprites/Monsters/MVP/Resources/Drake/andar_<direção>.png, 4 quadros lado a lado.
Uso: python Tools/gen_drake_walk.py   (depois de Tools/extract_boss_pirata.py)
"""
import os

import numpy as np
from PIL import Image

DIR = "Assets/Art/Sprites/Monsters/MVP/Resources/Drake"
PREVIEW = os.environ.get("PREVIEW_DIR", "")

# Por direção: linha onde as pernas saem de baixo do casaco e faixa de colunas delas (fora
# dela ficam o sabre, o gancho e a âncora, que não andam). Medido nos quadros alinhados.
LEGS = {
    "frente": (65, 47, 76),
    "esquerda": (66, 50, 70),
    "costas_direita": (68, 46, 71),
    "costas_esquerda": (67, 51, 70),
    "costas": (65, 47, 74),
}
SIDE = "esquerda"
FAR_SHADE = 0.72   # perna de trás no perfil: mais escura


def leg_region(alpha, hip, x0, x1):
    region = np.zeros_like(alpha)
    region[hip:, x0:x1 + 1] = alpha[hip:, x0:x1 + 1]
    return region


def split_legs(region, hip, x0, x1):
    """Duas pernas: a divisa é a coluna com menos pixels no meio da faixa."""
    fill = region[hip:].sum(0)
    mid0, mid1 = x0 + (x1 - x0) // 4, x1 - (x1 - x0) // 4
    cut = mid0 + int(np.argmin(fill[mid0:mid1 + 1]))
    left, right = region.copy(), region.copy()
    left[:, cut:] = False
    right[:, :cut] = False
    return left, right


RAMP = 5   # linhas em que o deslocamento cresce de 0 (quadril) até o valor cheio


def paste_leg(out, src, mask, dx=0, dy=0, swing=0, shade=1.0, top_dy=0):
    """Desenha a perna deslocada. dx/dy movem a perna; swing gira a partir do topo
    (0 no quadril, `swing` px no pé). O dx/dy não começa de uma vez na linha do quadril:
    cresce nas primeiras RAMP linhas, senão aparece um degrau (o corte) entre o que se
    move e o que fica. top_dy: o topo da perna acompanha o corpo quando ele sobe (sem
    isso abre uma fresta entre o casaco e a calça)."""
    H, W = mask.shape
    ys, xs = np.where(mask)
    if len(ys) == 0:
        return
    top, bottom = ys.min(), ys.max()
    span = max(1, bottom - top)
    layer = np.zeros_like(out)
    for y, x in zip(ys, xs):
        t = (y - top) / span
        k = min(1.0, (y - top) / RAMP)
        ny, nx = int(round(y + top_dy * (1 - k) + dy * k)), int(round(x + dx * k + swing * t))
        if 0 <= ny < H and 0 <= nx < W:
            px = src[y, x].copy()
            px[:3] = (px[:3] * shade).astype(np.uint8)
            layer[ny, nx] = px
    # Furos de 1 px que o giro (horizontal) e o esticar (vertical) deixam.
    a = layer[:, :, 3] > 0
    hole = ~a[:, 1:-1] & a[:, :-2] & a[:, 2:]
    hy, hx = np.where(hole)
    layer[hy, hx + 1] = layer[hy, hx]
    a = layer[:, :, 3] > 0
    hole = ~a[1:-1] & a[:-2] & a[2:]
    hy, hx = np.where(hole)
    layer[hy + 1, hx] = layer[hy, hx]
    drawn = layer[:, :, 3] > 0
    out[drawn] = layer[drawn]


def frame(src, body, legs_fn, body_dy):
    """Pernas primeiro (por trás), corpo por cima, deslocado body_dy."""
    out = np.zeros_like(src)
    legs_fn(out)
    H = src.shape[0]
    ys, xs = np.where(body)
    ny = np.clip(ys + body_dy, 0, H - 1)
    out[ny, xs] = src[ys, xs]
    return out


def build(name):
    hip, x0, x1 = LEGS[name]
    src = np.array(Image.open(os.path.join(DIR, name + ".png")).convert("RGBA"))
    alpha = src[:, :, 3] > 0
    region = leg_region(alpha, hip, x0, x1)
    body = alpha & ~region
    frames = []

    if name == SIDE:
        # Tesoura: perto (normal) e longe (escura) abrem e fecham a partir do quadril.
        # Quem vai à frente levanta o pé 1 px no meio do passo.
        for near, far, lift, dy in ((4, -4, 0, 0), (0, 0, -1, -1), (-4, 4, 0, 0), (0, 0, -1, -1)):
            def legs(out, near=near, far=far, lift=lift, dy=dy):
                paste_leg(out, src, region, swing=far, shade=FAR_SHADE, top_dy=dy)
                paste_leg(out, src, region, swing=near, dy=lift, top_dy=dy)
            frames.append(frame(src, body, legs, dy))
        return src, frames

    left, right = split_legs(region, hip, x0, x1)
    # Nas diagonais o pé que sobe vai 1 px para o lado em que ele anda.
    side = {"costas_direita": 1, "costas_esquerda": -1}.get(name, 0)
    steps = ((left, right), None, (right, left), None)
    for i, step in enumerate(steps):
        if step is None:
            def legs(out):
                paste_leg(out, src, left)
                paste_leg(out, src, right)
            frames.append(frame(src, body, legs, 0))
        else:
            up, planted = step
            def legs(out, up=up, planted=planted):
                paste_leg(out, src, planted, top_dy=-1)
                paste_leg(out, src, up, dy=-2, dx=side, top_dy=-1)
            frames.append(frame(src, body, legs, -1))
    return src, frames


def main():
    for name in LEGS:
        src, frames = build(name)
        H, W = src.shape[:2]
        sheet = np.zeros((H, W * len(frames), 4), np.uint8)
        for i, f in enumerate(frames):
            sheet[:, i * W:(i + 1) * W] = f
        Image.fromarray(sheet, "RGBA").save(os.path.join(DIR, f"andar_{name}.png"))
        print(f"  andar_{name}.png: {len(frames)} quadros")
        if PREVIEW:
            gif = [Image.fromarray(f, "RGBA").resize((W * 3, H * 3), Image.NEAREST)
                   .convert("P", palette=Image.ADAPTIVE) for f in frames]
            gif[0].save(os.path.join(PREVIEW, f"drake_andar_{name}.gif"), save_all=True,
                        append_images=gif[1:], duration=150, loop=0, disposal=2)


if __name__ == "__main__":
    main()
