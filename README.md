# MMO_RAEL

RPG 2D em pixel art, vista de cima (¾ top-down), Unity 6.
Mistura o ciclo de fazenda/crafting do Stardew Valley com combate, atributos e
equipamento inspirados no Ragnarok Online. Referência de escopo: Romestead.

Projeto pessoal, sem fins comerciais. Toda a arte é original do autor.

## Pastas

- `imagens/` — arte original (capturas JPEG enviadas pelo autor)
- `Tools/` — scripts Python que extraem a pixel art das capturas e geram animações/tiles
- `Assets/Art/Sprites/` — sprites prontos (cenário, personagens, monstros)
- `Assets/Resources/` — itens e folhas de animação carregadas em tempo de execução
- `Assets/Scripts/` — código do jogo (movimento, combate, atributos, inventário, UI)
- `Assets/Editor/Setup/` — scripts que montam a cena `Campo` por linha de comando

## Controles

WASD anda · Shift corre · clique ataca na direção do mouse ·
I inventário · E equipamento · C atributos · K habilidades · 1–0 barra de atalhos ·
clique direito num móvel (baú) do inventário para colocá-lo no mapa; clique no baú para abrir

**Ferramenta de desenvolvedor** (só no editor e em builds de desenvolvimento): botão **DEV**
no canto superior direito abre a janela **ITENS** com todos os itens do jogo (`ItemDB.All`,
itens novos aparecem sozinhos). Os itens são infinitos: arrastar para o inventário ou baú dá 1,
Shift + arrastar dá uma pilha cheia, clique direito manda 1 para o inventário.

## Jogo salvo

O mundo continua como foi deixado: personagem (posição, vida, SP, nível, atributos,
habilidades), inventário, equipamento, barra de atalhos, baús colocados com o conteúdo e
plantações. Grava sozinho a cada 15 s, ao fechar e ao construir algo
(`Assets/Scripts/SaveGame.cs`). O arquivo fica na pasta de dados do Unity de cada máquina,
fora do Git. Menu **MMO_RAEL → Apagar jogo salvo** para recomeçar do zero.

**Regra:** todo sistema, item no mapa ou progresso novo precisa entrar no jogo salvo
(campo em `SaveGame.Data`, gravar em `Collect()`, restaurar em `Load()`).

## Peças de interface

`Tools/extract_ui_equipamento.py` e `Tools/gen_ui_kit.py` geram a moldura, os cantos,
bordas, junções e faixas de título em `Assets/Resources/UI/`; `UIKit.cs` monta as janelas.

## Sincronização entre máquinas

`git pull` antes de abrir o Unity; `git add` / `commit` / `push` ao terminar.
