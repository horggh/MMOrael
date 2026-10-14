using UnityEngine;
using UnityEngine.InputSystem;

/// Janela de inventário (tecla I) e barra de ação, com arrastar e soltar entre elas.
/// Clique na barra usa; clique direito no inventário usa; arrastar para fora da barra remove o atalho.
public class ItemUI : MonoBehaviour
{
    public static bool PointerOverUI { get; private set; }

    private const float Slot = 48f, Gap = 4f;
    private const int Cols = 6;

    private Inventory _bag;
    private ActionBar _bar;
    private bool _showInv;
    private Rect _invWin = new Rect(380, 64, Cols * (Slot + Gap) + 16, 4 * (Slot + Gap) + 36);

    private enum Src { None, Inv, Bar }
    private Src _dragSrc;
    private int _dragIndex;
    private string _dragId;
    private Vector2 _downPos;

    private GUIStyle _count, _key;

    private void Start()
    {
        _bag = GetComponent<Inventory>();
        _bar = GetComponent<ActionBar>();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame) _showInv = !_showInv;
    }

    private Rect BarRect(int i)
    {
        float total = ActionBar.Size * (Slot + Gap) - Gap;
        float x0 = (Screen.width - total) / 2f;
        return new Rect(x0 + i * (Slot + Gap), Screen.height - Slot - 14f, Slot, Slot);
    }

    // Coordenadas em tela (não relativas à janela), para o arrasto funcionar entre áreas.
    private Rect InvRect(int i)
    {
        int r = i / Cols, c = i % Cols;
        return new Rect(_invWin.x + 8 + c * (Slot + Gap), _invWin.y + 28 + r * (Slot + Gap), Slot, Slot);
    }

    private void OnGUI()
    {
        if (_bag == null || _bar == null) return;
        if (_count == null)
        {
            _count = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
            _count.normal.textColor = Color.white;
            _key = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.UpperLeft };
            _key.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
        }

        Event e = Event.current;
        Vector2 mouse = e.mousePosition;

        if (_showInv)
        {
            GUI.Box(_invWin, "Inventário");
            for (int i = 0; i < Inventory.Size; i++)
            {
                var s = _bag.slots[i];
                DrawSlot(InvRect(i), s.Empty ? null : s.id, s.Empty ? 0 : s.count, null);
            }
            // Barra de título arrastável (a parte de cima da janela).
            HandleWindowDrag(e, mouse);
        }

        for (int i = 0; i < ActionBar.Size; i++)
        {
            string id = _bar.slots[i];
            int n = string.IsNullOrEmpty(id) ? 0 : _bag.Count(id);
            DrawSlot(BarRect(i), id, n, ((i + 1) % 10).ToString(), dim: !string.IsNullOrEmpty(id) && n == 0);
        }

        HandleDragAndClicks(e, mouse);

        if (_dragSrc != Src.None)
        {
            var def = ItemDB.Get(_dragId);
            if (def?.icon != null) GUI.DrawTexture(new Rect(mouse.x - 20, mouse.y - 20, 40, 40), def.icon, ScaleMode.ScaleToFit);
        }
        else
        {
            DrawTooltip(mouse);
        }

        PointerOverUI = Over(mouse) || _dragSrc != Src.None;
    }

    private void DrawSlot(Rect r, string id, int count, string key, bool dim = false)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.25f);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - 1, r.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, 1, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - 1, r.y, 1, r.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        var def = ItemDB.Get(id);
        if (def?.icon != null)
        {
            GUI.color = dim ? new Color(1f, 1f, 1f, 0.3f) : Color.white;
            GUI.DrawTexture(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), def.icon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            if (count > 1 || dim) GUI.Label(new Rect(r.x, r.y, r.width - 3, r.height - 1), count.ToString(), _count);
        }
        if (key != null) GUI.Label(new Rect(r.x + 3, r.y + 1, 20, 16), key, _key);
    }

    private bool _movingWin;
    private Vector2 _winGrab;

    private void HandleWindowDrag(Event e, Vector2 mouse)
    {
        var title = new Rect(_invWin.x, _invWin.y, _invWin.width, 22);
        if (e.type == EventType.MouseDown && e.button == 0 && title.Contains(mouse))
        {
            _movingWin = true; _winGrab = mouse - _invWin.position; e.Use();
        }
        else if (_movingWin && e.type == EventType.MouseDrag)
        {
            _invWin.position = mouse - _winGrab;
            _invWin.x = Mathf.Clamp(_invWin.x, 0, Screen.width - _invWin.width);
            _invWin.y = Mathf.Clamp(_invWin.y, 0, Screen.height - _invWin.height);
            e.Use();
        }
        else if (_movingWin && e.type == EventType.MouseUp) { _movingWin = false; e.Use(); }
    }

    private void HandleDragAndClicks(Event e, Vector2 mouse)
    {
        if (e.type == EventType.MouseDown)
        {
            if (_showInv)
            {
                for (int i = 0; i < Inventory.Size; i++)
                {
                    if (!InvRect(i).Contains(mouse) || _bag.slots[i].Empty) continue;
                    if (e.button == 1) { _bag.Use(_bag.slots[i].id); e.Use(); return; }
                    if (e.button == 0) { Begin(Src.Inv, i, _bag.slots[i].id, mouse); e.Use(); return; }
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
            if (_showInv) for (int i = 0; i < Inventory.Size; i++) if (InvRect(i).Contains(mouse)) invTarget = i;

            if (click && _dragSrc == Src.Bar) _bar.Trigger(_dragIndex);
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
        if (_showInv) for (int i = 0; i < Inventory.Size; i++) if (InvRect(i).Contains(mouse) && !_bag.slots[i].Empty) id = _bag.slots[i].id;

        var def = ItemDB.Get(id);
        if (def == null) return;
        string text = def.heal > 0 ? $"{def.name}\nRecupera {def.heal} HP" : def.name;
        var r = new Rect(mouse.x + 14, mouse.y - 44, 170, 40);
        GUI.Box(r, GUIContent.none);
        GUI.Label(new Rect(r.x + 6, r.y + 3, r.width - 8, r.height), text);
    }

    private bool Over(Vector2 mouse)
    {
        if (_showInv && _invWin.Contains(mouse)) return true;
        for (int i = 0; i < ActionBar.Size; i++) if (BarRect(i).Contains(mouse)) return true;
        return false;
    }
}
