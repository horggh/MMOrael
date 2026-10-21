"""Fundos de mapa novos, cada um uma área própria da cena (menus RaelMMO > Mapa > Colocar
mapa da ...), na largura do fundo do Cruzamento (2374 px a 32 PPU = 74 quadrados):

- cidade_fundo.png: ruas, quarteirões e rotatórias (imagens/mapa_cidade_ruas.webp), na mesma
  proporção do Cruzamento (74 x 61): corta no meio e amplia.
- praca_fundo.png: praça medieval com jardins e portões nos 4 lados
  (imagens/mapa_cidade_praca.webp). O desenho é quadrado: fica 74 x 74 (cortar tiraria os
  portões de cima e de baixo).
- caverna_fundo.png: caverna com lago, cachoeira, plataformas e cristais
  (imagens/mapa_caverna.webp), inteira na proporção dela (74 x 55,6 -> 2374 x 1780).
- caverna2_fundo.png: caverna com cachoeira, cristais azuis e ponte
  (imagens/mapa_caverna2.webp), inteira (74 x 49,5 -> 2374 x 1583).

Uso: python Tools/gen_fundo_cidade.py
"""
from PIL import Image

OUT = "Assets/Art/Sprites/Environment/Mapas/"
MAPS = [
    ("imagens/mapa_cidade_ruas.webp", "cidade_fundo.png", 2374, 1949),
    ("imagens/mapa_cidade_praca.webp", "praca_fundo.png", 2374, 2374),
    ("imagens/mapa_caverna.webp", "caverna_fundo.png", 2374, 1780),
    ("imagens/mapa_caverna2.webp", "caverna2_fundo.png", 2374, 1583),
]


def make(src, out, W, H):
    im = Image.open(src).convert("RGB")
    cw = round(im.height * W / H)
    if cw <= im.width:
        x0 = (im.width - cw) // 2
        im = im.crop((x0, 0, x0 + cw, im.height))
    else:
        ch = round(im.width * H / W)
        y0 = (im.height - ch) // 2
        im = im.crop((0, y0, im.width, y0 + ch))
    im.resize((W, H), Image.LANCZOS).save(OUT + out)
    print(out, (W, H))


if __name__ == "__main__":
    for m in MAPS:
        make(*m)
