using System;
using UnityEngine;

public enum HitKind { Miss, Hit, Critical }

public struct AttackResult
{
    public HitKind kind;
    public int damage;
}

/// Regras de resolução de um ataque: crítico, acerto/erro e dano.
public static class Combat
{
    /// Disparado a cada ataque resolvido — usado para mostrar os números na tela.
    public static event Action<Vector3, AttackResult> Resolved;

    public static AttackResult Resolve(CharacterStats a, CharacterStats d)
    {
        if (UnityEngine.Random.value * 100f < a.CritChance)
        {
            return new AttackResult { kind = HitKind.Critical, damage = Mathf.RoundToInt(a.Atk * 1.5f) };
        }

        // Precisão maior ou igual à esquiva acerta; cada ponto de esquiva acima da
        // precisão tira 10% de chance. Nunca chega a zero para não virar parede.
        int gap = d.Flee - a.Hit;
        float hitChance = gap <= 0 ? 95f : Mathf.Max(5f, 95f - gap * 10f);
        if (UnityEngine.Random.value * 100f >= hitChance)
        {
            return new AttackResult { kind = HitKind.Miss, damage = 0 };
        }

        int raw = Mathf.RoundToInt(a.Atk * UnityEngine.Random.Range(0.9f, 1.1f));
        return new AttackResult { kind = HitKind.Hit, damage = Mathf.Max(1, raw - d.Def) };
    }

    public static AttackResult Apply(CharacterStats attacker, Health target, Vector2 from, float knockback)
    {
        var defender = target.GetComponent<CharacterStats>();
        var result = Resolve(attacker, defender);
        Resolved?.Invoke(target.transform.position, result);
        if (result.kind != HitKind.Miss) target.TakeDamage(result.damage, from, knockback);
        return result;
    }
}
