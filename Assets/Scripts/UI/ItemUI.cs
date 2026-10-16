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
    private readonly Win _eq = new Win { r = new Rect(720, 64, 470, 300) };

    private class Win { public Rect r; public bool show, moving; public Vector2 grab; }
    private readonly Win _inv = new Win { r = new Rect(380, 64, Cols * (Slot + Gap) + 16, 4 * (Slot + Gap) + 36) };
    private readonly Win _sk = new Win { r = new Rect(380, 330, 300, 120) };

    private enum Src { None, Inv, Bar, Skill }
    private Src _dragSrc;
    private int _dragIndex;
    private string _dragId;
    private Vector2 _downPos;

    private GUIStyle _count, _key, _text;

    private void Start()
    {
        _bag = GetComponent<Inventory>();
        _bar = GetComponent<ActionBar>();
        _skills = GetComponent<SkillUser>();
        _equip = GetComponent<Equipment>();
        _sprite = GetComponent<SpriteRenderer>();
    }

    // Layout inspirado na referência: armadura numa coluna, arma e escudo grandes, acessórios embaixo.
    private Rect EqRect(EquipSlot s)
    {
        float x = _eq.r.x, y = _eq.r.y;
        switch (s)
        {
            case EquipSlot.Head: return new Rect(x + 172, y + 34, Slot, Slot);
            case EquipSlot.Armor: return new Rect(x + 172, y + 94, Slot, Slot);
            case EquipSlot.Pants: return new Rect(x + 172, y + 154, Slot, Slot);
            case EquipSlot.Boots: return new Rect(x + 172, y + 214, Slot, Slot);
            case EquipSlot.Cape: return new Rect(x + 230, y + 94, Slot, Slot);
            case EquipSlot.Gloves: return new Rect(x + 230, y + 154, Slot, Slot);
            case EquipSlot.Weapon: return new Rect(x + 296, y + 40, 72, 72);
            case EquipSlot.Shield: return new Rect(x + 382, y + 40, 72, 72);
            case EquipSlot.Accessory1: return new Rect(x + 306, y + 150, 56, 56);
            default: return new Rect(x + 392, y + 150, 56, 56);
        }
    }

    private void DrawEquipWindow(Event e, Vector2 mouse)
    {
        GUI.Box(_eq.r, "Equipamento  (E)");

        // Retrato: o sprite atual do personagem, ampliado sobre um pedestal escuro.
        var portrait = new Rect(_eq.r.x + 10, _eq.r.y + 30, 150, 230);
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(portrait, Texture2D.whiteTexture);
        GUI.color = Color.white;
        if (_sprite != null && _sprite.sprite != null)
        {
            var sp = _sprite.sprite;
            var t = sp.texture;
            var uv = new Rect(sp.textureRect.x / t.width, sp.textureRect.y / t.height,
                              sp.textureRect.width / t.width, sp.textureRect.height / t.height);
            float scale = Mathf.Floor(Mathf.Min(portrait.width / sp.rect.width, portrait.height / sp.rect.height));
            float w = sp.rect.width * scale, h = sp.rect.height * scale;
            GUI.DrawTextureWithTexCoords(new Rect(portrait.center.x - w / 2f, portrait.yMax - h - 6, w, h), t, uv);
        }

        for (int i = 0; i < Equipment.Size; i++)
        {
            var s = (EquipSlot)i;
            Rect r = EqRect(s);
            string id = _equip.Get(s);
            DrawSlot(r, id, null, null, false, 0f);
            if (string.IsNullOrEmpty(id))
                GUI.Label(new Rect(r.x, r.yMax - 16, r.width, 16), Equipment.SlotNames[i], _key);
        }

        var st = GetComponent<CharacterStats>();
        if (st != null)
            GUI.Label(new Rect(_eq.r.x + 172, _eq.r.yMax - 30, 290, 22), $"ATQ {st.Atk}    DEF {st.Def}    HP {st.MaxHp}", _text);

        MoveWindow(_eq, e, mouse);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.iKey.wasPressedThisFrame) _inv.show = !_inv.show;
        if (kb.kKey.wasPressedThisFrame) _sk.show = !_sk.show;
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
        return new Rect(_inv.r.x + 8 + c * (Slot + Gap), _inv.r.y + 28 + r * (Slot + Gap), Slot, Slot);
    }

    private Rect SkillRect(int i) => new Rect(_sk.r.x + 10, _sk.r.y + 30 + i * (Slot + 8), Slot, Slot);

    private void OnGUI()
    {
        if (_bag == null || _bar == null) return;
        if (_count == null)
        {
            _count = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
            _count.normal.textColor = Color.white;
            _key = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.UpperLeft };
            _key.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
            _text = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            _text.normal.textColor = Color.white;
        }

        Event e = Event.current;
        Vector2 mouse = e.mousePosition;

        if (_inv.show)
        {
            GUI.Box(_inv.r, "Inventário  (I)");
            for (int i = 0; i < Inventory.Size; i++)
            {
                var s = _bag.slots[i];
                DrawSlot(InvRect(i), s.Empty ? null : s.id, s.Empty || s.count < 2 ? null : s.count.ToString(), null, false, 0f);
            }
            MoveWindow(_inv, e, mouse);
        }

        if (_sk.show && _skills != null) DrawSkillWindow(e, mouse);
        if (_eq.show && _equip != null) DrawEquipWindow(e, mouse);

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

    private void DrawSkillWindow(Event e, Vector2 mouse)
    {
        var list = SkillDB.All;
        _sk.r.height = 40 + list.Count * (Slot + 8) + 24;
        GUI.Box(_sk.r, "Habilidades  (K)");

        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i];
            int lv = _skills.Level(d.id);
            Rect slot = SkillRect(i);
            DrawSlot(slot, ActionBar.SkillPrefix + d.id, null, null, lv == 0, 0f);

            GUI.Label(new Rect(slot.xMax + 10, slot.y + 2, 200, 20), d.name, _text);
            GUI.Label(new Rect(slot.xMax + 10, slot.y + 24, 200, 20), $"Nv {lv}/{d.maxLevel}   SP {d.spCost}", _text);

            GUI.enabled = _skills.skillPoints > 0 && lv < d.maxLevel;
            if (GUI.Button(new Rect(_sk.r.xMax - 40, slot.y + 12, 28, 24), "+")) _skills.Learn(d.id);
            GUI.enabled = true;
        }

        GUI.Label(new Rect(_sk.r.x + 10, _sk.r.yMax - 24, 280, 20), $"Pontos de habilidade: {_skills.skillPoints}", _text);
        MoveWindow(_sk, e, mouse);
    }

    private static void MoveWindow(Win w, Event e, Vector2 mouse)
    {
        var title = new Rect(w.r.x, w.r.y, w.r.width, 22);
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
                    if (e.button == 1) { _bag.Use(_bag.slots[i].id); e.Use(); return; }
                    if (e.button == 0) { Begin(Src.Inv, i, _bag.slots[i].id, mouse); e.Use(); return; }
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

            if (click && _dragSrc == Src.Bar) _bar.Trigger(_dragIndex);
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
        for (int i = 0; i < ActionBar.Size; i++) if (BarRect(i).Contains(mouse)) id = _bar.slots[i];
        if (_inv.show) for (int i = 0; i < Inventory.Size; i++) if (InvRect(i).Contains(mouse) && !_bag.slots[i].Empty) id = _bag.slots[i].id;
        if (_sk.show) for (int i = 0; i < SkillDB.All.Count; i++) if (SkillRect(i).Contains(mouse)) id = ActionBar.SkillPrefix + SkillDB.All[i].id;
        if (_eq.show && _equip != null)
            for (int i = 0; i < Equipment.Size; i++) if (EqRect((EquipSlot)i).Contains(mouse) && !string.IsNullOrEmpty(_equip.slots[i])) id = _equip.slots[i];
        if (string.IsNullOrEmpty(id)) return;

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
            text = d.heal > 0 ? $"{d.name}\nRecupera {d.heal} HP"
                 : d.kind == ItemKind.Equipment ? $"{d.name}\n{d.BonusText()}" : d.name;
        }

        var r = new Rect(mouse.x + 14, mouse.y - 64, 230, 60);
        GUI.Box(r, GUIContent.none);
        GUI.Label(new Rect(r.x + 6, r.y + 3, r.width - 8, r.height), text);
    }

    private bool Over(Vector2 mouse)
    {
        if (_inv.show && _inv.r.Contains(mouse)) return true;
        if (_sk.show && _sk.r.Contains(mouse)) return true;
        if (_eq.show && _eq.r.Contains(mouse)) return true;
        for (int i = 0; i < ActionBar.Size; i++) if (BarRect(i).Contains(mouse)) return true;
        return false;
    }
}
