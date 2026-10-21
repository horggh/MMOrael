"""Recorta os 4 prédios de imagens/predios_cidade.webp (fundo transparente, nome embaixo) em
Assets/Art/Sprites/Environment/Predios, com 192 px de largura: 6 unidades a 32 PPU, a mesma
área da casa do personagem (Building.Footprint). Também ajusta a mercearia a essa largura.

Uso: python Tools/extract_predios_folha.py
"""
import numpy as np
from PIL import Image

SRC = "imagens/predios_cidade.webp"
OUT = "Assets/Art/Sprites/Environment/Predios"
import sys as _sys, os as _os
_sys.path.insert(0, _os.path.dirname(_os.path.abspath(__file__)))
import escala  # noqa: E402
WIDTH = 6 * escala.PX   # padrão; cada prédio usa a largura da tabela (Tools/escala.py)
BOXES = {   # (x0, y0, x1, y1) sem a placa do nome
    "cavalaria": (0, 0, 792, 447),
    "estabulo": (792, 0, 1536, 447),
    "taberna": (0, 490, 772, 966),
    "hotel": (772, 490, 1536, 966),
}


def border_flood(bg):
    """Fundo = pixels claros ligados à borda."""
    m = np.zeros_like(bg)
    m[0, :], m[-1, :], m[:, 0], m[:, -1] = bg[0, :], bg[-1, :], bg[:, 0], bg[:, -1]
    while True:
        g = m.copy()
        g[1:] |= m[:-1]; g[:-1] |= m[1:]; g[:, 1:] |= m[:, :-1]; g[:, :-1] |= m[:, 1:]
        g &= bg
        if (g == m).all():
            return m
        m = g


def to_width(im, width=None):
    width = width or WIDTH
    s = width / im.width
    size = (width, max(1, round(im.height * s)))
    a = np.array(im).astype(float)
    al = a[:, :, 3:] / 255
    pre = Image.fromarray((a[:, :, :3] * al).astype(np.uint8)).resize(size, Image.LANCZOS)
    alr = np.array(Image.fromarray(a[:, :, 3].astype(np.uint8)).resize(size, Image.LANCZOS)).astype(float)
    rgb = np.clip(np.array(pre).astype(float) * 255 / np.maximum(alr, 1)[:, :, None], 0, 255)
    return Image.fromarray(np.dstack([rgb, np.where(alr >= 128, 255, 0)]).astype(np.uint8), "RGBA")


def main():
    src = np.array(Image.open(SRC).convert("RGBA")).astype(int)
    a = src[:, :, :3]
    for name, (x0, y0, x1, y1) in BOXES.items():
        c = a[y0:y1, x0:x1]
        keep = src[y0:y1, x0:x1, 3] >= 128   # a folha já vem sem fundo
        ys, xs = np.where(keep)
        c, keep = c[ys.min():ys.max() + 1, xs.min():xs.max() + 1], keep[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
        im = Image.fromarray(np.dstack([c, keep * 255]).astype(np.uint8), "RGBA")
        out = to_width(im, escala.largura_px(name))
        out.save(f"{OUT}/{name}.png")
        print(name, out.size)

    # (a mercearia vem de Tools/extract_predio.py; o tamanho dela já está no padrão de 64 px)


if __name__ == "__main__":
    main()
