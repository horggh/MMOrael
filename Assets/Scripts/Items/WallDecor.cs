using System.Collections.Generic;
using UnityEngine;

/// Janelas na parede do fundo da casa (por cima do papel de parede). Sprites em
/// Resources/Windows/<id>.png (64 px = 1 quadrado), gerados por Tools/extract_janelas.py.
///
/// Janela com vidro vazado ganha luz de dia: o céu (<id>_ceu.png) por trás dos vãos e raios
/// de sol descendo pela sala, com uma leve pulsação.
public class WallDecor : MonoBehaviour
{
    private const float PPU = 64f;
    public const int SkyOrder = -8975, WindowOrder = -8970, RaysOrder = 25000;

    /// Todas as janelas colocadas (para o jogo salvo).
    public static readonly List<WallDecor> All = new List<WallDecor>();

    /// Janelas na ordem da aba (clássicas primeiro): id, nome.
    public static readonly (string id, string name)[] Catalog =
    {
        ("janela_simples", "Simples"), ("janela_venezianas_abertas", "Venezianas abertas"),
        ("janela_arco_madeira", "Arco de madeira"), ("janela_floreira", "Com floreira"),
        ("janela_cortinas_vermelhas", "Cortinas vermelhas"), ("janela_portinhola", "Portinhola fechada"),
        ("janela_grade_ferro", "Grade de ferro"), ("janela_cela", "Cela"),
        ("janela_quebrada", "Quebrada com tábuas"), ("janela_porao", "Porão"),
        ("janela_escotilha_metal", "Escotilha de metal"), ("janela_seteira", "Seteira"),
        ("janela_vitral_gotico", "Vitral gótico"), ("janela_rosacea", "Rosácea"),
        ("janela_elfica", "Élfica"), ("janela_runas_brilhantes", "Runas brilhantes"),
        ("janela_arcos_pedra", "Arcos de pedra"), ("janela_lua_estrela", "Lua e estrela"),
        ("janela_cortinas_brancas", "Cortinas brancas"), ("janela_cortinas_azuis", "Cortinas azuis"),
        ("janela_sacada_telhado", "Sacada com telhado"), ("janela_triangular", "Triangular"),
        ("janela_moderna", "Moderna"), ("janela_shoji", "Shoji"),
        ("janela_hera_redonda", "Redonda com hera"), ("janela_congelada", "Congelada"),
        ("janela_nave", "Nave espacial"), ("janela_ferro_forjado", "Ferro forjado"),
        ("janela_tabuas", "Tábuas pregadas"), ("janela_tijolo_redonda", "Redonda de tijolo"),
        ("janela_vitral_floresta", "Vitral da floresta"), ("janela_tela_futurista", "Tela futurista"),
        ("janela_engrenagem", "Escotilha de engrenagem"), ("janela_vitral_morcego", "Vitral do morcego"),
        ("janela_lua_sol", "Lua e sol"), ("janela_quadrada_entalhada", "Quadrada entalhada"),
        ("janela_doze_vidros", "Doze vidros"), ("janela_sacada_pedra", "Sacada de pedra"),
        ("janela_art_deco", "Art déco"), ("janela_losangos", "Losangos de chumbo"),
        ("janela_dupla", "Dupla"), ("janela_cruz_pedra", "Cruz de pedra"),
        ("janela_hera", "Coberta de hera"), ("janela_arco_cortina", "Arco com pano"),
        ("janela_aquario", "Aquário"), ("janela_lava", "Lava"),
        ("janela_teia", "Teia"), ("janela_musgo", "Musgo"),
        ("janela_sacada_cortinas", "Sacada com cortinas"), ("janela_estufa", "Estufa"),
        ("janela_venezianas_floreira", "Venezianas com floreira"), ("janela_claraboia", "Claraboia"),
        ("janela_vitral_fenix", "Vitral da fênix"), ("janela_meia_lua", "Meia-lua"),
        ("janela_lapide", "Lápide de caveira"), ("janela_gaiola", "Gaiola suspensa"),
        ("janela_tela_metal", "Tela de metal"), ("janela_escotilha_mar", "Escotilha do mar"),
        ("janela_casinha_doce", "Casinha de doce"), ("janela_moldura_barroca", "Moldura barroca"),
    };

    public string id;
    /// Quadrado de baixo à esquerda ocupado na grade.
    public Vector2Int origin;
    public Vector2Int Size { get; private set; }

    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
    private static Sprite _rays;
    private SpriteRenderer _raysSr;
    private float _phase;

    public static Texture2D Texture(string id)
    {
        var tex = Resources.Load<Texture2D>("Windows/" + id);
        if (tex != null) tex.filterMode = FilterMode.Point;
        return tex;
    }

    public static Sprite SpriteFor(string id)
    {
        if (Cache.TryGetValue(id, out var s) && s != null) return s;
        var tex = Texture(id);
        if (tex == null) return null;
        s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), PPU);
        Cache[id] = s;
        return s;
    }

    /// Quadrados que a janela ocupa (arredondado para cima a partir do tamanho do desenho).
    public static Vector2Int SizeOf(string id)
    {
        var tex = Texture(id);
        if (tex == null) return Vector2Int.one;
        return new Vector2Int(Mathf.CeilToInt(tex.width / PPU), Mathf.CeilToInt(tex.height / PPU));
    }

    /// Centro da área ocupada, onde fica o desenho.
    public static Vector2 CenterOf(Vector2Int origin, Vector2Int size) =>
        new Vector2(origin.x + size.x / 2f, origin.y + size.y / 2f);

    /// Janela que ocupa o quadrado (null = nenhuma). except: ignora essa (a que está sendo movida).
    public static WallDecor At(Vector2Int c, WallDecor except = null)
    {
        foreach (var d in All)
            if (d != except && c.x >= d.origin.x && c.x < d.origin.x + d.Size.x && c.y >= d.origin.y && c.y < d.origin.y + d.Size.y)
                return d;
        return null;
    }

    public static WallDecor Create(string id, Vector2Int origin)
    {
        var go = new GameObject("Janela " + id);
        var d = go.AddComponent<WallDecor>();
        d.id = id;
        d.origin = origin;
        d.Size = SizeOf(id);
        go.transform.position = CenterOf(origin, d.Size);
        d.Build();
        return d;
    }

    private void Build()
    {
        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFor(id);
        sr.sortingOrder = WindowOrder;

        // Céu por trás dos vãos: só existe para janela com vidro vazado.
        var skyTex = Resources.Load<Texture2D>("Windows/" + id + "_ceu");
        if (skyTex == null) return;
        skyTex.filterMode = FilterMode.Point;
        var sky = new GameObject("Céu").AddComponent<SpriteRenderer>();
        sky.transform.SetParent(transform, false);
        sky.sprite = Sprite.Create(skyTex, new Rect(0, 0, skyTex.width, skyTex.height), new Vector2(0.5f, 0.5f), PPU);
        sky.sortingOrder = SkyOrder;

        // Raios de sol: saem da base da janela e descem inclinados pela sala.
        if (_rays == null)
        {
            var rt = Resources.Load<Texture2D>("Windows/luz_raios");
            if (rt != null) _rays = Sprite.Create(rt, new Rect(0, 0, rt.width, rt.height), new Vector2(0.5f, 1f), PPU);
        }
        if (_rays == null) return;
        _raysSr = new GameObject("Raios de sol").AddComponent<SpriteRenderer>();
        _raysSr.transform.SetParent(transform, false);
        float w = sr.sprite.bounds.size.x, h = sr.sprite.bounds.size.y;
        _raysSr.transform.localPosition = new Vector3(0f, -h * 0.25f, 0f);
        _raysSr.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
        _raysSr.transform.localScale = new Vector3(w * 0.8f, 1.25f, 1f);
        _raysSr.sprite = _rays;
        _raysSr.sortingOrder = RaysOrder;
        _phase = Random.value * 10f;
    }

    /// Muda de lugar (martelo) sem recriar: mantém tudo como está.
    public void MoveTo(Vector2Int newOrigin)
    {
        origin = newOrigin;
        transform.position = CenterOf(origin, Size);
    }

    public void SetHidden(bool hidden)
    {
        foreach (var r in GetComponentsInChildren<SpriteRenderer>(true)) r.enabled = !hidden;
    }

    private void Update()
    {
        if (_raysSr == null) return;
        // Pulsação lenta, como luz passando por folhas lá fora.
        float a = 0.55f + 0.12f * Mathf.Sin((Time.time + _phase) * 0.8f);
        _raysSr.color = new Color(1f, 1f, 1f, a);
    }

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);
}
