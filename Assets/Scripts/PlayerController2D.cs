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
    [Tooltip("Distância em que, perseguindo um alvo, o cavaleiro para e começa a bater.")]
    public float engageRange = 1.1f;

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
    private Health _target;
    private SkillUser _skills;
    private SkillDef _queuedSkill;   // espera chegar ao alcance do alvo
    private SkillDef _activeSkill;   // golpe em andamento
    private bool _strikePending;
    private SkillDef _queuedTool;      // ação de ferramenta esperando chegar perto do quadrado
    private Vector2Int _toolCell;
    private const float ToolReach = 1.4f;
    private Inventory _bag;
    private Vector2? _moveTo;
    private bool _holdMove;
    private float _lastDist = float.MaxValue;
    private int _stuck;

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _sprite = GetComponentInChildren<SpriteRenderer>();
        _health = GetComponent<Health>();
        _stats = GetComponent<CharacterStats>();
        // A cena atual foi montada antes destes componentes existirem.
        _bag = GetComponent<Inventory>();
        if (_bag == null) _bag = gameObject.AddComponent<Inventory>();
        if (GetComponent<ActionBar>() == null) gameObject.AddComponent<ActionBar>();
        if (GetComponent<Equipment>() == null) gameObject.AddComponent<Equipment>();
        if (GetComponent<ItemUI>() == null) gameObject.AddComponent<ItemUI>();
        if (GetComponent<KnightAnimator>() == null) gameObject.AddComponent<KnightAnimator>();
        if (GetComponent<CursorManager>() == null) gameObject.AddComponent<CursorManager>();
        _skills = GetComponent<SkillUser>();
        if (_skills == null) _skills = gameObject.AddComponent<SkillUser>();
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
            if (_hitTimer < 0f && _strikePending) Strike();
        }

        bool overUI = CombatHUD.PointerOverUI || ItemUI.PointerOverUI;
        Vector2 mouse = MouseWorld();
        Health hovered = overUI ? null : MonsterAt(mouse);
        CursorManager.SetSword(hovered != null);

        if (_target != null && _target.IsDead) _target = null;

        if (_attackTimer > 0f)
        {
            _attackTimer -= Time.deltaTime;
        }
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !overUI)
        {
            // Clique no monstro: trava o alvo e deixa o resto automático.
            // Clique no chão: anda até o ponto.
            _queuedTool = null;   // um clique novo substitui a ação pendente
            if (hovered != null) { _target = hovered; _moveTo = null; _holdMove = false; }
            else { _target = null; _moveTo = mouse; _holdMove = true; }
        }

        // Segurando o botão, o destino acompanha o cursor (como no Ragnarok).
        if (_holdMove)
        {
            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                if (!overUI) _moveTo = mouse;
            }
            else _holdMove = false;
        }
        else if (_queuedTool != null &&
                 Vector2.Distance(transform.position, FarmGrid.Center(_toolCell)) <= ToolReach)
        {
            DoTool(_queuedTool, _toolCell);
            _queuedTool = null;
            _moveTo = null;
        }
        else if (_target != null && DistanceTo(_target) <= engageRange)
        {
            Vector2 dir = (Vector2)_target.transform.position + Vector2.up * 0.3f - Chest;
            SkillDef skill = _queuedSkill != null && _skills.Spend(_queuedSkill) ? _queuedSkill : null;
            _queuedSkill = null;
            BeginAttack(dir, skill);
        }
    }

    /// Habilidade pedida pela barra: vai no alvo travado ou no monstro sob o mouse;
    /// sem alvo, sai na direção do mouse.
    public void UseSkill(SkillDef def)
    {
        if (Dead) return;

        if (def.kind == SkillKind.Tool)
        {
            var cell = FarmGrid.CellOf(MouseWorld());
            if (!FarmGrid.CanAct(def.id, cell, _bag)) return;
            _target = null; _queuedSkill = null; _holdMove = false;
            _queuedTool = def;
            _toolCell = cell;
            // Longe: anda até o quadrado; a ação sai ao entrar no alcance (ver Update).
            _moveTo = Vector2.Distance(transform.position, FarmGrid.Center(cell)) > ToolReach
                ? FarmGrid.Center(cell) : (Vector2?)null;
            return;
        }

        if (def.kind == SkillKind.AreaSelf)
        {
            if (_attackTimer > 0f || !_skills.Spend(def)) return;
            Explode(def);
            BeginAttack(MouseWorld() - Chest, null, strike: false);   // só a animação; o dano já saiu
            return;
        }

        Health t = _target != null ? _target : MonsterAt(MouseWorld());
        if (t != null)
        {
            _target = t;
            _moveTo = null;
            _queuedSkill = def;   // dispara ao chegar no alcance (ou já, se estiver perto)
        }
        else if (_attackTimer <= 0f && _skills.Spend(def))
        {
            BeginAttack(MouseWorld() - Chest, def);
        }
    }

    private Vector2 Chest => (Vector2)transform.position + Vector2.up * 0.45f;

    private float DistanceTo(Health h) => Vector2.Distance(transform.position, h.transform.position);

    private static Vector2 MouseWorld() =>
        Mouse.current != null ? (Vector2)Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()) : Vector2.zero;

    /// Monstro sob o mouse, pelo desenho do sprite (o colisor só cobre as patas).
    /// Havendo sobreposição, vale o que está mais à frente (mais abaixo na tela).
    private Health MonsterAt(Vector2 p)
    {
        Health best = null;
        float bestY = float.MaxValue;
        foreach (var h in FindObjectsByType<Health>(FindObjectsSortMode.None))
        {
            if (h == _health || h.IsDead) continue;
            var sr = h.GetComponentInChildren<SpriteRenderer>();
            if (sr == null || !sr.bounds.Contains(new Vector3(p.x, p.y, sr.bounds.center.z))) continue;
            if (h.transform.position.y < bestY) { bestY = h.transform.position.y; best = h; }
        }
        return best;
    }

    private void DoTool(SkillDef def, Vector2Int cell)
    {
        if (!FarmGrid.CanAct(def.id, cell, _bag) || !_skills.Spend(def)) return;
        FarmGrid.Act(def.id, cell, _bag);
        // Mesmo gesto do golpe, virado para o quadrado, sem causar dano.
        BeginAttack(FarmGrid.Center(cell) - Chest, null, strike: false);
    }

    /// Dano em todos os monstros dentro do raio, medido a partir dos pés do personagem.
    private void Explode(SkillDef def)
    {
        Vector2 center = transform.position;
        var hit = new HashSet<Health>();
        foreach (var col in Physics2D.OverlapCircleAll(center, def.radius))
        {
            var target = col.GetComponentInParent<Health>();
            if (target == null || target == _health || target.IsDead || !hit.Add(target)) continue;
            Combat.ApplyFixed(target, def.fixedDamage, center, knockback * 1.4f);
        }
        SkillFx.Ring(center, def.radius);
    }

    private void BeginAttack(Vector2 dir, SkillDef skill = null, bool strike = true)
    {
        _activeSkill = skill;
        _strikePending = strike;
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
            bool skill = _activeSkill != null;
            Combat.Apply(_stats, target, transform.position, skill ? knockback * 1.6f : knockback,
                         skill ? _activeSkill.damageMult : 1f, skill);
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

        // Qualquer movimento manual cancela o alvo.
        if (input.sqrMagnitude > 0f)
        {
            _target = null; _moveTo = null; _holdMove = false; _queuedSkill = null; _queuedTool = null;
        }
        else if (_target != null && _attackTimer <= 0f && DistanceTo(_target) > engageRange)
        {
            input = ((Vector2)_target.transform.position - (Vector2)transform.position).normalized;
        }
        else if (_target == null && _moveTo.HasValue && _attackTimer <= 0f)
        {
            Vector2 d = _moveTo.Value - (Vector2)transform.position;
            // Sem avançar por ~0,5s significa que algo bloqueia (um tronco): desiste.
            _stuck = d.magnitude > _lastDist - 0.005f ? _stuck + 1 : 0;
            _lastDist = d.magnitude;
            if (d.magnitude < 0.08f || (_stuck > 25 && !_holdMove)) { _moveTo = null; _stuck = 0; }
            else input = d.normalized;
        }

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
