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

    // Bônus do equipamento: não são salvos, o Equipment recalcula ao mudar.
    private readonly int[] _eqAttrs = new int[6];
    private int _eqAtk, _eqDef, _eqHp;

    public int Bonus(int i) => _eqAttrs[i];
    /// Atributo final = base distribuída + equipamento.
    public int Total(int i) => Get(i) + _eqAttrs[i];

    private int Str => Total(0);
    private int Agi => Total(1);
    private int Vit => Total(2);
    private int Int => Total(3);
    private int Dex => Total(4);
    private int Luk => Total(5);

    public int MaxHp => 40 + Vit * 8 + level * 5 + _eqHp;
    public int MaxSp => 10 + Int * 4 + level * 2;
    public int Atk => 5 + Str * 2 + Dex / 2 + weaponAtk + _eqAtk;
    public int Matk => Int * 2 + level;
    public int Def => Vit / 2 + _eqDef;
    public int Hit => level + Dex + Luk / 3;
    public int Flee => level + Agi + Luk / 5;
    /// Percentual. Crítico ignora esquiva e defesa.
    public float CritChance => Luk * 0.3f;
    /// Multiplicador: 1 = normal, 1.5 = ataca 50% mais rápido.
    public float AttackSpeed => 1f + Agi * 0.02f + Dex * 0.005f;

    public void SetEquipBonus(int[] attrs, int atk, int def, int hp)
    {
        for (int i = 0; i < 6; i++) _eqAttrs[i] = attrs[i];
        _eqAtk = atk; _eqDef = def; _eqHp = hp;
        Changed?.Invoke(false);
    }
}
