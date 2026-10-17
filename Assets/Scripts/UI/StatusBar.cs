using UnityEngine;

/// Barra de informações do personagem no topo da tela (HP, SP, nível, EXP e itens),
/// montada com as peças de Resources/UI/Status (Tools/extract_barra_status.py).
/// A arte é em alta resolução: medidas em pixels da referência (barra de 104 px de altura),
/// desenhadas com escala proporcional à altura da tela e suavização.
///
/// O layout mede o texto de verdade e coloca cada elemento depois do anterior: com fonte
/// maior ou menor (janela pequena, outra resolução) nada se encavala.
public static class StatusBar
{
    private const float H = 104f, Border = 14f, Corner = 28f;
    private const float Pad = 22f;        // da borda até o primeiro elemento
    private const float SepGap = 18f;     // de cada lado do separador
    private const float Gap = 9f;         // entre ícone, rótulo e número
    private const float FontPx = 22f;     // tamanho do texto, em pixels da referência
    private const float ExpBarW = 170f;

    private static Texture2D _cse, _csd, _cie, _cid, _top, _bottom, _left, _right, _wood, _sep,
                             _expFrame, _expFill, _iHp, _iSp, _iBag, _iMoney;
    private static GUIStyle _text;
    private static Font _serif;

    public static readonly Color Gold = new Color(0.95f, 0.8f, 0.42f);
    public static readonly Color Value = new Color(0.97f, 0.95f, 0.9f);

    /// Escala de partida: proporcional à altura da tela (~64 px de altura em 1080p). Se a
    /// barra não couber na largura disponível, Draw diminui até caber.
    private static float BaseScale => Screen.height / 1080f * 0.62f;

    private static Texture2D Load(string name)
    {
        var t = Resources.Load<Texture2D>("UI/Status/" + name);
        if (t != null) t.filterMode = FilterMode.Bilinear;
        return t;
    }

    private static void Init()
    {
        if (_wood != null) return;
        _cse = Load("canto_se"); _csd = Load("canto_sd"); _cie = Load("canto_ie"); _cid = Load("canto_id");
        _top = Load("borda_sup"); _bottom = Load("borda_inf"); _left = Load("borda_esq"); _right = Load("borda_dir");
        _wood = Load("madeira");
        if (_wood != null) _wood.wrapMode = TextureWrapMode.Repeat;
        _sep = Load("separador");
        _expFrame = Load("exp_moldura"); _expFill = Load("exp_cheia");
        _iHp = Load("icone_hp"); _iSp = Load("icone_sp"); _iBag = Load("icone_mochila");
        _iMoney = Load("icone_zeny");
        // Fonte com serifa, como na referência (do sistema; sem ela fica a padrão).
        try { _serif = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Cambria", "Times New Roman" }, 32); }
        catch { _serif = null; }
    }

    private static Vector2 _o;
    private static float _s;

    private static Rect R(float x, float y, float w, float h) => new Rect(_o.x + x * _s, _o.y + y * _s, w * _s, h * _s);

    private static void Draw(Texture2D t, Rect r)
    {
        if (t != null) GUI.DrawTexture(r, t);
    }

    private static GUIStyle Style(float size)
    {
        _text ??= new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(),
            clipping = TextClipping.Overflow, wordWrap = false,
        };
        if (_serif != null) _text.font = _serif;
        _text.fontSize = Mathf.Max(9, Mathf.RoundToInt(size * _s));
        return _text;
    }

    /// Largura do texto em pixels da referência (com o tamanho real da fonte na tela).
    private static float Measure(string s, float size) => Style(size).CalcSize(new GUIContent(s)).x / _s;

    /// Texto com sombra, centralizado na vertical em y. Devolve a largura usada.
    private static float Text(float x, float y, string s, Color c, float size)
    {
        var style = Style(size);
        var content = new GUIContent(s);
        float w = style.CalcSize(content).x / _s;
        float h = style.CalcSize(content).y / _s;
        var r = R(x, y - h / 2f, w + 2, h);
        style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), content, style);
        style.normal.textColor = c;
        GUI.Label(r, content, style);
        return w;
    }

    private static void DrawFrame(float width)
    {
        if (_wood != null)
        {
            float innerW = width - 2 * Border, innerH = H - 2 * Border;
            GUI.DrawTextureWithTexCoords(R(Border, Border, innerW, innerH), _wood,
                new Rect(0f, 0f, innerW / _wood.width, innerH / _wood.height));
        }
        Draw(_top, R(Corner, 0, width - 2 * Corner, Border));
        Draw(_bottom, R(Corner, H - Border, width - 2 * Corner, Border));
        Draw(_left, R(0, Corner, Border, H - 2 * Corner));
        Draw(_right, R(width - Border, Corner, Border, H - 2 * Corner));
        Draw(_cse, R(0, 0, Corner, Corner));
        Draw(_csd, R(width - Corner, 0, Corner, Corner));
        Draw(_cie, R(0, H - Corner, Corner, Corner));
        Draw(_cid, R(width - Corner, H - Corner, Corner, Corner));
    }

    /// Texto em cima, barra de progresso embaixo, as duas com a largura da seção.
    private static void ExpSection(float x, float mid, float width, string label, string value, float fill, float font)
    {
        float lw = Text(x, mid - 13, label, Gold, font);
        Text(x + lw + Gap, mid - 13, value, Value, font);
        Draw(_expFrame, R(x, mid + 5, width, 18));
        float f = Mathf.Clamp01(fill);
        if (_expFill != null && f > 0f) GUI.DrawTexture(R(x + 4, mid + 8, (width - 8) * f, 12), _expFill);
    }

    // Seção "ícone + rótulo + número": largura medida e desenho.
    private static float LineWidth(float iconW, string label, string value)
    {
        float w = 0f;
        if (iconW > 0) w += iconW + Gap;
        if (label != null) w += Measure(label, FontPx) + Gap;
        if (value != null) w += Measure(value, FontPx);
        return w;
    }

    private static void Line(float x, float mid, Texture2D icon, float iconW, float iconH, string label, string value)
    {
        if (icon != null) { Draw(icon, R(x, mid - iconH / 2f, iconW, iconH)); x += iconW + Gap; }
        if (label != null) x += Text(x, mid, label, Gold, FontPx) + Gap;
        if (value != null) Text(x, mid, value, Value, FontPx);
    }

    /// Desenha a barra com os valores atuais. maxWidth = espaço na tela até onde ela pode ir.
    /// Devolve o retângulo ocupado na tela.
    public static Rect Draw(Vector2 origin, float maxWidth, int hp, int maxHp, int sp, int maxSp, int level,
                            int exp, int expNext, int jobExp, int jobNext, int money, int itemsUsed, int itemsMax)
    {
        Init();
        _o = origin;
        const float mid = H / 2f;
        float expFont = FontPx * 0.85f;

        string hpV = $"{hp}/{maxHp}", spV = $"{sp}/{maxSp}", lvl = $"Nível {level}";
        string expL = "EXP Base", expV = $"{exp}/{expNext}", bagV = $"{itemsUsed}/{itemsMax}";
        string moneyV = Inventory.MoneyText(money);
        string jobL = "EXP Skill", jobV = jobNext > 0 ? $"{jobExp}/{jobNext}" : "máximo";
        if (expNext <= 0) expV = "máximo";

        // 1º passo: mede cada seção. Se não couber na largura disponível, diminui a escala e
        // mede de novo (a fonte muda de tamanho junto, então a medida também muda).
        float[] w = new float[7];
        float total = 0f;
        _s = BaseScale;
        for (int pass = 0; pass < 3; pass++)
        {
            w[0] = LineWidth(35, "HP", hpV);
            w[1] = LineWidth(30, "SP", spV);
            w[2] = LineWidth(0, null, lvl);
            w[3] = Mathf.Max(ExpBarW, Measure(expL, expFont) + Gap + Measure(expV, expFont));
            w[4] = Mathf.Max(ExpBarW, Measure(jobL, expFont) + Gap + Measure(jobV, expFont));
            w[5] = LineWidth(31, null, moneyV);
            w[6] = LineWidth(39, null, bagV);
            total = Border + Pad + Pad + Border + SepGap * 2 * (w.Length - 1);
            foreach (var v in w) total += v;
            if (total * _s <= maxWidth || maxWidth <= 0f) break;
            _s *= maxWidth / (total * _s);
        }
        DrawFrame(total);

        // 2º passo: desenha, cada seção depois da anterior.
        float x = Border + Pad;
        for (int i = 0; i < w.Length; i++)
        {
            switch (i)
            {
                case 0: Line(x, mid, _iHp, 35, 32, "HP", hpV); break;
                case 1: Line(x, mid, _iSp, 30, 32, "SP", spV); break;
                case 2: Line(x, mid, null, 0, 0, null, lvl); break;
                case 3: ExpSection(x, mid, w[i], expL, expV, expNext > 0 ? (float)exp / expNext : 1f, expFont); break;
                case 4: ExpSection(x, mid, w[i], jobL, jobV, jobNext > 0 ? (float)jobExp / jobNext : 1f, expFont); break;
                case 5: Line(x, mid, _iMoney, 31, 32, null, moneyV); break;
                case 6: Line(x, mid, _iBag, 39, 40, null, bagV); break;
            }
            x += w[i];
            if (i < w.Length - 1)
            {
                Draw(_sep, R(x + SepGap - 2.5f, mid - 24, 5, 48));
                x += SepGap * 2;
            }
        }
        return R(0, 0, total, H);
    }
}
