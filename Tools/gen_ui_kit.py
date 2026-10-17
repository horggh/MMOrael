"""Gera o kit de interface genérico a partir da janela de equipamento.

Corta a moldura em peças reutilizáveis (cantos, bordas, junções, divisórias e a
madeira de fundo) para montar janelas de qualquer tamanho, e cria a faixa de
título com outros nomes no mesmo estilo dourado do "EQUIPAMENTO" original.

Precisa das peças geradas antes por extract_ui_equipamento.py.

Medidas da moldura (pixels da arte):
  barra do topo y 2-7, cabeçalho escuro y 8-25, barra separadora y 26-31,
  corpo a partir de y 32; barra esquerda x 2-7; barra de baixo nos últimos 11 px;
  divisória vertical com 6 px. Os rebites ficam só nos cantos ornamentados, então
  as bordas podem ser esticadas sem repetir rebite.

Uso:  python3 Tools/extract_ui_equipamento.py && python3 Tools/gen_ui_kit.py
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont

SRC = "Assets/Resources/UI/Equipamento"
OUT = "Assets/Resources/UI/Kit"
FONT = "C:/Windows/Fonts/timesbd.ttf"   # a mais parecida com a letra do título original

TITLES = {
    "titulo_equipamento": "EQUIPAMENTO",
    "titulo_inventario": "INVENTÁRIO",
    "titulo_habilidades": "HABILIDADES",
    "titulo_atributos": "ATRIBUTOS",
    "titulo_bau": "BAÚ",
    "titulo_itens": "ITENS",
    "titulo_obra": "OBRA",
    "titulo_dev": "DEV",
}

BODY_TOP = 32       # primeira linha do corpo (abaixo da barra separadora)
SIDE = 8            # largura da borda lateral
BOTTOM_BAR = 224    # na janela original, a barra de baixo começa na linha 225

# Cores do título original, medidas linha a linha: metade de cima clara, de baixo escura.
GOLD_TOP = (246, 203, 122)
GOLD_TOP_EDGE = (243, 204, 136)
GOLD_BOTTOM = (205, 155, 89)
GOLD_TAIL = (193, 148, 85)
SHADOW = (26, 15, 13)
CAP_TOP = 10        # linha onde começam as maiúsculas dentro da faixa
CAP_HEIGHT = 12


def load(name):
    return np.array(Image.open(os.path.join(SRC, name + ".png")).convert("RGBA"))


def save(img, name):
    Image.fromarray(img.astype(np.uint8), "RGBA").save(os.path.join(OUT, name + ".png"))
    print(f"  {name}.png: {img.shape[1]}x{img.shape[0]}")


def composite():
    """Janela + painel direito: a moldura completa, como aparece no jogo."""
    win = Image.open(os.path.join(SRC, "janela.png")).convert("RGBA")
    win.alpha_composite(Image.open(os.path.join(SRC, "painel_direito.png")).convert("RGBA"), (156, 25))
    return np.array(win)


def clear(img, mask):
    out = img.copy()
    out[mask] = 0
    return out


def cut_pieces(c):
    h, w = c.shape[:2]
    bottom_h = h - BOTTOM_BAR

    # Cantos: o miolo (madeira e moldura do retrato) vira transparente.
    tl = c[0:44, 0:34]
    yy, xx = np.mgrid[0:tl.shape[0], 0:tl.shape[1]]
    tl = clear(tl, (yy >= BODY_TOP) & (xx >= SIDE))
    save(tl, "canto_sup_esq")
    save(tl[:, ::-1], "canto_sup_dir")

    bl = c[h - 32:h, 0:34]
    yy, xx = np.mgrid[0:bl.shape[0], 0:bl.shape[1]]
    bl = clear(bl, (yy < 32 - bottom_h) & (xx >= SIDE))
    save(bl, "canto_inf_esq")
    save(bl[:, ::-1], "canto_inf_dir")

    # Bordas: trechos lisos, para esticar.
    save(c[0:BODY_TOP, 40:56], "borda_sup")             # barra + cabeçalho + separadora
    save(c[BOTTOM_BAR:h, 60:76], "borda_inf")
    left = c[100:116, 0:SIDE]
    save(left, "borda_esq")
    save(left[:, ::-1], "borda_dir")

    # Divisórias e junções (onde a divisória encontra a separadora e a barra de baixo).
    save(c[100:116, 150:156], "divisoria_v")
    save(c[26:BODY_TOP, 40:56], "divisoria_h")

    js = c[22:40, 146:160]
    yy, xx = np.mgrid[0:js.shape[0], 0:js.shape[1]]
    save(clear(js, (yy >= BODY_TOP - 22) & ((xx < 4) | (xx > 9))), "juncao_sup")

    ji = c[h - 20:h, 146:160]
    yy, xx = np.mgrid[0:ji.shape[0], 0:ji.shape[1]]
    save(clear(ji, (yy < 20 - bottom_h) & ((xx < 4) | (xx > 9))), "juncao_inf")

    # Separadora encontrando a borda esquerda (para divisórias horizontais no corpo).
    je = c[22:36, 0:16]
    yy, xx = np.mgrid[0:je.shape[0], 0:je.shape[1]]
    je = clear(je, (xx >= SIDE) & ((yy < 26 - 22) | (yy >= BODY_TOP - 22)))
    save(je, "juncao_esq")
    save(je[:, ::-1], "juncao_dir")

    # Madeira do painel direito, longe dos rebites dos cantos. A altura vai de uma
    # linha entre tábuas (y 38) até logo antes de outra (y 172): repetida na
    # vertical, as tábuas continuam sem emenda.
    panel = load("painel_direito")
    save(panel[38:172, 14:226], "madeira")


def blank_banner(c):
    """Faixa do título sem o texto, espelhada para ficar simétrica.

    A faixa original é centrada em x≈203. O texto é apagado copiando, linha a linha,
    o fundo escuro da faixa logo antes da primeira letra.
    """
    left = c[0:BODY_TOP, 90:204].copy()
    sample = left[:, 131 - 90].copy()
    for y in range(left.shape[0]):
        left[y, 133 - 90:] = sample[y]
    return np.concatenate([left, left[:, ::-1]], axis=1)


def text_mask(text):
    """Texto em pixel art: desenha grande, reduz para a grade nativa e binariza."""
    k = 8
    size = 10
    while True:
        font = ImageFont.truetype(FONT, size)
        top, bottom = font.getbbox("E")[1], font.getbbox("E")[3]
        if bottom - top >= CAP_HEIGHT * k:
            break
        size += 2
    img = Image.new("L", (len(text) * CAP_HEIGHT * k * 2, CAP_HEIGHT * k * 4), 0)
    ImageDraw.Draw(img).text((10, CAP_HEIGHT * k * 2), text, font=font, fill=255)
    a = np.array(img).astype(float)
    cap_top = CAP_HEIGHT * k * 2 + top

    # Recorta alinhando o topo das maiúsculas a um múltiplo de k (o acento fica acima).
    rows = np.where(a.any(1))[0]
    cols = np.where(a.any(0))[0]
    y0 = cap_top - ((cap_top - rows.min() + k - 1) // k) * k
    a = a[y0:rows.max() + 1, cols.min():cols.max() + 1]
    h = (a.shape[0] + k - 1) // k
    w = (a.shape[1] + k - 1) // k
    pad = np.zeros((h * k, w * k))
    pad[:a.shape[0], :a.shape[1]] = a
    mask = pad.reshape(h, k, w, k).mean((1, 3)) > 110
    return mask, (cap_top - y0) // k     # linha do topo das maiúsculas dentro da máscara


def draw_title(banner, text):
    out = banner.copy()
    mask, cap_row = text_mask(text)
    mh, mw = mask.shape
    x0 = (out.shape[1] - mw) // 2
    y0 = CAP_TOP - cap_row
    for y in range(mh):
        for x in range(mw):
            if not mask[y, x]:
                continue
            # Sombra um pixel abaixo, como no original.
            sy, sx = y0 + y + 1, x0 + x
            if 0 <= sy < out.shape[0] and not (y + 1 < mh and mask[y + 1, x]):
                out[sy, sx, :3] = SHADOW
    for y in range(mh):
        row = y0 + y - CAP_TOP                    # 0 = topo das maiúsculas
        if row <= 0:
            color = GOLD_TOP_EDGE
        elif row < CAP_HEIGHT * 0.58:
            color = GOLD_TOP
        elif row < CAP_HEIGHT:
            color = GOLD_BOTTOM
        else:
            color = GOLD_TAIL
        for x in range(mw):
            if mask[y, x]:
                out[y0 + y, x0 + x, :3] = color
                out[y0 + y, x0 + x, 3] = 255
    return out


def plain_button():
    """Botão de cobre do "fechar" sem o X, para botões genéricos (+, etc.)."""
    b = load("botao_fechar")
    out = b.copy()
    h, w = b.shape[:2]
    lum = b[:, :, :3].mean(2)
    for y in range(4, h - 4):
        face = b[y, 3]
        for x in range(4, w - 4):
            if lum[y, x] > face[:3].mean() + 25:
                out[y, x] = face
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    c = composite()
    cut_pieces(c)
    banner = blank_banner(c)
    save(banner, "titulo_vazio")
    for name, text in TITLES.items():
        save(draw_title(banner, text), name)
    save(plain_button(), "botao")


if __name__ == "__main__":
    main()
