"""Gera animações do lobo a partir do sprite estático.

Quadrúpede de perfil: separa as quatro patas por conectividade (elas são inclinadas,
então faixas fixas de coluna não isolariam direito) e recompõe cada quadro movendo
pares diagonais — que é como um quadrúpede anda de verdade: pata traseira esquerda
junto com dianteira direita, e vice-versa.

Uso:  python3 Tools/gen_lobo_anim.py
"""
from PIL import Image
import numpy as np
import os

SRC = "Assets/Art/Sprites/Monsters/lobo.png"
OUT = "Assets/Art/Sprites/Monsters"
PREVIEW = os.environ.get("PREVIEW_DIR", "")

LEG_TOP = 49      # a partir desta linha as quatro patas já aparecem separadas
PAD_X, PAD_Y = 6, 4


def components(mask):
    H, W = mask.shape
    seen = np.zeros((H, W), bool)
    out = []
    for sy in range(H):
        for sx in range(W):
            if not mask[sy, sx] or seen[sy, sx]:
                continue
            stack, comp = [(sy, sx)], []
            seen[sy, sx] = True
            while stack:
                cy, cx = stack.pop()
                comp.append((cy, cx))
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        ny, nx = cy + dy, cx + dx
                        if 0 <= ny < H and 0 <= nx < W and mask[ny, nx] and not seen[ny, nx]:
                            seen[ny, nx] = True
                            stack.append((ny, nx))
            if len(comp) >= 12:
                out.append(comp)
    return out


def build():
    src = np.array(Image.open(SRC).convert("RGBA"))
    H, W = src.shape[:2]
    alpha = src[:, :, 3] > 0

    legs_region = alpha.copy()
    legs_region[:LEG_TOP] = False
    comps = components(legs_region)

    # A ponta da cauda desce abaixo da linha das patas e vira um componente solto,
    # mas é muito menor que uma perna. Sem este corte ela seria animada como pata.
    if len(comps) > 4:
        comps.sort(key=len, reverse=True)
        comps = comps[:4]

    comps.sort(key=lambda c: min(p[1] for p in c))   # da traseira para a dianteira

    print(f"patas encontradas: {len(comps)}")
    for i, c in enumerate(comps, 1):
        xs = [p[1] for p in c]
        print(f"  pata {i}: x {min(xs)}-{max(xs)} ({len(c)}px)")

    leg_masks = []
    for c in comps:
        m = np.zeros((H, W), bool)
        for y, x in c:
            m[y, x] = True
        leg_masks.append(m)

    body = alpha.copy()
    for m in leg_masks:
        body &= ~m

    return src, body, leg_masks


def compose(src, body, legs, body_d=(0, 0), leg_d=None, canvas=None):
    """Recompõe um quadro.

    As patas não se movem como bloco rígido: transladar a pata inteira descola o
    topo dela da barriga e aparece como um corte horizontal atravessando o lobo.
    Em vez disso cada pata gira a partir do quadril — o deslocamento cresce de cima
    (zero, preso ao corpo) para baixo (máximo, no pé). O topo também acompanha o
    sobe-e-desce do corpo, enquanto o pé continua apoiado no chão.
    """
    H, W = src.shape[:2]
    CW, CH = W + PAD_X * 2, H + PAD_Y * 2
    out = np.zeros((CH, CW, 4), np.uint8)
    bdx, bdy = body_d

    def put(y, x, ny, nx):
        ny, nx = int(round(ny)) + PAD_Y, int(round(nx)) + PAD_X
        if 0 <= ny < CH and 0 <= nx < CW:
            out[ny, nx] = src[y, x]

    leg_d = leg_d or [(0, 0)] * len(legs)
    for m, (swing, lift) in zip(legs, leg_d):
        layer = np.zeros_like(out)
        ys, xs = np.where(m)
        top, bottom = ys.min(), ys.max()
        span = max(1, bottom - top)
        for y, x in zip(ys, xs):
            t = (y - top) / span                 # 0 no quadril, 1 no pé
            ox = bdx + swing * t                 # pé balança, quadril segue o corpo
            oy = bdy * (1 - t) + lift * t        # quadril sobe com o corpo, pé fica no chão
            ny, nx = int(round(y + oy)) + PAD_Y, int(round(x + ox)) + PAD_X
            if 0 <= ny < CH and 0 <= nx < CW:
                layer[ny, nx] = src[y, x]

        # Quando a pata estica (quadril sobe, pé fica), o ponto em que o arredondamento
        # muda deixa uma linha sem pixel — aparece como um risco atravessando a pata.
        # Preenche furos de 1px na vertical repetindo o pixel de cima.
        a = layer[:, :, 3] > 0
        hole = ~a[1:-1] & a[:-2] & a[2:]
        hy, hx = np.where(hole)
        layer[hy + 1, hx] = layer[hy, hx]

        drawn = layer[:, :, 3] > 0
        out[drawn] = layer[drawn]

    ys, xs = np.where(body)
    for y, x in zip(ys, xs):
        put(y, x, y + bdy, x + bdx)
    return out


def main():
    src, body, legs = build()
    n = len(legs)
    if n < 4:
        print(f"aviso: esperava 4 patas, achei {n} - a animacao sai mais pobre")

    # (balanço do pé, elevação do pé) por pata. Patas ordenadas da traseira para a
    # dianteira: 0 e 1 são as traseiras, 2 e 3 as dianteiras.

    def gait(a, b):
        """Passada: pata mais traseira com a mais dianteira, e as duas do meio juntas.
        Quem avança levanta o pé; quem recua fica apoiado."""
        d = [(0, 0)] * n
        if n >= 4:
            d[0] = d[3] = a
            d[1] = d[2] = b
        return d

    def planted(body_dx, hind=0, front=0):
        """No bote o corpo avança mas as traseiras ficam cravadas no chão: o balanço
        delas anula o deslocamento do corpo. As dianteiras se esticam à frente."""
        d = [(0, 0)] * n
        if n >= 4:
            d[0] = d[1] = (-body_dx + hind, 0)
            d[2] = d[3] = (front, 0)
        return d

    step, lift = 3, -1
    anims = {
        "idle": [
            compose(src, body, legs),
            compose(src, body, legs, body_d=(0, -1)),
        ],
        "walk": [
            compose(src, body, legs, leg_d=gait((step, lift), (-step, 0))),
            compose(src, body, legs, body_d=(0, -1), leg_d=gait((1, 0), (-1, 0))),
            compose(src, body, legs, leg_d=gait((-step, 0), (step, lift))),
            compose(src, body, legs, body_d=(0, -1), leg_d=gait((-1, 0), (1, 0))),
        ],
        "run": [
            compose(src, body, legs, body_d=(0, -1), leg_d=gait((5, -2), (-5, 0))),
            compose(src, body, legs, body_d=(0, -2), leg_d=gait((2, -1), (-2, 0))),
            compose(src, body, legs, body_d=(0, -1), leg_d=gait((-5, 0), (5, -2))),
            compose(src, body, legs, body_d=(0, -2), leg_d=gait((-2, 0), (2, -1))),
        ],
        # Bote: recua, lança o corpo à frente com as traseiras cravadas, volta à guarda.
        "attack": [
            compose(src, body, legs, body_d=(-3, 1), leg_d=planted(-3)),
            compose(src, body, legs, body_d=(1, -1), leg_d=planted(1, front=1)),
            compose(src, body, legs, body_d=(6, 0), leg_d=planted(6, hind=2, front=3)),
            compose(src, body, legs, body_d=(3, 0), leg_d=planted(3, hind=1, front=1)),
            compose(src, body, legs),
        ],
    }

    for name, frames in anims.items():
        fh, fw = frames[0].shape[:2]
        sheet = np.zeros((fh, fw * len(frames), 4), np.uint8)
        for i, f in enumerate(frames):
            sheet[:, i * fw:(i + 1) * fw] = f
        Image.fromarray(sheet, "RGBA").save(f"{OUT}/lobo_{name}.png")
        print(f"  lobo_{name}: {len(frames)} quadros de {fw}x{fh}")

        if PREVIEW:
            gif = [Image.fromarray(f, "RGBA").resize((fw * 3, fh * 3), Image.NEAREST)
                   .convert("P", palette=Image.ADAPTIVE) for f in frames]
            gif[0].save(f"{PREVIEW}/lobo_{name}.gif", save_all=True, append_images=gif[1:],
                        duration=130, loop=0, disposal=2)


if __name__ == "__main__":
    main()
