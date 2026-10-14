using System;
using System.Collections;
using UnityEngine;

/// Vida compartilhada por jogador e monstros.
public class Health : MonoBehaviour
{
    public int maxHp = 30;
    [Tooltip("Janela após um golpe em que novos golpes são ignorados.")]
    public float invulnerableTime = 0.25f;
    [Tooltip("Tempo em que o empurrão do golpe tem prioridade sobre o movimento.")]
    public float stunTime = 0.15f;

    public int Hp { get; private set; }
    public bool IsDead => Hp <= 0;
    /// Os controladores sobrescrevem a velocidade a cada quadro; enquanto atordoado,
    /// eles param de fazer isso para o empurrão do golpe ter efeito.
    public bool Stunned => Time.time < _stunEnd;

    public event Action<Health> Died;

    private SpriteRenderer _sprite;
    private Rigidbody2D _body;
    private Color _baseColor = Color.white;
    private float _invulnerableEnd;
    private float _stunEnd;

    private void Awake()
    {
        _sprite = GetComponentInChildren<SpriteRenderer>();
        _body = GetComponent<Rigidbody2D>();
        if (_sprite != null) _baseColor = _sprite.color;
        var stats = GetComponent<CharacterStats>();
        if (stats != null)
        {
            maxHp = stats.MaxHp;
            stats.Changed += leveled =>
            {
                int gain = stats.MaxHp - maxHp;
                maxHp = stats.MaxHp;
                // Subir de nível cura tudo; ganhar VIT soma só a diferença.
                Hp = leveled ? maxHp : Mathf.Clamp(Hp + gain, 0, maxHp);
            };
        }
        Hp = maxHp;
    }

    public bool TakeDamage(int amount, Vector2 from, float knockback = 3f)
    {
        if (IsDead || Time.time < _invulnerableEnd) return false;

        Hp = Mathf.Max(0, Hp - amount);
        _invulnerableEnd = Time.time + invulnerableTime;
        _stunEnd = Time.time + stunTime;

        if (_body != null)
        {
            Vector2 dir = ((Vector2)transform.position - from).normalized;
            _body.linearVelocity = dir * knockback;
        }

        if (_sprite != null) StartCoroutine(Flash());
        if (IsDead) Died?.Invoke(this);
        return true;
    }

    /// Retorna quanto foi curado de fato.
    public int Heal(int amount)
    {
        if (IsDead) return 0;
        int before = Hp;
        Hp = Mathf.Min(maxHp, Hp + amount);
        return Hp - before;
    }

    public void Restore()
    {
        Hp = maxHp;
        if (_sprite != null) _sprite.color = _baseColor;
    }

    private IEnumerator Flash()
    {
        _sprite.color = new Color(1f, 0.35f, 0.35f, _sprite.color.a);
        yield return new WaitForSeconds(0.1f);
        if (!IsDead) _sprite.color = _baseColor;
    }
}
