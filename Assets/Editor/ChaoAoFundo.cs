using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Todo chão fica sempre embaixo de tudo (personagem, móveis, NPCs).
/// Pisos e texturas arrastados para a cena entram com a ordem de desenho 0, a mesma faixa dos
/// objetos do mapa: o que estivesse mais para cima no mapa sumiria atrás deles. Aqui, ao salvar
/// a cena e ao apertar Play, todo chão vai para o fundo (logo acima do fundo pintado, -10000).
/// Também pelo menu RaelMMO > Mapa > Mandar o chão para o fundo.
[InitializeOnLoad]
public static class ChaoAoFundo
{
    private const int Order = -9998;

    static ChaoAoFundo()
    {
        EditorSceneManager.sceneSaving += (scene, path) => Apply();
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.ExitingEditMode) Apply();
        };
    }

    /// É chão: texturas de chão, pisos e o chão da masmorra.
    public static bool IsGround(string path)
    {
        string file = System.IO.Path.GetFileName(path);
        return path.Contains("/Environment/Texturas/") || path.Contains("/Pisos") || path.Contains("/Resources/Floors/") ||
               file.StartsWith("piso_") || file.StartsWith("grama_base") || file.StartsWith("masmorra_piso");
    }

    [MenuItem("RaelMMO/Mapa/Mandar o chão para o fundo")]
    private static void Run()
    {
        int n = Apply();
        EditorUtility.DisplayDialog("Chão", n > 0 ? $"{n} peças de chão foram para o fundo. Salve a cena (Ctrl+S)."
                                                  : "Todo o chão já está no fundo.", "OK");
    }

    private static int Apply()
    {
        int n = 0;
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
        {
            if (sr.sprite == null || sr.sortingOrder <= -9000) continue;
            if (!IsGround(AssetDatabase.GetAssetPath(sr.sprite))) continue;
            // Chão não segue a altura do mapa: sem o YSortRenderer, que mudaria a ordem no Play.
            var ys = sr.GetComponent<YSortRenderer>();
            if (ys != null) Undo.DestroyObjectImmediate(ys);
            Undo.RecordObject(sr, "Chão para o fundo");
            sr.sortingOrder = Order;
            EditorSceneManager.MarkSceneDirty(sr.gameObject.scene);
            n++;
        }
        return n;
    }
}
