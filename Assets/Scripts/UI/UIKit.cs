using UnityEngine;

/// Peças da interface em pixel art (Resources/UI/Kit, geradas por Tools/gen_ui_kit.py)
/// e funções para montar janelas de qualquer tamanho no mesmo estilo da de equipamento.
/// Medidas em pixels da arte; na tela tudo é multiplicado por Px (escala inteira).
public static class UIKit
{
    // Medidas da moldura (ver gen_ui_kit.py).
    public const int Side = 8, Top = 32, Bottom = 11, Corner = 34, CornerTopH = 44, CornerBottomH = 32;
    public const int TitleH = 32, SlotSize = 42;

    /// Escala inteira para o pixel art não deformar: 2x em 1080p, 3x em 1440p.
    public static int Px => Mathf.Clamp(Mathf.FloorToInt(Screen.height / 450f), 1, 4);

    private static Texture2D _tl, _tr, _bl, _br, _top, _bottom, _left, _right, _wood, _button, _slot;
    private static GUIStyle _label;

    public static readonly Color Cream = new Color(0.93f, 0.85f, 0.7f);
    public static readonly Color Gold = new Color(0.96f, 0.8f, 0.48f);
    public static readonly Color Dim = new Color(0.62f, 0.55f, 0.46f);

    public static Texture2D Load(string path)
    {
        var t = Resources.Load<Texture2D>("UI/" + path);
        if (t != null) t.filterMode = FilterMode.Point;
        return t;
    }

    private static void Init()
    {
        if (_tl != null) return;
        _tl = Load("Kit/canto_sup_esq"); _tr = Load("Kit/canto_sup_dir");
        _bl = Load("Kit/canto_inf_esq"); _br = Load("Kit/canto_inf_dir");
        _top = Load("Kit/borda_sup"); _bottom = Load("Kit/borda_inf");
        _left = Load("Kit/borda_esq"); _right = Load("Kit/borda_dir");
        // Fundo das janelas: a madeira da barra de status (Tools/gen_fundo_janelas.py).
        _wood = Load("Kit/fundo_janela");
        if (_wood == null) _wood = Load("Kit/madeira");
        if (_wood != null) _wood.wrapMode = TextureWrapMode.Repeat;   // repete sem emenda
        _button = Load("Kit/botao");
        _slot = Load("Equipamento/slot");
    }

    /// Retângulo na tela a partir de uma posição de tela e medidas da arte.
    public static Rect ToScreen(Vector2 origin, float x, float y, float w, float h)
    {
        int px = Px;
        return new Rect(origin.x + x * px, origin.y + y * px, w * px, h * px);
    }

    private static void Draw(Texture2D t, Rect r)
    {
        if (t != null) GUI.DrawTexture(r, t);
    }

    /// Moldura completa: madeira repetida no corpo, bordas esticadas, cantos e o título no meio do topo.
    public static void Frame(Vector2 o, int w, int h, Texture2D title)
    {
        Init();
        if (_wood != null)
        {
            Rect body = ToScreen(o, Side, Top, w - 2 * Side, h - Top - Bottom);
            float u = (w - 2f * Side) / _wood.width, v = (h - Top - Bottom) / (float)_wood.height;
            // As coordenadas de textura começam embaixo: o topo do corpo fica alinhado com o topo da madeira.
            GUI.DrawTextureWithTexCoords(body, _wood, new Rect(0f, 1f - v, u, v));
        }
        Draw(_top, ToScreen(o, Corner, 0, w - 2 * Corner, Top));
        Draw(_bottom, ToScreen(o, Corner, h - 12, w - 2 * Corner, 12));
        Draw(_left, ToScreen(o, 0, CornerTopH, Side, h - CornerTopH - CornerBottomH));
        Draw(_right, ToScreen(o, w - Side, CornerTopH, Side, h - CornerTopH - CornerBottomH));
        Corners(o, w, h);
        if (title != null) Draw(title, ToScreen(o, (w - title.width) / 2, 0, title.width, title.height));
    }

    /// Só os quatro cantos ornamentados. O miolo deles é transparente, então dá para
    /// desenhá-los por cima de uma arte pronta e deixá-la com os mesmos cantos das outras janelas.
    public static void Corners(Vector2 o, int w, int h)
    {
        Init();
        Draw(_tl, ToScreen(o, 0, 0, Corner, CornerTopH));
        Draw(_tr, ToScreen(o, w - Corner, 0, Corner, CornerTopH));
        Draw(_bl, ToScreen(o, 0, h - CornerBottomH, Corner, CornerBottomH));
        Draw(_br, ToScreen(o, w - Corner, h - CornerBottomH, Corner, CornerBottomH));
    }

    /// Posição do botão de fechar, igual em todas as janelas: no cabeçalho, antes do canto direito.
    public static Rect CloseArt(int w) => new Rect(w - Corner - 21, 7, 20, 19);

    /// Barra horizontal de lado a lado, presa nas bordas pelas junções. y = topo da barra.
    public static void DividerH(Vector2 o, int w, float y)
    {
        _divH ??= Load("Kit/divisoria_h");
        _joinL ??= Load("Kit/juncao_esq");
        _joinR ??= Load("Kit/juncao_dir");
        // Nas junções a barra começa 4 px abaixo do topo da peça.
        Draw(_divH, ToScreen(o, 16, y, w - 32, 6));
        Draw(_joinL, ToScreen(o, 0, y - 4, 16, 14));
        Draw(_joinR, ToScreen(o, w - 16, y - 4, 16, 14));
    }

    private static Texture2D _divH, _joinL, _joinR, _divV;

    /// Barra de rolagem: trilho de metal (a divisória vertical) e marcador de cobre (o botão).
    public static void Scrollbar(Rect track, Rect thumb)
    {
        Init();
        _divV ??= Load("Kit/divisoria_v");
        Draw(_divV, track);
        Draw(_button, thumb);
    }

    /// Espaço de item com a moldura de metal; o ícone vai por dentro da borda.
    public static void Slot(Rect r, Texture2D icon, bool hover, bool dim = false)
    {
        Init();
        Draw(_slot, r);
        int px = Px;
        var inner = new Rect(r.x + 6 * px, r.y + 6 * px, r.width - 12 * px, r.height - 12 * px);
        if (hover)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            GUI.DrawTexture(inner, Texture2D.whiteTexture);
        }
        if (icon != null)
        {
            GUI.color = dim ? new Color(1f, 1f, 1f, 0.3f) : Color.white;
            GUI.DrawTexture(inner, icon, ScaleMode.ScaleToFit);
        }
        GUI.color = Color.white;
    }

    /// Botão de cobre. Devolve true no clique.
    public static bool Button(Rect r, string text, bool enabled, int textSize = 8, bool selected = false)
    {
        Init();
        GUI.color = enabled ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
        if (_button != null && r.width > r.height * 1.5f)
        {
            // Botão largo: as pontas de 6 px no tamanho original, só o miolo estica.
            float edge = 6f * Px, u = 6f / _button.width;
            GUI.DrawTextureWithTexCoords(new Rect(r.x, r.y, edge, r.height), _button, new Rect(0f, 0f, u, 1f));
            GUI.DrawTextureWithTexCoords(new Rect(r.x + edge, r.y, r.width - 2 * edge, r.height), _button, new Rect(u, 0f, 1f - 2 * u, 1f));
            GUI.DrawTextureWithTexCoords(new Rect(r.xMax - edge, r.y, edge, r.height), _button, new Rect(1f - u, 0f, u, 1f));
        }
        else Draw(_button, r);
        GUI.color = Color.white;
        Label(r, text, textSize, selected ? Gold : enabled ? Cream : Dim, TextAnchor.MiddleCenter);

        Event e = Event.current;
        if (enabled && e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
        {
            e.Use();
            return true;
        }
        return false;
    }

    /// Texto com sombra de 1 pixel da arte. size em pixels da arte.
    private static GUIStyle _field;
    private static Texture2D _fieldBg, _fieldBorder;

    /// Uma caixa de texto com o campo focado: enquanto for verdade, os atalhos de teclado
    /// (1–0, I, E, K, C) ficam desligados para o que se digita não acionar nada.
    /// (Só vale se a caixa foi desenhada agora: fechar a janela com o campo focado não pode
    /// deixar os atalhos desligados para sempre.)
    public static bool Typing => GUIUtility.keyboardControl != 0 && Time.frameCount - _fieldFrame <= 1;
    private static int _fieldFrame = -10;

    /// Caixa de texto do kit (fundo escuro, borda dourada). Enter ou Esc tira o foco.
    public static string TextField(Rect r, string text, int size, int maxLength = 6)
    {
        if (_fieldBg == null)
        {
            _fieldBg = new Texture2D(1, 1);
            _fieldBg.SetPixel(0, 0, new Color(0.08f, 0.05f, 0.04f, 0.95f));
            _fieldBg.Apply();
            _fieldBorder = new Texture2D(1, 1);
            _fieldBorder.SetPixel(0, 0, new Color(0.62f, 0.48f, 0.26f));
            _fieldBorder.Apply();
        }
        _field ??= new GUIStyle(GUI.skin.textField) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, padding = new RectOffset() };
        _field.normal.background = _field.hover.background = _field.focused.background = _fieldBg;
        _field.normal.textColor = _field.hover.textColor = _field.focused.textColor = Cream;
        _field.fontSize = size * Px;

        _fieldFrame = Time.frameCount;
        var e = Event.current;
        // Clique fora da caixa também encerra a digitação.
        if (e.type == EventType.MouseDown && !r.Contains(e.mousePosition)) GUIUtility.keyboardControl = 0;
        if (Typing && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Escape))
        {
            GUIUtility.keyboardControl = 0;
            e.Use();
        }
        float b = Px;
        GUI.DrawTexture(new Rect(r.x - b, r.y - b, r.width + 2 * b, r.height + 2 * b), _fieldBorder);
        return GUI.TextField(r, text, maxLength, _field);
    }

    public static void Label(Rect r, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft)
    {
        _label ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, padding = new RectOffset(), clipping = TextClipping.Overflow };
        int px = Px;
        _label.fontSize = size * px;
        _label.alignment = align;
        _label.normal.textColor = new Color(0.1f, 0.06f, 0.05f, 0.9f);
        GUI.Label(new Rect(r.x + px, r.y + px, r.width, r.height), text, _label);
        _label.normal.textColor = color;
        GUI.Label(r, text, _label);
    }
}

/// Janela do kit: posição, abrir/fechar, arrastar pela faixa do título e botão de fechar.
public class KitWindow
{
    public Vector2 pos;
    public int w, h;            // pixels da arte
    public bool show;

    // Carregado no primeiro desenho: a Unity não deixa usar Resources no construtor de um MonoBehaviour.
    private readonly string _titleName;
    private Texture2D _title;

    private bool _moving;
    private Vector2 _grab;
    private static Texture2D _close;

    public KitWindow(float x, float y, int w, int h, string titleName)
    {
        pos = new Vector2(x, y);
        this.w = w;
        this.h = h;
        _titleName = titleName;
    }

    public Rect ScreenRect => UIKit.ToScreen(pos, 0, 0, w, h);

    /// Retângulo na tela de algo posicionado em pixels da arte dentro da janela.
    public Rect Art(float x, float y, float aw, float ah) => UIKit.ToScreen(pos, x, y, aw, ah);

    // Fica no cabeçalho, entre a ponta da faixa do título e o canto ornamentado.
    private Rect CloseArt => UIKit.CloseArt(w);

    public void DrawFrame()
    {
        if (_title == null) _title = UIKit.Load("Kit/" + _titleName);
        UIKit.Frame(pos, w, h, _title);
        if (_close == null) _close = UIKit.Load("Equipamento/botao_fechar");
        if (_close != null) GUI.DrawTexture(Art(CloseArt.x, CloseArt.y, CloseArt.width, CloseArt.height), _close);
    }

    /// Chamar depois do conteúdo: o fechar e o arrastar só pegam o que sobrou do clique.
    public void HandleInput(Event e)
    {
        Vector2 mouse = e.mousePosition;
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (Art(CloseArt.x, CloseArt.y, CloseArt.width, CloseArt.height).Contains(mouse))
            {
                show = false;
                e.Use();
            }
            else if (Art(0, 0, w, UIKit.TitleH).Contains(mouse))
            {
                _moving = true;
                _grab = mouse - pos;
                e.Use();
            }
        }
        else if (_moving && e.type == EventType.MouseDrag)
        {
            Rect r = ScreenRect;
            pos = mouse - _grab;
            pos.x = Mathf.Clamp(pos.x, 0, Screen.width - r.width);
            pos.y = Mathf.Clamp(pos.y, 0, Screen.height - r.height);
            e.Use();
        }
        else if (_moving && e.type == EventType.MouseUp)
        {
            _moving = false;
            e.Use();
        }
    }
}
