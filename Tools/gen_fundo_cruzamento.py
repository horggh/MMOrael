"""Fundo do mapa "Cruzamento": o mapa do meio de imagens/mapas-referencia.jpg ampliado para
o tamanho do chão da cena do Campo (1 unidade = 32 px).

1. Recorta o mapa do meio (sem o xadrez em volta).
2. Tira as bolinhas vermelhas (marcam as saídas; as posições saem no terminal, em
   unidades da cena, para pôr as saídas de verdade depois).
3. Amplia suave, com um pouco de nitidez e grão fino de 2 px, e reduz a paleta.

Saída: Assets/Art/Sprites/Environment/Mapas/cruzamento_fundo.png
Uso: python Tools/gen_fundo_cruzamento.py [largura_unidades altura_unidades]
     (padrão: 74.18 x 60.91, o tamanho do chão "Chao" na cena)
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

SRC = "imagens/mapas-referencia.jpg"
OUT = "Assets/Art/Sprites/Environment/Mapas/cruzamento_fundo.png"
BOX = (308, 118, 674, 458)        # mapa do meio na imagem de referência
PPU = 32


def remove_red_dots(img):
    """Pontos vermelhos -> média dos vizinhos que não são vermelhos. Devolve os centros."""
    a = img.astype(float)
    r, g, b = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    red = (r > 150) & (r - g > 70) & (r - b > 70)
    # Engrossa 2 px: o JPEG deixa um halo rosado em volta do ponto.
    grown = red.copy()
    for _ in range(2):
        g2 = grown.copy()
        g2[1:] |= grown[:-1]; g2[:-1] |= grown[1:]
        g2[:, 1:] |= grown[:, :-1]; g2[:, :-1] |= grown[:, 1:]
        grown = g2
    out = a.copy()
    H, W = red.shape
    ys, xs = np.where(grown)
    for y, x in zip(ys, xs):
        for rad in range(3, 12):
            y0, y1, x0, x1 = max(0, y - rad), min(H, y + rad + 1), max(0, x - rad), min(W, x + rad + 1)
            ok = ~grown[y0:y1, x0:x1]
            if ok.sum() >= 6:
                out[y, x] = np.median(a[y0:y1, x0:x1][ok], axis=0)
                break
    # Centros dos pontos (grupos de vermelho).
    centers, seen = [], np.zeros_like(red)
    for y, x in zip(*np.where(red)):
        if seen[y, x]:
            continue
        stack, pts = [(y, x)], []
        seen[y, x] = True
        while stack:
            cy, cx = stack.pop()
            pts.append((cy, cx))
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                ny, nx = cy + dy, cx + dx
                if 0 <= ny < H and 0 <= nx < W and red[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        if len(pts) >= 4:
            centers.append(np.mean(pts, axis=0))
    return out.clip(0, 255).astype(np.uint8), centers


def main():
    wu = float(sys.argv[1]) if len(sys.argv) > 2 else 74.18
    hu = float(sys.argv[2]) if len(sys.argv) > 2 else 60.91
    W, H = int(round(wu * PPU)), int(round(hu * PPU))

    src = np.array(Image.open(SRC).convert("RGB").crop(BOX))
    clean, dots = remove_red_dots(src)
    sh, sw = clean.shape[:2]

    # Ampliação suave (blocos de ~30 px viravam quadrados fantasmas), um pouco de nitidez e
    # um grão fino de 2 px para não parecer pintura lisa perto dos sprites de pixel art.
    smooth = Image.fromarray(clean).resize((W, H), Image.LANCZOS)
    sharp = np.array(smooth.filter(ImageFilter.UnsharpMask(radius=3, percent=80, threshold=2))).astype(float)
    rng = np.random.default_rng(3)
    grain = rng.normal(0, 4.0, (H // 2 + 1, W // 2 + 1))
    grain = np.kron(grain, np.ones((2, 2)))[:H, :W]
    sharp = (sharp + grain[:, :, None]).clip(0, 255).astype(np.uint8)
    final = Image.fromarray(sharp).quantize(colors=96, method=Image.MEDIANCUT, dither=Image.NONE).convert("RGBA")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    final.save(OUT)
    print(f"  {OUT}: {W}x{H} px = {wu} x {hu} unidades")
    if dots:
        print("  saídas (bolinhas vermelhas), em unidades a partir do canto de baixo à esquerda:")
        for cy, cx in dots:
            print(f"    x {cx / sw * wu:5.1f}   y {(1 - cy / sh) * hu:5.1f}")


if __name__ == "__main__":
    main()
