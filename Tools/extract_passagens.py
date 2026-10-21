"""Escadas e portas (decoração com passagem: clicar perto abre "Subir!"/"Entrar").
Saída: Assets/Art/Sprites/Environment/Decoracao/Passagens/ (escada_*.png, porta_NN.png).

- escada_pedra: imagens/escada_pedra.webp (fundo transparente).
- escada_madeira: imagens/escada_madeira.webp (fundo preto com brilho marrom em volta): sai
  coluna a coluna, do ponto de madeira clara mais alto ao mais baixo (o brilho fica fora).
- porta_01..: imagens/portas.webp (fundo branco semitransparente), separadas pelos grupos de
  pixels, na ordem de leitura (linha a linha).
Escala: escada com 3 quadrados de largura; portas com a altura de ~2 quadrados (32 PPU).

Uso: python Tools/extract_passagens.py
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from extract_boss_pirata import components  # noqa: E402
from extract_predios_folha import border_flood  # noqa: E402

OUT = "Assets/Art/Sprites/Environment/Decoracao/Passagens"
PPU = 32


def resize(crop, keep, w, h):
    crop = crop.astype(float)
    al = keep.astype(float)
    pre = Image.fromarray((crop[:, :, :3] * al[:, :, None]).astype(np.uint8)).resize((w, h), Image.LANCZOS)
    alr = np.array(Image.fromarray((al * 255).astype(np.uint8)).resize((w, h), Image.LANCZOS)).astype(float)
    rgb = np.clip(np.array(pre).astype(float) * 255 / np.maximum(alr, 1)[:, :, None], 0, 255)
    return Image.fromarray(np.dstack([rgb, np.where(alr >= 128, 255, 0)]).astype(np.uint8), "RGBA")


def save(a, keep, name, width=None, scale=None):
    ys, xs = np.where(keep)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    crop, k = a[y0:y1, x0:x1], keep[y0:y1, x0:x1]
    s = width / (x1 - x0) if width else scale
    out = resize(crop, k, max(1, round((x1 - x0) * s)), max(1, round((y1 - y0) * s)))
    out.save(f"{OUT}/{name}.png")
    print(f"  {name}: {out.size}")


def grow(m, n):
    for _ in range(n):
        g = m.copy()
        g[1:] |= m[:-1]; g[:-1] |= m[1:]; g[:, 1:] |= m[:, :-1]; g[:, :-1] |= m[:, 1:]
        m = g
    return m


def main():
    os.makedirs(OUT, exist_ok=True)
    a = np.array(Image.open("imagens/escada_pedra.webp").convert("RGBA")).astype(int)
    save(a, a[:, :, 3] >= 128, "escada_pedra", width=3 * PPU)

    a = np.array(Image.open("imagens/escada_madeira.webp").convert("RGBA")).astype(int)
    lum = a[:, :, :3].mean(2)
    # Madeira clara = escada. Em cada coluna, tudo entre o ponto claro mais alto e o mais
    # baixo é escada (os espelhos escuros dos degraus ficam entre as tábuas); o brilho, fora
    # disso, sai. Depois volta o contorno escuro colado (mais escuro que o brilho perto dele).
    bright = lum >= 75
    keep = np.zeros_like(bright)
    for x in np.where(bright.any(0))[0]:
        ys = np.where(bright[:, x])[0]
        keep[ys.min():ys.max() + 1, x] = True
    ring = grow(keep, 8) & ~keep
    keep |= ring & (lum < 30)
    comps = components(keep)
    big = max(len(c[0]) for c in comps)
    keep[:] = False
    for cy, cx in comps:
        if len(cy) >= 0.05 * big:
            keep[cy, cx] = True
    save(a, keep, "escada_madeira", width=3 * PPU)

    a = np.array(Image.open("imagens/portas.webp").convert("RGBA")).astype(int)
    fg = a[:, :, 3] >= 225          # o fundo branco é semitransparente
    # 4 linhas de portas (faixas fixas da folha); em cada uma, as portas separadas pelas
    # colunas quase vazias.
    doors = []
    for y0, y1 in [(0, 155), (155, 308), (308, 462), (462, a.shape[0])]:
        band = fg[y0:y1]
        for x0, x1 in spans(band.mean(0) > 0.03, 15):
            keep = np.zeros_like(fg)
            keep[y0:y1, x0:x1] = band[:, x0:x1]
            doors.append(keep)
    # Altura das portas comuns (primeira linha) = 2 quadrados.
    hs = [np.ptp(np.where(d)[0]) + 1 for d in doors[:8]]
    scale = 2 * PPU / float(np.median(hs))
    for i, keep in enumerate(doors, 1):
        save(a, keep, f"porta_{i:02d}", scale=scale)


def spans(v, minw):
    out, s = [], None
    for i, x in enumerate(v):
        if x and s is None:
            s = i
        elif not x and s is not None:
            if i - s >= minw:
                out.append((s, i))
            s = None
    if s is not None and len(v) - s >= minw:
        out.append((s, len(v)))
    return out


if __name__ == "__main__":
    main()
