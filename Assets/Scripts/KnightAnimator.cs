using UnityEngine;

/// Escolhe e toca os quadros do cavaleiro conforme direção e ação.
/// As folhas ficam em Resources/Characters e são fatiadas em tempo de execução.
[RequireComponent(typeof(SpriteRenderer))]
public class KnightAnimator : MonoBehaviour
{
    public float walkFps = 8f;
    public float runFps = 12f;

    private const int FW = 64, FH = 48, PPU = 32;
    private Sprite[] _down, _side, _up, _run, _attack;
    private SpriteRenderer _sr;
    private PlayerController2D _ctrl;
    private enum Dir { Down, Up, Left, Right }
    private Dir _dir = Dir.Down;
    private float _t;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _ctrl = GetComponent<PlayerController2D>();
        // O Animator antigo trocaria o sprite por cima deste.
        var old = GetComponent<Animator>();
        if (old != null) old.enabled = false;

        _down = Load("cav_walk_down");
        _side = Load("cav_walk_side");
        _up = Load("cav_walk_up");
        _run = Load("cav_run_side");
        _attack = Load("cav_attack_side");
    }

    private static Sprite[] Load(string name)
    {
        var tex = Resources.Load<Texture2D>($"Characters/{name}");
        if (tex == null) { Debug.LogError($"[KnightAnimator] falta Resources/Characters/{name}"); return new Sprite[0]; }
        tex.filterMode = FilterMode.Point;
        int n = tex.width / FW;
        var frames = new Sprite[n];
        // Pivô nos pés (2px acima da base), igual ao alinhamento da extração.
        var pivot = new Vector2(0.5f, 2f / FH);
        for (int i = 0; i < n; i++)
            frames[i] = Sprite.Create(tex, new Rect(i * FW, 0, FW, FH), pivot, PPU, 0, SpriteMeshType.FullRect);
        return frames;
    }

    private void LateUpdate()
    {
        if (_ctrl == null) return;

        if (_ctrl.Attacking)
        {
            // Ataque só existe de perfil: vira para o lado do mouse.
            _sr.flipX = _ctrl.AttackDir.x < 0f;   // arte original olha para a direita
            Play(_attack, Mathf.Min(_attack.Length - 1, (int)(_ctrl.AttackProgress * _attack.Length)));
            return;
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
        if (moving && _ctrl.Running && side)
        {
            _sr.flipX = _dir == Dir.Left;         // corrida olha para a direita
            Loop(_run, runFps);
            return;
        }

        Sprite[] set = _dir == Dir.Down ? _down : _dir == Dir.Up ? _up : _side;
        // A caminhada de perfil foi desenhada olhando para a esquerda.
        _sr.flipX = _dir == Dir.Right;

        if (moving) Loop(set, _ctrl.Running ? runFps : walkFps);
        else Play(set, 0);
    }

    private void Loop(Sprite[] set, float fps)
    {
        if (set.Length == 0) return;
        _t += Time.deltaTime * fps;
        Play(set, (int)_t % set.Length);
    }

    private void Play(Sprite[] set, int i)
    {
        if (set.Length > 0) _sr.sprite = set[Mathf.Clamp(i, 0, set.Length - 1)];
    }
}
