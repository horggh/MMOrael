using System.Collections;
using UnityEngine;

/// Lobo: vaga perto de casa, persegue o jogador quando o vê, dá o bote quando perto.
[RequireComponent(typeof(Rigidbody2D), typeof(Health))]
public class WolfAI : MonoBehaviour
{
    [Header("Movimento")]
    public float wanderSpeed = 1.1f;
    public float chaseSpeed = 3.2f;
    public float wanderRadius = 3f;

    [Header("Percepção")]
    public float detectRadius = 5f;
    [Tooltip("Maior que a detecção, para o lobo não ficar alternando entre perseguir e desistir na borda.")]
    public float loseRadius = 8f;

    [Header("Ataque")]
    public float attackRange = 1.3f;
    public float attackCooldown = 1.4f;
    [Tooltip("Tempo entre o início do bote e o momento do dano: é a janela para o jogador desviar.")]
    public float attackWindup = 0.25f;
    public float attackDuration = 0.5f;

    [Header("Morte")]
    public float respawnDelay = 8f;
    [Range(0f, 1f)] public float potionDropChance = 0.35f;

    private Rigidbody2D _body;
    private Health _health;
    private CharacterStats _stats;
    private Animator _animator;
    private SpriteRenderer _sprite;
    private Collider2D _collider;
    private Transform _player;
    private Health _playerHealth;

    private Vector2 _home;
    private Vector2 _wanderTarget;
    private float _wanderTimer;
    private float _cooldown;
    private float _attackTimer;
    private bool _hitDone;
    private bool _chasing;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int AttackParam = Animator.StringToHash("Attack");

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _health = GetComponent<Health>();
        _stats = GetComponent<CharacterStats>();
        _animator = GetComponentInChildren<Animator>();
        _sprite = GetComponentInChildren<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
        _home = transform.position;
        _wanderTarget = _home;
        _health.Died += OnDied;
    }

    private void Start()
    {
        var player = FindFirstObjectByType<PlayerController2D>();
        if (player != null)
        {
            _player = player.transform;
            _playerHealth = player.GetComponent<Health>();
        }
    }

    private void FixedUpdate()
    {
        if (_health.IsDead || _health.Stunned) return;

        float dt = Time.fixedDeltaTime;
        _cooldown -= dt;

        if (_attackTimer > 0f)
        {
            UpdateAttack(dt);
            return;
        }

        Vector2 pos = _body.position;
        float dist = float.PositiveInfinity;
        if (_player != null && (_playerHealth == null || !_playerHealth.IsDead))
        {
            dist = Vector2.Distance(pos, _player.position);
        }

        if (!_chasing && dist < detectRadius) _chasing = true;
        else if (_chasing && dist > loseRadius) _chasing = false;

        Vector2 velocity = _chasing ? Chase(pos, dist) : Wander(pos, dt);

        _body.linearVelocity = velocity;
        // A arte do lobo olha para a direita.
        if (Mathf.Abs(velocity.x) > 0.05f) _sprite.flipX = velocity.x < 0f;
        if (_animator != null) _animator.SetFloat(SpeedParam, velocity.magnitude, 0.08f, dt);
    }

    private Vector2 Chase(Vector2 pos, float dist)
    {
        if (dist <= attackRange)
        {
            if (_cooldown <= 0f) BeginAttack();
            return Vector2.zero;
        }
        return ((Vector2)_player.position - pos).normalized * chaseSpeed;
    }

    private Vector2 Wander(Vector2 pos, float dt)
    {
        _wanderTimer -= dt;
        if (_wanderTimer <= 0f)
        {
            // Metade das vezes fica parado farejando: um lobo que nunca para parece robô.
            _wanderTarget = Random.value < 0.5f ? pos : _home + Random.insideUnitCircle * wanderRadius;
            _wanderTimer = Random.Range(1.5f, 4f);
        }

        Vector2 toTarget = _wanderTarget - pos;
        return toTarget.magnitude < 0.2f ? Vector2.zero : toTarget.normalized * wanderSpeed;
    }

    private void BeginAttack()
    {
        _attackTimer = attackDuration;
        _hitDone = false;
        _cooldown = attackCooldown / (_stats != null ? _stats.AttackSpeed : 1f);
        _body.linearVelocity = Vector2.zero;
        _sprite.flipX = _player.position.x < transform.position.x;
        if (_animator != null)
        {
            _animator.SetFloat(SpeedParam, 0f);
            _animator.SetTrigger(AttackParam);
        }
    }

    private void UpdateAttack(float dt)
    {
        _attackTimer -= dt;
        _body.linearVelocity = Vector2.zero;

        bool strikeMoment = _attackTimer <= attackDuration - attackWindup;
        if (!_hitDone && strikeMoment)
        {
            _hitDone = true;
            // Confere a distância de novo: se o jogador recuou durante a preparação, escapa.
            if (_playerHealth != null &&
                Vector2.Distance(_body.position, _player.position) <= attackRange + 0.3f)
            {
                Combat.Apply(_stats, _playerHealth, _body.position, 4f);
            }
        }
    }

    private void OnDied(Health h)
    {
        var playerStats = _player != null ? _player.GetComponent<CharacterStats>() : null;
        if (playerStats != null && _stats != null) playerStats.GainExp(_stats.expReward);

        var bag = _player != null ? _player.GetComponent<Inventory>() : null;
        if (bag != null && Random.value < potionDropChance) bag.Add("pocao_vermelha", 1);
        StartCoroutine(DieAndRespawn());
    }

    private IEnumerator DieAndRespawn()
    {
        _body.linearVelocity = Vector2.zero;
        _body.simulated = false;
        if (_collider != null) _collider.enabled = false;
        _chasing = false;
        _attackTimer = 0f;

        yield return Fade(1f, 0f, 0.6f);
        yield return new WaitForSeconds(respawnDelay);

        transform.position = _home;
        _health.Restore();
        yield return Fade(0f, 1f, 0.6f);

        _body.simulated = true;
        if (_collider != null) _collider.enabled = true;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            var c = _sprite.color;
            c.a = Mathf.Lerp(from, to, t / duration);
            _sprite.color = c;
            yield return null;
        }
        var end = _sprite.color;
        end.a = to;
        _sprite.color = end;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
