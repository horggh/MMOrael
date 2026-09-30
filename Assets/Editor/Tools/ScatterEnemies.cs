using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ScatterEnemies
{
    private const string ScenePath = "Assets/Scenes/Map_StarterField.unity";
    private const string ModelFolder = "Assets/Art/Models/Monsters_Quaternius";
    private const string ContainerName = "Enemies";

    private const int EnemyCount = 20;
    private const float FieldHalf = 92f;
    private const float PathClearance = 7f;     // nao nascem em cima da trilha
    private const float CenterClearance = 18f;  // nao nascem sob a arvore gigante
    private const float MinSpacing = 9f;        // evita amontoar

    // Criaturas de campo inicial: pequenas e nao ameacadoras.
    private static readonly (string model, float height)[] Roster =
    {
        ("GreenBlob", 0.85f),
        ("PinkBlob", 0.85f),
        ("GreenSpikyBlob", 0.95f),
        ("Mushnub", 1.05f),
    };

    private static System.Random _rng;
    private static float Rand01() => (float)_rng.NextDouble();
    private static float Range(float a, float b) => a + (b - a) * Rand01();

    private static float PathX(float z) =>
        28f * Mathf.Sin(z * 0.018f) + 12f * Mathf.Sin(z * 0.045f + 1.3f);

    public static void Scatter()
    {
        _rng = new System.Random(77001);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var old = GameObject.Find(ContainerName);
        if (old != null) Object.DestroyImmediate(old);

        var root = new GameObject(ContainerName);
        var placed = new List<Vector3>();

        int attempts = 0;
        while (placed.Count < EnemyCount && attempts < 5000)
        {
            attempts++;

            float x = Range(-FieldHalf, FieldHalf);
            float z = Range(-FieldHalf, FieldHalf);

            if (Mathf.Abs(x - PathX(z)) < PathClearance) continue;
            if (new Vector2(x, z).magnitude < CenterClearance) continue;

            bool tooClose = false;
            foreach (var p in placed)
            {
                if ((new Vector3(x, 0f, z) - p).sqrMagnitude < MinSpacing * MinSpacing)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            var entry = Roster[_rng.Next(Roster.Length)];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/{entry.model}.fbx");
            if (prefab == null)
            {
                Debug.LogWarning($"[ScatterEnemies] Modelo nao encontrado: {entry.model}");
                continue;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            go.name = $"Enemy_{entry.model}_{placed.Count + 1:00}";
            go.transform.localScale = Vector3.one;
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;

            // Escala calculada a partir do tamanho real do modelo, nao chutada.
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);

                float target = entry.height * Range(0.9f, 1.15f);
                go.transform.localScale = Vector3.one * (target / b.size.y);

                b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                go.transform.position = new Vector3(x, -b.min.y, z);
            }
            else
            {
                go.transform.position = new Vector3(x, 0f, z);
            }

            go.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
            placed.Add(go.transform.position);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[ScatterEnemies] {placed.Count} inimigos posicionados ({attempts} tentativas).");
    }
}
