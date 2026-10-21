"""grama_base2: quadradinhos da textura "grama" (Texturas/grama.png, 64x64 = 1 quadrado da
grade) encostados lado a lado e empilhados, sem mistura: 2 x 2 = 128x128 (o tamanho da
grama_base). O chão do mapa ("Chao") usa o próprio quadradinho "grama" repetido do mesmo
jeito, até cobrir a área inteira (menu RaelMMO > Mapa > Usar grama no chão).
Saída: Assets/Art/Sprites/Environment/Texturas/grama_base2.png
Uso: python Tools/gen_grama_base2.py
"""
import numpy as np
from PIL import Image

TEX = "Assets/Art/Sprites/Environment/Texturas/"


def main():
    g = np.array(Image.open(TEX + "grama.png").convert("RGBA"))
    out = np.tile(g, (2, 2, 1))
    Image.fromarray(out, "RGBA").save(TEX + "grama_base2.png")
    print(f"  {TEX}grama_base2.png {out.shape[1]}x{out.shape[0]} (2 x 2 quadrados de grama)")


if __name__ == "__main__":
    main()
