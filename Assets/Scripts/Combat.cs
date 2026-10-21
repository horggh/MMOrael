using System;
using UnityEngine;

public enum HitKind { Miss, Hit, Critical }

public struct AttackResult
{
    public HitKind kind;
    public int damage;
    public bool skill;
}

/// Regras de resolução de um ataque: crítico, acerto/erro e dano.
public static class Combat
{
    /// Disparado a cada ataque resolvido — usado para mostrar os números na tela.
    public static event Action<Vector3, AttackResult> Resolved;

    /// Golpe físico com as regras do Ragnarok pré-Renewal:
    /// 1. esquiva perfeita do alvo (1 + SOR/10 %): desvia de golpe normal;
    /// 2. crítico (1 + SOR × 0,3 − SOR do alvo × 0,2 %): sempre acerta, dano máximo, ignora DEF;
    /// 3. acerto: 80 + PRECISÃO − ESQUIVA, entre 5% e 100%;
    /// 4. dano × multiplicador da habilidade, menos a DEF dura (%) e depois a DEF macia (VIT).
    /// Habilidades não erram nem dão crítico.
    public static AttackResult Resolve(CharacterStats a, CharacterStats d, float mult = 1f, bool skill = false)
    {
        if (!skill)
        {
            if (d != null && UnityEngine.Random.value * 100f < d.PerfectDodge)
                return new AttackResult { kind = HitKind.Miss, damage = 0 };

            float crit = a.CritChance - (d != null ? d.CritShield : 0f);
            if (crit > 0f && UnityEngine.Random.value * 100f < crit)
                return new AttackResult { kind = HitKind.Critical, damage = Taken(d, a.RollDamage(true) * mult * a.DamageMultiplier) };

            float hitChance = Mathf.Clamp(80f + a.Hit - (d != null ? d.Flee : 0), 5f, 100f);
            if (UnityEngine.Random.value * 100f >= hitChance)
                return new AttackResult { kind = HitKind.Miss, damage = 0 };
        }

        // Bônus de dano do equipamento entra antes da defesa (como as cartas no Ragnarok).
        float dmg = a.RollDamage(false) * mult * a.DamageMultiplier;
        if (d != null)
        {
            dmg *= d.HardDefFactor;
            dmg -= d.RollSoftDef();
        }
        return new AttackResult { kind = HitKind.Hit, damage = Taken(d, dmg) };
    }

    /// Aplica a redução de dano recebido do alvo (MVP do bRO recebe 10%); no mínimo 1.
    private static int Taken(CharacterStats d, float dmg)
    {
        if (d != null) dmg = dmg * d.DamageTakenPercent / 100f;
        return Mathf.Max(1, Mathf.RoundToInt(dmg));
    }

    /// Dano fixo de habilidade: sempre acerta e ignora defesa.
    public static void ApplyFixed(Health target, int damage, Vector2 from, float knockback, CharacterStats attacker = null)
    {
        if (attacker != null) damage = Mathf.RoundToInt(damage * attacker.DamageMultiplier);
        damage = Taken(target.GetComponent<CharacterStats>(), damage);
        var result = new AttackResult { kind = HitKind.Hit, damage = damage, skill = true };
        Resolved?.Invoke(target.transform.position, result);
        target.TakeDamage(damage, from, knockback);
    }

    /// Golpe de arma de habilidade que pode errar: chance de acerto (80 + PRECISÃO − ESQUIVA)
    /// aumentada em hitBonusPercent %, sem crítico. Ex.: Impacto Explosivo (+10% por nível).
    public static AttackResult ApplySkillHit(CharacterStats attacker, Health target, Vector2 from, float knockback,
                                             float mult, float hitBonusPercent)
    {
        var defender = target.GetComponent<CharacterStats>();
        float chance = (80f + attacker.Hit - (defender != null ? defender.Flee : 0)) * (1f + hitBonusPercent / 100f);
        if (UnityEngine.Random.value * 100f >= Mathf.Clamp(chance, 5f, 100f))
        {
            var miss = new AttackResult { kind = HitKind.Miss, damage = 0, skill = true };
            Resolved?.Invoke(target.transform.position, miss);
            return miss;
        }
        var result = Resolve(attacker, defender, mult, true);
        result.skill = true;
        Resolved?.Invoke(target.transform.position, result);
        target.TakeDamage(result.damage, from, knockback);
        return result;
    }

    /// Golpe de habilidade com dano calculado pela própria habilidade, que ignora a DEF: pode
    /// errar (80 + PRECISÃO − ESQUIVA), vale o bônus de dano do equipamento e a redução do MVP.
    public static AttackResult ApplyIgnoreDef(CharacterStats attacker, Health target, Vector2 from, float knockback, float damage)
    {
        var defender = target.GetComponent<CharacterStats>();
        float chance = 80f + attacker.Hit - (defender != null ? defender.Flee : 0);
        if (UnityEngine.Random.value * 100f >= Mathf.Clamp(chance, 5f, 100f))
        {
            var miss = new AttackResult { kind = HitKind.Miss, damage = 0, skill = true };
            Resolved?.Invoke(target.transform.position, miss);
            return miss;
        }
        var result = new AttackResult { kind = HitKind.Hit, damage = Taken(defender, damage * attacker.DamageMultiplier), skill = true };
        Resolved?.Invoke(target.transform.position, result);
        target.TakeDamage(result.damage, from, knockback);
        return result;
    }

    public static AttackResult Apply(CharacterStats attacker, Health target, Vector2 from, float knockback,
                                     float mult = 1f, bool skill = false)
    {
        var defender = target.GetComponent<CharacterStats>();
        if (!skill && attacker != null && attacker.FireBonus) mult *= 1.2f;
        var result = Resolve(attacker, defender, mult, skill);
        result.skill = skill;
        Resolved?.Invoke(target.transform.position, result);
        if (result.kind != HitKind.Miss) target.TakeDamage(result.damage, from, knockback);
        return result;
    }
}
