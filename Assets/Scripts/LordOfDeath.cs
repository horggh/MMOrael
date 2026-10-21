using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// MVP Lord of Death (Ragnarok pré-Renewal, mob 1373, rAthena db/pre-re/mob_db.yml): nível 94,
/// 603.383 HP, ATQ 3430–4232, DEF 77, MDEF 73, AGI 99 VIT 30 INT 109 DES 100 SOR 106, ataque a
/// cada 1,446 s, alcance 3, chefe. Recebe só uma parte do dano, como o Drake (MVP do bRO).
/// Arte: Art/Sprites/Monsters/MVP/Resources/CavaleiroSangue (Tools/extract_mvp_cavaleiro.py):
/// 8 direções parado/andando, ataque, habilidade (área de lanças de sangue), dano e morte.
/// Coloque pela janela RaelMMO > Mapa > Monstros (aba MVPs). Vida e respawn vão no jogo salvo.
// Clicar no desenho na Scene seleciona o monstro inteiro (não só o desenho, que se
// soltaria do corpo ao mover ou apagar).
[SelectionBase]
public class LordOfDeath : MonoBehaviour
{
    public static readonly List<LordOfDeath> All = new List<LordOfDeath>();
    private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    private void OnDisable() => All.Remove(this);

    [Header("Perfil (rAthena pré-Renewal, 1373)")]
    public int level = 94, hp = 603383, atkMin = 3430, atkMax = 4232, def = 77, mdef = 73;
    public int str = 1, agi = 99, vit = 30, intl = 109, dex = 100, luk = 106;
    [Tooltip("Porcentagem do dano que ele recebe (MVP do bRO: 10%).")]
    public int damageTaken = 10;
    public int baseExp = 131343, jobExp = 43345, mvpExp = 65671;

    [Header("Comportamento")]
    public float wanderSpeed = 1f;
    public float chaseSpeed = 2.8f;
    public float wanderRadius = 4f;
    public float detectRadius = 8f;
    public float loseRadius = 14f;
    [Tooltip("Alcance 3 do Ragnarok, em quadrados.")]
    public float attackRange = 2.6f;
    [Tooltip("Tempo entre golpes (AttackDelay 1446 ms).")]
    public float attackCooldown = 1.446f;
    public float attackDuration = 0.9f;
    [Header("Habilidade (lanças de sangue em área)")]
    public float skillCooldown = 12f;
    public float skillRadius = 3f;
    public float skillDamageMult = 2.5f;
    public float skillDuration = 1.4f;
    [Tooltip("Segundos até reaparecer depois de morto.")]
    public float respawnDelay = 600f;
    public float walkFps = 4f;

    // Padrão do jogo: 64 px por quadrado da grade (ver Tools/padronizar_64.py).
    private const float PPU = 64f;
    private static readonly string[] Dirs = { "direita", "costas_direita", "costas", "costas_esquerda", "esquerda", "frente_esquerda", "frente", "frente_direita" };
    private static Dictionary<string, Sprite[]> _frames;

    private Rigidbody2D _body;
    private Health _health;
    private CharacterStats _stats;
    private SpriteRenderer _sprite;
    private Collider2D _collider;
    private Transform _player;
    private Health _playerHealth;
    private Vector2 _home, _wanderTarget;
    private float _wanderTimer, _cooldown, _skillCooldown, _actionTimer, _actionLength, _animT, _hurtUntil;
    private bool _hitDone, _chasing, _dead, _skill;
    private int _lastHp;
    private string _dir = "frente";
    public float RespawnIn { get; private set; }
    public bool IsDead => _dead || (_health != null && _health.IsDead);

    public static LordOfDeath Build(GameObject go)
    {
        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.mass = 100f;
        var col = go.AddComponent<CapsuleCollider2D>();
        col.direction = CapsuleDirection2D.Horizontal;
        col.size = new Vector2(1.5f, 0.5f);
        col.offset = new Vector2(0f, 0.25f);
        var art = new GameObject("Arte");
        art.transform.SetParent(go.transform, false);
        art.AddComponent<SpriteRenderer>();
        art.AddComponent<YSortRenderer>();
        go.AddComponent<CharacterStats>();
        go.AddComponent<Health>().immovable = true;
        var m = go.AddComponent<LordOfDeath>();
        m.ShowPreview();
        return m;
    }

    private static Sprite[] Load(string name)
    {
        var tex = Resources.Load<Texture2D>("CavaleiroSangue/" + name);
        if (tex == null) { Debug.LogError("[Lord of Death] falta CavaleiroSangue/" + name); return new Sprite[0]; }
        tex.filterMode = FilterMode.Point;
        // Quadros quadrados ou mais largos que altos: a quantidade vem da largura da tira.
        int count = name == "ataque" ? 6 : name == "habilidade" ? 4 : name == "dano" ? 3 : name.StartsWith("andar") ? 2 : 1;
        int fw = tex.width / count;
        var s = new Sprite[count];
        for (int i = 0; i < count; i++)
            s[i] = Sprite.Create(tex, new Rect(i * fw, 0, fw, tex.height), new Vector2(0.5f, 2f / tex.height), PPU, 0, SpriteMeshType.FullRect);
        return s;
    }

    private static void LoadAll()
    {
        if (_frames != null) return;
        _frames = new Dictionary<string, Sprite[]>();
        foreach (var d in Dirs)
        {
            _frames["parado_" + d] = Load("parado_" + d);
            _frames["andar_" + d] = Load("andar_" + d);
        }
        foreach (var n in new[] { "ataque", "habilidade", "dano", "morte" }) _frames[n] = Load(n);
    }

    public void ShowPreview()
    {
        var sr = GetComponentInChildren<SpriteRenderer>();
        var s = Load("parado_frente");
        if (sr == null || s.Length == 0) return;
        sr.sprite = s[0];
        sr.sortingOrder = YSortRenderer.OrderFor(transform.position.y);
    }

    /// Desenho perdido ou deslocado na edição (o filho "Arte" movido ou apagado): refaz.
    public static SpriteRenderer FixArt(Transform root)
    {
        var art = root.Find("Arte");
        if (art == null)
        {
            art = new GameObject("Arte").transform;
            art.SetParent(root, false);
        }
        art.localPosition = Vector3.zero;
        var sr = art.GetComponent<SpriteRenderer>();
        if (sr == null) sr = art.gameObject.AddComponent<SpriteRenderer>();
        if (art.GetComponent<YSortRenderer>() == null) art.gameObject.AddComponent<YSortRenderer>();
        return sr;
    }

    private void Awake()
    {
        _drops = MonsterDrops.Ensure(gameObject, "Lord of Death", true);
        FixArt(transform);
        _body = GetComponent<Rigidbody2D>();
        _health = GetComponent<Health>();
        _stats = GetComponent<CharacterStats>();
        _sprite = GetComponentInChildren<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
        _home = transform.position;
        _wanderTarget = _home;
        LoadAll();
        _stats.SetMonster(level, hp, atkMin, atkMax, def, mdef, str, agi, vit, intl, dex, luk, damageTaken);
        _lastHp = _health.Hp;
        _health.Died += OnDied;
        _skillCooldown = skillCooldown * 0.5f;
        Show("parado_frente", 0);
        if (_pendingHp > 0 || _pendingRespawn > 0f) ApplyPending();
    }

    private void Start()
    {
        var player = FindAnyObjectByType<PlayerController2D>();
        if (player == null) return;
        _player = player.transform;
        _playerHealth = player.GetComponent<Health>();
    }

    private void Show(string anim, int i, bool flip = false)
    {
        if (!_frames.TryGetValue(anim, out var f) || f.Length == 0) return;
        _sprite.sprite = f[Mathf.Clamp(i, 0, f.Length - 1)];
        _sprite.flipX = flip;
    }

    private static string Dir8(Vector2 v)
    {
        int i = Mathf.RoundToInt(Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg / 45f);
        return Dirs[(i % 8 + 8) % 8];
    }

    private void FixedUpdate()
    {
        if (_dead || _health.IsDead) return;
        float dt = Time.fixedDeltaTime;
        _cooldown -= dt;
        _skillCooldown -= dt;
        if (_health.Hp < _lastHp) _hurtUntil = Time.time + 0.3f;
        _lastHp = _health.Hp;
        if (_health.Stunned) return;
        if (_actionTimer > 0f) { UpdateAction(dt); return; }

        Vector2 pos = _body.position;
        float dist = float.PositiveInfinity;
        if (_player != null && (_playerHealth == null || !_playerHealth.IsDead))
            dist = Vector2.Distance(pos, _player.position);
        if (!_chasing && dist < detectRadius) _chasing = true;
        else if (_chasing && dist > loseRadius) _chasing = false;

        Vector2 v = Vector2.zero;
        if (_chasing)
        {
            if (_skillCooldown <= 0f && dist <= skillRadius + 1f) { Begin(true); return; }
            if (dist <= attackRange) { if (_cooldown <= 0f) Begin(false); }
            else v = ((Vector2)_player.position - pos).normalized * chaseSpeed;
        }
        else v = Wander(pos, dt);
        _body.linearVelocity = v;

        if (Time.time < _hurtUntil) { Show("dano", (int)((0.3f - (_hurtUntil - Time.time)) / 0.1f), _player != null && _player.position.x < pos.x); return; }
        if (v.sqrMagnitude > 0.01f)
        {
            _dir = Dir8(v);
            _animT += dt * walkFps;
            Show("andar_" + _dir, (int)_animT % 2);
        }
        else Show("parado_" + _dir, 0);
    }

    private Vector2 Wander(Vector2 pos, float dt)
    {
        _wanderTimer -= dt;
        if (_wanderTimer <= 0f)
        {
            _wanderTarget = Random.value < 0.5f ? pos : _home + Random.insideUnitCircle * wanderRadius;
            _wanderTimer = Random.Range(2f, 5f);
        }
        Vector2 to = _wanderTarget - pos;
        return to.magnitude < 0.2f ? Vector2.zero : to.normalized * wanderSpeed;
    }

    private void Begin(bool skill)
    {
        _skill = skill;
        _actionLength = _actionTimer = skill ? skillDuration : attackDuration;
        _hitDone = false;
        if (skill) _skillCooldown = skillCooldown;
        else _cooldown = attackCooldown + attackDuration;
        _body.linearVelocity = Vector2.zero;
    }

    private void UpdateAction(float dt)
    {
        _actionTimer -= dt;
        _body.linearVelocity = Vector2.zero;
        float k = 1f - _actionTimer / _actionLength;
        bool left = _player != null && _player.position.x < transform.position.x;
        if (_skill)
        {
            Show("habilidade", (int)(k * 4f), left);
            if (!_hitDone && k >= 0.55f)
            {
                _hitDone = true;
                // Lanças de sangue em volta dele: acerta quem estiver no raio.
                if (_playerHealth != null && Vector2.Distance(_body.position, _player.position) <= skillRadius)
                    Combat.Apply(_stats, _playerHealth, _body.position, 5f, skillDamageMult, true);
                SkillFx.Ring(_body.position, skillRadius);
            }
        }
        else
        {
            // O golpe da folha é para a direita: espelha quando o alvo está à esquerda.
            Show("ataque", (int)(k * 6f), left);
            if (!_hitDone && k >= 0.5f)
            {
                _hitDone = true;
                if (_playerHealth != null && Vector2.Distance(_body.position, _player.position) <= attackRange + 0.4f)
                    Combat.Apply(_stats, _playerHealth, _body.position, 3f);
            }
        }
        if (_actionTimer <= 0f) Show("parado_" + _dir, 0);
    }

    private MonsterDrops _drops;

    private void OnDied(Health h)
    {
        // Drops (ainda não definidos: lista vazia no MonsterDrops).
        var dropBag = _player != null ? _player.GetComponent<Inventory>() : null;
        if (_drops != null) _drops.Roll(dropBag);
        var ps = _player != null ? _player.GetComponent<CharacterStats>() : null;
        if (ps != null)
        {
            ps.GainExp((baseExp + mvpExp) * ExpTable.Rate);
            ps.GainJobExp(jobExp * ExpTable.Rate);
        }
        DrakeBoss.Anunciar("MVP! Lord of Death derrotado");
        StartCoroutine(DieAndRespawn(true, respawnDelay));
        SaveGame.Save();
    }

    private IEnumerator DieAndRespawn(bool animate, float wait)
    {
        _dead = true;
        _body.linearVelocity = Vector2.zero;
        _body.simulated = false;
        if (_collider != null) _collider.enabled = false;
        _chasing = false;
        _actionTimer = 0f;
        if (animate)
        {
            Show("morte", 0);
            yield return new WaitForSeconds(3f);
        }
        _sprite.enabled = false;
        for (RespawnIn = wait; RespawnIn > 0f; RespawnIn -= Time.deltaTime) yield return null;
        RespawnIn = 0f;
        transform.position = _home;
        _health.Restore();
        _lastHp = _health.Hp;
        _sprite.enabled = true;
        Show("parado_frente", 0);
        _body.simulated = true;
        if (_collider != null) _collider.enabled = true;
        _dead = false;
    }

    // ── Jogo salvo (o primeiro da cena) ──
    private static int _pendingHp;
    private static float _pendingRespawn;

    public static void Capture(out int hpNow, out float respawnIn)
    {
        hpNow = 0; respawnIn = 0f;
        if (All.Count == 0) return;
        var b = All[0];
        if (b._dead) respawnIn = b.RespawnIn > 0f ? b.RespawnIn : b.respawnDelay;
        else hpNow = b._health.Hp;
    }

    public static void Restore(int hpNow, float respawnIn)
    {
        _pendingHp = hpNow; _pendingRespawn = respawnIn;
        if (All.Count > 0) All[0].ApplyPending();
    }

    private void ApplyPending()
    {
        if (_pendingRespawn > 0f)
        {
            StopAllCoroutines();
            StartCoroutine(DieAndRespawn(false, _pendingRespawn));
        }
        else if (_pendingHp > 0 && !_dead)
        {
            _health.Restore(_pendingHp);
            _lastHp = _health.Hp;
        }
        _pendingHp = 0; _pendingRespawn = 0f;
    }

    // O desenho fica sempre em cima do corpo (vida e colisão): se algo o deslocar, volta.
    private void LateUpdate()
    {
        if (_sprite == null) return;
        var t = _sprite.transform;
        if (t != transform && t.localPosition != Vector3.zero)
        {
            if (!_warnedOffset) { _warnedOffset = true; Debug.LogWarning($"[{name}] desenho estava deslocado {t.localPosition} (pai: {t.parent?.name}); voltou ao corpo.", this); }
            t.localPosition = Vector3.zero;
        }
    }
    private bool _warnedOffset;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(0.8f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, skillRadius);
    }
}
