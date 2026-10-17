/// Tabela de XP do Ragnarok Online (pré-Renewal, EXP de base de personagem normal; fonte
/// iRO Wiki Classic, também em Docs/tabela-xp-ragnarok.md). Vale para o nível base e para o
/// nível de skill. Valor = XP para passar do nível indicado ao próximo. Nível máximo 99.
public static class ExpTable
{
    public const int MaxLevel = 99;

    /// Multiplicador de XP, como as taxas dos servidores do Ragnarok: 1 = XP real do monstro.
    /// Ajustado na janela DEV (aba Testes) e guardado no jogo salvo.
    public static int Rate = 1;
    public const int MaxRate = 10000;

    private static readonly int[] Next =
    {
        9, 16, 25, 36, 77, 112, 153, 200, 253, 320,                                   // 1-10
        385, 490, 585, 700, 830, 970, 1120, 1260, 1420, 1620,                         // 11-20
        1860, 1990, 2240, 2504, 2950, 3426, 3934, 4474, 6889, 7995,                   // 21-30
        9174, 10425, 11748, 13967, 15775, 17678, 19677, 21773, 30543, 34212,          // 31-40
        38065, 42102, 46323, 53026, 58419, 64041, 69892, 75973, 102468, 115254,       // 41-50
        128692, 142784, 157528, 178184, 196300, 215198, 234879, 255341, 330188, 365914,           // 51-60
        403224, 442116, 482590, 536948, 585191, 635278, 687211, 740988, 925400, 1473746,          // 61-70
        1594058, 1718928, 1848355, 1982340, 2230113, 2386162, 2547417, 2713878, 3206160, 3681024, // 71-80
        4022472, 4377024, 4744680, 5125440, 5767272, 6204000, 6655464, 7121664, 7602600, 9738720, // 81-90
        11649960, 13643520, 18339300, 23836800, 35658000, 48687000, 58135000, 99999998,          // 91-98
    };

    /// XP para sair do nível. No máximo (99), 0 = não sobe mais.
    public static int ToNext(int level) => level >= 1 && level < MaxLevel ? Next[level - 1] : 0;
}
