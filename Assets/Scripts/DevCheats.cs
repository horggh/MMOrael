/// Chaves de teste da janela DEV (aba Testes). Ficam no jogo salvo.
public static class DevCheats
{
    /// O personagem não perde vida (os golpes acertam, mas a vida volta ao máximo).
    public static bool InfiniteHp;
    /// As habilidades não gastam SP.
    public static bool InfiniteSp;
    /// Equipa qualquer item sem o nível mínimo exigido (testes).
    public static bool IgnoreEquipLevel;
    /// Atributos postos no máximo pela janela DEV: passam do que os pontos permitem, então a
    /// conta de pontos não os devolve a 1 ao carregar. O reset de atributos desliga.
    public static bool MaxedAttributes;
    /// Multiplicador do dano que o personagem causa (1 = normal). Não vale para os monstros.
    public static int DamageRate = 1;
    public const int MaxDamageRate = 10000;
}
