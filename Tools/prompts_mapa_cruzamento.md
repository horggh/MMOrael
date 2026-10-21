# Prompts para gerar os elementos do mapa "Cruzamento" em outra IA

Base: o mapa do meio de `imagens/mapas-referencia.jpg` (rio no alto, pontes, cruzamento de
caminhos de terra no centro, florestas, platôs com penhasco, pedras).

Em inglês de propósito: geradores de imagem respondem melhor. Peça **PNG** e **grande**
(1024×1024 ou mais). As mesmas observações de `prompts_texturas.md` valem aqui: a IA faz
algo *com cara* de pixel art; eu reamostro para a grade, reduzo a paleta e acerto o encaixe.

## Escala do jogo (para todos os pedidos)

- Ladrilho de chão: **32×32 px**.
- Personagem: **cerca de 48 px de altura** (1,5 ladrilho).
- Árvore: **2 a 3 ladrilhos** de altura. Ponte: **2 ladrilhos** de largura.

## Bloco de estilo (cole no começo de TODOS os pedidos)

```
16-bit SNES-era JRPG pixel art, top-down 3/4 view (like classic RPG Maker / Secret of
Mana maps), lush saturated greens, sandy dirt paths, deep blue water, grey-brown rocky
cliffs. Crisp hard pixels, no anti-aliasing, no blur, limited palette (max 32 colors),
consistent light from the top-left. Game-ready asset on a fully transparent background
(PNG with alpha), no checkerboard, no text, no watermark, no frame.
Scale reference: one ground tile = 32x32 pixels, a human character = 48 pixels tall.
```

> Se o gerador não fizer fundo transparente, peça "solid flat magenta background (#FF00FF)":
> eu removo o magenta. **Nunca** fundo xadrez.

---

## 1. Chão (ladrilhos que se repetem)

Acrescente ao bloco de estilo a regra de ladrilho:
`Seamless tileable square texture, left edge continues into right edge and top into bottom, flat top-down, no objects, no plants, no rocks, subtle low-contrast detail only, fills the whole image.`

| Elemento | Texto específico |
|---|---|
| Grama | `Lush short grass ground, bright and mid greens with tiny darker blades.` |
| Caminho de terra | `Light sandy dirt path ground, warm beige and tan with fine pebbly speckles.` |
| Água do rio | `Deep blue river water, gentle wave ripples in two lighter blues.` |
| Grama alta / mato | `Dense darker green meadow grass, slightly taller tufts, still flat and tileable.` |

## 2. Transições (as bordas entre terrenos)

```
Autotile transition sheet, 3x3 grid of 32x32 tiles (corners, edges and center) showing
<A> on the inside and <B> on the outside, organic irregular border, same style.
```

| Transição | `<A>` | `<B>` |
|---|---|---|
| Caminho na grama | `sandy dirt path` | `grass` |
| Margem do rio | `river water with a thin light foam line at the shore` | `grass` |
| Margem de pedra do rio | `river water` | `grey stone riverbank with small rocks` |

## 3. Penhascos e platôs

```
Cliff edge pieces for a raised grassy plateau, 3/4 top-down view: grass on top, vertical
grey-brown rock face with cracks and moss below, about 64 pixels tall. Provide separate
pieces in one sheet: straight horizontal front face (seamless left-right), left corner,
right corner, inner corners, and a straight side face (left and right). Same style.
```

E, separado, uma escada para subir no platô:

```
Small stone stairway carved into a rocky cliff, 3/4 top-down view, 2 tiles wide, same style.
```

## 4. Pontes

```
Wooden plank bridge over a river, 3/4 top-down view, 2 tiles wide, planks running
across, rope or wooden side rails. Two versions in one sheet: crossing left-to-right
and crossing top-to-bottom. Length 4 tiles, plus separate end pieces so it can be
made longer.
```

```
Old grey stone arch bridge over a river, 3/4 top-down view, 3 tiles wide, crossing
diagonally from bottom-left to top-right, same style.
```

## 5. Árvores e floresta

```
Set of 6 individual deciduous trees with round dense dark-green canopies and short
brown trunks, 3/4 top-down view, each 64 to 96 pixels tall, varied shapes, each tree
separated by empty space, same style.
```

```
Forest canopy edge strip: a dense wall of overlapping tree crowns seen from 3/4 top-down,
dark and mid greens, seamless left-right, about 96 pixels tall, bottom edge shows a few
trunks and shadow, same style.
```

```
Set of 8 small bushes and shrubs, round and leafy, 16 to 32 pixels, varied greens, a
couple with small flowers, separated by empty space, same style.
```

## 6. Pedras e detalhes

```
Set of 10 rocks and boulders, grey with soft highlights and moss patches, sizes from
16 to 48 pixels, some single stones and some small clusters, separated by empty space,
same style.
```

```
Set of small ground details: grass tufts, little flowers (white, yellow, pink),
pebbles, fallen leaves, mushrooms, each 8 to 16 pixels, separated, same style.
```

## Ordem sugerida

1. Chão (grama, caminho, água) — já dá para desenhar o mapa.
2. Transições de caminho e de margem — tiram o aspecto quadriculado.
3. Penhascos — fazem os platôs.
4. Pontes, árvores, pedras e detalhes.

Mande cada imagem gerada e eu separo, ajusto à grade e monto as peças no Unity.
