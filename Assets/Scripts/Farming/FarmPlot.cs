using System.Collections.Generic;
using UnityEngine;

/// Um canteiro arado. Sem semente por 2 min, volta a ser grama. Plantado, cada estágio
/// leva 2 min e só avança com o chão regado; ao mudar de estágio a terra seca de novo.
public class FarmPlot : MonoBehaviour
{
    public const float AbandonTime = 120f;
    public const float StageTime = 120f;
    public const int Yield = 5;
    // morango_02 (sementes) .. morango_05 (maduro)
    private const int FirstStage = 2, LastStage = 5;

    // Campos serializados: sobrevivem se o Unity recompilar no meio do Play.
    [SerializeField] private int _stage;
    [SerializeField] private bool _wet;
    public int Stage { get => _stage; private set => _stage = value; }   // 0 = só terra arada
    public bool Planted => Stage > 0;
    public bool Ripe => Stage == LastStage;
    public bool Wet { get => _wet; private set => _wet = value; }

    // Sprites por estágio, seco e molhado (molhado = só os tons de terra escurecidos).
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
    private SpriteRenderer _sr;
    [SerializeField] private float _timer;

    private void Awake()
    {
        _sr = gameObject.AddComponent<SpriteRenderer>();
        Refresh();
    }

    private void OnEnable() => FarmGrid.Register(this);

    private void Update()
    {
        if (!Planted)
        {
            _timer += Time.deltaTime;
            if (_timer >= AbandonTime) FarmGrid.Remove(this);
            return;
        }
        if (Ripe || !Wet) return;
        _timer += Time.deltaTime;
        if (_timer < StageTime) return;
        Stage++;
        Wet = false;
        _timer = 0f;
        Refresh();
    }

    private static GUIStyle _clock;

    /// Contagem do estágio atual logo acima da plantação. Seca, o tempo fica parado em cinza.
    private void OnGUI()
    {
        if (!Planted || Camera.main == null) return;
        _clock ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
        };
        float top = Stage >= 3 ? 1.1f : 0.6f;
        Vector3 p = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * top);
        if (p.z < 0) return;
        string text;
        Color color;
        if (Ripe) { text = "Colher!"; color = new Color(1f, 0.85f, 0.3f); }
        else
        {
            int left = Mathf.CeilToInt(StageTime - _timer);
            text = $"{left / 60}:{left % 60:00}" + (Wet ? "" : " (regar)");
            color = Wet ? new Color(0.55f, 0.85f, 1f) : new Color(0.75f, 0.75f, 0.75f);
        }
        var r = new Rect(p.x - 50, Screen.height - p.y - 10, 100, 20);
        _clock.normal.textColor = new Color(0, 0, 0, 0.8f);
        GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, _clock);   // sombra
        _clock.normal.textColor = color;
        GUI.Label(r, text, _clock);
    }

    public float Timer => _timer;

    public void Restore(int stage, bool wet, float timer)
    {
        Stage = stage;
        Wet = wet;
        _timer = timer;
        Refresh();
    }

    public void Plant() { Stage = FirstStage; _timer = 0f; Refresh(); }
    public void Water() { Wet = true; Refresh(); }

    public void Harvest()
    {
        Stage = 0;
        Wet = false;
        _timer = 0f;
        Refresh();
    }

    private void Refresh()
    {
        _sr.sprite = Load($"morango_0{(Planted ? Stage : 1)}", Wet);
        // Planta alta entra na ordem de profundidade pelos pés; terra pura fica no chão.
        _sr.sortingOrder = Stage >= 3 ? YSortRenderer.OrderFor(transform.position.y - 0.5f) : -9000;
    }

    private static Sprite Load(string name, bool wet)
    {
        string key = name + (wet ? "_molhado" : "");
        if (Cache.TryGetValue(key, out var s)) return s;
        var tex = Resources.Load<Texture2D>("Crops/" + name);
        if (tex == null) return null;
        if (wet) tex = Darken(tex);
        tex.filterMode = FilterMode.Point;
        // Terra de 34 px na base da imagem: o pivô fica no centro dela, assim estágios altos
        // crescem para cima sem tirar o canteiro do lugar. 32 px por unidade.
        var pivot = new Vector2(0.5f, 17f / tex.height);
        s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, 64);
        Cache[key] = s;
        return s;
    }

    /// Escurece só os tons de terra (marrons); folhas e frutos mantêm a cor.
    private static Texture2D Darken(Texture2D src)
    {
        var px = src.GetPixels32();
        for (int i = 0; i < px.Length; i++)
        {
            var c = px[i];
            bool soil = c.r >= c.g && c.g >= c.b && c.r < c.g * 1.6f + 10;
            if (c.a == 0 || !soil) continue;
            px[i] = new Color32((byte)(c.r * 0.62f), (byte)(c.g * 0.6f), (byte)(c.b * 0.62f), c.a);
        }
        var t = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        t.SetPixels32(px);
        t.Apply();
        return t;
    }
}
