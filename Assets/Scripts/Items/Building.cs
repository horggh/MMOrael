using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Construção no mapa (por enquanto a casa), com 5 níveis de obra: 1 = terreno marcado,
/// 2 = andaime, 3 = paredes, 4 = telhado em obra, 5 = pronta. Os sprites ficam em
/// Resources/Buildings/{tipo}_{nível}.png, todos com a mesma tela e a mesma base, para
/// trocar de nível sem a construção sair do lugar.
///
/// Para subir de nível (1 até 4): entregar 10 pedras + 10 madeiras + 10 cordas e esperar
/// 2 minutos. Clicar na obra faz o personagem ir até ela e abre a janela OBRA.
public class Building : MonoBehaviour
{
    public const int Stages = 5;
    public const float BuildTime = 120f;
    public const float Reach = 1.4f;
    /// Receita de cada nível: a mesma do 1 ao 4.
    public static readonly string[] Materials = { "pedra", "madeira", "corda" };
    public static readonly int[] Needed = { 10, 10, 10 };

    // A base ocupa 6 quadrados de largura (e 2 de fundo). Os sprites têm 192 px de largura:
    // 192 / 6 = 32 px por unidade, a mesma escala do personagem e do mapa, então a altura
    // (199 px, ~6,2 unidades) fica proporcional sem distorcer o pixel art.
    private const float PPU = 32f;
    public static readonly Vector2Int Footprint = new Vector2Int(6, 2);

    /// Todas as construções no mapa (para o jogo salvo).
    public static readonly List<Building> All = new List<Building>();
    /// Obra com a janela aberta agora (null = nenhuma).
    public static Building Current { get; private set; }

    public string type;
    public int stage = 1;
    /// Número do interior desta casa (cada uma tem a sala num lugar próprio). Salvo.
    public int interiorId;
    /// Materiais já entregues para o nível atual, na ordem de Materials.
    public int[] delivered = new int[3];
    /// Materiais completos: a obra está andando e timer conta até BuildTime.
    public bool working;
    public float timer;

    public bool Done => stage >= Stages;

    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
    private static Building _pending;   // clicada, esperando o personagem chegar
    private static KitWindow _win;
    private static Transform _player;
    private static Inventory _bag;
    private SpriteRenderer _sr;
    private BoxCollider2D _col;

    // Janela OBRA, em pixels da arte.
    private const int WinW = 340, RowsTop = 66, RowH = 48;
    private const int WinH = RowsTop + 3 * RowH + 30 + 8 + UIKit.Bottom;

    /// Mouse sobre a janela da obra: o clique é da interface, não um passo ou golpe.
    public static bool PointerOverUI
    {
        get
        {
            if (Current == null || _win == null || Mouse.current == null) return false;
            Vector2 m = Mouse.current.position.ReadValue();
            return _win.ScreenRect.Contains(new Vector2(m.x, Screen.height - m.y));
        }
    }

    public static Building Create(string type, Vector2 position, int stage = 1)
    {
        var go = new GameObject("Construção");
        go.transform.position = position;
        var b = go.AddComponent<Building>();
        b.type = type;
        int next = 0;
        foreach (var other in All) if (other != b) next = Mathf.Max(next, other.interiorId);
        b.interiorId = next + 1;
        b.SetStage(stage);
        return b;
    }

    public static Sprite SpriteFor(string type, int stage)
    {
        string key = $"{type}_{stage}";
        if (Cache.TryGetValue(key, out var s) && s != null) return s;
        var tex = Resources.Load<Texture2D>("Buildings/" + key);
        if (tex == null) return null;
        tex.filterMode = FilterMode.Point;
        s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), PPU);
        Cache[key] = s;
        return s;
    }

    private void Awake()
    {
        _sr = gameObject.AddComponent<SpriteRenderer>();
        gameObject.AddComponent<YSortRenderer>();
        // Sólido só na base (a frente da casa); dá para passar por trás do telhado.
        _col = gameObject.AddComponent<BoxCollider2D>();
        _col.size = new Vector2(Footprint.x - 0.2f, Footprint.y - 0.4f);
        _col.offset = new Vector2(0f, (Footprint.y - 0.4f) / 2f);
    }

    public void SetStage(int s)
    {
        stage = Mathf.Clamp(s, 1, Stages);
        _sr.sprite = SpriteFor(type, stage);
        gameObject.name = $"Construção {type} ({stage}/{Stages})";
    }

    /// Progresso do jogo salvo.
    public void Restore(int[] savedDelivered, bool savedWorking, float savedTimer, int savedInterior)
    {
        if (savedInterior > 0) interiorId = savedInterior;
        for (int i = 0; i < delivered.Length; i++)
            delivered[i] = savedDelivered != null && i < savedDelivered.Length ? savedDelivered[i] : 0;
        working = savedWorking && !Done;
        timer = savedTimer;
    }

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void OnDestroy()
    {
        if (Current == this) Current = null;
        if (_pending == this) _pending = null;
    }

    private void Update()
    {
        if (working)
        {
            timer += Time.deltaTime;
            if (timer >= BuildTime) FinishStage();
        }

        if (_player == null)
        {
            var pc = FindAnyObjectByType<PlayerController2D>();
            if (pc == null) return;
            _player = pc.transform;
            _bag = pc.GetComponent<Inventory>();
        }

        var mouse = Mouse.current;
        bool overUI = CombatHUD.PointerOverUI || ItemUI.PointerOverUI || PointerOverUI || Placement.BlocksInput;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && !overUI && Camera.main != null)
        {
            Vector2 p = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
            if (ClickHits(p)) _pending = this;
            else if (_pending == this) _pending = null;   // clicou em outro lugar: desistiu
        }

        float dist = Vector2.Distance(_player.position, _col.ClosestPoint(_player.position));
        if (_pending == this && dist <= Reach)
        {
            _pending = null;
            Open();
        }
        if (Current == this && dist > Reach + 1.5f) Close();
    }

    /// Clique na obra: no desenho (pixel não transparente da caixa) ou na base.
    private bool ClickHits(Vector2 p)
    {
        if (_col.OverlapPoint(p)) return true;
        var b = _sr.bounds;
        if (!b.Contains(new Vector3(p.x, p.y, b.center.z)) || _sr.sprite == null) return false;
        var tex = _sr.sprite.texture;
        if (!tex.isReadable) return true;
        int x = Mathf.FloorToInt((p.x - b.min.x) / b.size.x * tex.width);
        int y = Mathf.FloorToInt((p.y - b.min.y) / b.size.y * tex.height);
        return tex.GetPixel(x, y).a > 0.1f;
    }

    private void FinishStage()
    {
        working = false;
        timer = 0f;
        for (int i = 0; i < delivered.Length; i++) delivered[i] = 0;
        SetStage(stage + 1);
        SaveGame.Save();
    }

    private bool AllDelivered()
    {
        for (int i = 0; i < Materials.Length; i++) if (delivered[i] < Needed[i]) return false;
        return true;
    }

    /// Entrega o que tiver na bolsa, até completar cada material. Completo, a obra começa.
    private void Deliver()
    {
        if (_bag == null || Done || working) return;
        for (int i = 0; i < Materials.Length; i++)
        {
            int give = Mathf.Min(Needed[i] - delivered[i], _bag.Count(Materials[i]));
            if (give <= 0) continue;
            _bag.Remove(Materials[i], give);
            delivered[i] += give;
        }
        if (AllDelivered())
        {
            working = true;
            timer = 0f;
        }
        SaveGame.Save();
    }

    private bool CanDeliverSomething()
    {
        if (_bag == null || Done || working) return false;
        for (int i = 0; i < Materials.Length; i++)
            if (delivered[i] < Needed[i] && _bag.Count(Materials[i]) > 0) return true;
        return false;
    }

    public void Open()
    {
        if (_win == null)
            _win = new KitWindow(40, 110, WinW, WinH, "titulo_obra");
        Current = this;
        _win.show = true;
    }

    public void Close()
    {
        if (Current == this) Current = null;
    }

    private static string Clock(float seconds)
    {
        int s = Mathf.CeilToInt(seconds);
        return $"{s / 60}:{s % 60:00}";
    }

    private void OnGUI()
    {
        // Contagem sobre a obra enquanto ela anda.
        if (working && Camera.main != null)
        {
            Vector3 top = Camera.main.WorldToScreenPoint(new Vector3(transform.position.x, _sr.bounds.max.y + 0.2f, 0f));
            if (top.z >= 0)
            {
                var r = new Rect(top.x - 100, Screen.height - top.y - 10 * UIKit.Px, 200, 10 * UIKit.Px);
                UIKit.Label(r, $"Construindo {Clock(BuildTime - timer)}", 8, UIKit.Gold, TextAnchor.MiddleCenter);
            }
        }

        if (Current != this || _win == null) return;
        DrawWindow();
    }

    private void DrawWindow()
    {
        var w = _win;
        w.DrawFrame();
        string name = type == "casa" ? "Casa" : type;
        UIKit.Label(w.Art(16, 38, WinW - 32, 22), Done ? $"{name} pronta!" : $"{name}  —  nível {stage} de {Stages}", 16, UIKit.Gold);

        if (Done)
        {
            UIKit.Label(w.Art(16, RowsTop, WinW - 32, 20), "A construção está completa.", 14, UIKit.Cream);
            if (UIKit.Button(w.Art(16, RowsTop + 30, 120, 19), "Entrar", true, 10))
            {
                Close();
                HouseInterior.Enter(this);
                return;
            }
        }
        else
        {
            for (int i = 0; i < Materials.Length; i++)
            {
                float y = RowsTop + i * RowH;
                var def = ItemDB.Get(Materials[i]);
                bool full = delivered[i] >= Needed[i];
                UIKit.Slot(w.Art(16, y, UIKit.SlotSize, UIKit.SlotSize), def?.icon, false, false);
                UIKit.Label(w.Art(66, y + 2, 250, 20), def != null ? def.name : Materials[i], 14, full ? UIKit.Gold : UIKit.Cream);
                int have = _bag != null ? _bag.Count(Materials[i]) : 0;
                UIKit.Label(w.Art(66, y + 22, 250, 18), $"Entregue {delivered[i]}/{Needed[i]}    Na bolsa {have}", 12,
                            full ? UIKit.Gold : UIKit.Dim);
            }

            float yb = RowsTop + 3 * RowH + 2;
            if (working)
                UIKit.Label(w.Art(16, yb, WinW - 32, 20), $"Construindo o nível {stage + 1}...  {Clock(BuildTime - timer)}", 14, UIKit.Gold);
            else if (UIKit.Button(w.Art(16, yb, 170, 19), "Entregar materiais", CanDeliverSomething(), 10))
                Deliver();
        }

        w.HandleInput(Event.current);
        if (!w.show) Close();   // X da janela
    }
}
