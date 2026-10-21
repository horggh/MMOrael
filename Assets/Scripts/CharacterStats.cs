using UnityEngine;

/// Seis atributos básicos e os valores de combate derivados deles, com as fórmulas do
/// Ragnarok Online pré-Renewal (personagem: tabela do Espadachim; monstro: perfil fixo do
/// banco de monstros, ver SetMonster).
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
    public int statPoints = StartPoints;
    public int exp = 0;

    [Header("Skill (equivale ao nível de classe do Ragnarok)")]
    public int jobLevel = 1;
    public int jobExp = 0;

    /// Subiu de nível de skill (quantos níveis de uma vez): cada um dá 1 ponto de habilidade.
    public event System.Action<int> JobLeveled;

    public static readonly string[] Names = { "FOR", "AGI", "VIT", "INT", "DES", "SOR" };

    /// Disparado quando atributos ou nível mudam. true = subiu de nível.
    public event System.Action<bool> Changed;

    /// XP para o próximo nível (tabela do Ragnarok; 0 no nível máximo).
    public int ExpToNext => ExpTable.ToNext(level);
    public int JobExpToNext => ExpTable.ToNext(jobLevel);

    public int Get(int i) => i switch
    {
        0 => strength, 1 => agility, 2 => vitality, 3 => intelligence, 4 => dexterity, _ => luck,
    };

    // ── Pontos de atributo, como no Ragnarok pré-Renewal ──
    // Começa com 48 pontos e todos os atributos em 1. Subir do nível L para L+1 dá 3 + L/5
    // pontos (no nível 99 são 1.273 no total). Subir um atributo de x para x+1 custa
    // (x − 1)/10 + 2: 2 pontos até 10, 3 de 11 a 20 ... 11 de 91 a 98 (1 → 99 custa 628).

    public const int StartPoints = 48;

    public static int Cost(int value) => (value - 1) / 10 + 2;
    public int CostToRaise(int i) => Cost(Get(i));

    /// Pontos ganhos ao sair do nível `fromLevel` para o próximo.
    public static int PointsForLevelUp(int fromLevel) => 3 + fromLevel / 5;

    /// Total de pontos que um personagem deste nível recebeu desde o início.
    public static int TotalPoints(int lvl)
    {
        int total = StartPoints;
        for (int l = 1; l < lvl; l++) total += PointsForLevelUp(l);
        return total;
    }

    /// Pontos que custou levar um atributo de 1 até o valor atual.
    private static int SpentOn(int value)
    {
        int total = 0;
        for (int v = 1; v < value; v++) total += Cost(v);
        return total;
    }

    private int SpentOnAll()
    {
        int total = 0;
        for (int i = 0; i < 6; i++) total += SpentOn(Get(i));
        return total;
    }

    /// Pontos livres = total do nível − gasto nos atributos. Se o gasto passar do total
    /// (jogo salvo de quando os custos eram outros), volta tudo a 1 para redistribuir.
    public void RecountPoints()
    {
        int free = TotalPoints(level) - SpentOnAll();
        if (free < 0 && DevCheats.MaxedAttributes) free = 0;
        if (free < 0)
        {
            strength = agility = vitality = intelligence = dexterity = luck = 1;
            free = TotalPoints(level);
        }
        statPoints = free;
    }


    /// Reset de build: todos os atributos voltam a 1 e os pontos gastos voltam para redistribuir.
    /// Devolve quantos pontos voltaram.
    public int ResetAttributes()
    {
        int refund = 0;
        for (int i = 0; i < 6; i++) refund += SpentOn(Get(i));
        strength = agility = vitality = intelligence = dexterity = luck = 1;
        if (DevCheats.MaxedAttributes)
        {
            // Os 99 vieram da janela DEV, não de pontos: volta só o que o nível dá.
            DevCheats.MaxedAttributes = false;
            int before = statPoints;
            RecountPoints();
            refund = statPoints - before;
        }
        else statPoints += refund;
        Changed?.Invoke(false);
        return refund;
    }

    /// DEV: todos os atributos em 99, sem gastar pontos. Enche HP e SP.
    public void DevMaxAttributes()
    {
        DevCheats.MaxedAttributes = true;
        strength = agility = vitality = intelligence = dexterity = luck = 99;
        RecountPoints();
        Changed?.Invoke(true);
    }

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

    /// XP base. Sobe no máximo 1 nível por vez: o que passar do nível atual é perdido e o
    /// próximo nível começa do zero (a XP de cada nível tem de ser juntada matando monstros).
    public void GainExp(int amount)
    {
        if (level >= ExpTable.MaxLevel || amount <= 0) return;
        exp += amount;
        bool leveled = exp >= ExpToNext;
        if (leveled)
        {
            statPoints += PointsForLevelUp(level);
            level++;
            exp = 0;
        }
        Changed?.Invoke(leveled);
    }

    /// XP de skill: mesma regra (1 nível por vez, excedente perdido). Cada nível de skill dá
    /// 1 ponto de habilidade (SkillUser).
    public void GainJobExp(int amount)
    {
        if (jobLevel >= ExpTable.MaxLevel || amount <= 0) return;
        jobExp += amount;
        bool leveled = jobExp >= JobExpToNext;
        if (leveled)
        {
            jobLevel++;
            jobExp = 0;
            JobLeveled?.Invoke(1);
        }
        Changed?.Invoke(false);
    }

    // Bônus do equipamento: não são salvos, o Equipment recalcula ao mudar.
    private readonly int[] _eqAttrs = new int[6];
    private int _eqAtk, _eqDef, _eqHp;

    public int Bonus(int i) => _eqAttrs[i];
    /// Bônus de dano do equipamento em % (Martelo de Guerra do GM: +1000%).
    public int DamageBonusPercent { get; private set; }
    /// Multiplicador do dano causado: 1 + bônus/100, vezes o multiplicador de teste da
    /// janela DEV (só no personagem).
    public float DamageMultiplier => (1f + DamageBonusPercent / 100f) * (IsMonster ? 1 : DevCheats.DamageRate);
    /// Atributo final = base distribuída + equipamento.
    public int Total(int i) => Get(i) + _eqAttrs[i];

    private int Str => Total(0);
    private int Agi => Total(1);
    private int Vit => Total(2);
    private int Int => Total(3);
    private int Dex => Total(4);
    private int Luk => Total(5);

    // ── Monstro: valores fixos do banco de monstros do Ragnarok ──
    public bool IsMonster { get; private set; }
    /// Porcentagem do dano que recebe (MVPs do bRO: 10%).
    public int DamageTakenPercent { get; private set; } = 100;
    private int _monHp, _monAtkMin, _monAtkMax, _monDef, _monMdef;

    /// Perfil de monstro (ex.: Lobo: nível 25, 919 HP, ATQ 37–46). Cura por completo.
    public void SetMonster(int lvl, int hp, int atkMin, int atkMax, int def, int mdef,
                           int str, int agi, int vit, int intl, int dex, int luk, int damageTaken = 100)
    {
        IsMonster = true;
        DamageTakenPercent = damageTaken;
        level = lvl;
        _monHp = hp; _monAtkMin = atkMin; _monAtkMax = atkMax; _monDef = def; _monMdef = mdef;
        strength = str; agility = agi; vitality = vit; intelligence = intl; dexterity = dex; luck = luk;
        Changed?.Invoke(true);
    }

    // ── Personagem (Espadachim pré-Renewal) ──
    // HP base = 35 + nível × 5 + Σ(i = 2..nível) arred(0,7 × i); HP = base × (1 + VIT/100).
    // SP base = 10 + nível × 2; SP = base × (1 + INT/100).
    private const float HpJobA = 0.7f;
    private const int HpJobB = 5, SpJob = 2;

    public int MaxHp
    {
        get
        {
            if (IsMonster) return _monHp;
            int b = 35 + level * HpJobB;
            for (int i = 2; i <= level; i++) b += Mathf.RoundToInt(HpJobA * i);
            return b * (100 + Vit) / 100 + _eqHp;
        }
    }

    public int MaxSp => IsMonster ? 0 : (10 + level * SpJob) * (100 + Int) / 100;

    /// ATQ de status: FOR + (FOR/10)² + DES/5 + SOR/5.
    public int StatusAtk => Str + (Str / 10) * (Str / 10) + Dex / 5 + Luk / 5;
    /// ATQ da arma (equipamento).
    public int WeaponAtk => weaponAtk + _eqAtk;
    /// ATQ total mostrado nas janelas (monstro: o máximo da faixa).
    public int Atk => IsMonster ? _monAtkMax : StatusAtk + WeaponAtk + AtkBuff;
    /// ATQ mínimo (monstro: o começo da faixa).
    public int AtkMin => IsMonster ? _monAtkMin : Atk;

    /// ATQ mágico: mínimo INT + (INT/7)², máximo INT + (INT/5)².
    public int MatkMin => Int + (Int / 7) * (Int / 7);
    public int MatkMax => Int + (Int / 5) * (Int / 5);

    /// DEF do equipamento (dura): tira essa porcentagem do dano (máximo 99%).
    public int HardDef => IsMonster ? _monDef : Mathf.Min(99, _eqDef);
    /// DEF da VIT (macia): subtraída do dano depois da porcentagem.
    public int SoftDef => IsMonster ? Vit : Vit / 2;
    public int Def => HardDef + SoftDef;
    /// Quanto do dano passa pela DEF dura. Até 99: tira essa %. Acima (perfis Renewal, ex.:
    /// Drake 279), usa a conta do Renewal: (4000 + DEF) / (4000 + DEF × 10).
    public float HardDefFactor => HardDef <= 99 ? (100 - HardDef) / 100f : (4000f + HardDef) / (4000f + HardDef * 10f);
    public int Mdef => IsMonster ? _monMdef : Int;

    /// Precisão = nível + DES; esquiva = nível + AGI. Chance de acertar: 80 + PRECISÃO − ESQUIVA.
    public int Hit => level + Dex + (Time.time < _hitBuffEnd ? _hitBuff : 0);

    // Precisão extra de comida (pudim): valor e até quando vale.
    private int _hitBuff;
    private float _hitBuffEnd;

    /// Precisão extra por `seconds` segundos (um bônus novo substitui o anterior).
    public string HitBuffItem { get; private set; }
    public void AddHitBuff(int amount, float seconds, string item = null)
    {
        if (!string.IsNullOrEmpty(item)) HitBuffItem = item;
        _hitBuff = amount;
        _hitBuffEnd = Time.time + seconds;
        Changed?.Invoke(false);
    }

    /// Bônus de precisão ativo e segundos restantes (para o jogo salvo e a janela).
    public int HitBuff => Time.time < _hitBuffEnd ? _hitBuff : 0;
    public float HitBuffLeft => Mathf.Max(0f, _hitBuffEnd - Time.time);

    // ASPD extra de comida (bife): valor e até quando vale.
    private int _aspdBuff;
    private float _aspdBuffEnd;

    public string AspdBuffItem { get; private set; }
    public void AddAspdBuff(int amount, float seconds, string item = null)
    {
        if (!string.IsNullOrEmpty(item)) AspdBuffItem = item;
        _aspdBuff = amount;
        _aspdBuffEnd = Time.time + seconds;
        Changed?.Invoke(false);
    }

    public int AspdBuff => Time.time < _aspdBuffEnd ? _aspdBuff : 0;
    public float AspdBuffLeft => Mathf.Max(0f, _aspdBuffEnd - Time.time);

    // ATQ extra de comida (hambúrguer): valor e até quando vale.
    private int _atkBuff;
    private float _atkBuffEnd;

    public string AtkBuffItem { get; private set; }
    public void AddAtkBuff(int amount, float seconds, string item = null)
    {
        if (!string.IsNullOrEmpty(item)) AtkBuffItem = item;
        _atkBuff = amount;
        _atkBuffEnd = Time.time + seconds;
        Changed?.Invoke(false);
    }

    public int AtkBuff => !IsMonster && Time.time < _atkBuffEnd ? _atkBuff : 0;
    public float AtkBuffLeft => Mathf.Max(0f, _atkBuffEnd - Time.time);

    // Impacto Explosivo: golpes normais com +20% de dano de fogo por 10 s.
    private float _fireEnd;
    public void AddFireBonus(float seconds) => _fireEnd = Time.time + seconds;
    public bool FireBonus => Time.time < _fireEnd;
    public float FireBonusLeft => Mathf.Max(0f, _fireEnd - Time.time);
    public int Flee => level + Agi;
    /// Esquiva perfeita (%): desvia de qualquer golpe normal; 1 + SOR/10.
    public float PerfectDodge => IsMonster ? 0f : 1f + Luk / 10;
    /// Crítico (%): 1 + SOR × 0,3. A SOR do alvo tira SOR × 0,2.
    public float CritChance => IsMonster ? 0f : 1f + Luk * 0.3f;
    public float CritShield => Luk * 0.2f;

    /// Dano de um golpe normal antes da defesa do alvo.
    public int RollDamage(bool critical)
    {
        if (IsMonster) return critical ? _monAtkMax : Random.Range(_monAtkMin, _monAtkMax + 1);
        // A arma varia entre um mínimo (DES, até o ATQ da arma) e o ATQ da arma; o crítico
        // sempre sai com o máximo.
        int w = WeaponAtk;
        int wMin = Mathf.Min(Dex, w);
        return StatusAtk + AtkBuff + (critical ? w : Random.Range(wMin, w + 1));
    }

    /// DEF macia sorteada a cada golpe recebido.
    public int RollSoftDef()
    {
        if (IsMonster)
        {
            int extra = (Vit / 20) * (Vit / 20) - 1;
            return Vit + (extra > 0 ? Random.Range(0, extra + 1) : 0);
        }
        int lo = Vit * 3 / 10, hi = Mathf.Max(lo, Vit * Vit / 150 - 1);
        return Vit / 2 + Random.Range(lo, hi + 1);
    }

    // ── Regeneração natural (personagem) ──
    /// HP recuperado a cada 6 s: máx(1, HP/200) + VIT/5.
    public int HpRegen => Mathf.Max(1, MaxHp / 200) + Vit / 5;
    /// SP recuperado a cada 8 s: 1 + SP/100 + INT/6.
    public int SpRegen => 1 + MaxSp / 100 + Int / 6;
    // ── Velocidade de ataque (ASPD), fórmula do Ragnarok pré-Renewal ──
    // amotion = base − base × (4×AGI + DES) / 1000, em milissegundos; ASPD = 200 − amotion/10.
    // Entre um ataque e outro passam 2 × amotion (ASPD 150 = 1 s, 190 = 0,2 s; 190 é o máximo).
    // A base depende do tipo de arma: tabela do Espadachim (rAthena db/pre-re/job_aspd.yml).

    /// Tipo da arma na mão (o Equipment informa); sem arma = mãos vazias.
    public WeaponType Weapon { get; private set; } = WeaponType.Fist;

    public static int BaseAmotion(WeaponType w) => w switch
    {
        WeaponType.Dagger => 500,
        WeaponType.Sword1H => 550,
        WeaponType.Sword2H => 600,
        WeaponType.Spear1H => 650,
        WeaponType.Spear2H => 700,
        WeaponType.Axe1H => 700,
        WeaponType.Axe2H => 750,
        WeaponType.Mace => 650,
        WeaponType.Mace2H => 700,
        WeaponType.Crossbow => 700,
        WeaponType.Staff => 650,
        WeaponType.Book => 600,
        WeaponType.Instrument => 600,
        WeaponType.Whip => 600,
        WeaponType.Knuckle => 450,
        WeaponType.Katar => 500,
        WeaponType.Huuma => 650,
        _ => 400,   // mãos vazias
    };

    /// Duração do golpe em milissegundos (no mínimo 100: ASPD 190).
    public int Amotion
    {
        get
        {
            int b = BaseAmotion(Weapon);
            // ASPD extra de comida: cada ponto de ASPD tira 10 ms do golpe (ASPD = 200 − amotion/10).
            return Mathf.Max(100, b - b * (4 * Agi + Dex) / 1000 - AspdBuff * 10);
        }
    }

    public int Aspd => 200 - Amotion / 10;
    /// Segundos entre um ataque básico e o próximo.
    public float AttackInterval => Amotion * 2 / 1000f;
    /// Segundos em que o personagem fica preso no golpe (depois já pode andar).
    public float AttackMotion => Amotion / 1000f;
    public float AttacksPerSecond => 1f / AttackInterval;

    /// Nível, XP, pontos e atributos do jogo salvo.
    public void Restore(int savedLevel, int savedExp, int points, int[] attrs)
    {
        level = Mathf.Max(1, savedLevel);
        exp = savedExp;
        statPoints = points;
        if (attrs != null && attrs.Length == 6)
        {
            strength = attrs[0]; agility = attrs[1]; vitality = attrs[2];
            intelligence = attrs[3]; dexterity = attrs[4]; luck = attrs[5];
        }
        // Os pontos livres saem do nível e dos atributos (regra do Ragnarok), não do número salvo.
        RecountPoints();
        Changed?.Invoke(false);
    }

    public void SetEquipBonus(int[] attrs, int atk, int def, int hp, WeaponType weapon = WeaponType.Fist, int damagePct = 0)
    {
        Weapon = weapon;
        DamageBonusPercent = damagePct;
        for (int i = 0; i < 6; i++) _eqAttrs[i] = attrs[i];
        _eqAtk = atk; _eqDef = def; _eqHp = hp;
        Changed?.Invoke(false);
    }
}
