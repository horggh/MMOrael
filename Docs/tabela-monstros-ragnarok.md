# Monstros do Ragnarok (pré-Renewal) — referência

Fonte: RateMyServer, Mob DB pré-Renewal (https://ratemyserver.net/index.php?page=mob_db&rm_t=pre).
Valores oficiais a 1x. No RaelMMO a XP recebida é multiplicada por `ExpTable.Rate` (hoje 30x, para os testes).

| Monstro      | ID   | Nível | HP    | Ataque  | XP base | XP de skill (job) |
|--------------|------|-------|-------|---------|---------|-------------------|
| Poring       | 1002 | 1     | 50    | 7–10    | 2       | 1                 |
| Lunatic      | 1063 | 3     | 60    | 9–12    | 6       | 2                 |
| Esqueleto    | 1076 | 10    | 234   | 39–47   | 18      | 14                |
| Zumbi        | 1015 | 15    | 534   | 67–79   | 50      | 33                |
| Orc Guerreiro| 1023 | 24    | 1400  | 104–126 | 408     | 160               |
| **Lobo**     | 1013 | 25    | 919   | 37–46   | **329** | **199**           |
| Goblin       | 1122 | 25    | 1176  | 118–140 | 310     | 188               |

No jogo, o lobo (WolfAI.cs: `baseExp`, `jobExp`) já usa os valores do Lobo. Com 30x ele dá
9.870 de XP base e 5.970 de XP de skill por abate.
