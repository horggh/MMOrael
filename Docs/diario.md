# Diário de atividades — RaelMMO

Registro do que foi feito em cada dia de trabalho, o que ficou pendente e as decisões
tomadas. A entrada mais nova fica em cima.

---

## 20/10/2026

- Criado este diário (`Docs/diario.md`), com o histórico dos dias anteriores.
- **Git conferido:** não há atualização nova no GitHub (`horggh/MMOrael`, branch `main`). O
  último commit, local e remoto, é o de 16/10 23:53 ("regras do Ragnarok, boss Drake, skins,
  construção e interface nova"). Tudo de 17/10 a 20/10 está só neste computador, **sem commit**
  (cerca de 1.550 arquivos novos ou alterados).

- **Decisão: o 3D foi descartado** ("foi apenas um teste"). Apagados: `Assets/Art/Models`, cena
  `Izlude3D`, scripts `ThreeD`, menu 3D, renderizador 3D do URP e scripts do Blender. O Blender
  continua instalado, sem uso. Foco total no **2D pixel art**.
- Lido o guia de mapas top-down (`Downloads/unity_pixel_art_map_guide.txt`). Prioridades novas:
  1. ferramenta de **pintar terrenos com autotiling** (rios, trilhas, grama e demais);
  2. **todas as peças de transição** de cada terreno;
  3. **profundidade** (passar atrás de copas e telhados) com a melhor técnica do mercado;
  4. **modelo de mapa** pronto (Grid com camadas) para começar mapas novos;
  5. **escala padronizada** de personagens, prédios, decoração e monstros.
- Medição de escala (1 unidade = 1 quadrado): personagem 1,66; NPCs 1,59 a 1,78; prédios com
  6 de largura; decoração 0,3 a 2,8; **árvores só 1,28** (menores que o personagem); **peças da
  masmorra 0,5 a 0,8** (paredes abaixo do joelho); pisos 1,0.

- **Escala e 64 px (item 5) — feito.** Tudo do mundo passou a 64 px por quadrado
  (`Tools/padronizar_64.py`, registro em `Tools/padronizar_64.json`): 459 desenhos ampliados 2x
  pixel a pixel (mesmo tamanho na tela), árvores para ~3,2 de altura, masmorra 4x, baú e
  portal reamostrados. Prédios refeitos das imagens originais na largura da tabela
  (`Tools/escala.py`: casa 6, palácio 12, catedral 10, igreja grande 10, mansão 9, torres 5);
  a base sólida acompanha a largura. Código e importadores em 64; câmera Pixel Perfect em
  960x540 (mesma área visível). Exceção: fundos de mapa inteiros ficam em 32 (limite 4096 px).
- **Peças de transição (item 2) — feito.** Grade dupla: 13 terrenos x 16 peças
  (`Tools/gen_terrenos.py` → `Environment/Terrenos`): água (com espuma e colisão), areia, lama,
  terra, grama, grama florida, mato seco, trilha, cascalho, pedras, calçamento, tábuas, masmorra.
- **Pincel de terreno (item 1) — feito.** **RaelMMO > Mapa > Pincel de terreno**: escolhe o
  terreno, pinta arrastando, bordas automáticas, Shift/botão direito apaga, tamanho 1–10,
  preencher a área dos limites. Dados em `TerrenoMapa`.
- **Profundidade (item 3) — feito.** `Profundidade`: objetos altos ficam transparentes com o
  personagem atrás; camada "Por cima" some quando ele passa por baixo; silhueta do personagem
  atrás de prédios.
- **Modelo de mapa (item 4) — feito.** **RaelMMO > Mapa > Novo mapa (modelo)**: limites 40x30,
  terreno com grama, camadas Detalhes / Bloqueio (colisão) / Por cima e grupos organizados.

- **Decoração básica das salas de porta/escada.** Botões no Inspector da sala ("Interior - ..."):
  *Salvar a decoração desta sala como básica* e *Carregar a decoração básica nesta sala*
  (`Resources/DecoracaoBasica`, um modelo para sala e outro para corredor). Sala nova já nasce
  com a básica; no Play, sala de porta/escada sem decoração própria usa a básica. Menu extra:
  **RaelMMO > Prédios > Replicar decoração de porta para as outras portas**.

- **Decoração nova (186 peças, já em 64 px)** em `Environment/Decoracao`:
  - castelo 1 (`Tools/extract_castelo.py`, 54): mesas, tronos, estátuas, armaduras,
    estandartes, lustres, tochas, baús, lareira, tapeçarias;
  - mais 3 folhas (`Tools/extract_decor_folhas.py`, 132): castelo (armas, vitrais, túmulos,
    cozinha), vila (forno, horta, poço, alquimia, igreja) e tortura, quarto e jardim.
  - Os nomes repetidos ganham `_2`, `_3`.
  - A `escrivaninha.png` antiga foi sobrescrita pela de castelo e não pôde ser recuperada.
- **Botão "Limpar a decoração desta sala"** no Inspector da sala: apaga só a decoração dessa
  sala (as outras e a básica salva não mudam; Ctrl+Z desfaz).
- Pincel de terreno: botão direito apaga qualquer terreno pintado (em todos os terrenos do
  mapa), pincel quadrado do tamanho exato da grade, peças refeitas para encher o quadrado
  inteiro, erro de tilemap apagado (MissingReference) corrigido.
- **Commit e push** de todo o trabalho de 17/10 a 20/10.

**Pendente**
- Investigar: "não consigo usar passagens dentro dos prédios" (o usuário pediu para esperar).
- Testar no Unity: escala nova, pincel de terreno, modelo de mapa e profundidade.
- Scripts antigos de recorte (decoração, NPCs, masmorra...) ainda geram em 32 px: ajustar
  quando forem usados de novo.
- Próximas peças: cercas e muros que se conectam sozinhos (retas, L, T, cruz) e pontes.
- Árvores e masmorra ampliadas sem ganhar detalhe: gerar arte nova em resolução maior.

---

## 19/10/2026 (noite) — começo do 3D

**Decisão:** evoluir para visão isométrica 3D (mapa como o Ragnarok, personagens como o
Corepunk), em fases e sem quebrar o jogo 2D. Toda a lógica atual (atributos, combate,
inventário, save, janelas) será reaproveitada.

- **Blender 5.2.2** instalado; modelos em `Assets/Art/Models/` (`Personagens`, `Monstros`,
  `Mapas`, `Objetos`, `Animacoes`). O Unity importa `.blend` direto.
- **Mapa 3D de Izlude** (`Tools/blender_izlude.py` → `Models/Mapas/izlude.blend`): mesmas
  posições do Izlude do Ragnarok, visual simplificado (blocos, telhados em cunha, árvores em
  cone). Grade de passagem em `izlude_grade.txt`.
- **Aprendiz 3D por script** (`Tools/blender_aprendiz.py` → `aprendiz.blend`): original, inspirado
  no aprendiz; esqueleto e animações parado / andar / atacar.
- **Aprendiz gerado por IA** (Hunyuan3D, só a forma) preparado por
  `Tools/blender_preparar_modelo.py` → `aprendiz_ia.blend`: separado, sem base, 15 mil faces,
  pintado por regiões, com esqueleto e animações. *Atenção:* foi gerado a partir do sprite do
  Ragnarok; trocar por modelo de ilustração original antes de lançar.
- **Protótipo 3D**: `Player3D` (anda por clique, colide com prédios/árvores, não entra na água),
  `Iso3DCamera` (segue, zoom na roda, gira com botão direito), `Ground3D`; renderizador 3D próprio
  (`Settings/Renderer3D.asset`) só para essa cena. Menu **RaelMMO > 3D > Criar cena Izlude 3D**.
- Hunyuan3D online: a textura passa do limite gratuito do Hugging Face; o Meshy é a alternativa
  com textura e esqueleto automático (conferir licença do plano gratuito).

## 19/10/2026 (tarde e noite) — itens, habilidades e bônus

- **460 armas e afins** recortadas de 17 folhas (`Tools/extract_armas.py`): espadas, espadas
  mágicas, lanças, alabardas, machados, martelos de guerra, maças, foices, cajados, katares,
  soqueiras, chicotes, instrumentos, livros, shurikens, escudos e aljavas. Tipos de arma novos
  com ASPD própria. Cadastradas em `ItemDB.cs`.
- **60 consumíveis** (poções, bebidas e comidas; `Tools/extract_consumiveis.py`). Consumível
  agora recupera também SP.
- **Bônus de comida** por 5 min: Pudim +100 precisão, Bife +100 ASPD, Hambúrguer +100 ATQ.
  Ícones com o tempo restante ao lado da barra do personagem. Tudo no save.
- **Impacto Explosivo** com 10 níveis como o Magnum Break (área 5x5, centro/borda, precisão,
  empurrão, custo de HP, +20% de fogo por 10 s); no nível 10, 500% em toda a área.
- **Perfurar em Espiral** (Spiral Pierce, 5 níveis): só com lança, alcance 4, 5 golpes que
  ignoram a DEF, dano pelo peso da arma, prende o alvo 1 s, conjuração e espera por nível.
- **Lança de Caça** (Alabarda 20 → item 1422 Hunting_Spear): ATQ 180, peso 420, alcance 3,
  nível 60. Armas podem ter alcance e nível mínimo; chave DEV "Sem nível mín.".
- **Ficha do monstro** (botão direito no monstro): atributos, precisão, esquiva, chance de
  acerto e lista de drops (componente `MonsterDrops`, drops ainda não definidos).
- **Pintar parede**: janela **RaelMMO > Chão > Pintar parede** com os 60 papéis de parede.

## 19/10/2026 (tarde) — monstros, MVPs e mapas

- **Bloody Murderer** (rAthena 1507) e **Lord of Death** (MVP 1373, com habilidade em área),
  com os atributos oficiais pré-Renewal. Barras de vida pequenas sob os monstros; barra grande
  no topo para MVPs. Drake virou objeto da cena (mover, apagar, editar).
- **Janela RaelMMO > Mapa > Monstros** (abas Monstros e MVPs): clica, diz quantos e espalha.
- Pasta `Monsters/MVP` para os MVPs (sprites arrastáveis para a cena).
- **Mapas novos**: Cidade (ruas), Praça, Caverna e Caverna 2, cada um área própria com limites.
- **Portal de mapa** e **entradas de caverna** (pasta `Passagens`) que ligam áreas da mesma cena.
- **Desenhar barreira** com o mouse na Scene (**Ctrl+Shift+B**).
- Correções: câmera e personagem duplicados ao copiar mapa, desenho dos monstros separado do
  corpo, escala dos prédios mantida no Play, balcão em diagonal.

## 19/10/2026 (manhã) — prédios, NPCs, decoração

- Skins na mesma altura/resolução do "Cavaleiro (camadas)"; montaria com golpe para a frente.
- **Besta** (arma à distância, 5 quadrados, virote que voa).
- **26 prédios** (cavalaria, estábulo, taberna, hotel, boticário, catedral, palácio...) com a
  área, a barreira e o interior da casa; interiores decorados no editor (**RaelMMO > Prédios >
  Editar interior**); o jogador só personaliza a própria casa.
- **Escadas e 29 portas** com "Subir!" (corredor) e "Entrar" (sala).
- **NPCs da vila** (12, quatro direções), decorações de mercado (65) e bancas (36), papéis de
  parede (60) e janelas como sprites arrastáveis.
- Chão sempre embaixo, colisão na base das decorações, atalhos de camada (**Shift+PgUp/PgDn**).
- Investigado o save: a casa pronta e os baús tinham sido perdidos em 17/10 (sem cópia).

## 17/10/2026 — mapas e editor

- Limites do mapa editáveis, bordas, fundo do mapa Cruzamento, zoom extra de aproximação.
- Pisos com o tamanho exato da grade, alinhamento, grama em toda a base, Tilemap e paleta.
- Prédio comercial e mercearia, decorações, NPCs e peças de masmorra.
- Sistema de skins em camadas (corpo + arma + escudo só no golpe), janelas que vêm para a frente.

## 16/10/2026 — base do jogo

- Projeto em `D:\raelmmo` (repositório `horggh/MMOrael`).
- Janelas novas de equipamento, habilidades e atributos; baú com itens salvos.
- **Casa**: planta de projeto, 5 níveis de obra com materiais, interior com piso, papel de
  parede e janelas; martelo para mover/recolher móveis.
- Portal de teleporte e pedra de retorno; zoom pela roda do mouse.
- Regras do Ragnarok pré-Renewal: ASPD, atributos e custo de pontos, XP real da tabela,
  XP de skill, reset de atributos.
- Primeiro chefe: **Drake**; martelo de guerra do GM; janela DEV com aba Testes; instalador
  para Windows.

---

## Pendências gerais

- Definir os **drops** dos monstros (estrutura pronta em `MonsterDrops`).
- Itens dos drops oficiais ainda não existem (ex.: do Bloody Murderer e do Lord of Death).
- Sistemas que o jogo ainda não tem: **elemento, raça e tamanho** de monstro; Golpe Articulado.
- Abas por tipo de arma na janela DEV (hoje todas ficam em Equipamentos).
- Curva de XP do jogo (tabela do Ragnarok guardada em `Docs/tabela-xp-ragnarok.md`).
- Mapas 2D: cercas e muros com conexão automática, pontes, arte nova para árvores e masmorra.
