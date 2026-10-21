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
    private SkillUser _su;

    private void AddPlayerPopup(string text, Color color) => _popups.Add(new Popup
    {
        pos = player.transform.position + Vector3.up * 1.2f,
        text = text,
        color = color,
        born = Time.time,
    });
    private GUIStyle _label, _popup;
    private float _resetConfirmUntil;
    private float _helpY = 70f;   // linha de atalhos logo abaixo da barra de informações

    // Janela de atributos no estilo do kit (pixels da arte).
    // Textos no mesmo padrão da janela de habilidades: 16 para nomes, 14 para valores.
    private const int StatsW = 380, StatsRows = 102, StatsRowH = 26;
    private readonly KitWindow _statsWin = new KitWindow(40, 110, StatsW, StatsRows + 6 * StatsRowH + 57, "titulo_atributos");

    private void OnEnable()
    {
        Combat.Resolved += OnResolved;
        DrakeBoss.Announce += OnAnnounce;
    }

    private void OnDisable()
    {
        Combat.Resolved -= OnResolved;
        DrakeBoss.Announce -= OnAnnounce;
    }

    private void OnAnnounce(string text) => AddPlayerPopup(text, new Color(1f, 0.82f, 0.25f));

    private GUIStyle _bossName;

    /// Barra de vida do boss no alto da tela, centralizada, quando ele está perto.
    private void DrawBossBar()
    {
        if (player == null) return;
        var bosses = new System.Collections.Generic.List<(Health h, string name, bool dead)>();
        foreach (var b in DrakeBoss.All) bosses.Add((b.GetComponent<Health>(), DrakeBoss.DisplayName, b.IsDead));
        foreach (var b in LordOfDeath.All) bosses.Add((b.GetComponent<Health>(), "Lord of Death", b.IsDead));
        foreach (var (h, bossName, dead) in bosses)
        {
            if (dead || h == null || Vector2.Distance(h.transform.position, player.transform.position) > 16f) continue;
            float w = Screen.width * 0.36f, bh = Mathf.Max(8f, Screen.height * 0.016f);
            float x = (Screen.width - w) / 2f, y = _helpY + Screen.height * 0.05f;
            _bossName ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerCenter };
            _bossName.fontSize = Mathf.Max(11, Mathf.RoundToInt(Screen.height * 0.022f));
            var nameRect = new Rect(x, y - _bossName.fontSize * 1.6f, w, _bossName.fontSize * 1.5f);
            _bossName.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(nameRect.x + 1, nameRect.y + 1, w, nameRect.height), $"{bossName}  ·  MVP", _bossName);
            _bossName.normal.textColor = new Color(1f, 0.82f, 0.4f);
            GUI.Label(nameRect, $"{bossName}  ·  MVP", _bossName);
            DrawFill(new Rect(x, y, w, bh), h.Hp, h.maxHp, new Color(0.8f, 0.15f, 0.12f));
            _bossName.fontSize = Mathf.Max(9, Mathf.RoundToInt(bh * 0.85f));
            _bossName.alignment = TextAnchor.MiddleCenter;
            _bossName.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y, w, bh), $"{h.Hp:#,0} / {h.maxHp:#,0}", _bossName);
            _bossName.alignment = TextAnchor.LowerCenter;
            break;
        }
    }
    private void Start()
    {
        _su = player != null ? player.GetComponent<SkillUser>() : null;
        if (_su != null) _su.Used += d => AddPlayerPopup(d.name + "!", new Color(1f, 0.6f, 0.2f));

        _bag = player != null ? player.GetComponent<Inventory>() : null;
        if (_bag != null)
        {
            _bag.Healed += n => AddPlayerPopup($"+{n}", new Color(0.4f, 1f, 0.4f));
            _bag.Gained += (id, n) => AddPlayerPopup($"+{n} {ItemDB.Get(id)?.name}", new Color(0.6f, 0.85f, 1f));
            _bag.Notice += text => AddPlayerPopup(text, new Color(1f, 0.55f, 0.5f));
            _bag.MoneyGained += n => AddPlayerPopup("+" + Inventory.MoneyText(n), new Color(1f, 0.85f, 0.3f));
        }

        _stats = player != null ? player.GetComponent<CharacterStats>() : null;
        if (_stats == null) return;
        _stats.JobLeveled += ups => AddPlayerPopup($"Nível de skill {_stats.jobLevel}! +{ups} ponto" + (ups > 1 ? "s" : ""),
                                                    new Color(0.55f, 0.85f, 1f));
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
        if (Keyboard.current != null && !UIKit.Typing && Keyboard.current.cKey.wasPressedThisFrame) _statsWin.show = !_statsWin.show;
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
        if (r.skill && r.kind != HitKind.Miss) p.color = new Color(1f, 0.55f, 0.15f);
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
            // Barra de informações no topo (arte em Resources/UI/Status).
            int sp = _su != null ? _su.Sp : 0, maxSp = _su != null ? _su.maxSp : 0;
            int used = 0;
            if (_bag != null) foreach (var st in _bag.slots) if (st != null && !st.Empty) used++;
            // Espaço até o botão DEV no canto direito (a barra encolhe para caber).
            float margin = Screen.height * 0.01f;
            float room = Screen.width - margin * 2 - Screen.height * 0.09f;
            var bar = StatusBar.Draw(new Vector2(margin, margin), room, player.Hp, player.maxHp, sp, maxSp,
                                     _stats != null ? _stats.level : 1, _stats != null ? _stats.exp : 0,
                                     _stats != null ? _stats.ExpToNext : 0,
                                     _stats != null ? _stats.jobExp : 0, _stats != null ? _stats.JobExpToNext : 0,
                                     _bag != null ? _bag.money : 0,
                                     used, Inventory.Size);
            _helpY = bar.yMax + 4;
            float buffsBottom = DrawBuffs(bar, room + margin);
            if (buffsBottom > _helpY) _helpY = buffsBottom + 4;

            // Barra sob os pés do personagem, como no Ragnarok: sempre à vista no meio da luta.
            Vector3 feet = Camera.main.WorldToScreenPoint(player.transform.position);
            DrawFill(new Rect(feet.x - 22, Screen.height - feet.y + 4, 44, 5), player.Hp, player.maxHp,
                     player.Hp * 3 < player.maxHp ? new Color(1f, 0.3f, 0.2f) : new Color(0.3f, 0.9f, 0.3f));
        }
        GUI.Label(new Rect(14, _helpY, 500, 24), "I: inventário    E: equipamento    C: atributos    K: habilidades    1–0: atalhos", _label);

        DrawBossBar();

        DrawMonsterBars();

        foreach (var p in _popups)
        {
            float t = (Time.time - p.born) / PopupLife;
            Vector3 s = Camera.main.WorldToScreenPoint(p.pos + Vector3.up * t * 0.8f);
            _popup.normal.textColor = new Color(p.color.r, p.color.g, p.color.b, 1f - t);
            GUI.Label(new Rect(s.x - 50, Screen.height - s.y - 15, 100, 30), p.text, _popup);
        }

        WindowStack.Report("stats", _statsWin.show && _stats != null, _statsWin.ScreenRect);
        if (Event.current.type == EventType.MouseDown) WindowStack.Focus(Event.current.mousePosition);
        // Atributos na frente: este OnGUI é desenhado por cima do das outras janelas.
        GUI.depth = WindowStack.IsFront("stats") ? -1 : 1;
        if (_statsWin.show && _stats != null) WindowStack.Draw("stats", DrawStats);
        PointerOverUI = _statsWin.show && _statsWin.ScreenRect.Contains(Event.current.mousePosition);
    }

    /// Verdadeiro com o mouse sobre a janela: o clique é da interface, não um golpe.
    public static bool PointerOverUI { get; private set; }

    private void DrawStats()
    {
        var s = _stats;
        var w = _statsWin;
        w.DrawFrame();

        UIKit.Label(w.Art(16, 38, 160, 22), $"Nível {s.level}", 16, UIKit.Gold);
        UIKit.Label(w.Art(180, 40, StatsW - 196, 20), (s.ExpToNext > 0 ? $"XP {s.exp}/{s.ExpToNext}" : "XP máximo"), 14, UIKit.Cream, TextAnchor.MiddleRight);
        UIKit.Label(w.Art(16, 64, StatsW - 32, 20), $"Pontos disponíveis: {s.statPoints}", 14, s.statPoints > 0 ? UIKit.Gold : UIKit.Cream);

        // Reset de build: dois cliques (o primeiro pede confirmação por 3 s).
        bool confirming = Time.unscaledTime < _resetConfirmUntil;
        if (UIKit.Button(w.Art(StatsW - 16 - 96, 62, 96, 19), confirming ? "Confirmar?" : "Resetar", true, 9, confirming))
        {
            if (!confirming) _resetConfirmUntil = Time.unscaledTime + 3f;
            else
            {
                _resetConfirmUntil = 0f;
                int back = s.ResetAttributes();
                AddPlayerPopup($"+{back} pontos para redistribuir", new Color(1f, 0.85f, 0.3f));
                SaveGame.Save();
            }
        }
        UIKit.DividerH(w.pos, StatsW, 90);

        string[] derived =
        {
            $"ATQ {s.StatusAtk} + {s.WeaponAtk}", $"ESQUIVA {s.Flee} + {s.PerfectDodge:0}", $"HP {s.MaxHp}  DEF {s.HardDef} + {s.SoftDef}",
            $"SP {s.MaxSp}  ATQ.MÁG {s.MatkMin}~{s.MatkMax}", $"PRECISÃO {s.Hit}", $"CRÍTICO {s.CritChance:0.#}%",
        };

        for (int i = 0; i < 6; i++)
        {
            float y = StatsRows + i * StatsRowH;
            UIKit.Label(w.Art(16, y, 40, 22), CharacterStats.Names[i], 16, UIKit.Gold);
            UIKit.Label(w.Art(58, y, 50, 22), s.Bonus(i) > 0 ? $"{s.Get(i)}+{s.Bonus(i)}" : s.Get(i).ToString(), 16, UIKit.Cream);

            int cost = s.CostToRaise(i);
            if (UIKit.Button(w.Art(110, y + 1, 20, 19), "+", s.statPoints >= cost && s.Get(i) < 99)) s.TryRaise(i);
            UIKit.Label(w.Art(136, y, 34, 22), $"({cost})", 14, UIKit.Dim);
            UIKit.Label(w.Art(176, y, StatsW - 192, 22), derived[i], 14, UIKit.Cream);
        }

        float yEnd = StatsRows + 6 * StatsRowH + 4;
        UIKit.Label(w.Art(16, yEnd, StatsW - 32, 20), $"ASPD {s.Aspd}   ({s.AttacksPerSecond:0.00} ataques/s, {ItemDef.WeaponName(s.Weapon)})", 14, UIKit.Cream);
        UIKit.Label(w.Art(16, yEnd + 22, StatsW - 32, 16), "(n) = pontos para subir o atributo", 12, UIKit.Dim);

        w.HandleInput(Event.current);
    }

    /// Monstros comuns: barra pequena logo abaixo dos pés, com a vida restante escrita.
    /// (Os MVPs têm a barra grande no alto da tela.)
    private void DrawMonsterBars()
    {
        var cam = Camera.main;
        if (cam == null) return;
        _hpText ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        foreach (var h in Health.All)
        {
            if (h == null || h.IsDead || !h.gameObject.activeInHierarchy) continue;
            var st = h.GetComponent<CharacterStats>();
            if (st == null || !st.IsMonster || h.GetComponent<DrakeBoss>() != null || h.GetComponent<LordOfDeath>() != null) continue;
            var sr = h.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && (!sr.enabled || sr.color.a < 0.5f)) continue;
            Vector3 s = cam.WorldToScreenPoint(h.transform.position + Vector3.down * 0.12f);
            if (s.z < 0f || s.x < -50 || s.x > Screen.width + 50 || s.y < -20 || s.y > Screen.height + 20) continue;
            float w = Mathf.Max(36f, Screen.height * 0.045f), bh = Mathf.Max(4f, Screen.height * 0.006f);
            var r = new Rect(s.x - w / 2f, Screen.height - s.y, w, bh);
            DrawFill(r, h.Hp, h.maxHp, h.Hp * 3 < h.maxHp ? new Color(0.95f, 0.3f, 0.2f) : new Color(0.85f, 0.6f, 0.15f));
            _hpText.fontSize = Mathf.Max(8, Mathf.RoundToInt(bh * 2f));
            var tr = new Rect(r.x - 20, r.yMax, r.width + 40, _hpText.fontSize + 4);
            _hpText.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(tr.x + 1, tr.y + 1, tr.width, tr.height), h.Hp.ToString("N0"), _hpText);
            _hpText.normal.textColor = Color.white;
            GUI.Label(tr, h.Hp.ToString("N0"), _hpText);
        }
    }

    private GUIStyle _hpText;

    private GUIStyle _buffTime;

    /// Bônus ativos (comidas e bebidas, fogo do Impacto Explosivo): o ícone do item com o tempo
    /// que falta embaixo; mouse em cima mostra o que ele dá. Ao lado da barra do personagem;
    /// sem espaço, logo abaixo dela. Devolve a base da fileira (ou 0 se não há bônus).
    private float DrawBuffs(Rect bar, float right)
    {
        if (_stats == null) return 0f;
        var list = new System.Collections.Generic.List<(Texture2D icon, float left, string tip)>();
        void Add(string item, int amount, float left, string what, System.Func<ItemDef, bool> gives)
        {
            if (amount <= 0 || left <= 0f) return;
            var def = !string.IsNullOrEmpty(item) ? ItemDB.Get(item) : null;
            // Bônus de antes de o item ser guardado (save antigo): o item que dá esse bônus.
            if (def == null) foreach (var d in ItemDB.All) if (gives(d)) { def = d; break; }
            list.Add((def?.icon, left, $"{def?.name ?? "Bônus"}: +{amount} de {what}"));
        }
        Add(_stats.HitBuffItem, _stats.HitBuff, _stats.HitBuffLeft, "precisão", d => d.buffHit > 0);
        Add(_stats.AspdBuffItem, _stats.AspdBuff, _stats.AspdBuffLeft, "ASPD", d => d.buffAspd > 0);
        Add(_stats.AtkBuffItem, _stats.AtkBuff, _stats.AtkBuffLeft, "ATQ", d => d.buffAtk > 0);
        if (_stats.FireBonus)
            list.Add((SkillDB.Get("impacto_explosivo")?.icon, _stats.FireBonusLeft, "Impacto Explosivo: +20% de fogo nos golpes"));
        if (list.Count == 0) return 0f;

        float size = Mathf.Max(24f, bar.height * 0.62f), gap = size * 0.18f;
        float timeH = size * 0.42f;
        float width = list.Count * (size + gap);
        // Ao lado da barra; sem espaço, embaixo, alinhado à esquerda.
        float x = bar.xMax + gap * 2, y = bar.y + (bar.height - size - timeH) / 2f;
        if (x + width > right) { x = bar.x; y = bar.yMax + 4; }
        _buffTime ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold, padding = new RectOffset() };
        _buffTime.fontSize = Mathf.Max(9, Mathf.RoundToInt(timeH * 0.8f));
        Vector2 mouse = Event.current.mousePosition;
        string tip = null;
        foreach (var (icon, left, text) in list)
        {
            var r = new Rect(x, y, size, size);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (icon != null) GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), icon, ScaleMode.ScaleToFit);
            int s = Mathf.CeilToInt(left);
            string t = s >= 60 ? $"{s / 60}:{s % 60:00}" : $"{s}s";
            var tr = new Rect(x - gap / 2f, r.yMax + 1, size + gap, timeH);
            _buffTime.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(tr.x + 1, tr.y + 1, tr.width, tr.height), t, _buffTime);
            _buffTime.normal.textColor = s <= 10 ? new Color(1f, 0.45f, 0.35f) : Color.white;
            GUI.Label(tr, t, _buffTime);
            if (r.Contains(mouse)) tip = text;
            x += size + gap;
        }
        if (tip != null)
        {
            var size2 = GUI.skin.box.CalcSize(new GUIContent(tip));
            GUI.Box(new Rect(mouse.x + 14, mouse.y + 14, size2.x + 8, size2.y + 4), tip);
        }
        return y + size + timeH;
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
