using UnityEditor;
using UnityEngine;

/// Menu RaelMMO > Borda: põe na cena uma reta de borda (barranco, cerca, mureta) que estica.
/// É um objeto só, com o SpriteRenderer em modo Tiled: para mudar o comprimento, mude o
/// Width em "Size" no Inspector (ou arraste a borda com a ferramenta Rect, tecla T). Sai no
/// centro da vista da cena. As peças soltas (cantos, cachoeiras, troncos) é só arrastar da
/// pasta Assets/Art/Sprites/Environment/Borda.
public static class BordaMenu
{
    private const string Dir = "Assets/Art/Sprites/Environment/Borda/";

    [MenuItem("RaelMMO/Borda/Barranco (esticável)")]
    private static void Barranco() => Create("barranco_ladrilho", "Barranco", 12f);

    [MenuItem("RaelMMO/Borda/Cerca (esticável)")]
    private static void Cerca() => Create("cerca_ladrilho", "Cerca", 8f);

    [MenuItem("RaelMMO/Borda/Mureta de pedra (esticável)")]
    private static void Mureta() => Create("mureta_ladrilho", "Mureta", 8f);

    private static void Create(string file, string name, float width)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + file + ".png");
        if (sprite == null)
        {
            Debug.LogError($"[Borda] falta {Dir}{file}.png (rode Tools/extract_bordas.py)");
            return;
        }
        var parent = GameObject.Find("Borda") ?? new GameObject("Borda");
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        var view = SceneView.lastActiveSceneView;
        Vector3 at = view != null ? view.pivot : Vector3.zero;
        go.transform.position = new Vector3(Mathf.Round(at.x), Mathf.Round(at.y), 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = new Vector2(width, sprite.bounds.size.y);
        // Profundidade pela linha da base, como as árvores e os personagens.
        go.AddComponent<YSortRenderer>();
        sr.sortingOrder = YSortRenderer.OrderFor(go.transform.position.y);

        Undo.RegisterCreatedObjectUndo(go, "Criar " + name);
        Selection.activeGameObject = go;
    }
}
