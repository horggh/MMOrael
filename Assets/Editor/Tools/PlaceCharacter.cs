using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlaceCharacter
{
    private const string ScenePath = "Assets/Scenes/Map_StarterField.unity";
    private const string ModelPath = "Assets/Art/Models/Characters/adventurer.fbx";
    private const string ObjectName = "Player_Knight";
    private const float TargetHeight = 1.70f;

    private static float PathX(float z) =>
        28f * Mathf.Sin(z * 0.018f) + 12f * Mathf.Sin(z * 0.045f + 1.3f);

    public static void Place()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (prefab == null)
        {
            Debug.LogError($"[PlaceCharacter] Modelo nao encontrado em {ModelPath}");
            return;
        }

        var existing = GameObject.Find(ObjectName);
        if (existing != null) Object.DestroyImmediate(existing);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = ObjectName;
        go.transform.localScale = Vector3.one;
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;

        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogError("[PlaceCharacter] Modelo sem Renderer.");
            return;
        }

        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        float nativeHeight = b.size.y;

        float scale = TargetHeight / nativeHeight;
        go.transform.localScale = Vector3.one * scale;

        // Recalcula apos escalar para assentar os pes exatamente no chao (y = 0).
        b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        float footOffset = b.min.y;

        float z = 0f;
        go.transform.position = new Vector3(PathX(z), -footOffset, z);
        go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[PlaceCharacter] Altura nativa: {nativeHeight:F2} m | escala aplicada: {scale:F3} | altura final: {TargetHeight:F2} m");
        Debug.Log($"[PlaceCharacter] Posicionado em {go.transform.position} sobre a trilha.");
    }
}
