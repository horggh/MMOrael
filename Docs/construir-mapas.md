# Como construir mapas (Tilemap)

O chão do mapa é pintado com **blocos** (tiles), como num programa de desenho. Cada bloco
ocupa exatamente 1 quadrado da grade (64 px). O Unity desenha o mapa em pedaços grandes,
então fica leve mesmo com milhares de blocos.

## Preparar (uma vez, ou quando surgirem texturas novas)

1. **RaelMMO → Mapa → 1. Gerar blocos e paleta**
   Cria um bloco para cada textura de `Assets/Art/Sprites/Environment/Texturas`
   (em `Assets/Tiles/Chao`) e a paleta **Paleta RaelMMO**, organizada por tema:
   gramas · terras/areia/água · pedras e madeira · pisos urbanos.
2. **RaelMMO → Mapa → 2. Criar camadas na cena**
   Cria o objeto **Mapa (Tilemap)** com três camadas:
   | Camada | Para quê |
   |---|---|
   | **Chão** | grama, terra, areia, pisos — a base |
   | **Detalhes** | por cima do chão: trilhas, manchas, flores, pisos decorativos |
   | **Bloqueio** | água, penhasco, muro — **o personagem não passa** (colisor automático) |
3. Salve a cena (**Ctrl+S**).

## Pintar

1. **Window → 2D → Tile Palette** (deixe a janela encaixada ao lado da Scene).
2. Na lista do alto da janela, escolha **Paleta RaelMMO**.
3. Em **Active Tilemap**, escolha a camada: **Chão**, **Detalhes** ou **Bloqueio**.
4. Clique num bloco da paleta e pinte na Scene.

| Tecla | Ferramenta |
|---|---|
| **B** | pincel (um quadrado por vez; arraste para pintar vários) |
| **U** | retângulo (arraste para encher uma área) |
| **G** | balde (enche a área fechada) |
| **D** | borracha |
| **I** | conta-gotas (pega o bloco que está no mapa) |
| **S** | selecionar e mover uma parte já pintada |

Dica: para pegar **vários blocos de uma vez** (ex.: 2x2), arraste um retângulo na paleta.

## Texturas novas

- Coloque a imagem 64x64 (que emende com ela mesma) em `Assets/Art/Sprites/Environment/Texturas`
  ou gere com os scripts de `Tools/` (`gen_texturas_campo.py`, `extract_pisos_natureza.py`).
- Rode de novo **RaelMMO → Mapa → 1. Gerar blocos e paleta**.

## O que fica fora do Tilemap

Árvores, casas, baús, cercas, pedras grandes e personagens continuam como **objetos**
(sprites soltos), porque são mais altos que um quadrado e precisam aparecer na frente ou
atrás do personagem conforme a altura no mapa.
