using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Menu RaelMMO > Mapa: põe o desenho do mapa como fundo, por cima da grama verde ("Chao"),
/// no mesmo lugar e tamanho dela. Rodar de novo só reposiciona (não duplica).
public static class MapaMenu
{
    private const string Fundo = "Assets/Art/Sprites/Environment/Mapas/cruzamento_fundo.png";

    [MenuItem("RaelMMO/Mapa/Colocar fundo do Cruzamento")]
    private static void PlaceCruzamento()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Fundo);
        if (sprite == null)
        {
            Debug.LogError($"[Mapa] falta {Fundo} (rode Tools/gen_fundo_cruzamento.py)");
            return;
        }
        var ground = GameObject.Find("Chao");
        var gsr = ground != null ? ground.GetComponent<SpriteRenderer>() : null;

        var go = GameObject.Find("Fundo do mapa");
        if (go == null)
        {
            go = new GameObject("Fundo do mapa");
            Undo.RegisterCreatedObjectUndo(go, "Colocar fundo do mapa");
        }
        else Undo.RecordObject(go.transform, "Reposicionar fundo do mapa");

        // (?? não serve com objetos da Unity: componente ausente pode vir como "nulo falso".)
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = Undo.AddComponent<SpriteRenderer>(go);
        Undo.RecordObject(sr, "Fundo do mapa");
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Simple;
        // Logo acima da grama (-10000) e abaixo de tudo que tem ordem pela altura.
        sr.sortingOrder = -9999;

        if (gsr != null)
        {
            // Centro e tamanho do chão: a imagem foi gerada do tamanho dele, mas se o chão
            // mudar, a escala acompanha.
            var b = gsr.bounds;
            go.transform.position = new Vector3(b.center.x, b.center.y, 0f);
            var size = sprite.bounds.size;
            go.transform.localScale = new Vector3(b.size.x / size.x, b.size.y / size.y, 1f);
        }
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("[Mapa] Fundo do Cruzamento colocado sobre o chão. Salve a cena (Ctrl+S).");
    }
}

/// Menus RaelMMO > Mapa > Colocar mapa da Cidade / da Praça / da Caverna: cada um é uma área nova na mesma
/// cena (Tools/gen_fundo_cidade.py), da largura do Cruzamento, no meio da Scene, com o próprio
/// limite (MapBounds). Ligue as áreas com portais de mapa. Rodar de novo não duplica: deixa o
/// mapa no lugar e acerta os limites ao tamanho do desenho.
public static class NovosMapasMenu
{
    [MenuItem("RaelMMO/Mapa/Colocar mapa da Cidade")]
    private static void Cidade() => Place("Assets/Art/Sprites/Environment/Mapas/cidade_fundo.png", "Cidade");

    [MenuItem("RaelMMO/Mapa/Colocar mapa da Praça")]
    private static void Praca() => Place("Assets/Art/Sprites/Environment/Mapas/praca_fundo.png", "Praça");

    [MenuItem("RaelMMO/Mapa/Colocar mapa da Caverna")]
    private static void Caverna() => Place("Assets/Art/Sprites/Environment/Mapas/caverna_fundo.png", "Caverna");

    [MenuItem("RaelMMO/Mapa/Colocar mapa da Caverna 2")]
    private static void Caverna2() => Place("Assets/Art/Sprites/Environment/Mapas/caverna2_fundo.png", "Caverna 2");

    private static void Place(string path, string nome)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            Debug.LogError($"[Mapa] falta {path} (rode Tools/gen_fundo_cidade.py)");
            return;
        }
        var view = SceneView.lastActiveSceneView;
        Vector3 at = view != null ? view.pivot : Vector3.zero;
        // Encaixa na grade: o tamanho é inteiro, o canto fica num quadrado.
        Vector2 size = sprite.bounds.size;
        var min = new Vector2(Mathf.Round(at.x - size.x / 2f), Mathf.Round(at.y - size.y / 2f));

        var root = GameObject.Find("Mapa " + nome);
        if (root != null)
        {
            Vector2 c = root.transform.position;
            min = new Vector2(Mathf.Round(c.x - size.x / 2f), Mathf.Round(c.y - size.y / 2f));
            Undo.RecordObject(root.transform, "Mapa " + nome);
        }
        else
        {
            root = new GameObject("Mapa " + nome);
            Undo.RegisterCreatedObjectUndo(root, "Colocar mapa " + nome);
        }
        root.transform.position = new Vector3(min.x + size.x / 2f, min.y + size.y / 2f, 0f);

        var fundoT = root.transform.Find("Fundo") ?? root.transform.Find("Fundo da cidade");   // nome antigo
        var fundo = fundoT != null ? fundoT.gameObject : new GameObject("Fundo");
        fundo.transform.SetParent(root.transform, false);
        var sr = fundo.GetComponent<SpriteRenderer>();
        if (sr == null) sr = fundo.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = -9999;   // logo acima da grama, abaixo de tudo

        var limT = root.transform.Find("Limites") ?? root.transform.Find("Limites da cidade");
        var limites = limT != null ? limT.gameObject : new GameObject("Limites");
        limites.transform.SetParent(root.transform, false);
        var mb = limites.GetComponent<MapBounds>();
        if (mb == null) mb = limites.AddComponent<MapBounds>();
        Undo.RecordObject(mb, "Limites " + nome);
        mb.playable = new Rect(min.x + 0.5f, min.y + 0.5f, size.x - 1f, size.y - 1f);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[Mapa] Mapa {nome} colocado. Ajuste os limites (objeto \"Limites\") e salve a cena.");
    }
}

/// Menu RaelMMO > Mapa > Usar grama no chão: o "Chao" (a grama de todo o mapa) passa a ser o
/// quadradinho Texturas/grama repetido lado a lado e empilhado, 1 por quadrado da grade, até
/// cobrir a área (modo Tiled do SpriteRenderer: o Unity repete o quadrado, sem precisar de uma
/// imagem gigante). O canto e o tamanho do chão ficam em quadrados inteiros da grade.
public static class ChaoMenu
{
    private const string Grama = "Assets/Art/Sprites/Environment/Texturas/grama.png";

    [MenuItem("RaelMMO/Mapa/Usar grama no chão (quadrado por quadrado)")]
    private static void UseGrama()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Grama);
        var ground = GameObject.Find("Chao");
        var sr = ground != null ? ground.GetComponent<SpriteRenderer>() : null;
        if (sprite == null || sr == null)
        {
            Debug.LogError(sprite == null ? $"[Mapa] falta {Grama}" : "[Mapa] não achei o objeto 'Chao' na cena.");
            return;
        }
        Undo.RecordObject(sr, "Grama do chão");
        Undo.RecordObject(sr.transform, "Grama do chão");
        // Mesma área de antes, arredondada para quadrados inteiros.
        Bounds old = sr.bounds;
        float cell = FarmGrid.Cell;
        Vector2 min = new Vector2(Mathf.Floor(old.min.x / cell) * cell, Mathf.Floor(old.min.y / cell) * cell);
        Vector2 max = new Vector2(Mathf.Ceil(old.max.x / cell) * cell, Mathf.Ceil(old.max.y / cell) * cell);

        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.transform.localScale = Vector3.one;
        // 1 quadradinho = 1 quadrado da grade (o sprite tem 1 unidade).
        Vector2 tile = sprite.bounds.size;
        sr.size = new Vector2((max.x - min.x) / cell * tile.x, (max.y - min.y) / cell * tile.y);
        // O pivô da textura é o canto de baixo à esquerda: a posição é o canto da área.
        Vector3 pivotOffset = sr.bounds.min - sr.transform.position;
        sr.transform.position = new Vector3(min.x, min.y, sr.transform.position.z) - new Vector3(pivotOffset.x, pivotOffset.y, 0f);

        EditorUtility.SetDirty(sr);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(ground.scene);
        int nx = Mathf.RoundToInt((max.x - min.x) / cell), ny = Mathf.RoundToInt((max.y - min.y) / cell);
        Debug.Log($"[Mapa] Chão = {nx} x {ny} quadradinhos de grama ({nx * ny} no total). Salve a cena (Ctrl+S).");
    }
}

/// Menu RaelMMO > Mapa > Colocar prédio comercial: o prédio (loja) como objeto fixo da cena,
/// no centro da vista. Profundidade pelos pés (quem passa na frente fica na frente) e colisor
/// só no corpo do prédio — a calçada da frente é de andar. O colisor dá para ajustar no
/// Inspector (Box Collider 2D: Offset e Size) ou com o botão "Edit Collider".
public static class PredioMenu
{
    private const string Loja = "Assets/Art/Sprites/Environment/Predios/loja.png";

    [MenuItem("RaelMMO/Mapa/Colocar prédio comercial")]
    private static void PlaceShop()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Loja);
        if (sprite == null)
        {
            Debug.LogError($"[Mapa] falta {Loja} (rode Tools/extract_predio_comercial.py)");
            return;
        }
        var parent = GameObject.Find("Prédios") ?? new GameObject("Prédios");
        var go = new GameObject("Prédio comercial");
        go.transform.SetParent(parent.transform);
        var view = SceneView.lastActiveSceneView;
        Vector3 at = view != null ? view.pivot : Vector3.zero;
        go.transform.position = new Vector3(Mathf.Round(at.x), Mathf.Round(at.y), 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        // Profundidade pela base das paredes (1,4 acima da beira da calçada): na calçada o
        // personagem fica na frente do prédio; atrás das paredes, fica escondido.
        go.AddComponent<YSortRenderer>().sortOffset = 1.4f;
        sr.sortingOrder = YSortRenderer.OrderFor(go.transform.position.y + 1.4f);

        // Corpo do prédio: faixa acima da calçada (o desenho tem 6 x 6,2 unidades; a
        // calçada ocupa mais ou menos o primeiro 1,5 unidade de baixo).
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(3.6f, 1.6f);
        col.offset = new Vector2(0.4f, 2.1f);

        Undo.RegisterCreatedObjectUndo(go, "Colocar prédio comercial");
        Selection.activeGameObject = go;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
    }
}
