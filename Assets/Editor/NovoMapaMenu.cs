using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// RaelMMO > Mapa > Novo mapa (modelo): começa um mapa novo com tudo pronto, no meio da Scene:
/// - Limites (MapBounds) de 40 x 30 quadrados (editáveis);
/// - Terreno (pincel de terrenos) já com grama em toda a área;
/// - Camadas de tile: Detalhes (flores, pedrinhas), Bloqueio (com colisão) e Por cima (copas,
///   telhados: somem quando o personagem passa por baixo);
/// - Grupos vazios para organizar: Prédios, Decoração, NPCs, Monstros, Barreiras, Passagens.
public static class NovoMapaMenu
{
    private const float W = 40f, H = 30f;

    [MenuItem("RaelMMO/Mapa/Novo mapa (modelo)")]
    private static void Criar()
    {
        var v = SceneView.lastActiveSceneView;
        Vector3 c = v != null ? new Vector3(Mathf.Round(v.pivot.x), Mathf.Round(v.pivot.y), 0f) : Vector3.zero;

        var root = new GameObject("Mapa Novo");
        Undo.RegisterCreatedObjectUndo(root, "Novo mapa");
        root.transform.position = c;

        var lim = new GameObject("Limites");
        lim.transform.SetParent(root.transform, false);
        lim.AddComponent<MapBounds>().playable = new Rect(c.x - W / 2f, c.y - H / 2f, W, H);

        // Terreno com grama em toda a área (+1 de folga para a borda não aparecer).
        var terreno = TerrenoPincel.Criar(root.transform, c);
        int grama = System.Array.IndexOf(terreno.ids, "grama");
        if (grama >= 0)
        {
            var cells = new System.Collections.Generic.List<Vector3Int>();
            var a = terreno.dados.WorldToCell(new Vector3(c.x - W / 2f - 1, c.y - H / 2f - 1));
            var b = terreno.dados.WorldToCell(new Vector3(c.x + W / 2f + 1, c.y + H / 2f + 1));
            for (int y = a.y; y <= b.y; y++)
                for (int x = a.x; x <= b.x; x++)
                    cells.Add(new Vector3Int(x, y, 0));
            terreno.Pintar(cells, grama);
        }

        var grid = new GameObject("Camadas", typeof(Grid));
        grid.transform.SetParent(root.transform, false);
        Camada(grid.transform, "Detalhes", -9978);
        var bloq = Camada(grid.transform, "Bloqueio", -9977);
        var body = bloq.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
        bloq.AddComponent<CompositeCollider2D>();
        bloq.AddComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
        Camada(grid.transform, "Por cima", 30000).AddComponent<CamadaPorCima>();

        foreach (var n in new[] { "Prédios", "Decoração", "NPCs", "Monstros", "Barreiras", "Passagens" })
            new GameObject(n).transform.SetParent(root.transform, false);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[Mapa] Mapa novo criado. Pinte o terreno em RaelMMO > Mapa > Pincel de terreno.");
    }

    private static GameObject Camada(Transform parent, string name, int order)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<TilemapRenderer>().sortingOrder = order;
        return go;
    }
}
