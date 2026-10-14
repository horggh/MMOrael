using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Movimento em vista de cima: WASD anda, Shift corre, botão esquerdo ataca na direção do mouse.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Velocidade (unidades/s)")]
    public float walkSpeed = 2.6f;
    public float runSpeed = 5.2f;

    [Header("Ataque")]
    [Tooltip("Tempo em que o personagem fica preso no golpe, sem andar.")]
    public float attackDuration = 0.45f;
    [Tooltip("Momento do golpe em que a espada conecta.")]
    public float hitDelay = 0.15f;
    public float hitRadius = 0.75f;
    public float hitReach = 0.65f;
    public float knockback = 4.5f;

    [Header("Morte")]
    public float respawnDelay = 2f;

    // Estado lido pelo KnightAnimator.
    public Vector2 Velocity => _body.linearVelocity;
    public bool Running { get; private set; }
    public bool Attacking => _attackTimer > 0f;
    public float AttackProgress => _attackLength > 0f ? 1f - _attackTimer / _attackLength : 0f;
    public Vector2 AttackDir { get; private set; } = Vector2.right;

    private Rigidbody2D _body;
    private SpriteRenderer _sprite;
    private Health _health;
    private CharacterStats _stats;
    private Vector2 _spawn;
    private float _attackTimer, _attackLength;
    private float _hitTimer = -1f;

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _sprite = GetComponentInChildren<SpriteRenderer>();
        _health = GetComponent<Health>();
        _stats = GetComponent<CharacterStats>();
        // A cena atual foi montada antes destes componentes existirem.
        if (GetComponent<Inventory>() == null) gameObject.AddComponent<Inventory>();
        if (GetComponent<ActionBar>() == null) gameObject.AddComponent<ActionBar>();
        if (GetComponent<ItemUI>() == null) gameObject.AddComponent<ItemUI>();
        if (GetComponent<KnightAnimator>() == null) gameObject.AddComponent<KnightAnimator>();
        _spawn = transform.position;
        if (_health != null) _health.Died += _ => StartCoroutine(Respawn());
    }

    private bool Dead => _health != null && _health.IsDead;

    private void Update()
    {
        if (Dead) return;

        if (_hitTimer >= 0f)
        {
            _hitTimer -= Time.deltaTime;
            if (_hitTimer < 0f) Strike();
        }

        if (_attackTimer > 0f)
        {
            _attackTimer -= Time.deltaTime;
        }
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
                 && !CombatHUD.PointerOverUI && !ItemUI.PointerOverUI)
        {
            BeginAttack();
        }
    }

    private void BeginAttack()
    {
        // Mira: do centro do corpo até o ponto do mouse no mundo.
        Vector3 m = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 dir = (Vector2)m - ((Vector2)transform.position + Vector2.up * 0.45f);
        AttackDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : AttackDir;

        // Agilidade acelera o golpe inteiro: animação, momento do impacto e recuperação.
        float speed = _stats != null ? _stats.AttackSpeed : 1f;
        _attackLength = _attackTimer = attackDuration / speed;
        _hitTimer = hitDelay / speed;
        _body.linearVelocity = Vector2.zero;
    }

    /// Acerta tudo que tem vida num círculo na direção do golpe.
    private void Strike()
    {
        Vector2 center = (Vector2)transform.position + Vector2.up * 0.45f + AttackDir * hitReach;
        var hit = new HashSet<Health>();
        foreach (var col in Physics2D.OverlapCircleAll(center, hitRadius))
        {
            var target = col.GetComponentInParent<Health>();
            if (target == null || target == _health || !hit.Add(target)) continue;
            Combat.Apply(_stats, target, transform.position, knockback);
        }
    }

    private void FixedUpdate()
    {
        if (Dead || (_health != null && _health.Stunned)) return;

        var keyboard = Keyboard.current;
        Vector2 input = Vector2.zero;

        if (keyboard != null && _attackTimer <= 0f)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
            input = input.normalized;
        }

        Running = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        _body.linearVelocity = input * (Running ? runSpeed : walkSpeed);
    }

    private IEnumerator Respawn()
    {
        _body.linearVelocity = Vector2.zero;
        if (_sprite != null) _sprite.color = new Color(1f, 1f, 1f, 0.35f);

        yield return new WaitForSeconds(respawnDelay);

        transform.position = _spawn;
        _health.Restore();
        if (_sprite != null) _sprite.color = Color.white;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.45f + (Vector3)AttackDir * hitReach, hitRadius);
    }
}
