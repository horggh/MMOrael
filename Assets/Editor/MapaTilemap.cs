using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// Construção de mapas com Tilemap (pintar blocos como num programa de desenho).
///
/// RaelMMO > Mapa > 1. Gerar blocos e paleta: um bloco (Tile) por textura de
///   Assets/Art/Sprites/Environment/Texturas (1 bloco = 1 quadrado da grade) em Assets/Tiles/Chao,
///   e a paleta "Paleta RaelMMO" com todos eles, por tema. Rodar de novo atualiza.
/// RaelMMO > Mapa > 2. Criar camadas na cena: a grade "Mapa (Tilemap)" com as camadas
///   Chão, Detalhes e Bloqueio (o que for pintado no Bloqueio vira parede: água, penhasco).
///
/// Depois: Window > 2D > Tile Palette, escolha "Paleta RaelMMO", escolha a camada em
/// "Active Tilemap" e pinte (B pincel, U retângulo, G balde, D apagar, I conta-gotas).
public static class MapaTilemap
{
    private const string TexDir = "Assets/Art/Sprites/Environment/Texturas";
    private const string TileDir = "Assets/Tiles/Chao";
    private const string PalettePath = "Assets/Tiles/Paleta RaelMMO.prefab";
    private const int PerRow = 8;

    // Ordem e grupos da paleta (o que não estiver aqui vai no fim, em "outros").
    private static readonly string[][] Groups =
    {
        new[] { "grama_campo", "grama", "grama_base2", "grama_flores", "grama_trevos", "mato_seco", "folhagem", "folhas_outono", "musgo", "raizes" },
        new[] { "areia_campo", "areia", "terra_batida", "terra_galhos", "terra_arada", "barro", "cascalho_terra", "cascalho", "agua_campo" },
        new[] { "pedras_redondas", "lajotas_grandes", "pedras_quebradas", "lajotas_hera", "tijolo_vermelho", "ornamentado", "tabuas", "tabuas_em_pe" },
        new[] { "liso", "lajotas", "lajotas_rachadas", "lajotas_manchadas", "tijolos", "rachado", "pedras_soltas", "manchas",
                "quebrado", "arranhado", "concreto", "canaletas", "grade", "bueiro", "faixa", "placa", "retalhos" },
    };

    [MenuItem("RaelMMO/Mapa/1. Gerar blocos e paleta")]
    public static void BuildTilesAndPalette()
    {
        var sprites = AssetDatabase.FindAssets("t:Sprite", new[] { TexDir })
            .Select(g => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(s => s != null).ToDictionary(s => s.name);
        if (sprites.Count == 0)
        {
            Debug.LogError($"[Mapa] Nenhuma textura em {TexDir}.");
            return;
        }
        Directory.CreateDirectory(TileDir);

        var tiles = new Dictionary<string, Tile>();
        foreach (var kv in sprites)
        {
            string path = $"{TileDir}/{kv.Key}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = kv.Value;
            // Colisão do quadrado inteiro (só vale na camada Bloqueio, que tem colisor).
            tile.colliderType = Tile.ColliderType.Grid;
            EditorUtility.SetDirty(tile);
            tiles[kv.Key] = tile;
        }

        // Paleta: uma grade com os blocos em linhas, por tema (cada tema começa numa linha).
        var root = new GameObject("Paleta RaelMMO");
        var grid = root.AddComponent<Grid>();
        grid.cellSize = new Vector3(1f, 1f, 0f);
        var layer = new GameObject("Layer1");
        layer.transform.SetParent(root.transform);
        var tm = layer.AddComponent<Tilemap>();
        tm.tileAnchor = Vector3.zero;   // o pivô das texturas é o canto de baixo à esquerda
        layer.AddComponent<TilemapRenderer>();

        var used = new HashSet<string>();
        int row = 0;
        void PlaceGroup(IEnumerable<string> names)
        {
            int col = 0;
            foreach (var n in names)
            {
                if (!tiles.TryGetValue(n, out var t) || !used.Add(n)) continue;
                tm.SetTile(new Vector3Int(col, -row, 0), t);
                if (++col == PerRow) { col = 0; row++; }
            }
            if (col > 0) row++;
        }
        foreach (var g in Groups) PlaceGroup(g);
        PlaceGroup(tiles.Keys.OrderBy(k => k));   // texturas novas que ainda não têm grupo

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PalettePath);
        Object.DestroyImmediate(root);
        // O que faz o prefab aparecer na janela Tile Palette (classe do pacote de Tilemap).
        if (AssetDatabase.LoadAllAssetsAtPath(PalettePath).All(o => o == null || o.GetType().Name != "GridPalette"))
        {
            var settings = ScriptableObject.CreateInstance("GridPalette");
            if (settings != null)
            {
                settings.name = "Palette Settings";
                AssetDatabase.AddObjectToAsset(settings, prefab);
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Mapa] {tiles.Count} blocos em {TileDir} e a paleta '{PalettePath}'. " +
                  "Abra Window > 2D > Tile Palette e escolha 'Paleta RaelMMO'.");
    }

    [MenuItem("RaelMMO/Mapa/2. Criar camadas na cena")]
    public static void CreateLayers()
    {
        var existing = GameObject.Find("Mapa (Tilemap)");
        var root = existing != null ? existing : new GameObject("Mapa (Tilemap)");
        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(root, "Criar camadas do mapa");
            root.AddComponent<Grid>().cellSize = new Vector3(FarmGrid.Cell, FarmGrid.Cell, 0f);
        }
        // Ordem: acima da grama de fundo (-10000) e do fundo do mapa (-9999), abaixo de tudo
        // que tem ordem pela altura (árvores, personagens).
        Layer(root, "Chão", -9997, false);
        Layer(root, "Detalhes", -9996, false);
        Layer(root, "Bloqueio", -9995, true);
        Selection.activeGameObject = root.transform.Find("Chão").gameObject;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[Mapa] Camadas Chão, Detalhes e Bloqueio prontas em 'Mapa (Tilemap)'. Salve a cena (Ctrl+S).");
    }

    private static void Layer(GameObject root, string name, int order, bool blocks)
    {
        var t = root.transform.Find(name);
        GameObject go;
        if (t != null) go = t.gameObject;
        else
        {
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Criar camada " + name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<Tilemap>().tileAnchor = Vector3.zero;
        }
        var r = go.GetComponent<TilemapRenderer>();
        if (r == null) r = go.AddComponent<TilemapRenderer>();
        r.sortingOrder = order;
        r.mode = TilemapRenderer.Mode.Chunk;   // desenha em blocos grandes: leve com milhares de quadrados
        if (!blocks) return;
        // Bloqueio: um colisor só, juntando todos os quadrados pintados.
        if (go.GetComponent<TilemapCollider2D>() == null)
        {
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            go.AddComponent<CompositeCollider2D>();
            var col = go.AddComponent<TilemapCollider2D>();
            col.compositeOperation = Collider2D.CompositeOperation.Merge;
        }
    }
}
