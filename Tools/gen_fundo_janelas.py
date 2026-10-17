"""Fundo das janelas com a madeira da barra de status.

1. Kit/fundo_janela.png: ladrilho sem emenda feito da madeira da barra de status
   (Status/madeira.png, uma faixa de 1040x21). Usa só trechos sem as marcas dos
   separadores, junta três faixas na vertical (repete menos) e funde as pontas na
   horizontal para emendar.
2. Equipamento/janela.png e painel_direito.png: troca os pixels de madeira da arte
   original pelo ladrilho. As originais ficam em Tools/fontes/ (o script sempre parte delas).

Uso: python Tools/gen_fundo_janelas.py   (na raiz do projeto)
"""
import os
import shutil

import numpy as np
from PIL import Image

STATUS_WOOD = "Assets/Resources/UI/Status/madeira.png"
TILE_OUT = "Assets/Resources/UI/Kit/fundo_janela.png"
EQ = "Assets/Resources/UI/Equipamento/"
SRC = "Tools/fontes/"

TILE_W = 176          # largura final do ladrilho
BLEND = 16            # pixels fundidos para a emenda horizontal
SPANS = [400, 590, 800]   # começos de trechos limpos (as marcas ficam em ~157, 305, 397, 585, 778)

# Madeira da arte do equipamento (as cores dos rebites ficam de fora: são mais alaranjadas).
WOOD_PALETTE = np.array([
    (82, 50, 39), (82, 48, 38), (57, 32, 28), (56, 30, 29), (99, 66, 49), (102, 66, 49),
    (39, 24, 21), (41, 26, 21), (68, 41, 32), (73, 39, 32), (94, 57, 39),
])

SIDE, TOP = 8, 32          # UIKit.Side / UIKit.Top: onde o corpo da moldura começa
PANEL_POS = (156, 25)      # ItemUI.EqPanelPos


def tile():
    wood = np.array(Image.open(STATUS_WOOD).convert("RGBA")).astype(float)
    h = wood.shape[0]
    rows = []
    for x0 in SPANS:
        strip = wood[:, x0:x0 + TILE_W + BLEND].copy()
        # A ponta direita (além da largura) se funde com a esquerda: o ladrilho emenda.
        for i in range(BLEND):
            t = (i + 0.5) / BLEND
            strip[:, i] = strip[:, i] * t + strip[:, TILE_W + i] * (1 - t)
        rows.append(strip[:, :TILE_W])
    out = np.concatenate(rows, axis=0)
    out[:, :, 3] = 255
    # Pontinhos quase pretos soltos viram pontos repetidos no ladrilho: troca pela média dos vizinhos.
    lum = out[:, :, :3].mean(2)
    th, tw = lum.shape
    for y, x in zip(*np.where(lum < 14)):
        nb = [out[(y + dy) % th, (x + dx) % tw, :3] for dy in (-1, 0, 1) for dx in (-2, -1, 1, 2)
              if lum[(y + dy) % th, (x + dx) % tw] >= 14]
        if nb:
            out[y, x, :3] = np.mean(nb, axis=0)
    return out.round().astype(np.uint8)


def wood_mask(img):
    rgb = img[:, :, :3].astype(int)
    d = np.abs(rgb[:, :, None, :] - WOOD_PALETTE[None, None, :, :]).sum(3).min(2)
    return (d <= 10) & (img[:, :, 3] == 255)


def retexture(name, region, offset, tex, keep=None):
    """Troca a madeira dentro de `region` (x0, y0, x1, y1) pelo ladrilho, alinhado com o
    corpo da moldura (a arte fica em `offset` dentro da janela). `keep` (x0, y0, x1, y1)
    fica intacto."""
    src = os.path.join(SRC, "equipamento_" + name + ".png")
    if not os.path.exists(src):
        os.makedirs(SRC, exist_ok=True)
        shutil.copy(EQ + name + ".png", src)
    img = np.array(Image.open(src).convert("RGBA"))
    m = wood_mask(img)
    x0, y0, x1, y1 = region
    th, tw = tex.shape[:2]
    out = img.copy()
    for y in range(y0, y1):
        for x in range(x0, x1):
            if keep and keep[0] <= x < keep[2] and keep[1] <= y < keep[3]:
                continue
            if m[y, x]:
                out[y, x] = tex[(y + offset[1] - TOP) % th, (x + offset[0] - SIDE) % tw]
    Image.fromarray(out, "RGBA").save(EQ + name + ".png")
    print(f"  {name}.png: {int(m[y0:y1, x0:x1].sum())} pixels de madeira trocados")


def main():
    tex = tile()
    Image.fromarray(tex, "RGBA").save(TILE_OUT)
    print(f"  fundo_janela.png: {tex.shape[1]}x{tex.shape[0]}")
    # Janela: só o painel da esquerda abaixo do cabeçalho (o cabeçalho fica como está).
    # O fundo escuro do retrato fica como está.
    retexture("janela", (0, 33, 156, 236), (0, 0), tex, keep=(22, 40, 141, 216))
    # Painel direito: tudo menos a borda de ferro.
    retexture("painel_direito", (0, 8, 242, 198), PANEL_POS, tex)


if __name__ == "__main__":
    main()
