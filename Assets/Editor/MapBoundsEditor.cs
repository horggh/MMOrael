using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Edição do limite do mapa na janela Scene: uma alça no meio de cada borda (arrastar move
/// só aquela borda) e o tamanho da área. Selecione o objeto "Limites do mapa" para ver as alças.
[CustomEditor(typeof(MapBounds))]
public class MapBoundsEditor : Editor
{
    private void OnSceneGUI()
    {
        var mb = (MapBounds)target;
        var r = mb.playable;
        Handles.color = new Color(1f, 0.35f, 0.2f, 1f);
        float size = HandleUtility.GetHandleSize(r.center) * 0.12f;

        EditorGUI.BeginChangeCheck();
        float right = Handles.Slider(new Vector3(r.xMax, r.center.y), Vector3.right, size, Handles.DotHandleCap, 0.5f).x;
        float left = Handles.Slider(new Vector3(r.xMin, r.center.y), Vector3.left, size, Handles.DotHandleCap, 0.5f).x;
        float top = Handles.Slider(new Vector3(r.center.x, r.yMax), Vector3.up, size, Handles.DotHandleCap, 0.5f).y;
        float bottom = Handles.Slider(new Vector3(r.center.x, r.yMin), Vector3.down, size, Handles.DotHandleCap, 0.5f).y;
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(mb, "Mover limite do mapa");
            mb.playable = Rect.MinMaxRect(Mathf.Min(left, right - 1f), Mathf.Min(bottom, top - 1f), right, top);
            EditorUtility.SetDirty(mb);
        }
        Handles.Label(new Vector3(r.xMin + 0.3f, r.yMax + mb.thickness * 0.6f),
                      $"Limite do mapa: {r.width:0.#} x {r.height:0.#}  (arraste os pontos das bordas)");
    }
}

/// Cria o objeto "Limites do mapa" ao abrir a cena do Campo, se ainda não existir, já do
/// tamanho do chão.
[InitializeOnLoad]
public static class MapBoundsSetup
{
    static MapBoundsSetup()
    {
        EditorSceneManager.sceneOpened += (scene, mode) => EnsureInScene();
        EditorApplication.delayCall += EnsureInScene;
    }

    private static void EnsureInScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Campo" || Object.FindAnyObjectByType<MapBounds>() != null) return;

        var go = new GameObject("Limites do mapa");
        var mb = go.AddComponent<MapBounds>();
        // Do tamanho do chão, com uma margem pequena (o desenho do personagem sobe dos pés).
        var ground = GameObject.Find("Chao");
        var sr = ground != null ? ground.GetComponent<SpriteRenderer>() : null;
        if (sr != null)
        {
            var b = sr.bounds;
            mb.playable = Rect.MinMaxRect(b.min.x + 0.5f, b.min.y + 0.3f, b.max.x - 0.5f, b.max.y - 0.8f);
        }
        Undo.RegisterCreatedObjectUndo(go, "Criar limite do mapa");
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[Mapa] Criado o objeto 'Limites do mapa' do tamanho do chão. Salve a cena (Ctrl+S).");
    }
}
