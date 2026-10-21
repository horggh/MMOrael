using UnityEngine;

/// Escolhe e toca os quadros do personagem conforme direção e ação, com a skin em uso
/// (SkinDB). As folhas ficam em Resources e são fatiadas em tempo de execução.
///
/// Cada animação pode existir por direção ("walk_left", "attack_right"...) ou só de perfil
/// ("walk_side", espelhada para o outro lado). Com a skin modular, avisa as camadas de arma
/// e escudo (ModularLayers) de qual animação/quadro está mostrando.
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
    private ModularLayers _layers;
    private enum Dir { Down, Up, Left, Right }
    private Dir _dir = Dir.Down;
    // Direção em 8 (com diagonais), para skins que têm "walk_downleft" etc.; as outras usam _dir.
    private string _dir8 = "down";
    private static readonly string[] Dir8Names = { "right", "upright", "up", "upleft", "left", "downleft", "down", "downright" };

    private static string Dir8(Vector2 v)
    {
        float ang = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        int i = Mathf.RoundToInt(ang / 45f);
        return Dir8Names[(i % 8 + 8) % 8];
    }
    private float _t, _deathT;
    private SkinDef _skin;

    /// Animação e quadro mostrados agora ("walk_left", 2): as camadas seguem isto.
    public string CurrentKey { get; private set; }
    public int CurrentFrame { get; private set; }

    /// A skin tem animação de morte (o controlador então não deixa o personagem transparente).
    public bool HasDeath => _skin != null && (_skin.Get("death") != null || _skin.Get("death_down") != null);

    private static readonly string[] DirNames = { "down", "up", "left", "right" };

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _ctrl = GetComponent<PlayerController2D>();
        _health = GetComponent<Health>();
        // O Animator antigo trocaria o sprite por cima deste.
        var old = GetComponent<Animator>();
        if (old != null) old.enabled = false;
        _skin = SkinDB.Current;
        _layers = GetComponent<ModularLayers>();
        if (_layers == null) _layers = gameObject.AddComponent<ModularLayers>();
    }

    private void OnEnable() => SkinDB.Changed += OnSkinChanged;
    private void OnDisable() => SkinDB.Changed -= OnSkinChanged;
    private void OnSkinChanged() => _skin = SkinDB.Current;

    /// Animação "nome" na direção: a da própria direção, se existir; senão a de perfil
    /// (espelhada conforme o lado) ou a sem direção.
    private bool Pick(string name, Dir dir, out SkinAnim anim, out bool flip, out string key)
    {
        flip = false;
        // Primeiro a diagonal (se a skin tiver e a direção pedida for a atual).
        if (DirNames[(int)dir] != _dir8 && _dir8.Contains(DirNames[(int)dir]) || _dir8 == DirNames[(int)dir])
        {
            key = name + "_" + _dir8;
            anim = _skin.Get(key);
            if (anim != null) return true;
        }
        key = name + "_" + DirNames[(int)dir];
        anim = _skin.Get(key);
        if (anim != null) return true;
        if (dir == Dir.Left || dir == Dir.Right)
        {
            key = name + "_side";
            anim = _skin.Get(key);
            if (anim != null) { flip = (dir == Dir.Left) != anim.facesLeft; return true; }
        }
        key = name;
        anim = _skin.Get(key);
        if (anim != null) return true;
        // Só existe de frente (ex.: a morte do cavaleiro montado).
        key = name + "_down";
        anim = _skin.Get(key);
        return anim != null;
    }

    private void LateUpdate()
    {
        if (_ctrl == null || _skin == null) return;

        if (_health != null && _health.IsDead)
        {
            if (Pick("death", _dir, out var death, out bool dflip, out string dkey))
            {
                // Toca uma vez e fica no último quadro até renascer.
                _deathT += Time.deltaTime * deathFps;
                Show(death, dkey, Mathf.Min((int)_deathT, death.sprites.Length - 1), dflip || (dkey == "death" && _dir == Dir.Left));
                return;
            }
        }
        else _deathT = 0f;

        if (_ctrl.Attacking)
        {
            Vector2 ad = _ctrl.AttackDir;
            Dir adir = Mathf.Abs(ad.y) > Mathf.Abs(ad.x) ? (ad.y > 0f ? Dir.Up : Dir.Down) : (ad.x < 0f ? Dir.Left : Dir.Right);
            // Ataque na direção do golpe; sem a de frente/costas, usa a de perfil.
            bool found = Pick("attack", adir, out var atk, out bool aflip, out string akey);
            if (!found && (adir == Dir.Up || adir == Dir.Down))
                found = Pick("attack", ad.x < 0f ? Dir.Left : Dir.Right, out atk, out aflip, out akey);
            if (found)
            {
                _dir = adir;
                _dir8 = Dir8(ad);
                Show(atk, akey, Mathf.Min(atk.sprites.Length - 1, (int)(_ctrl.AttackProgress * atk.sprites.Length)), aflip);
                return;
            }
        }

        Vector2 v = _ctrl.Velocity;
        bool moving = v.sqrMagnitude > 0.01f;
        if (moving)
        {
            _dir8 = Dir8(v);
            _dir = Mathf.Abs(v.x) >= Mathf.Abs(v.y)
                ? (v.x < 0f ? Dir.Left : Dir.Right)
                : (v.y < 0f ? Dir.Down : Dir.Up);
        }

        if (moving && _ctrl.Running && Pick("run", _dir, out var run, out bool rflip, out string rkey) && rkey != "run")
        {
            Loop(run, rkey, runFps, rflip);
            return;
        }

        if (!Pick("walk", _dir, out var walk, out bool wflip, out string wkey)) return;
        if (moving) Loop(walk, wkey, _ctrl.Running ? runFps : walkFps, wflip);
        else
        {
            // Parado: a pose própria da direção, se a skin tiver (só de frente nas antigas).
            // (A volta para "idle_down" do Pick só vale olhando para baixo.)
            if (Pick("idle", _dir, out var idle, out bool iflip, out string ikey) && ikey != "idle" &&
                (ikey != "idle_down" || _dir == Dir.Down))
                Show(idle, ikey, 0, iflip);
            else Show(walk, wkey, 0, wflip);
        }
    }

    private void Loop(SkinAnim a, string key, float fps, bool flip)
    {
        _t += Time.deltaTime * fps;
        Show(a, key, (int)_t % a.sprites.Length, flip);
    }

    private void Show(SkinAnim a, string key, int i, bool flip)
    {
        i = Mathf.Clamp(i, 0, a.sprites.Length - 1);
        _sr.sprite = a.sprites[i];
        _sr.flipX = flip;
        CurrentKey = key;
        CurrentFrame = i;
        if (_layers != null) _layers.Show(_skin, key, i, flip);
    }
}
