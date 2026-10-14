using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Interface provisória de teste: vida, números de dano e painel de atributos (tecla C).
public class CombatHUD : MonoBehaviour
{
    public Health player;

    private struct Popup { public Vector3 pos; public string text; public Color color; public float born; }
    private readonly List<Popup> _popups = new List<Popup>();
    private const float PopupLife = 0.9f;

    private CharacterStats _stats;
    private Inventory _bag;

    private void AddPlayerPopup(string text, Color color) => _popups.Add(new Popup
    {
        pos = player.transform.position + Vector3.up * 1.2f,
        text = text,
        color = color,
        born = Time.time,
    });
    private bool _showStats;
    private GUIStyle _label, _popup;

    private void OnEnable() => Combat.Resolved += OnResolved;
    private void OnDisable() => Combat.Resolved -= OnResolved;
    private void Start()
    {
        _bag = player != null ? player.GetComponent<Inventory>() : null;
        if (_bag != null)
        {
            _bag.Healed += n => AddPlayerPopup($"+{n}", new Color(0.4f, 1f, 0.4f));
            _bag.Gained += (id, n) => AddPlayerPopup($"+{n} {ItemDB.Get(id)?.name}", new Color(0.6f, 0.85f, 1f));
        }

        _stats = player != null ? player.GetComponent<CharacterStats>() : null;
        if (_stats == null) return;
        _stats.Changed += leveled =>
        {
            if (!leveled) return;
            _popups.Add(new Popup
            {
                pos = player.transform.position + Vector3.up * 1.6f,
                text = $"Nível {_stats.level}!",
                color = new Color(0.4f, 1f, 0.5f),
                born = Time.time,
            });
        };
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) _showStats = !_showStats;
        _popups.RemoveAll(p => Time.time - p.born > PopupLife);
    }

    private void OnResolved(Vector3 pos, AttackResult r)
    {
        var p = new Popup { pos = pos + Vector3.up * 1.2f, born = Time.time };
        switch (r.kind)
        {
            case HitKind.Miss: p.text = "Errou"; p.color = new Color(0.8f, 0.8f, 0.8f); break;
            case HitKind.Critical: p.text = r.damage + "!"; p.color = new Color(1f, 0.85f, 0.2f); break;
            default: p.text = r.damage.ToString(); p.color = Color.white; break;
        }
        _popups.Add(p);
    }

    private void OnGUI()
    {
        if (_label == null)
        {
            _label = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _label.normal.textColor = Color.white;
            _popup = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        if (player != null)
        {
            // Painel fixo: HP e SP com números.
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(10, 10, 270, 56), Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawFill(new Rect(16, 16, 200, 18), player.Hp, player.maxHp, new Color(0.8f, 0.15f, 0.15f));
            GUI.Label(new Rect(222, 13, 80, 24), $"{player.Hp}/{player.maxHp}", _label);
            int sp = _stats != null ? _stats.MaxSp : 0;
            DrawFill(new Rect(16, 40, 200, 14), sp, sp, new Color(0.2f, 0.45f, 0.9f));
            GUI.Label(new Rect(222, 35, 80, 24), $"{sp}/{sp}", _label);

            // Barra sob os pés do personagem, como no Ragnarok: sempre à vista no meio da luta.
            Vector3 feet = Camera.main.WorldToScreenPoint(player.transform.position);
            DrawFill(new Rect(feet.x - 22, Screen.height - feet.y + 4, 44, 5), player.Hp, player.maxHp,
                     player.Hp * 3 < player.maxHp ? new Color(1f, 0.3f, 0.2f) : new Color(0.3f, 0.9f, 0.3f));
        }
        GUI.Label(new Rect(14, 70, 400, 24), "I: inventário    C: atributos    1–0: atalhos", _label);

        foreach (var wolf in FindObjectsByType<WolfAI>(FindObjectsSortMode.None))
        {
            var h = wolf.GetComponent<Health>();
            if (h == null || h.IsDead || h.Hp == h.maxHp) continue;
            Vector3 s = Camera.main.WorldToScreenPoint(wolf.transform.position + Vector3.up * 1.4f);
            DrawBar(new Rect(s.x - 25, Screen.height - s.y, 50, 6), h, new Color(0.85f, 0.6f, 0.15f));
        }

        foreach (var p in _popups)
        {
            float t = (Time.time - p.born) / PopupLife;
            Vector3 s = Camera.main.WorldToScreenPoint(p.pos + Vector3.up * t * 0.8f);
            _popup.normal.textColor = new Color(p.color.r, p.color.g, p.color.b, 1f - t);
            GUI.Label(new Rect(s.x - 50, Screen.height - s.y - 15, 100, 30), p.text, _popup);
        }

        if (_showStats && _stats != null) DrawStats();
        else PointerOverUI = false;
    }

    private Rect _statsWin = new Rect(16, 100, 330, 330);

    /// Verdadeiro com o mouse sobre a janela: o clique é da interface, não um golpe.
    public static bool PointerOverUI { get; private set; }

    private void DrawStats()
    {
        PointerOverUI = _statsWin.Contains(Event.current.mousePosition);
        _statsWin = GUI.Window(1, _statsWin, StatsWindow, "Atributos");
        // Mantém a janela dentro da tela mesmo se arrastada para fora.
        _statsWin.x = Mathf.Clamp(_statsWin.x, 0, Screen.width - _statsWin.width);
        _statsWin.y = Mathf.Clamp(_statsWin.y, 0, Screen.height - _statsWin.height);
    }

    private void StatsWindow(int id)
    {
        var s = _stats;
        float x = 14, y = 26;
        GUI.Label(new Rect(x, y, 300, 22), $"Nível {s.level}    XP {s.exp}/{s.ExpToNext}", _label);
        y += 24;
        GUI.Label(new Rect(x, y, 300, 22), $"Pontos disponíveis: {s.statPoints}", _label);
        y += 30;

        string[] derived =
        {
            $"ATQ {s.Atk}", $"ESQUIVA {s.Flee}", $"HP {s.MaxHp}  DEF {s.Def}",
            $"SP {s.MaxSp}  ATQ.MÁG {s.Matk}", $"PRECISÃO {s.Hit}", $"CRÍTICO {s.CritChance:0.#}%",
        };

        for (int i = 0; i < 6; i++)
        {
            GUI.Label(new Rect(x, y, 40, 24), CharacterStats.Names[i], _label);
            GUI.Label(new Rect(x + 40, y, 30, 24), s.Get(i).ToString(), _label);

            int cost = s.CostToRaise(i);
            GUI.enabled = s.statPoints >= cost && s.Get(i) < 99;
            if (GUI.Button(new Rect(x + 72, y, 26, 22), "+")) s.TryRaise(i);
            GUI.enabled = true;

            GUI.Label(new Rect(x + 102, y, 30, 24), $"({cost})", _label);
            GUI.Label(new Rect(x + 140, y, 170, 24), derived[i], _label);
            y += 28;
        }

        y += 6;
        GUI.Label(new Rect(x, y, 300, 22), $"Velocidade de ataque {s.AttackSpeed:0.00}x", _label);
        GUI.Label(new Rect(x, y + 22, 300, 22), "(n) = pontos para subir o atributo", _label);

        // Só a faixa do título arrasta; o resto da janela continua clicável.
        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    private static void DrawBar(Rect r, Health h, Color color)
    {
        if (h != null) DrawFill(r, h.Hp, h.maxHp, color);
    }

    private static void DrawFill(Rect r, int value, int max, Color color)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = color;
        float f = max > 0 ? (float)value / max : 0f;
        GUI.DrawTexture(new Rect(r.x + 1, r.y + 1, (r.width - 2) * f, r.height - 2), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
