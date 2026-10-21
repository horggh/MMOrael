"""Personagem modular (corpo + espada + escudo em camadas) a partir das três pranchas:
imagens/modular_corpo.png, modular_espada.png, modular_escudo.png.

As três têm a mesma organização: WALK (4 quadros), ATTACK (3), DEATH (4), cada um nas
direções Down, Left, Up, Right (painéis da esquerda para a direita).

Para cada prancha e animação/direção sai uma tira horizontal em
Assets/Resources/Characters/Skins/modular/<camada>/<anim>_<dir>.png (camada = corpo,
espada, escudo), com todos os quadros da tira do mesmo tamanho. Junto, o arquivo
Assets/Resources/Characters/Skins/modular/ancoras.json com, por quadro (em pixels da tira,
a partir do canto de cima à esquerda do quadro):
  - corpo: "mao_arma" e "mao_escudo" (palpite inicial; ajustar no editor RaelMMO >
    Personagem > Âncoras do modular); os pés ficam a 2 px da base, no meio do quadro;
  - espada: "empunhadura" (meio do cabo: pixels marrons/dourados);
  - escudo: "empunhadura" (centro do escudo).
O corpo é alinhado pelo tronco e pelos pés (como as outras skins).

Uso: python Tools/extract_modular.py
"""
import json
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_pixelart import resample  # noqa: E402
from extract_boss_pirata import components  # noqa: E402

SHEETS = {"corpo": "imagens/modular_corpo.png", "espada": "imagens/modular_espada.png",
          "escudo": "imagens/modular_escudo.png"}
OUT = "Assets/Resources/Characters/Skins/modular"
# A arte foi gerada com mais detalhe que a "grade nativa" (~3,5 px da imagem por pixel):
# reduzir até ela (personagem de ~52 px) deixava tudo pixelado. Metade disso (~104 px) a
# 64 px por unidade guarda o detalhe e mantém o mesmo tamanho no mapa.
PITCH = 3.47 / 2                 # px da imagem por pixel da sprite
PPU = 64
# Faixas (y na imagem original) só com os desenhos: sem títulos, rótulos e números.
BANDS = {"walk": (115, 314), "attack": (470, 670), "death": (822, 1008)}
FRAMES = {"walk": 4, "attack": 3, "death": 4}
PANELS = [("down", 18, 366), ("left", 372, 724), ("up", 729, 1079), ("right", 1084, 1432)]
BG = np.array([15, 18, 22])


def foreground(grid):
    """Fundo = o azul-escuro uniforme, achado por inundação a partir da borda de cada faixa
    (o contorno do desenho é ainda mais escuro, então não vaza para dentro)."""
    d = np.abs(grid - BG).max(2)
    cand = d <= 9
    H, W = cand.shape
    bg = np.zeros_like(cand)
    stack = [(y, x) for y in range(H) for x in (0, W - 1) if cand[y, x]]
    stack += [(y, x) for x in range(W) for y in (0, H - 1) if cand[y, x]]
    for s in stack:
        bg[s] = True
    while stack:
        y, x = stack.pop()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < H and 0 <= nx < W and cand[ny, nx] and not bg[ny, nx]:
                bg[ny, nx] = True
                stack.append((ny, nx))
    return ~bg


def frames_of(grid, fg, n):
    """Os n desenhos de um painel, da esquerda para a direita. Os quadros estão igualmente
    espaçados: o painel vira n colunas iguais. Em cada coluna fica o desenho principal e o
    que estiver colado nele; pedaços do vizinho que entram cortados na divisa saem."""
    H, W = fg.shape
    cw = W / n
    items = []
    for k in range(n):
        c0, c1 = int(round(k * cw)), int(round((k + 1) * cw))
        m = np.zeros_like(fg)
        m[:, c0:c1] = fg[:, c0:c1]
        grown = m.copy()
        grown[1:] |= m[:-1]; grown[:-1] |= m[1:]; grown[:, 1:] |= m[:, :-1]; grown[:, :-1] |= m[:, 1:]
        parts = []
        for ys, xs in components(grown):
            keep = m[ys, xs]
            if keep.sum() >= 4:
                parts.append((ys[keep], xs[keep]))
        if not parts:
            continue
        # Linha divisória dos painéis (fina e comprida) não é desenho.
        parts = [p for p in parts if not (p[1].max() - p[1].min() <= 4 and p[0].max() - p[0].min() >= 30)]
        if not parts:
            continue
        parts.sort(key=lambda p: len(p[0]), reverse=True)
        main = parts[0]
        my0, my1, mx0, mx1 = main[0].min() - 4, main[0].max() + 4, main[1].min() - 4, main[1].max() + 4
        ys, xs = [main[0]], [main[1]]
        for py, px in parts[1:]:
            # Fica só o que encosta no desenho principal (números e lixo soltos saem).
            near = py.max() >= my0 and py.min() <= my1 and px.max() >= mx0 and px.min() <= mx1
            if near and (len(py) >= 6 or len(py) >= 0.25 * len(main[0])):
                ys.append(py); xs.append(px)
        items.append((np.concatenate(ys), np.concatenate(xs)))
    return items


def body_anchor(ys, xs):
    """Tronco (mediana entre 25% e 55% da altura) e pés (base)."""
    h = ys.max() - ys.min()
    band = (ys >= ys.min() + 0.25 * h) & (ys <= ys.min() + 0.55 * h)
    return int(round(np.median(xs[band]))), int(ys.max())


def guess_hands(mask, direction, anim):
    """Mãos (x, y no quadro): as pontas dos braços na faixa entre o ombro e o quadril.
    A arma vai na mão que fica do lado para onde ele olha (de frente, a direita dele, que é
    a esquerda da imagem; de costas, a direita da imagem; de perfil, a mão da frente, que é
    a que soca no ataque); o escudo, na ponta do outro lado. Na morte, o meio do corpo."""
    ys, xs = np.where(mask)
    top, bot = ys.min(), ys.max()
    h = bot - top
    if anim == "death":
        c = (int(np.median(xs)), int(top + 0.6 * h))
        return c, c
    band = (ys >= top + 0.32 * h) & (ys <= top + 0.75 * h)
    bx, by = xs[band], ys[band]

    def tip(side):
        x = bx.min() if side < 0 else bx.max()
        sel = np.abs(bx - x) <= 4
        # 4 px para dentro: o centro do punho, não a borda do contorno.
        return int(x - side * 4), int(round(by[sel].mean()))

    left, right = tip(-1), tip(1)
    weapon_side = {"down": -1, "up": 1, "left": -1, "right": 1}[direction]
    return (left, right) if weapon_side < 0 else (right, left)


def grip_of(rgb, mask, layer):
    ys, xs = np.where(mask)
    if layer == "escudo":
        return int(round(xs.mean())), int(round(ys.mean()))
    px = rgb[ys, xs].astype(int)
    r, g, b = px[:, 0], px[:, 1], px[:, 2]
    # Cabo e guarda: marrom/dourado (vermelho > verde > azul, saturado); a lâmina é cinza.
    hilt = (r - b > 30) & (r >= g)
    if hilt.sum() >= 3:
        return int(round(xs[hilt].mean())), int(round(ys[hilt].mean()))
    return int(round(xs.mean())), int(round(ys.mean()))


def main():
    # Espada e escudo da prancha são grandes para o corpo: escala de cada camada (o editor
    # de âncoras muda).
    data = {"ppu": PPU, "escala_espada": 0.8, "escala_escudo": 0.65, "anims": []}
    grids = {}
    for layer, path in SHEETS.items():
        a = np.array(Image.open(path).convert("RGB")).astype(float)
        gw = int(round(a.shape[1] / PITCH))
        grids[layer] = (a, resample(a, gw), gw / a.shape[1])

    for anim, (y0, y1) in BANDS.items():
        n = FRAMES[anim]
        for direction, x0, x1 in PANELS:
            key = f"{anim}_{direction}"
            per_layer = {}
            for layer, (a, grid, s) in grids.items():
                gy0, gy1, gx0, gx1 = int(y0 * s), int(y1 * s), int(x0 * s), int(x1 * s)
                sub = grid[gy0:gy1, gx0:gx1]
                fg = foreground(sub)
                its = frames_of(sub, fg, n)
                if len(its) != n:
                    print(f"  aviso: {layer} {key}: {len(its)} quadros, esperava {n}")
                per_layer[layer] = (sub, fg, its)

            # Corpo: quadro comum alinhado pelo tronco e pelos pés.
            sub, fg, its = per_layer["corpo"]
            anchors = [body_anchor(ys, xs) for ys, xs in its]
            half = max(max(ax - xs.min(), xs.max() - ax) for (ys, xs), (ax, _) in zip(its, anchors)) + 3
            height = max(fy - ys.min() for (ys, xs), (_, fy) in zip(its, anchors)) + 4
            fw, fh = half * 2, height
            strip = np.zeros((fh, fw * len(its), 4), np.uint8)
            frames = []
            for i, ((ys, xs), (ax, fy)) in enumerate(zip(its, anchors)):
                ty, tx = ys - fy + fh - 2, xs - ax + half
                ok = (ty >= 0) & (ty < fh) & (tx >= 0) & (tx < fw)
                cell = np.zeros((fh, fw), bool)
                cell[ty[ok], tx[ok]] = True
                strip[ty[ok], i * fw + tx[ok], :3] = sub[ys[ok], xs[ok]].astype(np.uint8)
                strip[ty[ok], i * fw + tx[ok], 3] = 255
                w_hand, s_hand = guess_hands(cell, direction, anim)
                frames.append({"mao_arma": list(w_hand), "mao_escudo": list(s_hand)})
            save_strip(strip, "corpo", key)
            entry = {"chave": key, "largura": fw, "altura": fh, "corpo": frames}

            # Arma e escudo: cada quadro recortado no próprio tamanho (tira com o maior).
            for layer in ("espada", "escudo"):
                sub, fg, its = per_layer[layer]
                boxes = [(ys.min(), ys.max(), xs.min(), xs.max()) for ys, xs in its]
                lw = max(b[3] - b[2] + 1 for b in boxes) + 2
                lh = max(b[1] - b[0] + 1 for b in boxes) + 2
                strip = np.zeros((lh, lw * len(its), 4), np.uint8)
                grips = []
                for i, ((ys, xs), (by0, by1, bx0, bx1)) in enumerate(zip(its, boxes)):
                    oy = (lh - (by1 - by0 + 1)) // 2
                    ox = (lw - (bx1 - bx0 + 1)) // 2
                    ty, tx = ys - by0 + oy, xs - bx0 + ox
                    strip[ty, i * lw + tx, :3] = sub[ys, xs].astype(np.uint8)
                    strip[ty, i * lw + tx, 3] = 255
                    m = np.zeros((lh, lw), bool)
                    m[ty, tx] = True
                    rgb = np.zeros((lh, lw, 3))
                    rgb[ty, tx] = sub[ys, xs]
                    grips.append({"empunhadura": list(grip_of(rgb, m, layer))})
                save_strip(strip, layer, key)
                entry[layer] = {"largura": lw, "altura": lh, "quadros": grips}
                entry[layer + "_quadros"] = len(grips)

            # Ordem das camadas por direção (palpite; o editor muda): arma na mão direita.
            entry["arma_na_frente"] = direction != "up"
            entry["escudo_na_frente"] = direction == "down"
            data["anims"].append(entry)
            print(f"  {key}: corpo {fw}x{fh} x{len(its)}")

    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, "ancoras.json"), "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=1, default=int)
    print(f"  {OUT}/ancoras.json")


def save_strip(strip, layer, key):
    d = os.path.join(OUT, layer)
    os.makedirs(d, exist_ok=True)
    Image.fromarray(strip, "RGBA").save(os.path.join(d, key + ".png"))


if __name__ == "__main__":
    main()
