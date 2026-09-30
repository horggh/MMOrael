# MMO_RAEL

Projeto pessoal de MMORPG inspirado no Ragnarok Online (mecânicas: classes,
atributos, cartas, refino, WoE/guildas) e no Stardew Valley (fazenda/crafting
em instância privada com modo cooperativo). Visual em 3D estilizado
(referências: Albion Online e Corepunk), engine Unity 6.

Projeto solo, sem fins comerciais.

## Sincronização entre máquinas

Este repositório é usado para manter o projeto sincronizado entre 3
computadores (Ubuntu, Windows e um terceiro PC). Pasta local em todas as
máquinas: `MMO_RAEL`.

Rotina recomendada:

- **Ao começar a trabalhar em qualquer PC**: `git pull` antes de abrir o Unity.
- **Ao terminar a sessão**: `git add`, `git commit`, `git push` antes de
  trocar de máquina.
- Evitar deixar o Unity aberto com alterações não commitadas ao trocar de PC.

## Assets baixados

Arquivos brutos baixados da internet (zips de pacotes de modelos, texturas etc.)
vão para a pasta `Downloads/` na raiz do projeto — **nunca dentro de `Assets/`**,
porque o Unity tentaria importar os zips e quebraria.

De lá, o conteúdo é extraído para o lugar certo dentro de `Assets/Art/`.
A pasta `Downloads/` está no `.gitignore`: só o que de fato entra no jogo é versionado.

Todo asset de terceiros deve ser registrado em [CREDITS.md](CREDITS.md) com autor e
licença. Alguns exigem atribuição obrigatória.

## Estrutura

```
Assets/
  Art/
    Models/
      Nature_Quaternius/   modelos de vegetação (CC0)
      Characters/          personagens
    Materials/
    Textures/
  Editor/Tools/            scripts de editor que montam/ajustam a cena
  Scenes/
    Map_StarterField.unity primeiro mapa (200x200 m)
```

### Convenção de escala

**1 unidade do Unity = 1 metro.** Personagem de referência: 1,70 m.
Árvores comuns entre 3 e 8 m; a árvore-marco no centro do mapa tem 15 m.
