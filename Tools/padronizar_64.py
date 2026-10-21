"""Padroniza a arte do MUNDO em 64 px por quadrado da grade (1 unidade = 1 quadrado = 64 px).

Regras (decididas em 20/10/2026, ver Docs/diario.md):
- Já no tamanho certo, a 32 px/quadrado (decoração, passagens, NPCs, bordas, prédios loja e
  mercearia, casa, monstros, plantações, sprites soltos do ambiente): desenho ampliado 2x
  (pixel por pixel, sem borrar) -> mesmo tamanho na tela, densidade 64.
- Árvores: 1,28 -> ~3,2 quadrados de altura (ampliação inteira, pixel por pixel).
- Peças da masmorra (já a 64, mas pequenas: parede no joelho): 4x.
- Baú (112 px/quadrado) e portal (40): reamostrados para 64 mantendo o tamanho na tela.
- Fundos de mapa inteiros (Environment/Mapas) ficam a 32: dobrar passaria do limite de
  textura de 4096 px.
- Prédios da cidade: refeitos das imagens originais na largura da tabela (Tools/escala.py).

Cada arquivo convertido entra em Tools/padronizar_64.json (com o tamanho antigo): rodar de
novo não amplia duas vezes. Onde a regra não vem do importador (sprites soltos do
ambiente), o .meta é ajustado para 64 px/unidade.

Uso: python Tools/padronizar_64.py [--simular]
"""
import glob
import json
import os
import re
import sys

from PIL import Image

ROOT = "Assets"
LOG = "Tools/padronizar_64.json"
SIM = "--simular" in sys.argv

DOUBLE = [
    "Assets/Art/Sprites/Environment/Decoracao/**/*.png",
    "Assets/Art/Sprites/NPCs/**/*.png",
    "Assets/Art/Sprites/Environment/Borda/*.png",
    "Assets/Art/Sprites/Environment/Predios/loja.png",
    "Assets/Art/Sprites/Environment/Predios/mercearia.png",
    "Assets/Resources/Buildings/*.png",
    "Assets/Resources/Crops/*.png",
    "Assets/Art/Sprites/**/Resources/Drake/*.png",
    "Assets/Art/Sprites/**/Resources/CavaleiroSangue/*.png",
    "Assets/Art/Sprites/**/Resources/SerialKiller/*.png",
]
# Sprites soltos do ambiente (regra no .meta): os do tamanho certo dobram; árvores crescem.
LOOSE = "Assets/Art/Sprites/Environment/*.png"
TREE_TARGET = 3.2       # quadrados de altura
MASMORRA = ("Assets/Art/Sprites/Environment/Masmorra/*.png", 4)
RESAMPLE = {"Assets/Resources/Props/bau_*.png": 64 / 112, "Assets/Resources/Props/portal_*.png": 64 / 40}
SKIP_BIG = 1024         # imagens maiores que isso (bases de grama) ficam como estão

done = json.load(open(LOG, encoding="utf-8")) if os.path.exists(LOG) else {}


def key(p):
    return p.replace("\\", "/")


def nearest(p, k):
    im = Image.open(p)
    new = im.resize((im.width * k, im.height * k), Image.NEAREST)
    if not SIM:
        new.save(p)
    return im.size


def resample(p, f):
    im = Image.open(p).convert("RGBA")
    w, h = max(1, round(im.width * f)), max(1, round(im.height * f))
    a = im.split()[3]
    new = im.resize((w, h), Image.LANCZOS if f < 1 else Image.NEAREST)
    if f < 1:   # reduzindo: transparência sem meio-termo (pixel art)
        al = a.resize((w, h), Image.LANCZOS).point(lambda v: 255 if v >= 128 else 0)
        new.putalpha(al)
    if not SIM:
        new.save(p)
    return im.size


def set_meta_ppu(p, ppu):
    meta = p + ".meta"
    if not os.path.exists(meta):
        return
    s = open(meta, encoding="utf-8").read()
    s2 = re.sub(r"spritePixelsToUnits: [\d.]+", f"spritePixelsToUnits: {ppu}", s)
    if s2 != s and not SIM:
        open(meta, "w", encoding="utf-8").write(s2)


def mark(p, what, size):
    done[key(p)] = {"regra": what, "antes": list(size)}


n = {"dobrar": 0, "arvore": 0, "masmorra": 0, "reamostrar": 0}
for pat in DOUBLE:
    for p in glob.glob(pat, recursive=True):
        if key(p) in done:
            continue
        mark(p, "dobrar", nearest(p, 2))
        n["dobrar"] += 1

for p in glob.glob(LOOSE):
    if key(p) in done:
        continue
    im = Image.open(p)
    if max(im.size) > SKIP_BIG:
        continue
    name = os.path.basename(p)
    if name.startswith("arvore"):
        k = max(2, round(TREE_TARGET * 64 / im.height))
        mark(p, f"arvore x{k}", nearest(p, k))
        n["arvore"] += 1
    else:
        mark(p, "dobrar", nearest(p, 2))
        n["dobrar"] += 1
    set_meta_ppu(p, 64)

pat, k = MASMORRA
for p in glob.glob(pat):
    if key(p) in done:
        continue
    mark(p, f"masmorra x{k}", nearest(p, k))
    n["masmorra"] += 1

for pat, f in RESAMPLE.items():
    for p in glob.glob(pat):
        if key(p) in done:
            continue
        mark(p, f"reamostrar x{f:.3f}", resample(p, f))
        n["reamostrar"] += 1

if not SIM:
    json.dump(done, open(LOG, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
print(("SIMULAÇÃO " if SIM else "") + str(n))
