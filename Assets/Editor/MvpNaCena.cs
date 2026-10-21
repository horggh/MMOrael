using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Arrastar qualquer desenho de um MVP (pasta Monsters/MVP) da aba Project para a cena põe o
/// boss inteiro ali, como a janela RaelMMO > Mapa > Monstros: dá para mover, apagar e
/// editar no Inspector, como os lobos.
[InitializeOnLoad]
public static class MvpNaCena
{
    static MvpNaCena() => EditorApplication.hierarchyChanged += Fix;

    private static void Fix()
    {
        if (EditorApplication.isPlaying) return;
        foreach (var go in Selection.gameObjects)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null || go.GetComponent<DrakeBoss>() != null || go.GetComponent<BloodyMurderer>() != null) continue;
            string path = AssetDatabase.GetAssetPath(sr.sprite);
            if (path.Contains("/Monsters/MVP/Resources/CavaleiroSangue/") && go.GetComponent<LordOfDeath>() == null)
            {
                Undo.DestroyObjectImmediate(sr);
                Undo.RegisterFullObjectHierarchyUndo(go, "Colocar Lord of Death");
                go.name = "Lord of Death (MVP)";
                LordOfDeath.Build(go);
                EditorSceneManager.MarkSceneDirty(go.scene);
                continue;
            }
            if (path.Contains("/Monsters/SerialKiller/Resources/SerialKiller/"))
            {
                Undo.DestroyObjectImmediate(sr);
                Undo.RegisterFullObjectHierarchyUndo(go, "Colocar Bloody Murderer");
                go.name = "Bloody Murderer";
                BloodyMurderer.Build(go);
                EditorSceneManager.MarkSceneDirty(go.scene);
                continue;
            }
            if (!path.Contains("/Monsters/MVP/Resources/Drake/")) continue;
            // O desenho solto dá lugar ao boss montado (corpo, arte, vida e atributos).
            Undo.DestroyObjectImmediate(sr);
            Undo.RegisterFullObjectHierarchyUndo(go, "Colocar Drake");
            go.name = "Drake (boss)";
            DrakeBoss.Build(go);
            EditorSceneManager.MarkSceneDirty(go.scene);
        }
    }
}
