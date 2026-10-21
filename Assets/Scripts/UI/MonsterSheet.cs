using UnityEngine;
using UnityEngine.InputSystem;

/// Ficha do monstro: clique com o botão direito num monstro (ou MVP) para abrir. Mostra nível,
/// vida, ataque, defesas, atributos, precisão, esquiva, a chance de você acertar e de ele
/// acertar você, e os drops (MonsterDrops; ainda vazios).
public class MonsterSheet : MonoBehaviour
{
    private const int WinW = 300, Top = 62, Row = 18;
    private static MonsterSheet _instance;
    private KitWindow _win;
    private Health _target;

    /// Mouse sobre a ficha: o clique é dela, não um passo ou golpe.
    public static bool PointerOverUI
    {
        get
        {
            if (_instance == null || _instance._win == null || !_instance._win.show || Mouse.current == null) return false;
            Vector2 m = Mouse.current.position.ReadValue();
            return _instance._win.ScreenRect.Contains(new Vector2(m.x, Screen.height - m.y));
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (_instance == null) _instance = new GameObject("Ficha do monstro").AddComponent<MonsterSheet>();
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || !mouse.rightButton.wasPressedThisFrame || Camera.main == null) return;
        if (CombatHUD.PointerOverUI || ItemUI.PointerOverUI || Building.PointerOverUI || PointerOverUI) return;
        Vector2 p = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
        var h = MonsterAt(p);
        if (h == null) return;
        _target = h;
        if (_win == null) _win = new KitWindow(Screen.width * 0.5f - 200, 120, WinW, 200, "titulo_vazio");
        _win.show = true;
    }

    /// Monstro sob o ponto: pelo desenho ou pelo corpo; o mais à frente se houver vários.
    private static Health MonsterAt(Vector2 p)
    {
        Health best = null;
        float bestY = float.MaxValue;
        foreach (var h in Health.All)
        {
            if (h == null || h.IsDead) continue;
            var st = h.GetComponent<CharacterStats>();
            if (st == null || !st.IsMonster) continue;
            var sr = h.GetComponentInChildren<SpriteRenderer>();
            var col = h.GetComponent<Collider2D>();
            bool hit = (col != null && col.enabled && col.OverlapPoint(p)) ||
                       (sr != null && sr.enabled && sr.bounds.Contains(new Vector3(p.x, p.y, sr.bounds.center.z)));
            if (hit && h.transform.position.y < bestY) { bestY = h.transform.position.y; best = h; }
        }
        return best;
    }

    private void OnGUI()
    {
        if (_win == null || !_win.show) return;
        if (_target == null) { _win.show = false; return; }
        var st = _target.GetComponent<CharacterStats>();
        var drops = _target.GetComponent<MonsterDrops>();
        int nDrops = drops != null ? drops.drops.Count : 0;
        _win.h = Top + 8 * Row + 30 + Mathf.Max(1, nDrops) * 20 + 12 + UIKit.Bottom;

        GUI.depth = -2;
        var w = _win;
        w.DrawFrame();
        string name = drops != null && !string.IsNullOrEmpty(drops.displayName) ? drops.displayName : _target.name;
        bool mvp = drops != null && drops.mvp;
        UIKit.Label(w.Art(16, 36, WinW - 32, 22), mvp ? name + "  ·  MVP" : name, 16, UIKit.Gold);

        float y = Top;
        void Line(string a, string va, string b = null, string vb = null)
        {
            UIKit.Label(w.Art(16, y, 70, Row), a, 12, UIKit.Dim);
            UIKit.Label(w.Art(80, y, 70, Row), va, 12, UIKit.Cream);
            if (b != null)
            {
                UIKit.Label(w.Art(156, y, 70, Row), b, 12, UIKit.Dim);
                UIKit.Label(w.Art(220, y, 70, Row), vb, 12, UIKit.Cream);
            }
            y += Row;
        }

        Line("Nível", st.level.ToString(), "Vida", $"{_target.Hp:N0}");
        Line("Vida máx.", $"{_target.maxHp:N0}", "ATQ", $"{st.AtkMin:N0}~{st.Atk:N0}");
        Line("DEF", st.HardDef.ToString(), "MDEF", st.Mdef.ToString());
        Line("FOR", st.Total(0).ToString(), "AGI", st.Total(1).ToString());
        Line("VIT", st.Total(2).ToString(), "INT", st.Total(3).ToString());
        Line("DES", st.Total(4).ToString(), "SOR", st.Total(5).ToString());
        Line("Precisão", st.Hit.ToString(), "Esquiva", st.Flee.ToString());

        // Chances contra o personagem (80 + precisão − esquiva, entre 5% e 95%).
        var pc = PlayerController2D.Main;
        var me = pc != null ? pc.GetComponent<CharacterStats>() : null;
        if (me != null)
        {
            int youHit = Mathf.Clamp(80 + me.Hit - st.Flee, 5, 95);
            int itHits = Mathf.Clamp(80 + st.Hit - me.Flee, 5, 95);
            Line("Você acerta", youHit + "%", "Ele acerta", itHits + "%");
        }
        else y += Row;

        y += 8;
        UIKit.Label(w.Art(16, y, WinW - 32, 20), "Drops", 14, UIKit.Gold);
        y += 22;
        if (nDrops == 0)
        {
            UIKit.Label(w.Art(16, y, WinW - 32, 20), "Nenhum drop definido ainda.", 12, UIKit.Dim);
        }
        else
        {
            foreach (var d in drops.drops)
            {
                var def = ItemDB.Get(d.item);
                UIKit.Slot(w.Art(16, y, 18, 18), def?.icon, false, false);
                string label = (def != null ? def.name : d.item) + (d.amount > 1 ? $" x{d.amount}" : "");
                UIKit.Label(w.Art(40, y, WinW - 120, 18), label, 12, UIKit.Cream);
                UIKit.Label(w.Art(WinW - 80, y, 64, 18), d.chance.ToString("0.##") + "%", 12, UIKit.Gold, TextAnchor.MiddleRight);
                y += 20;
            }
        }

        w.HandleInput(Event.current);
    }
}
