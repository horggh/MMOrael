using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Na edição, o desenho (filho "Arte") dos monstros fica sempre em cima do corpo; se foi
/// apagado, volta. Roda ao abrir a cena e quando a Hierarchy muda.
[InitializeOnLoad]
public static class MonstroArteFix
{
    static MonstroArteFix()
    {
        EditorApplication.hierarchyChanged += Fix;
        EditorSceneManager.sceneOpened += (s, m) => Fix();
    }

    private static void Fix()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        foreach (var m in Object.FindObjectsByType<BloodyMurderer>()) Check(m.transform, () => m.ShowPreview());
        foreach (var m in Object.FindObjectsByType<LordOfDeath>()) Check(m.transform, () => m.ShowPreview());
    }

    private static void Check(Transform root, System.Action preview)
    {
        var art = root.Find("Arte");
        if (art != null && art.localPosition == Vector3.zero && art.GetComponent<SpriteRenderer>() != null) return;
        BloodyMurderer.FixArt(root);
        preview();
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
    }
}
