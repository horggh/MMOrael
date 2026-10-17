using UnityEngine;
using UnityEngine.InputSystem;

/// Inventário (I), habilidades (K) e barra de atalhos, com arrastar e soltar entre eles.
/// Barra: clique usa, arrastar para fora remove. Inventário: clique direito usa.
public class ItemUI : MonoBehaviour
{
    public static bool PointerOverUI { get; private set; }

    private const float Slot = 48f, Gap = 4f;
    private const int Cols = 6;

    private Inventory _bag;
    private ActionBar _bar;
    private SkillUser _skills;
    private Equipment _equip;
    private SpriteRenderer _sprite;
    private readonly Win _eq = new Win { r = new Rect(720, 64, 0, 0) };

    // Tela de equipamento desenhada com as peças de Resources/UI/Equipamento.
    // Medidas em pixels da arte; na tela tudo é multiplicado por _px (escala inteira).
    private const int EqW = 406, EqH = 236, EqStatsH = 18, EqTitleH = 40;
    private static readonly Rect EqClose = UIKit.CloseArt(EqW);
    private static readonly Vector2 EqPanelPos = new Vector2(156, 25);
    private static readonly Vector2 EqFeet = new Vector2(82, 166);   // topo do pedestal
    private int _px = 2;
    private Texture2D _tWindow, _tPanel, _tClose, _tSlot, _tBig, _tBigClean, _tAcc, _tAccClean,
                      _tStatAtk, _tStatDef, _tStatHp;

    private class Win { public Rect r; public bool show, moving; public Vector2 grab; }

    // Inventário e habilidades montados com o kit (UIKit), em pixels da arte.
    private const int KitW = 340, KitStep = UIKit.SlotSize + 2, KitTop = 40;
    private const int InvX = (KitW - Cols * KitStep + 2) / 2;
    // Habilidades: ícone 20% maior que o slot comum (42 -> 50) e texto em dobro.
    private const int SkillSlot = 50, SkillRowH = 58, SkillTextX = 16 + SkillSlot + 10;
    // Com mais habilidades que isso, a lista rola (linha a linha, para o pixel art não ficar cortado).
    private const int SkillVisible = 5, ScrollX = KitW - UIKit.Side - 10;
    private int _skFirst;
    private bool _skDragBar;
    private KitWindow _inv, _sk;

    private enum Src { None, Inv, Bar, Skill, Chest, Dev }

    // Ferramenta de desenvolvedor: botão no canto superior direito e janela com todos os itens.
    // Só em builds de desenvolvimento (e no editor).
    // Janela ITENS com abas por tipo; o Chão vem na ordem por tema (FloorTiles.Catalog).
    private const int DevW = 466, DevCols = 9, DevRows = 4, DevTop = 64;
    private const int DevX = (DevW - DevCols * KitStep + 2) / 2;
    private static readonly string[] DevTabs = { "Testes", "Skins", "Equipamentos", "Consumíveis", "Materiais", "Construção", "Chão", "Paredes", "Janelas" };
    private const int DevTestsTab = 0;   // aba sem itens: chaves de teste (DevCheats)
    private const int DevSkinsTab = 1;   // aba sem itens: aparências do personagem (SkinDB)
    private int _devTab;
    private readonly System.Collections.Generic.List<ItemDef> _devList = new System.Collections.Generic.List<ItemDef>();
    private KitWindow _dev;
    private int _devFirstRow;
    private bool _devDragBar;
    private Texture2D _tDevButton;
    private static bool DevTools => Debug.isDebugBuild;

    // Baú aberto (StorageChest.Current): 20 espaços em 5 colunas, ao lado do inventário.
    private const int ChestCols = 5;
    private const int ChestX = (KitW - ChestCols * KitStep + 2) / 2;
    private KitWindow _chestWin;
    private StorageChest _chestShown;
    private Src _dragSrc;
    private int _dragIndex;
    private string _dragId;
    private Vector2 _downPos;

    private GUIStyle _count, _key, _text, _eqStat;

    private void Start()
    {
        _bag = GetComponent<Inventory>();
        _bar = GetComponent<ActionBar>();
        _skills = GetComponent<SkillUser>();
        _equip = GetComponent<Equipment>();
        _sprite = GetComponent<SpriteRenderer>();
        LoadEquipArt();

        // Atributos (CombatHUD) à esquerda, equipamento ao lado, inventário à direita, habilidades no meio.
        int px = UIKit.Px;
        int invH = KitTop + Inventory.Size / Cols * KitStep - 2 + 8 + UIKit.Bottom;
        _inv = new KitWindow(Screen.width - KitW * px - 30, 110, KitW, invH, "titulo_inventario");
        _sk = new KitWindow((Screen.width - KitW * px) / 2f, 110, KitW, SkillWindowH(), "titulo_habilidades");
        int chestH = KitTop + StorageChest.Size / ChestCols * KitStep - 2 + 8 + UIKit.Bottom;
        _chestWin = new KitWindow(_inv.pos.x - KitW * px - 10, 110, KitW, chestH, "titulo_bau");
        _dev = new KitWindow(Screen.width - DevW * px - 30, 110 + invH * px + 10, DevW, DevTop + DevRows * KitStep - 2 + 8 + 26 + UIKit.Bottom, "titulo_dev");
        _tDevButton = UIKit.Load("Kit/botao_dev");
        _eq.r.position = new Vector2(380, 110);
    }

    private static int SkillWindowH() => KitTop + Mathf.Min(SkillDB.All.Count, SkillVisible) * SkillRowH + 46 + UIKit.Bottom;

    // Posição de cada espaço no layout (pixels da arte, a partir do canto da janela).
    // Armadura numa coluna, arma e escudo grandes, acessórios no meio, capa e luvas embaixo.
    private static Rect EqLayout(EquipSlot s) => s switch
    {
        EquipSlot.Head => new Rect(178, 38, 42, 42),
        EquipSlot.Armor => new Rect(178, 84, 42, 42),
        EquipSlot.Pants => new Rect(178, 129, 42, 42),
        EquipSlot.Boots => new Rect(178, 175, 42, 42),
        EquipSlot.Weapon => new Rect(250, 68, 53, 57),
        EquipSlot.Shield => new Rect(320, 68, 53, 57),
        EquipSlot.Accessory1 => new Rect(255, 129, 42, 42),
        EquipSlot.Accessory2 => new Rect(326, 129, 42, 42),
        EquipSlot.Cape => new Rect(254, 173, 45, 45),
        _ => new Rect(325, 173, 45, 45),   // luvas
    };

    private Rect EqRect(EquipSlot s) => EqScreen(EqLayout(s));

    private Rect EqScreen(Rect art) =>
        new Rect(_eq.r.x + art.x * _px, _eq.r.y + art.y * _px, art.width * _px, art.height * _px);

    private static Texture2D LoadUI(string name)
    {
        var t = Resources.Load<Texture2D>("UI/Equipamento/" + name);
        if (t != null) t.filterMode = FilterMode.Point;
        return t;
    }

    private void LoadEquipArt()
    {
        _tWindow = LoadUI("janela");
        _tPanel = LoadUI("painel_direito");
        _tClose = LoadUI("botao_fechar");
        _tSlot = LoadUI("slot");
        _tBig = LoadUI("slot_grande");
        _tBigClean = LoadUI("slot_grande_limpo");
        _tAcc = LoadUI("slot_acessorio");
        _tAccClean = LoadUI("slot_acessorio_limpo");
        _tStatAtk = LoadUI("etiqueta_ataque");
        _tStatDef = LoadUI("icone_escudo");
        _tStatHp = LoadUI("icone_coracao");
    }

    private void DrawArt(Texture2D t, Rect art)
    {
        if (t != null) GUI.DrawTexture(EqScreen(art), t);
    }

    private void DrawArt(Texture2D t, Vector2 pos)
    {
        if (t != null) DrawArt(t, new Rect(pos.x, pos.y, t.width, t.height));
    }

    private void DrawEquipWindow(Event e, Vector2 mouse)
    {
        // Moldura do kit por baixo: completa o cabeçalho do lado direito, que na arte
        // original fica vazio. Por cima vai a arte, e depois os cantos do kit, iguais aos das outras janelas.
        UIKit.Frame(_eq.r.position, EqW, EqH, null);
        DrawArt(_tWindow, Vector2.zero);
        DrawArt(_tPanel, EqPanelPos);
        UIKit.Corners(_eq.r.position, EqW, EqH);
        DrawArt(_tClose, EqClose);
        DrawPortrait();

        for (int i = 0; i < Equipment.Size; i++)
        {
            var s = (EquipSlot)i;
            string id = _equip.Get(s);
            bool empty = string.IsNullOrEmpty(id);
            // Arma, escudo, capa e luvas mostram uma silhueta quando vazios.
            Texture2D frame = s switch
            {
                EquipSlot.Weapon or EquipSlot.Shield => empty ? _tBig : _tBigClean,
                EquipSlot.Cape or EquipSlot.Gloves => empty ? _tAcc : _tAccClean,
                _ => _tSlot,
            };
            Rect art = EqLayout(s);
            DrawArt(frame, art);

            Rect r = EqRect(s);
            Rect inner = new Rect(r.x + 6 * _px, r.y + 6 * _px, r.width - 12 * _px, r.height - 12 * _px);
            if (r.Contains(mouse) && _dragSrc == Src.None)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.12f);
                GUI.DrawTexture(inner, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            var icon = IconFor(id);
            if (icon != null) GUI.DrawTexture(inner, icon, ScaleMode.ScaleToFit);
        }

        DrawEquipStats();

        if (e.type == EventType.MouseDown && e.button == 0 && EqScreen(EqClose).Contains(mouse))
        {
            _eq.show = false;
            e.Use();
            return;
        }
        MoveWindow(_eq, e, mouse, EqTitleH * _px);
    }

    /// O sprite atual do personagem, ampliado em escala inteira, de pé sobre o pedestal.
    private void DrawPortrait()
    {
        if (_sprite == null || _sprite.sprite == null) return;
        var sp = _sprite.sprite;
        var t = sp.texture;
        var uv = new Rect(sp.textureRect.x / t.width, sp.textureRect.y / t.height,
                          sp.textureRect.width / t.width, sp.textureRect.height / t.height);
        float k = _px * Mathf.Max(1f, Mathf.Floor(Mathf.Min(110f / sp.rect.width, 115f / sp.rect.height)));
        float w = sp.rect.width * k, h = sp.rect.height * k;
        // Os quadros do cavaleiro têm 2px de folga embaixo dos pés.
        float feetX = _eq.r.x + EqFeet.x * _px, feetY = _eq.r.y + EqFeet.y * _px + 2 * k;
        GUI.DrawTextureWithTexCoords(new Rect(feetX - w / 2f, feetY - h, w, h), t, uv);
    }

    /// Faixa embaixo da janela com ataque, defesa e vida já somando o equipamento.
    private void DrawEquipStats()
    {
        var st = GetComponent<CharacterStats>();
        if (st == null) return;

        var strip = EqScreen(new Rect(128, EqH + 2, 150, 14));
        GUI.color = new Color(0.11f, 0.09f, 0.08f, 0.92f);
        GUI.DrawTexture(strip, Texture2D.whiteTexture);
        GUI.color = Color.white;

        _eqStat.fontSize = 6 * _px;
        float x = 132;
        void Stat(Texture2D icon, Rect uv, string text)
        {
            if (icon != null)
            {
                float iw = uv.width * icon.width, ih = uv.height * icon.height;
                GUI.DrawTextureWithTexCoords(EqScreen(new Rect(x, EqH + 2 + (14 - ih) / 2f, iw, ih)), icon, uv);
                x += iw + 3;
            }
            GUI.Label(EqScreen(new Rect(x, EqH + 2, 34, 14)), text, _eqStat);
            x += 34;
        }
        // O ícone de ataque é a espada no começo da etiqueta "Atac".
        Stat(_tStatAtk, _tStatAtk != null ? new Rect(0f, 0f, 11f / _tStatAtk.width, 1f) : default, st.Atk.ToString());
        Stat(_tStatDef, new Rect(0f, 0f, 1f, 1f), st.Def.ToString());
        Stat(_tStatHp, new Rect(0f, 0f, 1f, 1f), st.MaxHp.ToString());
    }

    private void Update()
    {
        // Soltar o botão fora da janela do jogo não gera o MouseUp do OnGUI: sem isto o
        // arraste ficava preso e bloqueava todos os cliques (personagem "congelado").
        var m = Mouse.current;
        if (_dragSrc != Src.None && m != null && !m.leftButton.isPressed) _dragSrc = Src.None;

        var kb = Keyboard.current;
        if (kb == null || UIKit.Typing) return;
        if (kb.iKey.wasPressedThisFrame) _inv.show = !_inv.show;
        if (kb.kKey.wasPressedThisFrame) { _sk.show = !_sk.show; _skDragBar = false; }
        if (kb.eKey.wasPressedThisFrame) _eq.show = !_eq.show;
    }

    private static bool IsSkill(string id) => id != null && id.StartsWith(ActionBar.SkillPrefix);
    private static string SkillId(string id) => id.Substring(ActionBar.SkillPrefix.Length);

    private static Texture2D IconFor(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return IsSkill(id) ? SkillDB.Get(SkillId(id))?.icon : ItemDB.Get(id)?.icon;
    }

    private Rect BarRect(int i)
    {
        float total = ActionBar.Size * (Slot + Gap) - Gap;
        float x0 = (Screen.width - total) / 2f;
        return new Rect(x0 + i * (Slot + Gap), Screen.height - Slot - 14f, Slot, Slot);
    }

    private Rect InvRect(int i)
    {
        int r = i / Cols, c = i % Cols;
        return _inv.Art(InvX + c * KitStep, KitTop + r * KitStep, UIKit.SlotSize, UIKit.SlotSize);
    }

    /// Retângulo do ícone da habilidade i; fora da parte visível da lista, vazio (não recebe clique).
    private Rect SkillRect(int i)
    {
        int row = i - _skFirst;
        if (row < 0 || row >= SkillVisible) return Rect.zero;
        return _sk.Art(16, KitTop + row * SkillRowH, SkillSlot, SkillSlot);
    }

    private void OnGUI()
    {
        // O Unity recompilou no meio do Play e apagou as janelas (não são serializadas): recria.
        if (_inv == null) Start();
        if (_bag == null || _bar == null) return;
        if (_count == null)
        {
            _count = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
            _count.normal.textColor = Color.white;
            _key = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.UpperLeft };
            _key.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
            _text = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            _text.normal.textColor = Color.white;
            _eqStat = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset() };
            _eqStat.normal.textColor = new Color(0.93f, 0.85f, 0.7f);
        }

        Event e = Event.current;
        Vector2 mouse = e.mousePosition;

        _px = UIKit.Px;
        _eq.r.size = new Vector2(EqW * _px, (EqH + EqStatsH) * _px);

        if (_inv.show) DrawInventoryWindow(e, mouse);

        if (_sk.show && _skills != null) DrawSkillWindow(e, mouse);
        if (_eq.show && _equip != null) DrawEquipWindow(e, mouse);
        DrawChestWindow(e, mouse);
        if (DevTools)
        {
            if (_dev.show) DrawDevWindow(e, mouse);
            DrawDevButton(e, mouse);
        }

        for (int i = 0; i < ActionBar.Size; i++) DrawBarSlot(i);

        HandleDragAndClicks(e, mouse);

        if (_dragSrc != Src.None)
        {
            var icon = IconFor(_dragId);
            if (icon != null) GUI.DrawTexture(new Rect(mouse.x - 20, mouse.y - 20, 40, 40), icon, ScaleMode.ScaleToFit);
        }
        else DrawTooltip(mouse);

        PointerOverUI = Over(mouse) || _dragSrc != Src.None;
    }

    private void DrawBarSlot(int i)
    {
        string id = _bar.slots[i];
        string key = ((i + 1) % 10).ToString();
        if (string.IsNullOrEmpty(id)) { DrawSlot(BarRect(i), null, null, key, false, 0f); return; }

        if (IsSkill(id))
        {
            string sid = SkillId(id);
            bool usable = _skills != null && _skills.Usable(sid);
            DrawSlot(BarRect(i), id, null, key, !usable, _skills != null ? _skills.CooldownFraction(sid) : 0f);
        }
        else
        {
            int n = _bag.Count(id);
            DrawSlot(BarRect(i), id, n != 1 ? n.ToString() : null, key, n == 0, 0f);
        }
    }

    private void DrawSlot(Rect r, string id, string countText, string key, bool dim, float cooldown)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.25f);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - 1, r.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, 1, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - 1, r.y, 1, r.height), Texture2D.whiteTexture);

        var icon = IconFor(id);
        if (icon != null)
        {
            GUI.color = dim ? new Color(1f, 1f, 1f, 0.3f) : Color.white;
            GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), icon, ScaleMode.ScaleToFit);
        }
        if (cooldown > 0f)
        {
            // Sombra que desce conforme a recarga termina.
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, r.height * cooldown), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;

        if (countText != null) GUI.Label(new Rect(r.x, r.y, r.width - 3, r.height - 1), countText, _count);
        if (key != null) GUI.Label(new Rect(r.x + 3, r.y + 1, 20, 16), key, _key);
    }

    private void DrawInventoryWindow(Event e, Vector2 mouse)
    {
        _inv.DrawFrame();
        for (int i = 0; i < Inventory.Size; i++)
        {
            var s = _bag.slots[i];
            Rect r = InvRect(i);
            UIKit.Slot(r, s.Empty ? null : IconFor(s.id), r.Contains(mouse) && _dragSrc == Src.None);
            if (!s.Empty && s.count > 1)
                UIKit.Label(new Rect(r.x, r.y, r.width - 6 * _px, r.height - 5 * _px), s.count.ToString(), 7, Color.white, TextAnchor.LowerRight);
        }
        _inv.HandleInput(e);
    }

    private Rect ChestRect(int i)
    {
        int r = i / ChestCols, c = i % ChestCols;
        return _chestWin.Art(ChestX + c * KitStep, KitTop + r * KitStep, UIKit.SlotSize, UIKit.SlotSize);
    }

    /// Janela do baú aberto. Ao abrir, mostra o inventário junto para dar para trocar itens.
    private void DrawChestWindow(Event e, Vector2 mouse)
    {
        var chest = StorageChest.Current;
        if (chest != _chestShown)
        {
            _chestShown = chest;
            if (chest != null) { _inv.show = true; _chestWin.show = true; }
            if (_dragSrc == Src.Chest) _dragSrc = Src.None;
        }
        if (chest == null) return;

        _chestWin.DrawFrame();
        for (int i = 0; i < StorageChest.Size; i++)
        {
            var s = chest.slots[i];
            Rect r = ChestRect(i);
            UIKit.Slot(r, s.Empty ? null : IconFor(s.id), r.Contains(mouse) && _dragSrc == Src.None);
            if (!s.Empty && s.count > 1)
                UIKit.Label(new Rect(r.x, r.y, r.width - 6 * _px, r.height - 5 * _px), s.count.ToString(), 7, Color.white, TextAnchor.LowerRight);
        }
        _chestWin.HandleInput(e);
        if (!_chestWin.show) chest.Close();   // X da janela
    }

    /// Clique direito com o baú aberto: manda a pilha inteira para o outro lado (o que couber).
    private void Transfer(ItemStack[] from, int i, ItemStack[] to)
    {
        var s = from[i];
        if (s.HasData)
        {
            // Item com conteúdo próprio (baú com itens) vai inteiro, num espaço vazio.
            if (Inventory.PutStack(to, s)) s.Clear();
            _bag.MarkChanged();
            return;
        }
        int moved = Inventory.AddTo(to, s.id, s.count);
        s.count -= moved;
        if (s.count <= 0) s.Clear();
        _bag.MarkChanged();
    }

    private void DrawSkillWindow(Event e, Vector2 mouse)
    {
        var list = SkillDB.All;
        int visible = Mathf.Min(list.Count, SkillVisible);
        _skFirst = Mathf.Clamp(_skFirst, 0, list.Count - visible);
        _sk.h = SkillWindowH();
        _sk.DrawFrame();

        for (int i = _skFirst; i < _skFirst + visible; i++)
        {
            var d = list[i];
            int lv = _skills.Level(d.id);
            float y = KitTop + (i - _skFirst) * SkillRowH;
            Rect slot = SkillRect(i);
            // Habilidade de ferramenta fica apagada até o equipamento dela estar na mão.
            bool hasTool = _skills.HasTool(d);
            UIKit.Slot(slot, d.icon, slot.Contains(mouse) && _dragSrc == Src.None, lv == 0 || !hasTool);

            UIKit.Label(_sk.Art(SkillTextX, y + 2, 210, 22), d.name, 16, lv > 0 && hasTool ? UIKit.Gold : UIKit.Dim);
            string info = hasTool ? $"Nv {lv}/{d.maxLevel}    SP {d.spCost}"
                                  : $"Requer {ItemDB.Get(d.requiredTool)?.name ?? d.requiredTool} equipado(a)";
            UIKit.Label(_sk.Art(SkillTextX, y + 28, 210, 20), info, hasTool ? 14 : 12, hasTool ? UIKit.Cream : UIKit.Dim);

            bool canLearn = _skills.skillPoints > 0 && lv < d.maxLevel;
            if (UIKit.Button(_sk.Art(ScrollX - 26, y + (SkillSlot - 19) / 2, 20, 19), "+", canLearn)) _skills.Learn(d.id);
        }

        var stats = GetComponent<CharacterStats>();
        string job = stats == null ? "" : stats.JobExpToNext > 0
            ? $"Nível de skill {stats.jobLevel}   ·   XP {stats.jobExp}/{stats.JobExpToNext}"
            : $"Nível de skill {stats.jobLevel} (máximo)";
        UIKit.Label(_sk.Art(16, KitTop + visible * SkillRowH, 300, 20), job, 12, UIKit.Gold);
        UIKit.Label(_sk.Art(16, KitTop + visible * SkillRowH + 20, 300, 20),
                    $"Pontos de habilidade: {_skills.skillPoints}", 14, UIKit.Cream);
        if (list.Count > visible) ScrollList(_sk, e, mouse, ref _skFirst, ref _skDragBar, list.Count, visible, SkillRowH);
        _sk.HandleInput(e);
    }

    /// Barra de rolagem de uma lista em linhas (habilidades, itens de desenvolvedor): roda do
    /// mouse sobre a janela ou arrastar o marcador. Rola linha a linha.
    private void ScrollList(KitWindow w, Event e, Vector2 mouse, ref int first, ref bool dragging,
                            int count, int visible, float rowH, float top = KitTop, float trackX = ScrollX)
    {
        int max = count - visible;
        float trackH = visible * rowH - 8;
        float thumbH = Mathf.Max(16f, trackH * visible / count);
        float thumbY = top + (trackH - thumbH) * first / max;
        Rect track = w.Art(trackX, top, 6, trackH);
        UIKit.Scrollbar(track, w.Art(trackX - 1, thumbY, 8, thumbH));

        if (e.type == EventType.ScrollWheel && w.ScreenRect.Contains(mouse))
        {
            first = Mathf.Clamp(first + (e.delta.y > 0 ? 1 : -1), 0, max);
            e.Use();
            return;
        }

        Rect grab = w.Art(trackX - 4, top, 14, trackH);
        if (e.type == EventType.MouseDown && e.button == 0 && grab.Contains(mouse)) dragging = true;
        if (dragging && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
        {
            // O meio do marcador segue o mouse.
            float free = track.height - thumbH * _px;
            float t = free > 0f ? (mouse.y - track.y - thumbH * _px / 2f) / free : 0f;
            first = Mathf.Clamp(Mathf.RoundToInt(t * max), 0, max);
            e.Use();
        }
        else if (dragging && e.type == EventType.MouseUp)
        {
            dragging = false;
            e.Use();
        }
    }

    // ---------- Ferramenta de desenvolvedor ----------

    /// Retângulo do item i do catálogo na janela de itens; fora da parte visível, vazio.
    private Rect DevRect(int i)
    {
        int row = i / DevCols - _devFirstRow, c = i % DevCols;
        if (row < 0 || row >= DevRows) return Rect.zero;
        return _dev.Art(DevX + c * KitStep, DevTop + row * KitStep, UIKit.SlotSize, UIKit.SlotSize);
    }

    /// Aba de cada item: pelo tipo; posicionáveis se dividem em construção e chão.
    private static int DevTabOf(ItemDef d) => d.kind switch
    {
        ItemKind.Equipment => 2,
        ItemKind.Consumable => 3,
        ItemKind.Material => 4,
        _ => d.place == "janela" ? 8 : d.place == "parede" ? 7 : d.place == "piso" || d.place == "raspar" ? 6 : 5,
    };

    /// Itens da aba aberta, na ordem do catálogo (que já põe os pisos em ordem de tema).
    private System.Collections.Generic.List<ItemDef> DevList()
    {
        _devList.Clear();
        foreach (var d in ItemDB.All) if (DevTabOf(d) == _devTab) _devList.Add(d);
        return _devList;
    }

    /// Botão DEV no canto superior direito. Em 1080p o desenho fica em tamanho real (72 px).
    private Rect DevButtonRect()
    {
        int k = Mathf.Max(1, _px / 2);
        float size = 72 * k;
        return new Rect(Screen.width - size - 12, 12, size, size);
    }

    private void DrawDevButton(Event e, Vector2 mouse)
    {
        Rect r = DevButtonRect();
        if (_tDevButton != null)
        {
            GUI.color = r.Contains(mouse) || _dev.show ? Color.white : new Color(0.85f, 0.85f, 0.85f);
            GUI.DrawTexture(r, _tDevButton);
            GUI.color = Color.white;
        }
        if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(mouse))
        {
            _dev.show = !_dev.show;
            e.Use();
        }
    }

    /// Todos os itens do jogo (ItemDB.All), infinitos: arrastar para o inventário ou baú dá 1,
    /// com Shift uma pilha cheia; clique direito manda 1 para o inventário.
    private void DrawDevWindow(Event e, Vector2 mouse)
    {
        _dev.DrawFrame();
        // Abas repartem a largura da janela, quantas houver.
        float tabStep = (DevW - 32f) / DevTabs.Length;
        for (int t = 0; t < DevTabs.Length; t++)
        {
            if (UIKit.Button(_dev.Art(16 + t * tabStep, 38, tabStep - 2, 19), DevTabs[t], true, 7, t == _devTab))
            {
                _devTab = t;
                _devFirstRow = 0;
            }
        }
        if (_devTab == DevTestsTab) DrawDevTests();
        if (_devTab == DevSkinsTab) DrawDevSkins(Event.current, mouse);
        var items = DevList();
        int rows = (items.Count + DevCols - 1) / DevCols;
        _devFirstRow = Mathf.Clamp(_devFirstRow, 0, Mathf.Max(0, rows - DevRows));
        for (int i = 0; i < items.Count; i++)
        {
            Rect r = DevRect(i);
            if (r.width == 0f) continue;
            UIKit.Slot(r, items[i].icon, r.Contains(mouse) && _dragSrc == Src.None);
            UIKit.Label(new Rect(r.x, r.y, r.width - 6 * _px, r.height - 5 * _px), "\u221E", 7, UIKit.Gold, TextAnchor.LowerRight);
        }
        if (rows > DevRows)
            ScrollList(_dev, e, mouse, ref _devFirstRow, ref _devDragBar, rows, DevRows, KitStep, DevTop, DevW - UIKit.Side - 10);

        // Dinheiro para testes.
        float yMoney = DevTop + DevRows * KitStep + 4;
        if (UIKit.Button(_dev.Art(16, yMoney, 130, 19), "+1.000 unidades", true, 9)) _bag.AddMoney(1000);
        UIKit.Label(_dev.Art(156, yMoney, DevW - 172, 19), "Carteira: " + Inventory.MoneyText(_bag.money), 9, UIKit.Cream);
        _dev.HandleInput(e);
    }

    /// Aba Skins: um cartão por aparência (quadro de frente + nome); clique veste.
    private void DrawDevSkins(Event e, Vector2 mouse)
    {
        const int cardW = 84, cardH = 112, gap = 8;
        var skins = SkinDB.All;
        for (int i = 0; i < skins.Count; i++)
        {
            var skin = skins[i];
            int perRow = Mathf.Max(1, (DevW - 32 + gap) / (cardW + gap));
            int col = i % perRow, row = i / perRow;
            Rect r = _dev.Art(16 + col * (cardW + gap), DevTop + 2 + row * (cardH + gap), cardW, cardH);
            bool on = SkinDB.CurrentId == skin.id;
            UIKit.Slot(r, null, r.Contains(mouse) || on);
            var s = skin.Preview;
            if (s != null)
            {
                // Quadro centralizado no cartão, mantendo a proporção.
                Rect tr = s.textureRect;
                float scale = Mathf.Min((r.width - 16 * _px) / tr.width, (r.height - 34 * _px) / tr.height);
                float w = tr.width * scale, h = tr.height * scale;
                var dst = new Rect(r.center.x - w / 2f, r.y + 8 * _px, w, h);
                var uv = new Rect(tr.x / s.texture.width, tr.y / s.texture.height, tr.width / s.texture.width, tr.height / s.texture.height);
                GUI.DrawTextureWithTexCoords(dst, s.texture, uv);
            }
            UIKit.Label(new Rect(r.x, r.yMax - 24 * _px, r.width, 18 * _px), skin.name, 9, on ? UIKit.Gold : UIKit.Cream, TextAnchor.MiddleCenter);
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(mouse))
            {
                SkinDB.Set(skin.id);
                SaveGame.Save();
                e.Use();
            }
        }
    }

    /// Aba Testes: chaves liga/desliga para HP e SP infinitos.
    private void DrawDevTests()
    {
        float y = DevTop + 4;
        if (DevToggle(y, "HP infinito", "O personagem não perde vida.", DevCheats.InfiniteHp))
        {
            DevCheats.InfiniteHp = !DevCheats.InfiniteHp;
            var h = GetComponent<Health>();
            if (DevCheats.InfiniteHp && h != null && !h.IsDead) h.Restore(h.maxHp);
            SaveGame.Save();
        }
        y += 30;
        if (DevToggle(y, "SP infinito", "As habilidades não gastam SP.", DevCheats.InfiniteSp))
        {
            DevCheats.InfiniteSp = !DevCheats.InfiniteSp;
            if (DevCheats.InfiniteSp) _skills.RefillSp();
            SaveGame.Save();
        }
        y += 30;
        DevNumber(y, "XP ×", "Multiplicador da XP dos monstros (1 = real).", ref _rateText,
                  ExpTable.Rate, ExpTable.MaxRate, v => ExpTable.Rate = v);
        y += 30;
        DevNumber(y, "Dano ×", "Multiplicador do dano que você causa (1 = normal).", ref _damageText,
                  DevCheats.DamageRate, DevCheats.MaxDamageRate, v => DevCheats.DamageRate = v);
        y += 30;
        DevBoss(y);
        y += 30;
        DevMaxAttributes(y);
    }

    /// Todos os atributos em 99 de uma vez (o reset da janela de atributos desfaz).
    private void DevMaxAttributes(float y)
    {
        var stats = GetComponent<CharacterStats>();
        UIKit.Label(_dev.Art(20, y, 120, 22), "Atributos", 11, UIKit.Gold);
        UIKit.Label(_dev.Art(130, y, DevW - 260, 22), "Todos em 99. O reset (C) desfaz.", 8, UIKit.Cream);
        if (stats != null && UIKit.Button(_dev.Art(DevW - 116, y + 1, 96, 20), "Tudo no máximo", true, 9))
        {
            stats.DevMaxAttributes();
            SaveGame.Save();
        }
    }

    /// Drake: reaparece na hora (o respawn normal é de 2 horas, como no Ragnarok).
    private void DevBoss(float y)
    {
        DrakeBoss boss = DrakeBoss.All.Count > 0 ? DrakeBoss.All[0] : null;
        UIKit.Label(_dev.Art(20, y, 120, 22), "Boss Drake", 11, UIKit.Gold);
        string state = boss == null ? "Não está no mapa." : !boss.IsDead ? "Vivo."
            : boss.RespawnIn > 0f ? "Morto: volta em " + System.TimeSpan.FromSeconds(boss.RespawnIn).ToString(@"h\:mm\:ss") + "." : "Morrendo...";
        UIKit.Label(_dev.Art(130, y, DevW - 260, 22), state, 8, UIKit.Cream);
        if (boss != null && boss.IsDead && UIKit.Button(_dev.Art(DevW - 116, y + 1, 96, 20), "Reaparecer", true, 9))
            boss.RespawnNow();
    }

    private string _rateText, _damageText;

    /// Multiplicador digitado à mão (XP, dano...). Vale na hora e fica salvo.
    private void DevNumber(float y, string name, string hint, ref string text, int current, int max, System.Action<int> apply)
    {
        UIKit.Label(_dev.Art(20, y, 120, 22), name, 11, UIKit.Gold);
        UIKit.Label(_dev.Art(130, y, DevW - 260, 22), hint, 8, UIKit.Cream);
        // Fora de foco, a caixa mostra o valor em uso (ex.: depois de carregar o jogo).
        if (text == null || !UIKit.Typing) text = current.ToString();
        string typed = UIKit.TextField(_dev.Art(DevW - 116, y + 1, 96, 20), text, 10, 5);
        if (typed == text) return;
        text = new string(System.Array.FindAll(typed.ToCharArray(), char.IsDigit));
        if (int.TryParse(text, out int v) && v >= 1)
        {
            apply(Mathf.Min(v, max));
            SaveGame.Save();
        }
    }

    /// Uma linha da aba Testes: nome, explicação e a chave (verdade quando clicada).
    private bool DevToggle(float y, string name, string hint, bool on)
    {
        UIKit.Label(_dev.Art(20, y, 120, 22), name, 11, UIKit.Gold);
        UIKit.Label(_dev.Art(130, y, DevW - 260, 22), hint, 8, UIKit.Cream);
        return UIKit.Button(_dev.Art(DevW - 116, y + 1, 96, 20), on ? "Ligado" : "Desligado", true, 9, on);
    }

    /// Item do desenvolvedor solto num espaço: completa a pilha ou ocupa o espaço vazio;
    /// em espaço com outro item, vai para qualquer lugar livre da mesma lista.
    private static void GiveInto(ItemStack[] slots, int index, string id, bool fullStack)
    {
        int max = ItemDB.Get(id)?.maxStack ?? 99;
        int amount = fullStack ? max : 1;
        var s = slots[index];
        if (s.Empty) { s.id = id; s.count = amount; return; }
        if (s.id == id) { s.count = Mathf.Min(max, s.count + amount); return; }
        Inventory.AddTo(slots, id, amount);
    }

    private static void MoveWindow(Win w, Event e, Vector2 mouse, float titleHeight = 22f)
    {
        var title = new Rect(w.r.x, w.r.y, w.r.width, titleHeight);
        if (e.type == EventType.MouseDown && e.button == 0 && title.Contains(mouse))
        {
            w.moving = true; w.grab = mouse - w.r.position; e.Use();
        }
        else if (w.moving && e.type == EventType.MouseDrag)
        {
            w.r.position = mouse - w.grab;
            w.r.x = Mathf.Clamp(w.r.x, 0, Screen.width - w.r.width);
            w.r.y = Mathf.Clamp(w.r.y, 0, Screen.height - w.r.height);
            e.Use();
        }
        else if (w.moving && e.type == EventType.MouseUp) { w.moving = false; e.Use(); }
    }

    private void HandleDragAndClicks(Event e, Vector2 mouse)
    {
        if (e.type == EventType.MouseDown)
        {
            if (_inv.show)
            {
                for (int i = 0; i < Inventory.Size; i++)
                {
                    if (!InvRect(i).Contains(mouse) || _bag.slots[i].Empty) continue;
                    if (e.button == 1)
                    {
                        // Com o baú aberto, o clique direito guarda a pilha nele em vez de usar.
                        if (_chestShown != null) Transfer(_bag.slots, i, _chestShown.slots);
                        else _bag.UseSlot(i);
                        e.Use(); return;
                    }
                    if (e.button == 0) { Begin(Src.Inv, i, _bag.slots[i].id, mouse); e.Use(); return; }
                }
            }
            if (DevTools && _dev.show)
            {
                var items = DevList();
                for (int i = 0; i < items.Count; i++)
                {
                    if (!DevRect(i).Contains(mouse)) continue;
                    if (e.button == 1) { _bag.Add(items[i].id, 1); e.Use(); return; }
                    if (e.button == 0) { Begin(Src.Dev, i, items[i].id, mouse); e.Use(); return; }
                }
            }
            if (_chestShown != null)
            {
                for (int i = 0; i < StorageChest.Size; i++)
                {
                    if (!ChestRect(i).Contains(mouse) || _chestShown.slots[i].Empty) continue;
                    if (e.button == 1) { Transfer(_chestShown.slots, i, _bag.slots); e.Use(); return; }
                    if (e.button == 0) { Begin(Src.Chest, i, _chestShown.slots[i].id, mouse); e.Use(); return; }
                }
            }
            if (_sk.show && _skills != null && e.button == 0)
            {
                var list = SkillDB.All;
                for (int i = 0; i < list.Count; i++)
                {
                    // Só dá para arrastar o que já foi aprendido.
                    if (!SkillRect(i).Contains(mouse) || _skills.Level(list[i].id) == 0) continue;
                    Begin(Src.Skill, i, ActionBar.SkillPrefix + list[i].id, mouse); e.Use(); return;
                }
            }
            if (_eq.show && _equip != null)
            {
                for (int i = 0; i < Equipment.Size; i++)
                {
                    // Clique num espaço vestido devolve o item ao inventário.
                    if (!EqRect((EquipSlot)i).Contains(mouse) || string.IsNullOrEmpty(_equip.slots[i])) continue;
                    _equip.Unequip((EquipSlot)i); e.Use(); return;
                }
            }
            for (int i = 0; i < ActionBar.Size; i++)
            {
                if (e.button != 0 || !BarRect(i).Contains(mouse) || string.IsNullOrEmpty(_bar.slots[i])) continue;
                Begin(Src.Bar, i, _bar.slots[i], mouse); e.Use(); return;
            }
        }
        else if (e.type == EventType.MouseUp && _dragSrc != Src.None)
        {
            bool click = (mouse - _downPos).sqrMagnitude < 16f;
            int barTarget = -1, invTarget = -1;
            for (int i = 0; i < ActionBar.Size; i++) if (BarRect(i).Contains(mouse)) barTarget = i;
            if (_inv.show) for (int i = 0; i < Inventory.Size; i++) if (InvRect(i).Contains(mouse)) invTarget = i;

            int eqTarget = -1;
            if (_eq.show && _equip != null)
                for (int i = 0; i < Equipment.Size; i++) if (EqRect((EquipSlot)i).Contains(mouse)) eqTarget = i;
            int chestTarget = -1;
            if (_chestShown != null)
                for (int i = 0; i < StorageChest.Size; i++) if (ChestRect(i).Contains(mouse)) chestTarget = i;

            if (_dragSrc == Src.Dev)
            {
                // Infinito: a janela não perde o item. Shift = pilha cheia.
                if (invTarget >= 0) { GiveInto(_bag.slots, invTarget, _dragId, e.shift); _bag.MarkChanged(); }
                else if (chestTarget >= 0) GiveInto(_chestShown.slots, chestTarget, _dragId, e.shift);
            }
            else if (_dragSrc == Src.Chest)
            {
                // Do baú só dá para soltar no próprio baú ou no inventário (atalho e equipamento
                // pedem o item na bolsa).
                if (chestTarget >= 0) Inventory.Move(_chestShown.slots, _dragIndex, _chestShown.slots, chestTarget);
                else if (invTarget >= 0) { Inventory.Move(_chestShown.slots, _dragIndex, _bag.slots, invTarget); _bag.MarkChanged(); }
            }
            else if (chestTarget >= 0 && _dragSrc == Src.Inv)
            {
                Inventory.Move(_bag.slots, _dragIndex, _chestShown.slots, chestTarget);
                _bag.MarkChanged();
            }
            else if (click && _dragSrc == Src.Bar) _bar.Trigger(_dragIndex);
            else if (eqTarget >= 0 && _dragSrc == Src.Inv) _equip.EquipTo(_dragId, (EquipSlot)eqTarget);
            else if (barTarget >= 0)
            {
                // Soltar na barra cria o atalho; vindo da própria barra, troca de posição.
                if (_dragSrc == Src.Bar) _bar.slots[_dragIndex] = _bar.slots[barTarget];
                _bar.slots[barTarget] = _dragId;
            }
            else if (invTarget >= 0 && _dragSrc == Src.Inv) _bag.Swap(_dragIndex, invTarget);
            else if (_dragSrc == Src.Bar && !click) _bar.slots[_dragIndex] = null;   // arrastou para fora

            _dragSrc = Src.None;
            e.Use();
        }
    }

    private void Begin(Src src, int index, string id, Vector2 mouse)
    {
        _dragSrc = src; _dragIndex = index; _dragId = id; _downPos = mouse;
    }

    private void DrawTooltip(Vector2 mouse)
    {
        string id = null;
        ItemStack stack = null;   // pilha sob o mouse (para itens com conteúdo, como baú recolhido)
        for (int i = 0; i < ActionBar.Size; i++) if (BarRect(i).Contains(mouse)) id = _bar.slots[i];
        if (_inv.show)
            for (int i = 0; i < Inventory.Size; i++)
                if (InvRect(i).Contains(mouse) && !_bag.slots[i].Empty) { id = _bag.slots[i].id; stack = _bag.slots[i]; }
        if (_sk.show) for (int i = 0; i < SkillDB.All.Count; i++) if (SkillRect(i).Contains(mouse)) id = ActionBar.SkillPrefix + SkillDB.All[i].id;
        if (_chestShown != null)
            for (int i = 0; i < StorageChest.Size; i++)
                if (ChestRect(i).Contains(mouse) && !_chestShown.slots[i].Empty) { id = _chestShown.slots[i].id; stack = _chestShown.slots[i]; }
        if (DevTools && _dev.show)
        {
            var list = DevList();
            for (int i = 0; i < list.Count; i++) if (DevRect(i).Contains(mouse)) id = list[i].id;
        }
        string emptySlot = null;
        if (_eq.show && _equip != null)
            for (int i = 0; i < Equipment.Size; i++)
            {
                if (!EqRect((EquipSlot)i).Contains(mouse)) continue;
                if (string.IsNullOrEmpty(_equip.slots[i])) emptySlot = Equipment.SlotNames[i];
                else id = _equip.slots[i];
            }
        if (string.IsNullOrEmpty(id))
        {
            // Espaço vazio: diz o que vai nele.
            if (emptySlot == null) return;
            var rs = new Rect(mouse.x + 14, mouse.y - 30, 110, 26);
            GUI.Box(rs, GUIContent.none);
            GUI.Label(new Rect(rs.x + 6, rs.y + 3, rs.width - 8, rs.height), emptySlot);
            return;
        }

        string text;
        if (IsSkill(id))
        {
            var s = SkillDB.Get(SkillId(id));
            if (s == null) return;
            text = $"{s.name}\n{s.description}\nSP {s.spCost}   Recarga {s.cooldown:0.#}s";
        }
        else
        {
            var d = ItemDB.Get(id);
            if (d == null) return;
            text = d.effect == "retorno" ? $"{d.name}\nClique direito: volta para o portal ativo"
                 : d.heal > 0 ? $"{d.name}\nRecupera {d.heal} HP"
                 : d.kind == ItemKind.Equipment ? $"{d.name}\n{d.BonusText()}"
                 : d.place == "raspar" ? $"{d.name}\nClique direito: raspar pisos e papel (voltam para a bolsa)"
                 : d.place == "janela" ? $"{d.name}\nClique direito: colocar na parede (dentro de casa)"
                 : d.place == "parede" ? $"{d.name}\nTema: {d.theme}\nClique direito: pintar a parede (dentro de casa)"
                 : d.place == "piso" ? $"{d.name}\nTema: {d.theme}\nClique direito: pintar o chão"
                 : d.kind == ItemKind.Placeable ? $"{d.name}\nClique direito: colocar no mapa" : d.name;
        }

        if (stack != null && stack.HasData && stack.id == "bau")
            text += $"\nGuardado com {StorageChest.CountIn(stack.data)} itens dentro";

        var r = new Rect(mouse.x + 14, mouse.y - 64, 230, 60);
        GUI.Box(r, GUIContent.none);
        GUI.Label(new Rect(r.x + 6, r.y + 3, r.width - 8, r.height), text);
    }

    private bool Over(Vector2 mouse)
    {
        if (_inv.show && _inv.ScreenRect.Contains(mouse)) return true;
        if (_sk.show && _sk.ScreenRect.Contains(mouse)) return true;
        if (_chestShown != null && _chestWin.ScreenRect.Contains(mouse)) return true;
        if (DevTools && (DevButtonRect().Contains(mouse) || (_dev.show && _dev.ScreenRect.Contains(mouse)))) return true;
        if (_eq.show && _eq.r.Contains(mouse)) return true;
        for (int i = 0; i < ActionBar.Size; i++) if (BarRect(i).Contains(mouse)) return true;
        return false;
    }
}
