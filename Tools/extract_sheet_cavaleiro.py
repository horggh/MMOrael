"""Extrai as animações do cavaleiro da folha de sprites (8 linhas x 12 quadros).

Bloco 1 (colunas 1-4): andar por direção. Linha 1 = frente, 3 = perfil (olha p/ esquerda),
5 = costas. Blocos 2 e 3 (corrida e ataque) só existem de perfil olhando p/ direita.

Cada quadro é alinhado pelos pés num quadro 64x48, para o personagem não "pular"
entre animações de larguras diferentes.

Uso:  python3 Tools/extract_sheet_cavaleiro.py
"""
import sys
import numpy as np
from PIL import Image
sys.path.insert(0, "Tools")
import extract_pixelart as EP

SRC = "imagens/sprite-sheet-cavaleiro.jpeg"
OUT = "Assets/Resources/Characters"
GRID = 688
FW, FH = 64, 48
FEET_Y = 2          # pés a 2px da base do quadro

ANIMS = {
    "cav_walk_down": (0, range(0, 4)),
    "cav_walk_side": (2, range(0, 4)),
    "cav_walk_up": (4, range(0, 4)),
    "cav_run_side": (0, range(4, 8)),
    "cav_attack_side": (0, range(8, 12)),
}


def bands(v, minw):
    out, s = [], None
    for i, x in enumerate(v):
        if x and s is None:
            s = i
        elif not x and s is not None:
            if i - s >= minw:
                out.append((s, i - 1))
            s = None
    if s is not None:
        out.append((s, len(v) - 1))
    return out


def main():
    a = np.array(Image.open(SRC).convert("RGB")).astype(float)
    grid = EP.resample(a, GRID)
    bg = EP.background_mask(grid)
    fg = ~bg

    rows = bands(fg.any(1), 3)
    cells = [[(y0, y1, c0, c1) for c0, c1 in bands(fg[y0:y1 + 1].any(0), 2)] for y0, y1 in rows]

    import os
    os.makedirs(OUT, exist_ok=True)
    for name, (row, cols) in ANIMS.items():
        frames = []
        for c in cols:
            y0, y1, x0, x1 = cells[row][c]
            rgb = grid[y0:y1 + 1, x0:x1 + 1]
            alpha = ~bg[y0:y1 + 1, x0:x1 + 1]
            snapped, _ = EP.quantize(rgb.astype(int), np.where(alpha, 255, 0).astype(np.uint8))

            # âncora = centro dos pixels das 4 linhas de baixo (os pés)
            ys, xs = np.where(alpha)
            bottom = ys.max()
            feet_x = int(round(xs[ys >= bottom - 3].mean()))

            f = np.zeros((FH, FW, 4), np.uint8)
            ox = FW // 2 - feet_x
            oy = FH - FEET_Y - 1 - bottom
            for y, x in zip(ys, xs):
                ny, nx = y + oy, x + ox
                if 0 <= ny < FH and 0 <= nx < FW:
                    f[ny, nx, :3] = snapped[y, x]
                    f[ny, nx, 3] = 255
            frames.append(f)

        sheet = np.concatenate(frames, axis=1)
        Image.fromarray(sheet, "RGBA").save(f"{OUT}/{name}.png")
        print(f"  {name}: {len(frames)} quadros {FW}x{FH}")


if __name__ == "__main__":
    main()
