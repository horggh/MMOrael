"""Ícone de habilidade no padrão do jogo: moldura de Tools/skill_frame.png + desenho no centro.

Uso: python3 Tools/make_skill_icon.py desenho.png Assets/Resources/Skills/<id>.png
"""
import sys
from PIL import Image

frame = Image.open("Tools/skill_frame.png").convert("RGBA")
art = Image.open(sys.argv[1]).convert("RGBA")
art = art.crop(art.getbbox())
box = 82  # área útil dentro da borda de 13 px, com folga
k = box / max(art.size)
art = art.resize((max(1, round(art.width * k)), max(1, round(art.height * k))), Image.NEAREST)
frame.alpha_composite(art, ((128 - art.width) // 2, (128 - art.height) // 2))
frame.save(sys.argv[2])
