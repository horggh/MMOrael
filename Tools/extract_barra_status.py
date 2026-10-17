"""Peças da barra de informações do personagem (imagens/barra-status.jpeg).

A referência é arte em alta resolução (filetes de 1-2 px, texto suave), não pixel art em
grade: as peças ficam na resolução da imagem. O jogo monta a barra com elas (UIKit.StatusBar)
e escreve os textos e números por cima.

Peças (Assets/Resources/UI/Status/):
  canto_se/sd/ie/id  — cantos ornamentados (28x28)
  borda_sup/inf/esq/dir — trechos lisos da moldura (para esticar)
  madeira            — textura do fundo (faixa sem texto, repete)
  separador          — filete dourado vertical entre as informações
  exp_moldura        — barra de EXP vazia (moldura dourada e fundo escuro)
  exp_cheia          — faixa verde do preenchimento (estica na horizontal)
  icone_hp, icone_sp, icone_zeny, icone_mochila

Uso:  python3 Tools/extract_barra_status.py
"""
import os
import numpy as np
from PIL import Image

SRC = "imagens/barra-status.jpeg"
OUT = "Assets/Resources/UI/Status"

# Barra na imagem: x 33-1135, y 337-440.
X0, Y0, X1, Y1 = 33, 337, 1135, 440
C = 28            # cantos
B = 14            # espessura da moldura (dois filetes)


def crop(a, x0, y0, x1, y1):
    return a[y0:y1, x0:x1].copy()


def save(img, name):
    if img.shape[2] == 3:
        img = np.dstack([img, np.full(img.shape[:2], 255, np.uint8)])
    Image.fromarray(img.astype(np.uint8), "RGBA").save(os.path.join(OUT, name + ".png"))
    print(f"  {name}.png: {img.shape[1]}x{img.shape[0]}")


def icon(a, x0, y0, x1, y1, name, dark_limit=70):
    """Ícone com o fundo de madeira tirado: o escuro ligado à borda do recorte vira transparente."""
    sub = crop(a, x0, y0, x1, y1).astype(int)
    h, w, _ = sub.shape
    dark = sub.max(2) < dark_limit
    bg = np.zeros((h, w), bool)
    stack = [(y, x) for y in range(h) for x in (0, w - 1) if dark[y, x]]
    stack += [(y, x) for x in range(w) for y in (0, h - 1) if dark[y, x]]
    for p in stack:
        bg[p] = True
    while stack:
        y, x = stack.pop()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and dark[ny, nx] and not bg[ny, nx]:
                bg[ny, nx] = True
                stack.append((ny, nx))
    # Só o maior bloco: pontinhos soltos de madeira em volta saem.
    keep = ~bg
    seen = np.zeros_like(keep)
    best = None
    for y, x in zip(*np.where(keep)):
        if seen[y, x]:
            continue
        comp = np.zeros_like(keep)
        st = [(y, x)]
        seen[y, x] = comp[y, x] = True
        while st:
            cy, cx = st.pop()
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                ny, nx = cy + dy, cx + dx
                if 0 <= ny < h and 0 <= nx < w and keep[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = comp[ny, nx] = True
                    st.append((ny, nx))
        if best is None or comp.sum() > best.sum():
            best = comp
    alpha = np.where(best, 255, 0).astype(np.uint8)
    save(np.dstack([sub.astype(np.uint8), alpha]), name)


def to_blue(path):
    """Troca o matiz do losango dourado para azul, mantendo luz e sombra do desenho."""
    import colorsys
    img = np.array(Image.open(path).convert("RGBA")).astype(float)
    out = img.copy()
    for y in range(img.shape[0]):
        for x in range(img.shape[1]):
            r, g, b, al = img[y, x] / 255.0
            if al == 0:
                continue
            h, l, sat = colorsys.rgb_to_hls(r, g, b)
            r2, g2, b2 = colorsys.hls_to_rgb(0.58, min(1.0, l * 1.05), sat)   # 0.58 = azul
            out[y, x, :3] = np.array([r2, g2, b2]) * 255
    Image.fromarray(out.astype(np.uint8), "RGBA").save(path)


def main():
    a = np.array(Image.open(SRC).convert("RGB"))
    os.makedirs(OUT, exist_ok=True)

    save(crop(a, X0, Y0, X0 + C, Y0 + C), "canto_se")
    save(crop(a, X1 + 1 - C, Y0, X1 + 1, Y0 + C), "canto_sd")
    save(crop(a, X0, Y1 + 1 - C, X0 + C, Y1 + 1), "canto_ie")
    save(crop(a, X1 + 1 - C, Y1 + 1 - C, X1 + 1, Y1 + 1), "canto_id")

    save(crop(a, 300, Y0, 316, Y0 + B), "borda_sup")
    save(crop(a, 300, Y1 + 1 - B, 316, Y1 + 1), "borda_inf")
    save(crop(a, X0, 366, X0 + B, 410), "borda_esq")
    save(crop(a, X1 + 1 - B, 366, X1 + 1, 410), "borda_dir")

    # Faixa de madeira sem texto (acima das palavras), de ponta a ponta do miolo. As colunas
    # dos separadores dourados (e pontas de letras) viram madeira copiada do vizinho: senão
    # aparecem como riscos repetidos quando a textura é ladrilhada.
    wood = crop(a, 60, Y0 + B, 1100, 372).astype(int)
    bright = (wood.max(2) > 95).any(0)
    grow = bright.copy()
    grow[1:] |= bright[:-1]; grow[:-1] |= bright[1:]
    for x in np.where(grow)[0]:
        src = x - 12
        while src >= 0 and grow[src]:
            src -= 3
        wood[:, x] = wood[:, max(src, 0)]
    save(wood.astype(np.uint8), "madeira")

    save(crop(a, 218, 364, 223, 412), "separador")

    # Barra de EXP: a moldura vazia (preenchimento verde trocado pelo fundo escuro da
    # própria barra, linha a linha) e uma fatia do verde para esticar.
    bar = crop(a, 473, 393, 632, 413)
    empty_col = bar[:, 140].copy()                 # trecho vazio (escuro) dentro da barra
    for x in range(4, 134):
        bar[3:17, x] = empty_col[3:17]
    save(bar, "exp_moldura")
    save(crop(a, 520, 396, 522, 410), "exp_cheia")

    icon(a, 55, 371, 90, 403, "icone_hp")
    icon(a, 231, 371, 261, 403, "icone_sp")
    to_blue(os.path.join(OUT, "icone_sp.png"))   # SP em azul (mana), como no Ragnarok
    icon(a, 848, 371, 879, 403, "icone_zeny")
    # Mochila de couro escuro, quase no tom da madeira: só o bem escuro sai.
    icon(a, 998, 368, 1037, 408, "icone_mochila", 38)


if __name__ == "__main__":
    main()
