using UnityEditor;
using UnityEngine;

/// Manda os objetos selecionados para trás ou para a frente na ordem de desenho.
/// Com YSortRenderer (a ordem vem da altura no mapa), mexe no sortOffset: assim vale no Play
/// também. Sem ele (chão, fundo), mexe direto no Order in Layer.
public static class Camadas
{
    // Shift+PageDown / Shift+PageUp
    [MenuItem("RaelMMO/Camadas/Mandar para trás #PGDN")]
    private static void Back() => Shift(-1);

    [MenuItem("RaelMMO/Camadas/Trazer para a frente #PGUP")]
    private static void Front() => Shift(+1);

    [MenuItem("RaelMMO/Camadas/Mandar para o fundo (chão)")]
    private static void Bottom()
    {
        foreach (var go in Selection.gameObjects)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) continue;
            var ys = go.GetComponent<YSortRenderer>();
            if (ys != null) Undo.DestroyObjectImmediate(ys);   // chão não segue a altura
            Undo.RecordObject(sr, "Mandar para o fundo");
            sr.sortingOrder = -9998;
        }
    }

    private static void Shift(int dir)
    {
        foreach (var go in Selection.gameObjects)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) continue;
            var ys = go.GetComponent<YSortRenderer>();
            Undo.RecordObject(sr, "Camada");
            if (ys != null)
            {
                // Uma "linha" de profundidade (1/32 de unidade) por vez; offset maior = mais atrás.
                Undo.RecordObject(ys, "Camada");
                ys.sortOffset -= dir / (float)YSortRenderer.Precision;
                sr.sortingOrder = YSortRenderer.OrderFor(go.transform.position.y + ys.sortOffset);
            }
            else sr.sortingOrder += dir;
        }
    }

    [MenuItem("RaelMMO/Camadas/Mandar para trás #PGDN", true)]
    [MenuItem("RaelMMO/Camadas/Trazer para a frente #PGUP", true)]
    [MenuItem("RaelMMO/Camadas/Mandar para o fundo (chão)", true)]
    private static bool HasSelection() => Selection.gameObjects.Length > 0;
}
