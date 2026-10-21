using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Drake, o capitão pirata morto-vivo: o primeiro boss (MVP).
/// Perfil do bRO (rAthena db/re/mob_db.yml, mob 1112; confere com o Divine Pride): nível 91,
/// 804.500 HP, ATQ 2.227–3.234, DEF 279, MDEF 135, recebe só 10% do dano, ataca a cada 0,62 s.
/// Corpo a corpo com o sabre; de longe, de vez em quando, atira com a pistola.
/// Sprites: Art/Sprites/Monsters/MVP/Resources/Drake (Tools/extract_boss_pirata.py).
[RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(CharacterStats))]
// Clicar no desenho na Scene seleciona o monstro inteiro (não só o desenho, que se
// soltaria do corpo ao mover ou apagar).
[SelectionBase]
public class DrakeBoss : MonoBehaviour
{
    public static readonly List<DrakeBoss> All = new List<DrakeBoss>();
    private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    private void OnDisable() => All.Remove(this);

    public const string DisplayName = "Drake";
    /// Avisos para o HUD (ex.: "MVP!").
    public static event Action<string> Announce;
    /// Aviso na tela (outros MVPs também usam).
    public static void Anunciar(string msg) => Announce?.Invoke(msg);


    /// Monta o boss inteiro por código (corpo, colisor, arte, vida e atributos).
    public static DrakeBoss Create(Vector2 pos)
    {
        var go = new GameObject("Drake (boss)");
        go.transform.position = pos;
        return Build(go);
    }

    /// Põe no objeto tudo que o boss precisa. O editor usa isto para colocar o Drake na cena
    /// (janela RaelMMO > Mapa > Monstros, aba MVPs): fica como os lobos, para mover, apagar e editar.
    public static DrakeBoss Build(GameObject go)
    {
        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.mass = 100f;   // o jogador não o empurra
        var col = go.AddComponent<CapsuleCollider2D>();
        col.direction = CapsuleDirection2D.Horizontal;
        col.size = new Vector2(1.1f, 0.4f);
        col.offset = new Vector2(0f, 0.2f);
        // A arte fica num filho: balança ao andar sem mexer no corpo.
        var art = new GameObject("Arte");
        art.transform.SetParent(go.transform, false);
        art.AddComponent<SpriteRenderer>();
        art.AddComponent<YSortRenderer>();
        go.AddComponent<DrakePreview>();
        go.AddComponent<CharacterStats>();
        go.AddComponent<Health>().immovable = true;
        return go.AddComponent<DrakeBoss>();
    }

    [Header("Perfil (bRO)")]
    public int level = 91, hp = 804500, atkMin = 2227, atkMax = 3234, def = 279, mdef = 135;
    public int str = 121, agi = 103, vit = 114, intl = 107, dex = 162, luk = 71;
    [Tooltip("Porcentagem do dano que ele recebe (MVP do bRO: 10%).")]
    public int damageTaken = 10;

    [Header("Recompensa (antes do multiplicador de XP)")]
    public int baseExp = 309096, jobExp = 208980, mvpExp = 154548;

    [Header("Movimento")]
    public float wanderSpeed = 1.2f;
    [Tooltip("WalkSpeed 400 do Ragnarok = 2,5 células por segundo.")]
    public float chaseSpeed = 2.5f;
    public float wanderRadius = 3f;
    public float detectRadius = 7f;
    [Tooltip("ChaseRange 12: desiste da perseguição além disto.")]
    public float chaseRange = 12f;

    [Header("Sabre")]
    public float attackRange = 1.6f;
    [Tooltip("AttackDelay 620 ms: tempo entre um golpe e outro.")]
    public float attackDelay = 0.62f;
    [Tooltip("AttackMotion 420 ms: duração do golpe.")]
    public float attackMotion = 0.42f;
    public float strikeAt = 0.25f;

    [Header("Pistola")]
    public float shotMinRange = 2.5f, shotRange = 10f;
    [Tooltip("Chance por segundo de atirar quando o jogador está longe.")]
    [Range(0f, 1f)] public float shotChance = 0.2f;
    public float shotCooldown = 5f, shotCast = 0.5f, bulletSpeed = 11f;

    [Header("Morte")]
    [Tooltip("Respawn do Drake no Ragnarok: 2 horas.")]
    public float respawnDelay = 7200f;

    // Padrão do jogo: 64 px por quadrado da grade (ver Tools/padronizar_64.py).
    private const float PPU = 64f;
    private static readonly string[] FrameNames =
    {
        "frente", "esquerda", "costas_direita", "costas_esquerda", "costas", "parado", "golpe_1", "golpe_2",
        "tiro_direita", "tiro_esquerda", "dor_1", "dor_2", "morte_1", "morte_2", "morte_3",
    };
    private static Dictionary<string, Sprite> _frames;
    // Andar: 4 quadros por direção, gerados por Tools/gen_drake_walk.py (pernas recortadas
    // e recompostas a cada passo, como o lobo).
    private static readonly string[] WalkNames = { "frente", "esquerda", "costas_direita", "costas_esquerda", "costas" };
    private static Dictionary<string, Sprite[]> _walk;

    private enum State { Idle, Attack, Aim, Dead }
    private State _state;
    private Rigidbody2D _body;
    private Health _health;
    private CharacterStats _stats;
    private SpriteRenderer _sprite;
    private Transform _art;
    private Collider2D _collider;
    private Transform _player;
    private Health _playerHealth;

    private Vector2 _home, _wanderTarget, _facing = Vector2.down, _velocity;
    private float _wanderTimer, _timer, _nextAttack, _nextShot, _shotRoll, _shotPoseUntil, _hurtUntil;
    private int _attackCount, _lastHp;
    private bool _chasing, _hitDone, _respawnNow;
    private string _deathFrame;

    public bool IsDead => _state == State.Dead;
    /// Segundos até reaparecer (0 = vivo).
    public float RespawnIn { get; private set; }

    private void Awake()
    {
        _drops = MonsterDrops.Ensure(gameObject, DisplayName, true);
        _body = GetComponent<Rigidbody2D>();
        _health = GetComponent<Health>();
        _stats = GetComponent<CharacterStats>();
        _sprite = GetComponentInChildren<SpriteRenderer>();
        _art = _sprite.transform;
        _collider = GetComponent<Collider2D>();
        _home = transform.position;
        _wanderTarget = _home;
        LoadFrames();
        _stats.SetMonster(level, hp, atkMin, atkMax, def, mdef, str, agi, vit, intl, dex, luk, damageTaken);
        _lastHp = _health.Hp;
        _health.Died += OnDied;
        Show("parado", false);
        if (_pendingHp > 0 || _pendingRespawn > 0f) ApplyPending();
    }

    private static void LoadFrames()
    {
        if (_frames != null) return;
        _walk = new Dictionary<string, Sprite[]>();
        foreach (var n in WalkNames)
        {
            var sheet = Resources.Load<Texture2D>("Drake/andar_" + n);
            if (sheet == null) continue;
            sheet.filterMode = FilterMode.Point;
            int fw = sheet.width / 4;
            var walk = new Sprite[4];
            for (int i = 0; i < 4; i++)
                walk[i] = Sprite.Create(sheet, new Rect(i * fw, 0, fw, sheet.height),
                                        new Vector2(0.5f, 4f / sheet.height), PPU, 0, SpriteMeshType.FullRect);
            _walk[n] = walk;
        }
        _frames = new Dictionary<string, Sprite>();
        foreach (var n in FrameNames)
        {
            var tex = Resources.Load<Texture2D>("Drake/" + n);
            if (tex == null) { Debug.LogError("[Drake] falta Monsters/MVP/Resources/Drake/" + n); continue; }
            tex.filterMode = FilterMode.Point;
            // Pés 2 px acima da base, como na extração.
            _frames[n] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                       new Vector2(0.5f, 4f / tex.height), PPU, 0, SpriteMeshType.FullRect);
        }
    }

    private void Start()
    {
        var player = FindAnyObjectByType<PlayerController2D>();
        if (player == null) return;
        _player = player.transform;
        _playerHealth = player.GetComponent<Health>();
    }

    private bool PlayerAlive => _player != null && (_playerHealth == null || !_playerHealth.IsDead);

    private void FixedUpdate()
    {
        if (_state == State.Dead) return;
        float dt = Time.fixedDeltaTime;
        Vector2 pos = _body.position;
        float dist = PlayerAlive ? Vector2.Distance(pos, _player.position) : float.PositiveInfinity;
        _velocity = Vector2.zero;

        switch (_state)
        {
            case State.Attack:
                _timer += dt;
                if (!_hitDone && _timer >= strikeAt)
                {
                    _hitDone = true;
                    // Confere de novo: quem recuou durante o golpe escapa.
                    if (dist <= attackRange + 0.4f) Combat.Apply(_stats, _playerHealth, pos, 0f);
                }
                if (_timer >= attackMotion) _state = State.Idle;
                break;

            case State.Aim:
                _timer += dt;
                if (_timer >= shotCast)
                {
                    Fire();
                    _shotPoseUntil = Time.time + 0.3f;
                    _state = State.Idle;
                }
                break;

            default:
                if (!_chasing && dist < detectRadius) _chasing = true;
                else if (_chasing && dist > chaseRange) _chasing = false;
                if (_chasing) Chase(pos, dist, dt);
                else Wander(pos, dt);
                break;
        }
        _body.linearVelocity = _velocity;
    }

    private void Chase(Vector2 pos, float dist, float dt)
    {
        Vector2 toPlayer = (Vector2)_player.position - pos;
        if (dist <= attackRange)
        {
            Face(toPlayer);
            if (Time.time >= _nextAttack) BeginAttack();
            return;
        }
        // Longe: de vez em quando para e atira (como as skills de longe do Drake no Ragnarok).
        _shotRoll += dt;
        if (_shotRoll >= 1f)
        {
            _shotRoll -= 1f;
            if (dist >= shotMinRange && dist <= shotRange && Time.time >= _nextShot && UnityEngine.Random.value < shotChance)
            {
                BeginAim(toPlayer);
                return;
            }
        }
        _velocity = toPlayer.normalized * chaseSpeed;
        Face(_velocity);
    }

    private void Wander(Vector2 pos, float dt)
    {
        _wanderTimer -= dt;
        if (_wanderTimer <= 0f)
        {
            _wanderTarget = UnityEngine.Random.value < 0.5f ? pos : _home + UnityEngine.Random.insideUnitCircle * wanderRadius;
            _wanderTimer = UnityEngine.Random.Range(2f, 5f);
        }
        Vector2 to = _wanderTarget - pos;
        if (to.magnitude < 0.2f) return;
        _velocity = to.normalized * wanderSpeed;
        Face(_velocity);
    }

    private void Face(Vector2 dir)
    {
        if (dir.sqrMagnitude > 0.0001f) _facing = dir.normalized;
    }

    private void BeginAttack()
    {
        _state = State.Attack;
        _timer = 0f;
        _hitDone = false;
        _attackCount++;
        _nextAttack = Time.time + attackDelay;
    }

    private void BeginAim(Vector2 toPlayer)
    {
        Face(toPlayer);
        _state = State.Aim;
        _timer = 0f;
        _nextShot = Time.time + shotCooldown;
    }

    private void Fire()
    {
        if (!PlayerAlive) return;
        float side = _facing.x < 0f ? -1f : 1f;
        Vector2 muzzle = (Vector2)transform.position + new Vector2(side * 1.4f, 1.3f);
        Vector2 target = (Vector2)_player.position + Vector2.up * 0.5f;
        DrakeBullet.Spawn(muzzle, target, bulletSpeed, _stats, _playerHealth);
    }

    // ── Visual ──

    private void Update()
    {
        if (_state == State.Dead) { if (_deathFrame != null) Show(_deathFrame, false); return; }

        // Levou dano: vira o rosto para o jogador, entra na perseguição e mostra a dor.
        if (_health.Hp < _lastHp)
        {
            _hurtUntil = Time.time + 0.2f;
            _chasing = true;
        }
        _lastHp = _health.Hp;

        if (_state == State.Attack) Show(_attackCount % 2 == 1 ? "golpe_1" : "golpe_2", _facing.x < 0f);
        else if (_state == State.Aim || Time.time < _shotPoseUntil) Show(_facing.x < 0f ? "tiro_esquerda" : "tiro_direita", false);
        else if (Time.time < _hurtUntil) Show("dor_1", _facing.x > 0f);
        else if (_velocity.sqrMagnitude > 0.01f)
        {
            Vector2 v = _velocity;
            string dir;
            bool flip = false;
            if (Mathf.Abs(v.x) > Mathf.Abs(v.y) * 1.2f) { dir = "esquerda"; flip = v.x > 0f; }
            else if (v.y > 0f) dir = Mathf.Abs(v.x) < 0.3f * v.y ? "costas" : v.x < 0f ? "costas_esquerda" : "costas_direita";
            else dir = "frente";
            // Passos mais rápidos correndo atrás do jogador do que vagando.
            float fps = 2.8f * v.magnitude;
            if (_walk.TryGetValue(dir, out var walk))
            {
                _walkTime += Time.deltaTime * fps;
                ShowSprite(walk[(int)_walkTime % walk.Length], flip);
            }
            else Show(dir, flip);
        }
        else
        {
            _walkTime = 0f;
            Show("parado", false);
        }
    }

    private float _walkTime;

    private void ShowSprite(Sprite s, bool flip)
    {
        if (_sprite.sprite != s) _sprite.sprite = s;
        _sprite.flipX = flip;
    }

    private void Show(string frame, bool flip)
    {
        if (_frames.TryGetValue(frame, out var s) && _sprite.sprite != s) _sprite.sprite = s;
        _sprite.flipX = flip;
    }

    // ── Morte e respawn ──

    private MonsterDrops _drops;

    private void OnDied(Health h)
    {
        // Drops (ainda não definidos: lista vazia no MonsterDrops).
        var dropBag = _player != null ? _player.GetComponent<Inventory>() : null;
        if (_drops != null) _drops.Roll(dropBag);
        var playerStats = _player != null ? _player.GetComponent<CharacterStats>() : null;
        if (playerStats != null)
        {
            // XP normal + XP de MVP (no Ragnarok vai para quem mais causou dano).
            playerStats.GainExp((baseExp + mvpExp) * ExpTable.Rate);
            playerStats.GainJobExp(jobExp * ExpTable.Rate);
        }
        Announce?.Invoke("MVP! Drake derrotado");
        StartCoroutine(DieAndRespawn(true, respawnDelay));
        SaveGame.Save();
    }

    // ── Jogo salvo ──
    private static int _pendingHp;
    private static float _pendingRespawn;

    /// Estado para o jogo salvo: vida (vivo) ou segundos até reaparecer (morto).
    public static void Capture(out int hp, out float respawnIn)
    {
        hp = 0; respawnIn = 0f;
        if (All.Count == 0) return;
        var b = All[0];
        if (b.IsDead) respawnIn = b.RespawnIn > 0f ? b.RespawnIn : b.respawnDelay;
        else hp = b._health.Hp;
    }

    /// Restaura do jogo salvo (se o boss ainda não nasceu, guarda para quando nascer).
    public static void Restore(int hp, float respawnIn)
    {
        _pendingHp = hp; _pendingRespawn = respawnIn;
        if (All.Count > 0) All[0].ApplyPending();
    }

    private void ApplyPending()
    {
        if (_pendingRespawn > 0f)
        {
            StopAllCoroutines();
            StartCoroutine(DieAndRespawn(false, _pendingRespawn));
        }
        else if (_pendingHp > 0 && !IsDead)
        {
            _health.Restore(_pendingHp);
            _lastHp = _health.Hp;
        }
        _pendingHp = 0; _pendingRespawn = 0f;
    }

    /// DEV: volta agora em vez de esperar o tempo de respawn.
    public void RespawnNow() => _respawnNow = true;

    /// Morte (com a animação, ou já sumido ao carregar o jogo) e volta depois de `wait` segundos.
    private IEnumerator DieAndRespawn(bool animate, float wait)
    {
        _state = State.Dead;
        _chasing = false;
        _body.linearVelocity = Vector2.zero;
        _body.simulated = false;
        if (_collider != null) _collider.enabled = false;
        _art.localPosition = Vector3.zero;
        RespawnIn = wait;

        if (animate)
        {
            foreach (var (frame, time) in new[] { ("dor_2", 0.35f), ("morte_1", 0.45f), ("morte_2", 0.45f), ("morte_3", 3f) })
            {
                _deathFrame = frame;
                yield return new WaitForSeconds(time);
            }
            yield return Fade(1f, 0f, 1f);
        }
        else
        {
            var c = _sprite.color; c.a = 0f; _sprite.color = c;
        }

        _respawnNow = false;
        for (float t = wait; t > 0f && !_respawnNow; t -= Time.deltaTime)
        {
            RespawnIn = t;
            yield return null;
        }
        RespawnIn = 0f;

        transform.position = _home;
        _body.position = _home;
        _facing = Vector2.down;
        _deathFrame = null;
        _health.Restore();
        _lastHp = _health.Hp;
        _state = State.Idle;
        Show("parado", false);
        yield return Fade(0f, 1f, 1f);
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

/// Bala da pistola do Drake: voa em linha reta até o ponto mirado; acerta o jogador se
/// passar por ele (golpe físico normal: pode errar pela esquiva).
public class DrakeBullet : MonoBehaviour
{
    private static Sprite _sprite;
    private Vector2 _dir;
    private float _speed, _life;
    private CharacterStats _owner;
    private Health _target;

    public static void Spawn(Vector2 from, Vector2 to, float speed, CharacterStats owner, Health target)
    {
        if (_sprite == null)
        {
            var tex = new Texture2D(4, 2) { filterMode = FilterMode.Point };
            var hot = new Color(1f, 0.85f, 0.4f);
            tex.SetPixels(new[] { hot, hot, Color.white, Color.white, hot, hot, Color.white, Color.white });
            tex.Apply();
            _sprite = Sprite.Create(tex, new Rect(0, 0, 4, 2), new Vector2(0.5f, 0.5f), 32f);
        }
        var go = new GameObject("Bala");
        go.transform.position = from;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _sprite;
        sr.sortingOrder = 30000;
        var b = go.AddComponent<DrakeBullet>();
        b._dir = (to - from).normalized;
        b._speed = speed;
        b._life = (to - from).magnitude / speed + 0.4f;
        b._owner = owner;
        b._target = target;
        go.transform.right = b._dir;
    }

    private void Update()
    {
        transform.position += (Vector3)(_dir * _speed * Time.deltaTime);
        _life -= Time.deltaTime;
        if (_target != null && !_target.IsDead &&
            Vector2.Distance(transform.position, (Vector2)_target.transform.position + Vector2.up * 0.5f) < 0.45f)
        {
            Combat.Apply(_owner, _target, transform.position, 0f);
            Destroy(gameObject);
        }
        else if (_life <= 0f) Destroy(gameObject);
    }
}
