# Prompts para gerar texturas de chão em outra IA

Texto pronto para colar num gerador de imagem. Em inglês de propósito: esses modelos
respondem bem melhor a prompts em inglês.

## Antes de usar: o que esperar

Geradores de imagem **não produzem pixel art de verdade**. Eles produzem ilustrações
*com aparência* de pixel art: a grade não é regular, saem centenas de cores em vez de
quatro, e as bordas quase nunca encaixam de verdade.

Na prática, o resultado serve como **referência de aparência**, não como arquivo final.
O fluxo que funciona:

1. Gerar a imagem com o prompt abaixo
2. Me mandar o arquivo (de preferência PNG)
3. Eu reamostro para a grade real, reduzo a paleta e **forço o encaixe nas bordas**

Peça a imagem **grande** (1024×1024 ou mais). Quanto maior, mais fácil recuperar a
grade. E peça **PNG**, nunca JPEG.

## Paleta do projeto

Para as texturas novas combinarem com a grama que já temos, inclua estas cores no prompt:

| Papel | Cor |
|---|---|
| Verde base | `#2f6244` |
| Verde sombra | `#163433` |
| Verde claro | `#4e9047` |
| Verde brilho | `#7dbb52` |
| Terra média | `#6e4839` |
| Terra escura | `#503233` |

## Prompt base

Substitua `<TERRENO>` pela descrição do terreno desejado.

```
Seamless tileable pixel art texture of <TERRENO>, top-down view for a 2D RPG game.

Requirements:
- Perfectly seamless and tileable: the left edge must continue into the right edge,
  and the top edge into the bottom edge, with no visible seam when repeated
- Flat top-down view, no perspective, no horizon, no shadows cast in any direction
- Uniform ambient lighting, no light source, no vignette, no gradient
- Limited palette of exactly 4 to 5 colors, hard edges, no anti-aliasing, no dithering
- Subtle, low-contrast surface texture only
- No large objects, no plants, no rocks, no props, no focal points
- No border, no frame, no grid lines, no text, no watermark
- Fills the entire square image edge to edge
```

A regra sobre **não ter objetos grandes** é a mais importante e a mais ignorada pelos
modelos. Chão é fundo: pedras, moitas e flores entram depois, como sprites soltos por
cima. Se vierem desenhados na textura, cada moita se repete pelo mapa inteiro.

## Terrenos para pedir

Troque o `<TERRENO>` por:

| Terreno | Descrição para o prompt |
|---|---|
| Terra batida | `packed dirt path, fine brown soil with tiny pebbles` |
| Areia | `fine beach sand, pale warm yellow` |
| Pedra | `cobblestone floor, grey cut stones with thin dark joints` |
| Rocha | `rough natural rock surface, cracked grey stone` |
| Madeira | `wooden plank floor, warm brown boards` |
| Neve | `fresh snow, near-white with faint blue shadows` |
| Lama | `wet mud, dark brown with subtle moisture sheen` |
| Cascalho | `loose gravel, small grey and brown stones` |
| Grama seca | `dry straw grass, pale yellow-brown` |
| Terra de plantio | `tilled farm soil, dark brown parallel furrows` |
| Água rasa | `shallow clear water over sand, gentle ripples` |
| Caverna | `dark cave floor, damp grey stone with dirt` |

Para combinar com a nossa grama, acrescente ao fim do prompt:

```
Color palette should harmonize with these colors: #2f6244, #163433, #4e9047, #7dbb52.
```

## Vegetação e objetos (prompt diferente)

Para tufos, moitas, pedras e flores — que vão **por cima** do chão — o prompt muda:
aqui você quer fundo transparente e um objeto só.

```
Single <OBJETO> sprite, pixel art, top-down view for a 2D RPG game.
Transparent background, one isolated object centered in the image.
Dark outline around the silhouette, limited palette of 4 to 6 colors,
hard edges, no anti-aliasing, no gradient, no shadow on the ground,
no border, no text. Flat top-down perspective.
```

Peça **um objeto por imagem**. Folhas de contato com vários objetos juntos dão mais
trabalho para separar, e os modelos costumam variar o estilo entre um e outro na
mesma imagem.
