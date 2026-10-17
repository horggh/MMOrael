using UnityEngine;

/// Escolhe e toca os quadros do personagem conforme direção e ação, com a skin em uso
/// (SkinDB). As folhas ficam em Resources e são fatiadas em tempo de execução.
[RequireComponent(typeof(SpriteRenderer))]
public class KnightAnimator : MonoBehaviour
{
    public float walkFps = 8f;
    public float runFps = 12f;
    [Tooltip("Quadros por segundo da animação de morte.")]
    public float deathFps = 6f;

    private SpriteRenderer _sr;
    private PlayerController2D _ctrl;
    private Health _health;
    private enum Dir { Down, Up, Left, Right }
    private Dir _dir = Dir.Down;
    private float _t, _deathT;
    private SkinDef _skin;

    /// A skin tem animação de morte (o controlador então não deixa o personagem transparente).
    public bool HasDeath => _skin != null && _skin.Get("death") != null;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _ctrl = GetComponent<PlayerController2D>();
        _health = GetComponent<Health>();
        // O Animator antigo trocaria o sprite por cima deste.
        var old = GetComponent<Animator>();
        if (old != null) old.enabled = false;
        _skin = SkinDB.Current;
    }

    private void OnEnable() => SkinDB.Changed += OnSkinChanged;
    private void OnDisable() => SkinDB.Changed -= OnSkinChanged;
    private void OnSkinChanged() => _skin = SkinDB.Current;

    private void LateUpdate()
    {
        if (_ctrl == null || _skin == null) return;

        if (_health != null && _health.IsDead)
        {
            var death = _skin.Get("death");
            if (death != null)
            {
                // Toca uma vez e fica no último quadro até renascer.
                _deathT += Time.deltaTime * deathFps;
                Show(death, Mathf.Min((int)_deathT, death.sprites.Length - 1), _dir == Dir.Left);
                return;
            }
        }
        else _deathT = 0f;

        if (_ctrl.Attacking)
        {
            Vector2 ad = _ctrl.AttackDir;
            // Ataque na direção do golpe; sem a de frente/costas, usa a de perfil.
            SkinAnim atk = null;
            bool flip = false;
            if (Mathf.Abs(ad.y) > Mathf.Abs(ad.x))
                atk = _skin.Get(ad.y > 0f ? "attack_up" : "attack_down");
            if (atk == null)
            {
                atk = _skin.Get("attack_side");
                flip = atk != null && (ad.x < 0f) != atk.facesLeft;
            }
            if (atk != null)
            {
                Show(atk, Mathf.Min(atk.sprites.Length - 1, (int)(_ctrl.AttackProgress * atk.sprites.Length)), flip);
                return;
            }
        }

        Vector2 v = _ctrl.Velocity;
        bool moving = v.sqrMagnitude > 0.01f;
        if (moving)
        {
            _dir = Mathf.Abs(v.x) >= Mathf.Abs(v.y)
                ? (v.x < 0f ? Dir.Left : Dir.Right)
                : (v.y < 0f ? Dir.Down : Dir.Up);
        }

        bool side = _dir == Dir.Left || _dir == Dir.Right;
        if (moving && _ctrl.Running)
        {
            var run = side ? _skin.Get("run_side") : _dir == Dir.Up ? _skin.Get("run_up") : null;
            if (run != null)
            {
                Loop(run, runFps, side && (_dir == Dir.Left) != run.facesLeft);
                return;
            }
        }

        var set = _dir == Dir.Down ? _skin.Get("walk_down") : _dir == Dir.Up ? _skin.Get("walk_up") : _skin.Get("walk_side");
        if (set == null) return;
        bool flipWalk = side && (_dir == Dir.Left) != set.facesLeft;

        if (moving) Loop(set, _ctrl.Running ? runFps : walkFps, flipWalk);
        else
        {
            var idle = _dir == Dir.Down ? _skin.Get("idle_down") : null;
            Show(idle ?? set, 0, flipWalk);
        }
    }

    private void Loop(SkinAnim a, float fps, bool flip)
    {
        _t += Time.deltaTime * fps;
        Show(a, (int)_t % a.sprites.Length, flip);
    }

    private void Show(SkinAnim a, int i, bool flip)
    {
        _sr.sprite = a.sprites[Mathf.Clamp(i, 0, a.sprites.Length - 1)];
        _sr.flipX = flip;
    }
}
