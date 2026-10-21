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
    [Tooltip("Empurrão base das skills (o golpe básico não empurra).")]
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
    [SerializeField] private Vector2 _spawn;
    [SerializeField] private Vector2 _sceneSpawn;   // início do mapa, quando não há portal ativo

    /// Vai direto para um ponto (pedra de retorno): para de andar e de atacar.
    public void Teleport(Vector2 p)
    {
        _moveTo = null; _target = null; _holdMove = false; _queuedSkill = null; _queuedTool = null; _aimSkill = null;
        transform.position = new Vector3(p.x, p.y, transform.position.z);
        if (_body != null) { _body.position = p; _body.linearVelocity = Vector2.zero; }
        var cam = FindAnyObjectByType<CameraFollow2D>();
        if (cam != null) cam.Snap();
    }

    /// Onde renasce ao morrer (o portal ativo chama isto).
    public void SetRespawn(Vector2 p) => _spawn = p;
    public void ResetRespawn() => _spawn = _sceneSpawn;
    private float _attackTimer, _attackLength;
    private float _hitTimer = -1f;
    private float _nextAttackAt;   // ASPD: antes disto o próximo golpe básico não sai
    private Health _target;
    private SkillUser _skills;
    private SkillDef _queuedSkill;   // espera chegar ao alcance do alvo
    private SkillDef _activeSkill;   // golpe em andamento
    private SkillDef _aimSkill;      // mirando: o cursor vira círculo e o clique escolhe o monstro

    /// Habilidade esperando o clique no monstro (cursor de seleção).
    public SkillDef AimingSkill => _aimSkill;
    private bool _strikePending;
    private SkillDef _queuedTool;      // ação de ferramenta esperando chegar perto do quadrado
    private Vector2Int _toolCell;
    private const float ToolReach = 1.4f;
    private Inventory _bag;
    private Vector2? _moveTo;
    private bool _holdMove;
    private float _lastDist = float.MaxValue;
    private int _stuck;
    private float _chaseDist = float.MaxValue;
    private int _chaseStuck;
    private int _whiffs;   // golpes seguidos que não acertaram nada

    /// O personagem da cena (o primeiro a acordar). Cópias (ex.: o mapa copiado junto com ele)
    /// são desligadas: senão as duas andam juntas, cada uma com a barra de vida.
    public static PlayerController2D Main { get; private set; }

    private void Awake()
    {
        if (Main != null && Main != this && Main.gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"Há mais de um personagem na cena: \"{name}\" foi desligado. Apague a cópia na Hierarchy.", this);
            gameObject.SetActive(false);
            return;
        }
        Main = this;
        _body = GetComponent<Rigidbody2D>();
        _sprite = GetComponentInChildren<SpriteRenderer>();
        _health = GetComponent<Health>();
        _stats = GetComponent<CharacterStats>();
        // Jogo novo: pontos livres pela regra do Ragnarok (o jogo salvo refaz a conta ao carregar).
        if (_stats != null) _stats.RecountPoints();
        // A cena atual foi montada antes destes componentes existirem.
        _bag = GetComponent<Inventory>();
        if (_bag == null) _bag = gameObject.AddComponent<Inventory>();
        if (GetComponent<ActionBar>() == null) gameObject.AddComponent<ActionBar>();
        if (GetComponent<Equipment>() == null) gameObject.AddComponent<Equipment>();
        if (GetComponent<ItemUI>() == null) gameObject.AddComponent<ItemUI>();
        if (GetComponent<KnightAnimator>() == null) gameObject.AddComponent<KnightAnimator>();
        if (GetComponent<CursorManager>() == null) gameObject.AddComponent<CursorManager>();
        if (GetComponent<NaturalRegen>() == null) gameObject.AddComponent<NaturalRegen>();
        _skills = GetComponent<SkillUser>();
        if (_skills == null) _skills = gameObject.AddComponent<SkillUser>();
        _spawn = transform.position;
        _sceneSpawn = _spawn;
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

        // Posicionando um móvel, o clique é do posicionamento (ver Placement).
        bool overUI = CombatHUD.PointerOverUI || ItemUI.PointerOverUI || Building.PointerOverUI || MonsterSheet.PointerOverUI || Placement.BlocksInput;
        Vector2 mouse = MouseWorld();
        Health hovered = overUI ? null : MonsterAt(mouse);
        if (_aimSkill != null) CursorManager.SetTarget(hovered != null);
        else CursorManager.SetSword(hovered != null);

        if (_target != null && _target.IsDead) _target = null;

        // Esc: larga alvo, caminho e ação pendente (destrava se algo der errado).
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            _target = null; _moveTo = null; _holdMove = false; _queuedSkill = null; _queuedTool = null; _aimSkill = null;
        }
        // Botão direito desiste da mira.
        if (_aimSkill != null && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) _aimSkill = null;

        if (_attackTimer > 0f) _attackTimer -= Time.deltaTime;

        // WASD sempre larga o alvo, mesmo no meio do golpe. Antes o ataque automático
        // emendava um golpe no outro e o teclado nunca era lido: o personagem ficava preso.
        if (MoveKeysHeld())
        {
            _target = null; _moveTo = null; _holdMove = false; _queuedSkill = null; _queuedTool = null;
        }

        // O clique também vale durante o golpe: troca a ação assim que ele termina.
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !overUI)
        {
            // Clique no monstro: trava o alvo e deixa o resto automático.
            // Clique no chão: anda até o ponto.
            _queuedTool = null;   // um clique novo substitui a ação pendente
            if (_aimSkill != null)
            {
                // Mirando: clique no monstro trava o alvo com a habilidade na fila (anda até ele
                // se estiver longe); clique no chão só desiste da mira.
                if (hovered != null)
                {
                    _target = hovered; _moveTo = null; _holdMove = false;
                    _queuedSkill = _aimSkill;
                }
                _aimSkill = null;
            }
            else if (hovered != null) { _target = hovered; _moveTo = null; _holdMove = false; }
            else { LogMissedClick(mouse); _target = null; _moveTo = mouse; _holdMove = true; }
        }

        // Habilidade tem preferência sobre o golpe básico: no alcance, corta o golpe básico em
        // andamento (o dano dele não sai) e a habilidade sai na hora.
        if (_queuedSkill != null && _attackTimer > 0f && _activeSkill == null && _strikePending &&
            _target != null && DistanceTo(_target) <= Reach && _skills.CanUse(_queuedSkill.id))
        {
            _attackTimer = 0f; _hitTimer = -1f; _strikePending = false;
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
        else if (_queuedTool != null && _attackTimer <= 0f &&
                 Vector2.Distance(transform.position, FarmGrid.Center(_toolCell)) <= ToolReach)
        {
            DoTool(_queuedTool, _toolCell);
            _queuedTool = null;
            _moveTo = null;
        }
        else if (_target != null && _attackTimer <= 0f && (Time.time >= _nextAttackAt || _queuedSkill != null) &&
                 (DistanceTo(_target) <= Reach || ChaseBlocked()))
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

        if (def.kind == SkillKind.Furniture)
        {
            if (!_skills.Spend(def)) return;
            _target = null; _queuedSkill = null; _holdMove = false; _moveTo = null;
            Placement.BeginHammer(_bag, def.id == "recolher");
            return;
        }

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
            if (_attackTimer > 0f || !_skills.CanUse(def.id)) return;
            // Impacto Explosivo custa vida (20 no Nv 1 até 16 no Nv 10); sem vida, não sai.
            int lv = Mathf.Max(1, _skills.Level(def.id));
            if (def.id == "impacto_explosivo" && (_health == null || !_health.PayHp(20 - (lv - 1) / 2))) return;
            if (!_skills.Spend(def)) return;
            if (def.id == "impacto_explosivo") MagnumBreak(def, lv);
            else Explode(def);
            BeginAttack(MouseWorld() - Chest, null, strike: false);   // só a animação; o dano já saiu
            return;
        }

        // Corpo a corpo: o cursor vira um círculo de seleção e o clique no monstro escolhe o
        // alvo (ver Update). Longe, o personagem anda até ele e solta a habilidade ao chegar.
        _aimSkill = def;
        _queuedTool = null;
    }

    /// Perseguindo o alvo sem conseguir chegar mais perto (bloqueado por árvore, baú ou pelo
    /// corpo do monstro): se já está quase no alcance, golpeia daqui; se está longe, desiste
    /// do alvo em vez de ficar empurrando parado.
    private bool ChaseBlocked()
    {
        if (_chaseStuck < 20) return false;   // ~0,4 s sem avançar (contado no FixedUpdate)
        _chaseStuck = 0;
        if (DistanceTo(_target) <= Reach * 1.8f) return true;
        _target = null;
        _queuedSkill = null;
        return false;
    }

    private static bool MoveKeysHeld()
    {
        var k = Keyboard.current;
        return k != null && (k.aKey.isPressed || k.dKey.isPressed || k.wKey.isPressed || k.sKey.isPressed ||
                             k.leftArrowKey.isPressed || k.rightArrowKey.isPressed ||
                             k.upArrowKey.isPressed || k.downArrowKey.isPressed);
    }

    /// Larga alvo, caminho e ação pendente (ex.: ao passar por um portal de mapa).
    public void ClearOrders()
    {
        _target = null; _moveTo = null; _holdMove = false; _queuedSkill = null; _queuedTool = null; _aimSkill = null;
    }

    private Vector2 Chest => (Vector2)transform.position + Vector2.up * 0.45f;

    /// Com besta, ataca de longe: 5 quadrados do chão.
    private bool Ranged => _stats != null && _stats.Weapon == WeaponType.Crossbow;
    /// Alcance do ataque básico da arma na mão (lança de alcance 3), em unidades; 0 = comum.
    private float WeaponRange
    {
        get
        {
            var eq = GetComponent<Equipment>();
            var w = eq != null ? ItemDB.Get(eq.Get(EquipSlot.Weapon)) : null;
            return w != null ? w.range * FarmGrid.Cell : 0f;
        }
    }

    private float Reach => Ranged ? 5f * FarmGrid.Cell
        : _queuedSkill != null && _queuedSkill.range > 0f ? Mathf.Max(engageRange, _queuedSkill.range * FarmGrid.Cell)
        : Mathf.Max(engageRange, WeaponRange);

    private float DistanceTo(Health h) => Vector2.Distance(transform.position, h.transform.position);

    private static Vector2 MouseWorld() =>
        Mouse.current != null ? (Vector2)Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()) : Vector2.zero;

    /// Monstro sob o mouse, pelo desenho do sprite (o colisor só cobre as patas).
    /// Havendo sobreposição, vale o que está mais à frente (mais abaixo na tela).
    private Health MonsterAt(Vector2 p)
    {
        Health best = null;
        float bestY = float.MaxValue;
        foreach (var h in Health.All)
        {
            if (h == _health || h.IsDead) continue;
            var sr = h.GetComponentInChildren<SpriteRenderer>();
            var col = h.GetComponent<Collider2D>();
            bool onBody = col != null && col.enabled && col.OverlapPoint(p);
            if (!onBody && (sr == null || !sr.bounds.Contains(new Vector3(p.x, p.y, sr.bounds.center.z)))) continue;
            if (h.transform.position.y < bestY) { bestY = h.transform.position.y; best = h; }
        }
        return best;
    }

    /// Diagnóstico: clique que não pegou monstro, mas havia um perto (o que impediu).
    private void LogMissedClick(Vector2 p)
    {
        foreach (var h in Health.All)
        {
            if (h == _health || Vector2.Distance(h.transform.position, p) > 3f) continue;
            var sr = h.GetComponentInChildren<SpriteRenderer>();
            string why = h.IsDead ? "morto (vida " + h.Hp + ")"
                       : sr == null ? "sem SpriteRenderer"
                       : sr.sprite == null ? "SpriteRenderer sem desenho"
                       : "clique fora do desenho (desenho em " + sr.bounds.min + " a " + sr.bounds.max + ", objeto do desenho \"" + sr.name +
                         "\" filho de \"" + (sr.transform.parent != null ? sr.transform.parent.name : "-") + "\", corpo em " + h.transform.position + ")";
            Debug.Log($"[Clique] {h.name} perto de {p}: não selecionado — {why}", h);
        }
    }

    private void DoTool(SkillDef def, Vector2Int cell)
    {
        if (!FarmGrid.CanAct(def.id, cell, _bag) || !_skills.Spend(def)) return;
        FarmGrid.Act(def.id, cell, _bag);
        // Mesmo gesto do golpe, virado para o quadrado, sem causar dano.
        BeginAttack(FarmGrid.Center(cell) - Chest, null, strike: false);
    }

    /// Perfurar em Espiral (Spiral Pierce, pré-Renewal): no alvo travado, a até 4 quadrados.
    /// Por golpe: peso da arma × 0,8 × (1 + 0,5×Nv) + (FOR/10)²; 5 golpes (somados num número);
    /// ignora a DEF; prende o alvo 1 s (chefes não).
    private void SpiralPierce(SkillDef def)
    {
        var target = _target;
        if (target == null || target.IsDead || DistanceTo(target) > def.range * FarmGrid.Cell + 0.6f) return;
        int lv = Mathf.Max(1, _skills.Level(def.id));
        var eq = GetComponent<Equipment>();
        var weapon = eq != null ? ItemDB.Get(eq.Get(EquipSlot.Weapon)) : null;
        int weight = weapon != null && weapon.weight > 0 ? weapon.weight : 100;
        int str = _stats != null ? _stats.Total(0) : 1;
        float perHit = weight * 0.8f * (1f + 0.5f * lv) + (str / 10) * (str / 10);
        var r = Combat.ApplyIgnoreDef(_stats, target, transform.position, 0f, perHit * 5f);
        if (r.kind != HitKind.Miss) target.Hold(1f);
    }

    /// Impacto Explosivo (Magnum Break): 5x5 quadrados em volta dos pés. Centro (3x3):
    /// ATQ × (100 + 20×Nv)%; borda: ATQ × (100 + 10×Nv)%; no Nv 10, 500% em tudo; precisão +10×Nv %; empurra 2
    /// quadrados. Depois, 10 s de golpes normais com +20% de fogo.
    private void MagnumBreak(SkillDef def, int lv)
    {
        Vector2 center = transform.position;
        var hit = new HashSet<Health>();
        foreach (var col in Physics2D.OverlapBoxAll(center, new Vector2(5f, 5f) * FarmGrid.Cell, 0f))
        {
            var target = col.GetComponentInParent<Health>();
            if (target == null || target == _health || target.IsDead || !hit.Add(target)) continue;
            Vector2 d = ((Vector2)target.transform.position - center) / FarmGrid.Cell;
            int cells = Mathf.Max(Mathf.RoundToInt(Mathf.Abs(d.x)), Mathf.RoundToInt(Mathf.Abs(d.y)));
            float mult = (100f + (cells <= 1 ? 20f : 10f) * lv) / 100f;
            if (lv >= 10) mult = 5f;   // nível 10: 500% em toda a área (regra própria do jogo)
            Combat.ApplySkillHit(_stats, target, center, knockback * 2f, mult, 10f * lv);
        }
        if (_stats != null) _stats.AddFireBonus(10f);
        SkillFx.Ring(center, def.radius);
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
            Combat.ApplyFixed(target, def.fixedDamage, center, knockback * 1.4f, _stats);
        }
        SkillFx.Ring(center, def.radius);
    }

    private void BeginAttack(Vector2 dir, SkillDef skill = null, bool strike = true)
    {
        _activeSkill = skill;
        _strikePending = strike;
        AttackDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : AttackDir;

        // ASPD (Ragnarok): o golpe dura o amotion e o próximo só sai depois do intervalo
        // inteiro (2 × amotion). AGI e DES encurtam os dois; a arma define a base.
        float motion = _stats != null ? _stats.AttackMotion : attackDuration;
        _attackLength = _attackTimer = motion;
        _hitTimer = motion * hitDelay / attackDuration;   // impacto na mesma fração do golpe
        if (strike) _nextAttackAt = Time.time + (_stats != null ? _stats.AttackInterval : attackDuration);
        // Habilidade com conjuração (Perfurar em Espiral): o golpe sai ao fim dela.
        float cast = skill != null && _skills != null ? skill.CastAt(_skills.Level(skill.id)) : 0f;
        if (cast > 0f)
        {
            _hitTimer = cast;
            _attackLength = _attackTimer = cast + motion * 0.5f;
        }
        _body.linearVelocity = Vector2.zero;
    }

    /// Acerta tudo que tem vida num círculo na direção do golpe.
    private void Strike()
    {
        if (_activeSkill != null && _activeSkill.id == "perfurar_espiral")
        {
            SpiralPierce(_activeSkill);
            return;
        }
        // Lança de alcance (ex.: Lança de Caça, alcance 3): acerta o alvo travado de longe.
        if (!Ranged && WeaponRange > engageRange && _target != null && !_target.IsDead &&
            DistanceTo(_target) <= WeaponRange + 0.5f)
        {
            bool sk = _activeSkill != null;
            Combat.Apply(_stats, _target, transform.position, sk ? knockback * 1.6f : 0f, sk ? _activeSkill.damageMult : 1f, sk);
            _whiffs = 0;
            return;
        }
        if (Ranged)
        {
            // Besta: o virote voa até o alvo travado (ou na direção do golpe) e acerta o primeiro
            // que tiver vida no caminho.
            bool skillShot = _activeSkill != null;
            Bolt.Fire(_stats, _health, Chest, AttackDir, Reach + 0.5f, skillShot ? knockback * 1.6f : 0f,
                      skillShot ? _activeSkill.damageMult : 1f, skillShot);
            _whiffs = 0;
            return;
        }
        Vector2 center = (Vector2)transform.position + Vector2.up * 0.45f + AttackDir * hitReach;
        var hit = new HashSet<Health>();
        foreach (var col in Physics2D.OverlapCircleAll(center, hitRadius))
        {
            var target = col.GetComponentInParent<Health>();
            if (target == null || target == _health || target.IsDead || !hit.Add(target)) continue;
            bool skill = _activeSkill != null;
            // O golpe básico corpo a corpo não empurra o monstro; só as skills empurram.
            Combat.Apply(_stats, target, transform.position, skill ? knockback * 1.6f : 0f,
                         skill ? _activeSkill.damageMult : 1f, skill);
        }
        // Golpe no ar com alvo travado (atrás de algo, de lado): depois de 3 seguidos, larga o
        // alvo em vez de ficar golpeando o vazio para sempre.
        if (_target != null && !hit.Contains(_target))
        {
            if (++_whiffs >= 3) { _target = null; _queuedSkill = null; _whiffs = 0; }
        }
        else _whiffs = 0;
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

        if (_target == null) { _chaseStuck = 0; _chaseDist = float.MaxValue; }

        // Qualquer movimento manual cancela o alvo.
        if (input.sqrMagnitude > 0f)
        {
            _target = null; _moveTo = null; _holdMove = false; _queuedSkill = null; _queuedTool = null;
        }
        else if (_target != null && _attackTimer <= 0f && DistanceTo(_target) > Reach)
        {
            input = ((Vector2)_target.transform.position - (Vector2)transform.position).normalized;
            float dist = DistanceTo(_target);
            _chaseStuck = dist > _chaseDist - 0.005f ? _chaseStuck + 1 : 0;
            _chaseDist = dist;
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
        _aimSkill = null; _queuedSkill = null; _target = null;
        // Skin com animação de morte: ela mostra a queda; sem ela, o personagem fica translúcido.
        var anim = GetComponent<KnightAnimator>();
        if (_sprite != null && (anim == null || !anim.HasDeath)) _sprite.color = new Color(1f, 1f, 1f, 0.35f);

        yield return new WaitForSeconds(respawnDelay);

        if (HouseInterior.Inside != null) HouseInterior.Exit();   // renasce do lado de fora
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
