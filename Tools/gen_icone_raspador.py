"""Ícone provisório do Raspador de piso (espátula com cabo de madeira), em pixel art 32x32.

Desenhado à mão em código até existir a arte definitiva; segue a paleta dos outros itens
(contorno escuro, metal azulado, madeira marrom).

Uso:  python3 Tools/gen_icone_raspador.py
"""
import numpy as np
from PIL import Image

OUT = "Assets/Resources/Items/raspador.png"

PALETTE = {
    ".": (0, 0, 0, 0),
    "k": (34, 26, 24, 255),      # contorno
    "s": (176, 190, 204, 255),   # metal claro
    "m": (120, 134, 150, 255),   # metal
    "d": (78, 88, 104, 255),     # metal escuro
    "w": (150, 98, 58, 255),     # madeira
    "b": (104, 64, 38, 255),     # madeira escura
    "c": (196, 134, 70, 255),    # brilho da madeira
    "r": (124, 112, 96, 255),    # anel do cabo
}

# Lâmina larga embaixo à esquerda, cabo em diagonal para cima à direita.
ART = [
    "................................",
    "...........................kk...",
    "..........................kcbk..",
    ".........................kcwbk..",
    "........................kcwbk...",
    ".......................kcwbk....",
    "......................kcwbk.....",
    ".....................kcwbk......",
    "....................kcwbk.......",
    "...................kcwbk........",
    "..................kcwbk.........",
    ".................kcwbk..........",
    "................krrrk...........",
    "...............krrrrk...........",
    "..............kkrrrk............",
    ".............kssmmk.............",
    "............ksmmmdk.............",
    "...........ksmmmmdk.............",
    "..........ksmmmmmdk.............",
    ".........ksmmmmmmdk.............",
    "........ksmmmmmmdk..............",
    ".......ksmmmmmmdk...............",
    "......ksmmmmmmdk................",
    ".....ksmmmmmmdk.................",
    "....ksmmmmmmdk..................",
    "...kssmmmmmdk...................",
    "...kssssmmdk....................",
    "....kkkkkkk.....................",
    "................................",
    "................................",
    "................................",
    "................................",
]


def main():
    img = np.array([[PALETTE[ch] for ch in row] for row in ART], dtype=np.uint8)
    Image.fromarray(img, "RGBA").save(OUT)
    print(f"  {OUT}: 32x32")


if __name__ == "__main__":
    main()
