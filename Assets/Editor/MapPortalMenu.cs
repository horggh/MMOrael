using UnityEditor;
using UnityEngine;

/// Liga dois portais de mapa (ida e volta). Selecione os dois na Hierarchy (Ctrl+clique).
public static class MapPortalMenu
{
    [MenuItem("RaelMMO/Mapa/Ligar os 2 portais selecionados")]
    private static void Link()
    {
        var gos = Selection.gameObjects;
        if (gos.Length != 2)
        {
            EditorUtility.DisplayDialog("Portal", "Selecione exatamente 2 portais (Ctrl+clique na Hierarchy).", "OK");
            return;
        }
        var a = Ensure(gos[0]);
        var b = Ensure(gos[1]);
        Undo.RecordObjects(new Object[] { a, b }, "Ligar portais");
        a.destino = b;
        b.destino = a;
        EditorUtility.SetDirty(a);
        EditorUtility.SetDirty(b);
    }

    /// Objeto com o desenho do portal: ganha o componente (e perde o colisor de decoração).
    private static MapPortal Ensure(GameObject go)
    {
        var p = go.GetComponent<MapPortal>();
        if (p != null) return p;
        foreach (var c in go.GetComponents<Collider2D>()) Undo.DestroyObjectImmediate(c);
        p = Undo.AddComponent<MapPortal>(go);
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null && sr.sprite.name.StartsWith("entrada_caverna")) p.ComoEntradaDeCaverna();
        return p;
    }
}
