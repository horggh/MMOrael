"""Recorta as peças da tela de equipamento a partir do layout desenhado.

A imagem de origem é uma captura ampliada com fundo xadrez (mesmo caso do
extract_pixelart.py): reamostra para a grade nativa, troca o xadrez por
transparência e separa cada peça pelos grupos de pixels conectados.

Os espaços de equipamento vêm com tamanhos levemente diferentes (41 a 45 px),
então só um de cada tipo é exportado. Os que trazem a silhueta de "vazio"
ganham também uma versão limpa, para desenhar por baixo do item equipado.

Uso:  python3 Tools/extract_ui_equipamento.py
"""
import os
import sys
import numpy as np
from PIL import Image
sys.path.insert(0, "Tools")
import extract_pixelart as EP

SRC = "imagens/ui-equipamento-pecas.jpeg"
# O mockup tem a mesma posição de tudo, e é de onde vem a madeira do painel direito
# (que nas peças ficou transparente) e a borda direita da moldura.
MOCKUP = "imagens/ui-equipamento-mockup.jpeg"
PANEL = (193, 40, 441, 237)      # x0, y0, x1, y1 na grade, inclusive
WOOD_STRIP = (260, 285)          # faixa de madeira sem nenhum espaço em cima, em todas as linhas
OUT = "Assets/Resources/UI/Equipamento"
GRID = 480

# Nome de cada peça pelo centro aproximado na grade de 480 colunas.
# Peças fora da lista (os espaços repetidos) são ignoradas.
PIECES = {
    "janela": (45, 132),
    "botao_fechar": (425, 31),
    "canto": (383, 59),
    "etiqueta_ataque": (424, 55),
    "etiqueta_defesa": (424, 71),
    "icone_gema": (432, 85),
    "icone_escudo": (432, 104),
    "icone_coracao": (432, 120),
    "icone_gema_grande": (435, 204),
    "legenda": (240, 256),
    "slot": (235, 165),
    "slot_grande": (313, 111),
    "slot_acessorio": (313, 210),
}
# A janela mistura madeira, metal e pedra em tons vizinhos: com o agrupamento
# padrão de cores o pedestal fica manchado de marrom.
QUANTIZE_DIST = {"janela": 14}
# Peças com silhueta que ganham uma cópia "_limpo" sem ela.
SILHOUETTE = ("slot_grande", "slot_acessorio")


def components(fg):
    """Grupos de pixels conectados (inclui diagonais): devolve rótulos e caixas."""
    H, W = fg.shape
    lab = np.zeros((H, W), int)
    boxes = {}
    n = 0
    for y in range(H):
        for x in range(W):
            if not fg[y, x] or lab[y, x]:
                continue
            n += 1
            lab[y, x] = n
            stack = [(y, x)]
            y0, x0, y1, x1 = y, x, y, x
            while stack:
                cy, cx = stack.pop()
                y0, y1 = min(y0, cy), max(y1, cy)
                x0, x1 = min(x0, cx), max(x1, cx)
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        ny, nx = cy + dy, cx + dx
                        if 0 <= ny < H and 0 <= nx < W and fg[ny, nx] and not lab[ny, nx]:
                            lab[ny, nx] = n
                            stack.append((ny, nx))
            boxes[n] = (x0, y0, x1, y1)
    return lab, boxes


def remove_silhouette(img):
    """Pinta a silhueta com a cor do fundo do espaço.

    A silhueta é um cinza-azulado bem mais claro que o fundo escuro, e fica no meio:
    pega a cor dela no "peito" e a do fundo perto do canto de dentro, e troca só o
    que está no miolo, para não mexer na moldura de metal (que também é cinza).
    """
    out = img.copy()
    h, w = img.shape[:2]
    rgb = img[:, :, :3].astype(int)
    body = rgb[int(h * 0.80), w // 2]
    fill = img[int(h * 0.22), int(w * 0.22)].copy()
    inner = np.zeros((h, w), bool)
    inner[int(h * 0.14):int(h * 0.86), int(w * 0.14):int(w * 0.86)] = True
    close = ((rgb - body) ** 2).sum(2) < 45 ** 2
    lighter = rgb.mean(2) > fill[:3].mean() + 20
    out[inner & (close | lighter)] = fill
    return out


def build_panel(slot_boxes):
    """Painel direito de madeira, a partir do mockup, sem os itens desenhados nele.

    As tábuas são horizontais: cada pixel coberto por um espaço é trocado por um da
    mesma linha, tirado da faixa limpa entre as colunas — o veio continua no lugar.
    """
    a = np.array(Image.open(MOCKUP).convert("RGB")).astype(float)
    grid = EP.resample(a, GRID)
    x0, y0, x1, y1 = PANEL
    panel = grid[y0:y1 + 1, x0:x1 + 1].copy()

    covered = np.zeros(panel.shape[:2], bool)
    for bx0, by0, bx1, by1 in slot_boxes:
        covered[max(0, by0 - 2 - y0):by1 + 3 - y0, max(0, bx0 - 2 - x0):bx1 + 3 - x0] = True

    s0, s1 = WOOD_STRIP
    width = s1 - s0
    for y, x in zip(*np.where(covered)):
        panel[y, x] = grid[y0 + y, s0 + (x + x0 - s0) % width]

    alpha = np.full(panel.shape[:2], 255, np.uint8)
    snapped, ncols = EP.quantize(panel.astype(int), alpha, 14)
    img = np.dstack([snapped, alpha]).astype(np.uint8)
    Image.fromarray(img, "RGBA").save(os.path.join(OUT, "painel_direito.png"))
    print(f"  painel_direito.png: {img.shape[1]}x{img.shape[0]}, {ncols} cores")


def main():
    a = np.array(Image.open(SRC).convert("RGB")).astype(float)
    grid = EP.resample(a, GRID)
    bg = EP.background_mask(grid)
    lab, boxes = components(~bg)

    os.makedirs(OUT, exist_ok=True)
    for name, (cx, cy) in PIECES.items():
        n = lab[cy, cx]
        if n == 0:
            # O ponto caiu num buraco da peça (o canto tem formato de L): usa a menor
            # caixa que contém o ponto — a da janela contém quase tudo.
            dentro = [k for k, (x0, y0, x1, y1) in boxes.items() if x0 <= cx <= x1 and y0 <= cy <= y1]
            area = lambda k: (boxes[k][2] - boxes[k][0]) * (boxes[k][3] - boxes[k][1])
            n = min(dentro, key=area, default=0)
        if n == 0:
            print(f"  {name}: NAO ENCONTRADO")
            continue
        x0, y0, x1, y1 = boxes[n]
        rgb = grid[y0:y1 + 1, x0:x1 + 1].astype(int)
        alpha = np.where(lab[y0:y1 + 1, x0:x1 + 1] == n, 255, 0).astype(np.uint8)
        snapped, ncols = EP.quantize(rgb, alpha, QUANTIZE_DIST.get(name, 34))
        img = np.dstack([snapped, alpha]).astype(np.uint8)

        Image.fromarray(img, "RGBA").save(os.path.join(OUT, f"{name}.png"))
        print(f"  {name}.png: {img.shape[1]}x{img.shape[0]}, {ncols} cores")
        if name in SILHOUETTE:
            Image.fromarray(remove_silhouette(img), "RGBA").save(os.path.join(OUT, f"{name}_limpo.png"))
            print(f"  {name}_limpo.png")

    # Todos os espaços (os 10, não só os modelos exportados) para apagar do mockup.
    janela = lab[PIECES["janela"][1], PIECES["janela"][0]]
    slots = [b for k, b in boxes.items()
             if k != janela and PANEL[0] <= b[0] and b[2] <= PANEL[2] and b[2] - b[0] >= 35]
    print(f"  espaços apagados do mockup: {len(slots)}")
    build_panel(slots)


if __name__ == "__main__":
    main()
