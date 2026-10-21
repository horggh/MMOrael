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
    [Tooltip("Boss: não é empurrado nem atordoado pelos golpes.")]
    public bool immovable;

    [SerializeField] private int _hp;   // serializado: sobrevive a recompilar no meio do Play
    public int Hp { get => _hp; private set => _hp = value; }
    public bool IsDead => Hp <= 0;
    /// Os controladores sobrescrevem a velocidade a cada quadro; enquanto atordoado,
    /// eles param de fazer isso para o empurrão do golpe ter efeito.
    public bool Stunned => Time.time < _stunEnd;

    public event Action<Health> Died;

    /// Todos os que têm vida na cena. Lista fixa em vez de procurar na cena a cada quadro:
    /// a busca cria um vetor novo a cada chamada, e o lixo de memória causava engasgos.
    public static readonly System.Collections.Generic.List<Health> All = new System.Collections.Generic.List<Health>();
    private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    private void OnDisable() => All.Remove(this);

    private SpriteRenderer _sprite;
    private Rigidbody2D _body;
    private Color _baseColor = Color.white;
    private float _invulnerableEnd;
    private float _stunEnd;

    private void Awake()
    {
        _sprite = GetComponentInChildren<SpriteRenderer>();
        _body = GetComponent<Rigidbody2D>();
        // A física roda a 50 passos por segundo e a tela a 60 ou mais: sem interpolar, o
        // personagem e os monstros andam aos trancos (alguns quadros repetem a posição).
        if (_body != null) _body.interpolation = RigidbodyInterpolation2D.Interpolate;
        if (_sprite != null) _baseColor = _sprite.color;
        var stats = GetComponent<CharacterStats>();
        if (stats != null)
        {
            maxHp = stats.MaxHp;
            stats.Changed += leveled =>
            {
                int gain = stats.MaxHp - maxHp;
                maxHp = stats.MaxHp;
                // Subir de nível cura tudo; ganhar VIT soma só a diferença. Perder vida máxima
                // (reset de atributos, tirar equipamento) nunca zera a vida: zerar sem o evento
                // de morte deixaria o personagem travado, "morto" sem renascer.
                if (IsDead) return;
                Hp = leveled ? maxHp : Mathf.Clamp(Hp + gain, 1, maxHp);
            };
        }
        Hp = maxHp;
    }

    /// Vida do jogo salvo (pelo menos 1: salvar morto não deixa nascer morto).
    public void Restore(int hp) => Hp = Mathf.Clamp(hp, 1, maxHp);

    /// Prende no lugar por `seconds` (não funciona em chefes: immovable).
    public void Hold(float seconds)
    {
        if (!immovable) _stunEnd = Mathf.Max(_stunEnd, Time.time + seconds);
    }

    /// Gasta vida como custo de habilidade (sem empurrão nem piscar). Não deixa morrer: com
    /// vida igual ou menor que o custo, não paga.
    public bool PayHp(int amount)
    {
        if (IsDead || Hp <= amount) return false;
        if (!(DevCheats.InfiniteHp && GetComponent<PlayerController2D>() != null)) Hp -= amount;
        return true;
    }

    public bool TakeDamage(int amount, Vector2 from, float knockback = 3f)
    {
        if (IsDead || Time.time < _invulnerableEnd) return false;

        Hp = Mathf.Max(0, Hp - amount);
        // DEV: HP infinito no personagem.
        if (DevCheats.InfiniteHp && GetComponent<PlayerController2D>() != null) Hp = maxHp;
        _invulnerableEnd = Time.time + invulnerableTime;
        if (!immovable) _stunEnd = Time.time + stunTime;

        if (_body != null && !immovable)
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
