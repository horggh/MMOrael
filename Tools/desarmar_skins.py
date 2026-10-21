"""Tira a espada desenhada dos quadros de ataque do Cavaleiro e do Andarilho e prepara as
duas skins para a mecânica de camadas (a espada e o escudo equipados aparecem por cima, só
no golpe — ModularLayers), como o "Cavaleiro (camadas)".

Para cada quadro com espada:
1. A lâmina é a reta clara e fina mais longa do quadro (RANSAC; uma mancha clara, como o
   cabelo branco do Andarilho, perde porque não é fina).
2. Saem a lâmina, o contorno dela e o arco azul do golpe. O que a lâmina cobria do corpo
   é refeito com a cor dos vizinhos; o que ficava fora do corpo vira transparente.
3. A espada apagada vira a âncora: a ponta junto do corpo é a mão; a direção da lâmina
   escolhe, entre os quadros da espada do "Cavaleiro (camadas)", o de inclinação mais
   parecida, e o comprimento dá a escala.
O escudo vai na ponta do outro braço.

Saída:
  Assets/Resources/Characters/Skins/<skin>/sem_arma/<anim>.png   (quadros sem a espada)
  Assets/Resources/Characters/Skins/<skin>/ancoras.json          (âncoras e camada de cada quadro)
Uso: python Tools/desarmar_skins.py
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_boss_pirata import components  # noqa: E402

RES = "Assets/Resources/"
LIB = "Characters/Skins/modular/"
LIB_JSON = RES + LIB + "ancoras.json"

# skin: (pasta de saída, ppu, [(chave da animação, arquivo de origem, quadros, olha para a esquerda)])
SKINS = {
    "cavaleiro": ("Characters/Skins/cavaleiro", 64, [
        ("attack_side", "Characters/Skins/cavaleiro/attack_side", 4, False),
    ]),
    "andarilho": ("Characters/Skins/andarilho", 64, [
        ("attack_down", "Characters/Skins/andarilho/attack_down", 3, False),
        ("attack_side", "Characters/Skins/andarilho/attack_side", 4, False),
        ("attack_up", "Characters/Skins/andarilho/attack_up", 4, False),
        ("death", "Characters/Skins/andarilho/death", 6, False),
    ]),
}
rng = np.random.default_rng(11)
# Aumento da arte em relação ao tamanho para o qual os limites (em pixels) foram ajustados.
S = 1.0
WIDE_CUT = False
ART_SCALE = {"cavaleiro": 2.94, "andarilho": 1.68}


# ── espada da biblioteca (Cavaleiro em camadas): direção e comprimento de cada quadro ──

LIB_PPU = 32


def library():
    global LIB_PPU
    lib = json.load(open(LIB_JSON, encoding="utf-8"))
    LIB_PPU = lib.get("ppu", 32)
    out = []
    for a in lib["anims"]:
        L = a["espada"]
        img = np.array(Image.open(RES + LIB + "espada/" + a["chave"] + ".png").convert("RGBA"))
        for k, q in enumerate(L["quadros"]):
            fr = img[:, k * L["largura"]:(k + 1) * L["largura"]]
            ys, xs = np.where(fr[:, :, 3] > 0)
            gx, gy = q["empunhadura"]
            d = np.hypot(xs - gx, ys - gy)
            i = int(np.argmax(d))
            out.append({"chave": a["chave"], "quadro": k, "ang": math.atan2(ys[i] - gy, xs[i] - gx),
                        "len": float(d[i])})
    return out


def best_sword(lib, ang, length):
    def diff(e):
        return abs((e["ang"] - ang + math.pi) % (2 * math.pi) - math.pi)
    e = min(lib, key=diff)
    return e["chave"], e["quadro"], round(max(0.3, min(2.0, length / max(e["len"], 1))), 3)


# ── achar e apagar a lâmina ──

def find_blade(fr):
    """Reta da lâmina: (pontos da lâmina, ponta perto do corpo, ponta longe) ou None."""
    a = fr.astype(int)
    alpha = a[:, :, 3] > 0
    rgb = a[:, :, :3]
    lum = rgb.mean(2)
    metal = alpha & (lum >= 125) & (rgb[:, :, 2] >= rgb[:, :, 0] - 6) & ((rgb.max(2) - rgb.min(2)) <= 80)
    ys, xs = np.where(metal)
    if len(ys) < 8 * S * S:
        return None
    pts = np.stack([xs, ys], 1).astype(float)
    best, best_score = None, -1e9
    for _ in range(500):
        i, j = rng.choice(len(pts), 2, replace=False)
        d = pts[j] - pts[i]
        n = np.hypot(*d)
        if n < 5 * S:
            continue
        nrm = np.array([-d[1], d[0]]) / n
        dist = np.abs((pts - pts[i]) @ nrm)
        inl = dist < 1.3 * S
        band = dist < 3.2 * S
        score = (inl.sum() - 1.5 * (band.sum() - inl.sum())) / S
        if score > best_score:
            best_score, best = score, (inl, pts[i], d / n)
    if best is None or best[0].sum() < 8 * S or best_score < 6:
        return None
    inl, p0, u = best
    # Só o trecho contínuo mais longo da reta.
    t = (pts[inl] - p0) @ u
    order = np.argsort(t)
    t_sorted = t[order]
    gaps = np.where(np.diff(t_sorted) > 2.5 * S)[0]
    starts = np.concatenate([[0], gaps + 1]); ends = np.concatenate([gaps, [len(t_sorted) - 1]])
    k = int(np.argmax(t_sorted[ends] - t_sorted[starts]))
    t0, t1 = t_sorted[starts[k]], t_sorted[ends[k]]
    if t1 - t0 < 7 * S:
        return None
    blade_pts = pts[inl][(t >= t0) & (t <= t1)]
    e0, e1 = p0 + u * t0, p0 + u * t1
    body = np.stack(np.where(alpha), 1)[:, ::-1].astype(float)
    center = np.median(body, 0)
    near, far = (e0, e1) if np.hypot(*(e0 - center)) < np.hypot(*(e1 - center)) else (e1, e0)
    return blade_pts, near, far


def remove_weapon(fr):
    """Quadro sem a espada (e sem o arco azul). Devolve (quadro, lâmina ou None)."""
    out = fr.copy()
    a = fr.astype(int)
    alpha = a[:, :, 3] > 0
    H, W = alpha.shape
    removed = np.zeros_like(alpha)
    blade = find_blade(fr)
    if blade is not None:
        bp, near, far = blade
        u = (far - near) / max(np.hypot(*(far - near)), 1e-6)
        nrm = np.array([-u[1], u[0]])
        yy, xx = np.mgrid[0:H, 0:W]
        rel = np.stack([xx - near[0], yy - near[1]], -1)
        along, across = rel @ u, np.abs(rel @ nrm)
        length = np.hypot(*(far - near))
        # Lâmina (clara) e o contorno escuro colado nela, ao longo da reta, da mão até a
        # ponta (um pouco além).
        on_line = (along >= S) & (along <= length + 2 * S) & (across <= (1.7 if S == 1 else 2.3) * S)
        lum = a[:, :, :3].mean(2)
        removed |= on_line & alpha & ((lum >= 110) | ((lum < 70) & (across >= 0.8 * S)))
        if WIDE_CUT:
            # Arte ampliada: o contorno da lâmina fica grosso; além da mão sai tudo na faixa.
            removed |= alpha & (along >= 3 * S) & (along <= length + 3 * S) & (across <= 3.2 * S)
    # Arco azul do golpe.
    rgb = a[:, :, :3]
    bluish = alpha & (rgb[:, :, 2] - rgb[:, :, 0] >= 12) & (rgb.mean(2) >= 105)
    removed |= bluish
    if bluish.sum() > 30 * S * S:
        # Xadrez que aparecia dentro do arco: cinza claro neutro colado no que saiu.
        neutral = (rgb.max(2) - rgb.min(2) < 14) & (rgb.mean(2) > 95)
        grown = removed.copy()
        for _ in range(round(3 * S)):
            g = grown.copy()
            g[1:] |= grown[:-1]; g[:-1] |= grown[1:]; g[:, 1:] |= grown[:, :-1]; g[:, :-1] |= grown[:, 1:]
            grown = g
        for cy, cx in components(alpha & neutral & ~removed):
            if len(cy) >= 4 and grown[cy, cx].any():
                removed[cy, cx] = True
    keep = alpha & ~removed
    # Fios finos que sobram (contorno da lâmina): pixel com até 2 vizinhos sai, de fora para
    # dentro, algumas vezes (a borda do corpo tem mais vizinhos e fica).
    for _ in range(round(8 * S)):
        pad = np.pad(keep, 1)
        n8 = sum(pad[1 + dy:1 + dy + H, 1 + dx:1 + dx + W].astype(int)
                 for dy in (-1, 0, 1) for dx in (-1, 0, 1) if dy or dx)
        thin = keep & (n8 <= 2)
        if not thin.any():
            break
        keep &= ~thin
        removed |= thin
    # Pedaços soltos que sobram (fim do arco, ponta da lâmina): saem.
    parts = components(keep)
    if parts:
        main = max(parts, key=lambda c: len(c[0]))
        for cy, cx in parts:
            if cy is not main[0] and len(cy) < 40 * S * S:
                keep[cy, cx] = False
                removed[cy, cx] = True
    # Buracos dentro do corpo: cor dos vizinhos; fora do corpo: transparente.
    rgbf = out[:, :, :3].astype(float)
    fill = removed & alpha
    for _ in range(14):
        pad = np.pad(keep, 2)
        cnt = sum(pad[2 + dy:2 + dy + H, 2 + dx:2 + dx + W].astype(int)
                  for dy in range(-2, 3) for dx in range(-2, 3))
        inner = fill & (cnt >= 11)
        if not inner.any():
            break
        for y, x in zip(*np.where(inner)):
            y0, y1, x0, x1 = max(0, y - 1), min(H, y + 2), max(0, x - 1), min(W, x + 2)
            m = keep[y0:y1, x0:x1]
            if m.any():
                rgbf[y, x] = rgbf[y0:y1, x0:x1][m].mean(0)
                keep[y, x] = True
                fill[y, x] = False
    out[:, :, :3] = rgbf.clip(0, 255).astype(np.uint8)
    out[:, :, 3] = np.where(keep, 255, 0)
    return out, blade


def shield_hand(mask, weapon_xy):
    """Ponta do braço do lado oposto ao da arma, entre o ombro e o quadril."""
    ys, xs = np.where(mask)
    top, bot = ys.min(), ys.max()
    h = bot - top
    band = (ys >= top + 0.32 * h) & (ys <= top + 0.75 * h)
    bx, by = xs[band], ys[band]
    cx = np.median(xs)
    side = -1 if weapon_xy[0] >= cx else 1
    x = bx.min() if side < 0 else bx.max()
    sel = np.abs(bx - x) <= 2
    return [int(x - side * 2), int(round(by[sel].mean()))]


def main():
    lib = library()
    global S, WIDE_CUT
    for skin, (folder, ppu, anims) in SKINS.items():
        S = ART_SCALE.get(skin, 1.0)
        WIDE_CUT = skin == "cavaleiro"
        out_anims = []
        os.makedirs(RES + folder + "/sem_arma", exist_ok=True)
        for key, src, n, faces_left in anims:
            img = np.array(Image.open(RES + src + ".png").convert("RGBA"))
            fw, fh = img.shape[1] // n, img.shape[0]
            strip = img.copy()
            frames = []
            for k in range(n):
                fr = img[:, k * fw:(k + 1) * fw]
                clean, blade = remove_weapon(fr)
                strip[:, k * fw:(k + 1) * fw] = clean
                mask = clean[:, :, 3] > 0
                if blade is not None:
                    _, near, far = blade
                    ang = math.atan2(far[1] - near[1], far[0] - near[0])
                    length = float(np.hypot(*(far - near))) * LIB_PPU / ppu   # em pixels da biblioteca
                    chave, quadro, escala = best_sword(lib, ang, length)
                    hand = [int(round(near[0])), int(round(near[1]))]
                else:
                    ys, xs = np.where(mask)
                    hand = [int(np.median(xs)), int(ys.min() + 0.6 * (ys.max() - ys.min()))]
                    chave, quadro, escala = "", -1, 0.0
                frames.append({"mao_arma": hand, "mao_escudo": shield_hand(mask, hand),
                               "arma_chave": chave, "arma_quadro": quadro, "arma_escala": escala,
                               "escudo_chave": "attack_" + {"attack_up": "up", "attack_down": "down"}.get(key, "right"),
                               "escudo_quadro": 0})
                print(f"  {skin} {key} {k}: lâmina {'achada' if blade is not None else 'não achada'}"
                      + (f", espada = {chave} #{quadro} x{escala}" if chave else ""))
            # Quadro de golpe sem lâmina visível (só o arco): usa a espada e a mão do quadro
            # mais próximo que tinha lâmina.
            found = [i for i, f in enumerate(frames) if f["arma_chave"]]
            for i, f in enumerate(frames):
                if not f["arma_chave"] and found:
                    j = min(found, key=lambda k: abs(k - i))
                    for field in ("arma_chave", "arma_quadro", "arma_escala", "mao_arma"):
                        f[field] = frames[j][field]
            Image.fromarray(strip, "RGBA").save(RES + folder + "/sem_arma/" + key + ".png")
            direction = {"attack_up": "up", "attack_down": "down"}.get(key, "side")
            out_anims.append({"chave": key, "largura": fw, "altura": fh, "corpo": frames,
                              "arma_na_frente": direction != "up", "escudo_na_frente": direction == "down"})
        data = {"ppu": ppu, "escala_espada": 1.0, "escala_escudo": 0.65,
                "biblioteca": LIB + "ancoras", "anims": out_anims}
        with open(RES + folder + "/ancoras.json", "w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
