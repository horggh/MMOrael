using UnityEngine;

/// Seis atributos básicos e os valores de combate derivados deles.
/// Fórmulas próprias, inspiradas no Ragnarok Online.
public class CharacterStats : MonoBehaviour
{
    public int level = 1;

    [Header("Atributos")]
    [Tooltip("Força: ataque físico")] [Range(1, 99)] public int strength = 1;
    [Tooltip("Agilidade: velocidade de ataque e esquiva")] [Range(1, 99)] public int agility = 1;
    [Tooltip("Vitalidade: vida máxima e defesa")] [Range(1, 99)] public int vitality = 1;
    [Tooltip("Inteligência: SP e ataque mágico")] [Range(1, 99)] public int intelligence = 1;
    [Tooltip("Destreza: precisão e um pouco de ataque")] [Range(1, 99)] public int dexterity = 1;
    [Tooltip("Sorte: crítico, um pouco de precisão e esquiva")] [Range(1, 99)] public int luck = 1;

    [Header("Equipamento")]
    public int weaponAtk = 0;

    [Header("Progressão")]
    public int statPoints = 10;
    public int exp = 0;
    [Tooltip("XP que este personagem dá ao ser derrotado.")]
    public int expReward = 30;

    public static readonly string[] Names = { "FOR", "AGI", "VIT", "INT", "DES", "SOR" };

    /// Disparado quando atributos ou nível mudam. true = subiu de nível.
    public event System.Action<bool> Changed;

    public int ExpToNext => 50 + level * level * 10;

    public int Get(int i) => i switch
    {
        0 => strength, 1 => agility, 2 => vitality, 3 => intelligence, 4 => dexterity, _ => luck,
    };

    /// Atributo alto custa mais: incentiva distribuir em vez de jogar tudo num só.
    public int CostToRaise(int i) => 1 + Get(i) / 10;

    public bool TryRaise(int i)
    {
        int cost = CostToRaise(i);
        if (Get(i) >= 99 || statPoints < cost) return false;
        statPoints -= cost;
        switch (i)
        {
            case 0: strength++; break;
            case 1: agility++; break;
            case 2: vitality++; break;
            case 3: intelligence++; break;
            case 4: dexterity++; break;
            default: luck++; break;
        }
        Changed?.Invoke(false);
        return true;
    }

    public void GainExp(int amount)
    {
        exp += amount;
        bool leveled = false;
        while (exp >= ExpToNext)
        {
            exp -= ExpToNext;
            level++;
            statPoints += 3 + level / 5;
            leveled = true;
        }
        Changed?.Invoke(leveled);
    }

    public int MaxHp => 40 + vitality * 8 + level * 5;
    public int MaxSp => 10 + intelligence * 4 + level * 2;
    public int Atk => 5 + strength * 2 + dexterity / 2 + weaponAtk;
    public int Matk => intelligence * 2 + level;
    public int Def => vitality / 2;
    public int Hit => level + dexterity + luck / 3;
    public int Flee => level + agility + luck / 5;
    /// Percentual. Crítico ignora esquiva e defesa.
    public float CritChance => luck * 0.3f;
    /// Multiplicador: 1 = normal, 1.5 = ataca 50% mais rápido.
    public float AttackSpeed => 1f + agility * 0.02f + dexterity * 0.005f;
}
