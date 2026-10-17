using System.Collections.Generic;
using UnityEngine;

/// Revestimentos pintados um por quadrado da grade: pisos (no chão, dentro ou fora de casa)
/// e papéis de parede (na parede do fundo da casa). Sprites 64x64 = 1 quadrado:
/// Resources/Floors/<id>.png (Tools/extract_pisos.py) e Resources/Walls/<id>.png
/// (Tools/extract_papeis.py; ids começam com "parede_").
/// Pisos ficam acima do chão e abaixo de canteiros, móveis e personagens; papéis ficam
/// sobre a parede da sala e abaixo do rodapé.
public static class FloorTiles
{
    public const int SortOrder = -9100;
    public const int WallOrder = -8990;

    public static bool IsWall(string id) => id != null && id.StartsWith("parede_");

    /// Pisos na ordem por tema (a mesma do extract_pisos.py): id, nome, tema.
    public static readonly (string id, string name, string theme)[] Catalog =
    {
        ("piso_liso", "Liso", "Lajotas"),
        ("piso_lajotas", "Lajotas", "Lajotas"),
        ("piso_lajotas_rachadas", "Lajotas rachadas", "Lajotas"),
        ("piso_lajotas_manchadas", "Lajotas manchadas", "Lajotas"),
        ("piso_tijolos", "Tijolos", "Lajotas"),
        ("piso_rachado", "Rachado", "Desgastado"),
        ("piso_pedras_soltas", "Pedras soltas", "Desgastado"),
        ("piso_manchas", "Manchas", "Desgastado"),
        ("piso_quebrado", "Quebrado com vergalhão", "Desgastado"),
        ("piso_arranhado", "Arranhado", "Desgastado"),
        ("piso_musgo", "Musgo", "Natureza"),
        ("piso_raizes", "Raízes", "Natureza"),
        ("piso_cascalho", "Cascalho", "Natureza"),
        ("piso_concreto", "Concreto", "Urbano"),
        ("piso_canaletas", "Canaletas", "Urbano"),
        ("piso_grade", "Grade", "Urbano"),
        ("piso_bueiro", "Bueiro", "Urbano"),
        ("piso_faixa", "Faixa", "Urbano"),
        ("piso_placa", "Placa de metal", "Urbano"),
        ("piso_retalhos", "Retalhos", "Urbano"),
    };

    // Preenchido pelos próprios FloorTile ao ligar (ver FloorTile.cs).
    private static readonly Dictionary<Vector2Int, FloorTile> Tiles = new Dictionary<Vector2Int, FloorTile>();
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
    private static Transform _root;

    /// Papéis de parede na ordem por tema (a mesma do extract_papeis.py): id, nome, tema.
    public static readonly (string id, string name, string theme)[] WallCatalog =
    {
        ("parede_damasco_vermelho", "Damasco vermelho", "Clássico"),
        ("parede_damasco_roxo", "Damasco roxo", "Clássico"),
        ("parede_flor_de_lis", "Flor-de-lis", "Clássico"),
        ("parede_ornamento_azul", "Ornamento azul", "Clássico"),
        ("parede_listras_bege", "Listras bege", "Clássico"),
        ("parede_coroas", "Coroas reais", "Clássico"),
        ("parede_folhas_douradas", "Folhas douradas", "Clássico"),
        ("parede_leque", "Leque art déco", "Clássico"),
        ("parede_rosas", "Rosas", "Natureza"),
        ("parede_flores_rosa", "Florzinhas", "Natureza"),
        ("parede_folhagem", "Folhagem", "Natureza"),
        ("parede_arvores", "Árvores e pássaros", "Natureza"),
        ("parede_cogumelos", "Cogumelos", "Natureza"),
        ("parede_ceu", "Céu", "Natureza"),
        ("parede_mar", "Mar", "Natureza"),
        ("parede_noite", "Noite estrelada", "Natureza"),
        ("parede_xadrez", "Xadrez", "Padrões"),
        ("parede_losangos", "Losangos", "Padrões"),
        ("parede_ondas", "Ondas retrô", "Padrões"),
        ("parede_zigue_zague", "Zigue-zague", "Padrões"),
        ("parede_aneis", "Anéis", "Padrões"),
        ("parede_mosaico", "Mosaico", "Padrões"),
        ("parede_bolinhas", "Bolinhas", "Padrões"),
        ("parede_listras_coloridas", "Listras coloridas", "Padrões"),
        ("parede_listras_marinho", "Listras marinho", "Padrões"),
        ("parede_labirinto", "Labirinto", "Padrões"),
        ("parede_azulejos", "Azulejos", "Padrões"),
        ("parede_runas", "Runas", "Fantasia"),
        ("parede_coracoes_sombrios", "Corações sombrios", "Fantasia"),
        ("parede_coracoes", "Corações", "Fantasia"),
    };

    /// Todos os pisos e papéis colocados (quadrado, id), para o jogo salvo.
    public static IEnumerable<KeyValuePair<Vector2Int, string>> All
    {
        get
        {
            foreach (var kv in Tiles)
                if (kv.Value != null) yield return new KeyValuePair<Vector2Int, string>(kv.Key, kv.Value.id);
        }
    }

    public static string At(Vector2Int c) => Tiles.TryGetValue(c, out var t) && t != null ? t.id : null;

    public static void Register(FloorTile t) => Tiles[t.cell] = t;

    public static void Unregister(FloorTile t)
    {
        if (Tiles.TryGetValue(t.cell, out var cur) && cur == t) Tiles.Remove(t.cell);
    }

    public static Texture2D Texture(string id)
    {
        var tex = Resources.Load<Texture2D>((IsWall(id) ? "Walls/" : "Floors/") + id);
        if (tex != null) tex.filterMode = FilterMode.Point;
        return tex;
    }

    public static Sprite SpriteFor(string id)
    {
        if (Cache.TryGetValue(id, out var s) && s != null) return s;
        var tex = Texture(id);
        if (tex == null) return null;
        // Largura da imagem = 1 unidade: o piso cobre exatamente um quadrado.
        s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        Cache[id] = s;
        return s;
    }

    /// Raspa o piso do quadrado: o chão original volta a aparecer.
    public static void Remove(Vector2Int c)
    {
        if (!Tiles.TryGetValue(c, out var t)) return;
        Tiles.Remove(c);
        if (t != null) Object.Destroy(t.gameObject);
    }

    /// Pinta o quadrado (troca o piso que estiver lá).
    public static void Set(Vector2Int c, string id)
    {
        if (_root == null) _root = new GameObject("Pisos").transform;
        if (!Tiles.TryGetValue(c, out var t) || t == null)
        {
            // Criado desligado: o FloorTile só se inscreve (OnEnable) depois de saber o quadrado.
            var go = new GameObject($"Piso {c.x},{c.y}");
            go.SetActive(false);
            go.transform.SetParent(_root);
            go.transform.position = FarmGrid.Center(c);
            go.AddComponent<SpriteRenderer>();
            t = go.AddComponent<FloorTile>();
            t.cell = c;
            go.SetActive(true);
        }
        var sr = t.GetComponent<SpriteRenderer>();
        sr.sortingOrder = IsWall(id) ? WallOrder : SortOrder;
        sr.sprite = SpriteFor(id);
        t.id = id;
    }
}
