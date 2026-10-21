using UnityEditor;
using UnityEngine;

/// Papel de parede ou janela arrastados das pastas Environment/PapelDeParede e Environment/Janelas para a cena: encaixa no
/// quadrado da grade e vai para a camada da parede (atrás dos móveis e quadros, na frente da
/// parede da sala). Vale para o que acabou de entrar (selecionado e ainda na camada 0).
[InitializeOnLoad]
public static class PapelDeParedeNaCena
{
    static PapelDeParedeNaCena() => EditorApplication.hierarchyChanged += Fix;

    private static void Fix()
    {
        if (EditorApplication.isPlaying) return;
        foreach (var go in Selection.gameObjects)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null || sr.sortingOrder != 0) continue;
            string path = AssetDatabase.GetAssetPath(sr.sprite);
            bool papel = path.Contains("/Environment/PapelDeParede/");
            bool janela = path.Contains("/Environment/Janelas/");
            string file = System.IO.Path.GetFileName(path);
            if (ChaoAoFundo.IsGround(path))
            {
                // Chão: atrás de tudo (o personagem e os móveis andam por cima).
                Undo.RecordObject(sr, "Chão");
                sr.sortingOrder = -9998;
                continue;
            }
            if (!papel && !janela) continue;
            Undo.RecordObject(sr, "Parede");
            sr.sortingOrder = papel ? FloorTiles.WallOrder : WallDecor.WindowOrder;
            Undo.RecordObject(go.transform, "Parede");
            // Quadrados ocupados (janela: arredondado para cima, como no jogo); o desenho fica
            // centrado neles.
            var tex = sr.sprite.texture;
            var size = papel ? Vector2Int.one : new Vector2Int(Mathf.CeilToInt(tex.width / 64f), Mathf.CeilToInt(tex.height / 64f));
            var p = go.transform.position;
            var origin = new Vector2Int(Mathf.RoundToInt(p.x - size.x / 2f), Mathf.RoundToInt(p.y - size.y / 2f));
            go.transform.position = new Vector3(origin.x + size.x / 2f, origin.y + size.y / 2f, 0f);
        }
    }
}
