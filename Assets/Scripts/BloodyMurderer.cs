using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Bloody Murderer (Ragnarok pré-Renewal, mob 1507, rAthena db/pre-re/mob_db.yml): nível 72,
/// 27.521 HP, ATQ 864–1081, DEF 37, MDEF 41, AGI 30 VIT 90 INT 15 DES 52 SOR 12, ataque a
/// cada 0,914 s, alcance 2, agressivo. Os números ficam no Inspector para ajustar.
/// Arte: Art/Sprites/Monsters/SerialKiller/Resources/SerialKiller (Tools/extract_serial_killer.py).
/// Coloque pela janela RaelMMO > Mapa > Monstros (ou arrastando um desenho dele):
/// como o lobo, dá para mover, apagar e editar.
// Clicar no desenho na Scene seleciona o monstro inteiro (não só o desenho, que se
// soltaria do corpo ao mover ou apagar).
[SelectionBase]
public class BloodyMurderer : MonoBehaviour
{
    [Header("Perfil (rAthena pré-Renewal, 1507)")]
    public int level = 72, hp = 27521, atkMin = 864, atkMax = 1081, def = 37, mdef = 41;
    public int str = 0, agi = 30, vit = 90, intl = 15, dex = 52, luk = 12;
    [Tooltip("XP base e de skill que ele dá, antes da taxa de XP (ExpTable.Rate).")]
    public int baseExp = 9742, jobExp = 3559;

    [Header("Comportamento")]
    public float wanderSpeed = 1.1f;
    public float chaseSpeed = 2.9f;
    public float wanderRadius = 3f;
    [Tooltip("Agressivo: persegue quem chegar perto.")]
    public float detectRadius = 6f;
    public float loseRadius = 12f;
    [Tooltip("Alcance 2 do Ragnarok, em quadrados.")]
    public float attackRange = 1.8f;
    [Tooltip("Tempo entre golpes (AttackDelay 914 ms).")]
    public float attackCooldown = 0.914f;
    public float attackDuration = 0.75f;
    [Range(0f, 1f)] public float hitMoment = 0.55f;
    public float respawnDelay = 20f;
    public float walkFps = 7f;

    // Padrão do jogo: 64 px por quadrado da grade (ver Tools/padronizar_64.py).
    private const float PPU = 64f;
    private static readonly string[] Anims = { "andar_frente", "andar_direita", "andar_esquerda", "ataque", "morte" };
    private static readonly int[] Counts = { 3, 3, 3, 5, 1 };
    private static Dictionary<string, Sprite[]> _frames;

    private Rigidbody2D _body;
    private Health _health;
    private CharacterStats _stats;
    private SpriteRenderer _sprite;
    private Collider2D _collider;
    private Transform _player;
    private Health _playerHealth;
    private Vector2 _home, _wanderTarget;
    private float _wanderTimer, _cooldown, _attackTimer, _animT;
    private bool _hitDone, _chasing, _dead;
    private string _walk = "andar_frente";
    // Cada um tem o seu lado em volta do jogador e um tempo de reação: senão todos vêm pelo
    // mesmo caminho na mesma velocidade e andam em bloco.
    private float _slotAngle, _reactAt = -1f, _slotTimer;
    private static readonly List<BloodyMurderer> All = new List<BloodyMurderer>();
    private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    private void OnDisable() => All.Remove(this);

    /// Monta o monstro no objeto (corpo, colisor, arte, vida e atributos).
    public static BloodyMurderer Build(GameObject go)
    {
        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.mass = 20f;
        var col = go.AddComponent<CapsuleCollider2D>();
        col.direction = CapsuleDirection2D.Horizontal;
        col.size = new Vector2(1.3f, 0.45f);
        col.offset = new Vector2(0f, 0.22f);
        var art = new GameObject("Arte");
        art.transform.SetParent(go.transform, false);
        art.AddComponent<SpriteRenderer>();
        art.AddComponent<YSortRenderer>();
        go.AddComponent<CharacterStats>();
        go.AddComponent<Health>();
        var m = go.AddComponent<BloodyMurderer>();
        m.ShowPreview();
        return m;
    }

    private static Sprite[] Load(string name, int count)
    {
        var tex = Resources.Load<Texture2D>("SerialKiller/" + name);
        if (tex == null) { Debug.LogError("[Bloody Murderer] falta SerialKiller/" + name); return new Sprite[0]; }
        tex.filterMode = FilterMode.Point;
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
        for (int i = 0; i < Anims.Length; i++) _frames[Anims[i]] = Load(Anims[i], Counts[i]);
    }

    /// Desenho parado na edição.
    public void ShowPreview()
    {
        var sr = GetComponentInChildren<SpriteRenderer>();
        var s = Load("andar_frente", 3);
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
        _drops = MonsterDrops.Ensure(gameObject, "Bloody Murderer", false);
        FixArt(transform);
        _body = GetComponent<Rigidbody2D>();
        _health = GetComponent<Health>();
        _stats = GetComponent<CharacterStats>();
        _sprite = GetComponentInChildren<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
        _home = transform.position;
        _wanderTarget = _home;
        LoadAll();
        _stats.SetMonster(level, hp, atkMin, atkMax, def, mdef, str, agi, vit, intl, dex, luk);
        _speedJitter = Random.Range(0.85f, 1.1f);
        _slotAngle = Random.Range(0f, Mathf.PI * 2f);
        _cooldown = Random.Range(0f, attackCooldown);
        _health.Died += OnDied;
        Show("andar_frente", 0);
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

    private void FixedUpdate()
    {
        if (_dead || _health.IsDead || _health.Stunned) return;
        float dt = Time.fixedDeltaTime;
        _cooldown -= dt;
        if (_attackTimer > 0f) { UpdateAttack(dt); return; }

        Vector2 pos = _body.position;
        float dist = float.PositiveInfinity;
        if (_player != null && (_playerHealth == null || !_playerHealth.IsDead))
            dist = Vector2.Distance(pos, _player.position);
        if (!_chasing && dist < detectRadius)
        {
            if (_reactAt < 0f) _reactAt = Time.time + Random.Range(0.1f, 0.9f);
            if (Time.time >= _reactAt) { _chasing = true; _reactAt = -1f; }
        }
        else if (_chasing && dist > loseRadius) _chasing = false;

        Vector2 v = _chasing ? Chase(pos, dist) : Wander(pos, dt);
        _body.linearVelocity = v;

        if (v.sqrMagnitude > 0.01f)
        {
            _walk = Mathf.Abs(v.x) > Mathf.Abs(v.y) ? (v.x > 0f ? "andar_direita" : "andar_esquerda") : "andar_frente";
            _animT += dt * walkFps;
            Show(_walk, (int)_animT % 3);
        }
        else Show(_walk, 0);
    }

    private Vector2 Chase(Vector2 pos, float dist)
    {
        if (dist <= attackRange)
        {
            if (_cooldown <= 0f) BeginAttack();
            return Separation(pos) * 0.6f;
        }
        // Vai para o seu lado do jogador (troca de vez em quando), não para o mesmo ponto.
        _slotTimer -= Time.fixedDeltaTime;
        if (_slotTimer <= 0f)
        {
            _slotAngle = Random.Range(0f, Mathf.PI * 2f);
            _slotTimer = Random.Range(2f, 4f);
        }
        Vector2 slot = (Vector2)_player.position + new Vector2(Mathf.Cos(_slotAngle), Mathf.Sin(_slotAngle)) * attackRange * 0.8f;
        Vector2 dir = (slot - pos).normalized + Separation(pos);
        float speed = chaseSpeed * _speedJitter;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized * speed : Vector2.zero;
    }

    /// Empurrão para longe dos outros Bloody Murderers muito perto.
    private Vector2 Separation(Vector2 pos)
    {
        Vector2 push = Vector2.zero;
        foreach (var o in All)
        {
            if (o == this || o._dead) continue;
            Vector2 d = pos - (Vector2)o.transform.position;
            float m = d.magnitude;
            if (m < 1.4f && m > 0.001f) push += d / m * (1.4f - m);
        }
        return push;
    }

    private float _speedJitter = 1f;

    private Vector2 Wander(Vector2 pos, float dt)
    {
        _wanderTimer -= dt;
        if (_wanderTimer <= 0f)
        {
            _wanderTarget = Random.value < 0.5f ? pos : _home + Random.insideUnitCircle * wanderRadius;
            _wanderTimer = Random.Range(1.5f, 4f);
        }
        Vector2 to = _wanderTarget - pos;
        return to.magnitude < 0.2f ? Vector2.zero : to.normalized * wanderSpeed;
    }

    private void BeginAttack()
    {
        _attackTimer = attackDuration;
        _hitDone = false;
        _cooldown = attackCooldown + attackDuration;
        _body.linearVelocity = Vector2.zero;
    }

    private void UpdateAttack(float dt)
    {
        _attackTimer -= dt;
        _body.linearVelocity = Vector2.zero;
        float k = 1f - _attackTimer / attackDuration;
        // O golpe da folha é para a direita: espelha quando o alvo está à esquerda.
        bool left = _player != null && _player.position.x < transform.position.x;
        Show("ataque", (int)(k * 5f), left);
        if (!_hitDone && k >= hitMoment)
        {
            _hitDone = true;
            if (_playerHealth != null && Vector2.Distance(_body.position, _player.position) <= attackRange + 0.4f)
                Combat.Apply(_stats, _playerHealth, _body.position, 2f);
        }
        if (_attackTimer <= 0f) Show(_walk, 0);
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
            ps.GainExp(baseExp * ExpTable.Rate);
            ps.GainJobExp(jobExp * ExpTable.Rate);
        }
        StartCoroutine(DieAndRespawn());
    }

    private IEnumerator DieAndRespawn()
    {
        _dead = true;
        _body.linearVelocity = Vector2.zero;
        _body.simulated = false;
        if (_collider != null) _collider.enabled = false;
        _chasing = false;
        _attackTimer = 0f;
        Show("morte", 0);
        yield return new WaitForSeconds(2f);
        yield return Fade(1f, 0f, 0.8f);
        yield return new WaitForSeconds(respawnDelay);
        transform.position = _home;
        _health.Restore();
        Show("andar_frente", 0);
        yield return Fade(0f, 1f, 0.6f);
        _body.simulated = true;
        if (_collider != null) _collider.enabled = true;
        _dead = false;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            var c = _sprite.color; c.a = Mathf.Lerp(from, to, t / duration); _sprite.color = c;
            yield return null;
        }
        var e = _sprite.color; e.a = to; _sprite.color = e;
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
    }
}
