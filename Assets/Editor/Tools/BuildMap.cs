using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildMap
{
    private const string ScenePath = "Assets/Scenes/Map_StarterField.unity";
    private const string ModelFolder = "Assets/Art/Models/Nature_Quaternius";
    private const string PropsRoot = "Map_Props";

    private const float FieldHalf = 100f;   // mapa 200x200 m
    private const float PathHalfWidth = 3.5f;
    private const float TreeClearance = 6.5f;

    // Alturas nativas medidas (metros), usadas para calcular a escala alvo.
    private static readonly Dictionary<string, float> NativeHeight = new Dictionary<string, float>
    {
        { "CommonTree_1", 7.26f }, { "CommonTree_2", 7.64f }, { "CommonTree_3", 9.43f },
        { "CommonTree_4", 9.44f }, { "CommonTree_5", 7.01f },
        { "Pine_1", 7.32f }, { "Pine_2", 7.38f }, { "Pine_3", 7.39f },
        { "Pine_4", 10.24f }, { "Pine_5", 8.72f },
        { "TwistedTree_1", 16.73f }, { "TwistedTree_2", 18.95f }, { "TwistedTree_3", 16.07f },
        { "TwistedTree_4", 18.74f }, { "TwistedTree_5", 15.66f },
        { "DeadTree_1", 9.50f }, { "DeadTree_2", 11.49f }, { "DeadTree_3", 13.28f },
        { "Bush_Common", 1.58f }, { "Bush_Common_Flowers", 1.58f },
        { "Grass_Common_Short", 1.33f }, { "Grass_Common_Tall", 1.87f },
        { "Grass_Wispy_Short", 1.07f }, { "Grass_Wispy_Tall", 1.67f },
        { "Plant_1", 1.01f }, { "Plant_7", 0.25f },
        { "Flower_3_Group", 2.05f }, { "Flower_4_Group", 2.49f },
        { "Clover_1", 1.14f }, { "Clover_2", 1.26f },
        { "Mushroom_Common", 0.46f },
        { "Rock_Medium_1", 2.26f }, { "Rock_Medium_2", 1.90f }, { "Rock_Medium_3", 2.32f },
    };

    private static readonly string[] CanopyTrees =
    {
        "CommonTree_1", "CommonTree_2", "CommonTree_3", "CommonTree_4", "CommonTree_5",
        "Pine_1", "Pine_2", "Pine_3", "Pine_4", "Pine_5"
    };

    private static readonly string[] PathTiles =
    {
        "RockPath_Round_Small_1", "RockPath_Round_Small_2", "RockPath_Round_Small_3",
        "RockPath_Round_Thin", "RockPath_Round_Wide"
    };

    private static System.Random _rng;
    private static float Rand01() => (float)_rng.NextDouble();
    private static float Range(float a, float b) => a + (b - a) * Rand01();

    // Trilha sinuosa: duas senoides somadas para dar um traçado orgânico.
    private static float PathX(float z) =>
        28f * Mathf.Sin(z * 0.018f) + 12f * Mathf.Sin(z * 0.045f + 1.3f);

    private static float DistanceToPath(float x, float z) => Mathf.Abs(x - PathX(z));

    public static void Build()
    {
        _rng = new System.Random(20260930);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log($"[BuildMap] Cena aberta: {scene.name}");

        RemoveOld("Nature_Props");
        RemoveOld(PropsRoot);

        ApplyGrassMaterial();

        var root = new GameObject(PropsRoot);
        var pathRoot = NewChild(root, "Path");
        var treeRoot = NewChild(root, "Trees");
        var undergrowthRoot = NewChild(root, "Undergrowth");

        int tiles = BuildPath(pathRoot);
        int trees = ScatterTrees(treeRoot);
        PlaceCenterpiece(treeRoot);
        int props = ScatterUndergrowth(undergrowthRoot);
        AddReferenceCapsule();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[BuildMap] PRONTO: {trees} arvores + 1 gigante central, {tiles} pedras de trilha, {props} objetos de vegetacao rasteira.");
    }

    private static GameObject NewChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        return go;
    }

    private static void RemoveOld(string name)
    {
        var old = GameObject.Find(name);
        if (old != null)
        {
            Object.DestroyImmediate(old);
            Debug.Log($"[BuildMap] Removido container antigo: {name}");
        }
    }

    // ---------- Grama ----------

    private static void ApplyGrassMaterial()
    {
        var tex = GrassTexture.CreateAndSave();
        var ground = GameObject.Find("Ground_StarterField");
        if (ground == null)
        {
            Debug.LogWarning("[BuildMap] Ground_StarterField nao encontrado.");
            return;
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Mat_GroundGrass.mat");
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, "Assets/Art/Materials/Mat_GroundGrass.mat");
        }

        mat.shader = Shader.Find("Standard");
        mat.color = Color.white;
        mat.mainTexture = tex;
        mat.mainTextureScale = new Vector2(50f, 50f); // 1 tile a cada 4 m
        mat.SetFloat("_Glossiness", 0.03f);
        mat.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(mat);

        ground.GetComponent<Renderer>().sharedMaterial = mat;
        Debug.Log("[BuildMap] Material de grama aplicado com textura procedural.");
    }

    // ---------- Trilha ----------

    private static int BuildPath(GameObject parent)
    {
        int count = 0;
        for (float z = -FieldHalf + 2f; z <= FieldHalf - 2f; z += 1.1f)
        {
            float baseX = PathX(z);
            // 2 a 3 pedras lado a lado para dar largura a trilha
            int across = _rng.Next(2, 4);
            for (int i = 0; i < across; i++)
            {
                float offset = Range(-PathHalfWidth, PathHalfWidth);
                var prefab = LoadModel(PathTiles[_rng.Next(PathTiles.Length)]);
                if (prefab == null) continue;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
                go.transform.position = new Vector3(baseX + offset, 0.02f, z + Range(-0.4f, 0.4f));
                go.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
                go.transform.localScale = Vector3.one * Range(0.9f, 1.4f);
                MarkStatic(go);
                count++;
            }
        }
        return count;
    }

    // ---------- Arvores ----------

    private static int ScatterTrees(GameObject parent)
    {
        int count = 0;
        const float cell = 10f;

        for (float z = -FieldHalf + 6f; z <= FieldHalf - 6f; z += cell)
        {
            for (float x = -FieldHalf + 6f; x <= FieldHalf - 6f; x += cell)
            {
                float px = x + Range(-cell * 0.4f, cell * 0.4f);
                float pz = z + Range(-cell * 0.4f, cell * 0.4f);

                if (DistanceToPath(px, pz) < TreeClearance) continue;
                if (new Vector2(px, pz).magnitude < 14f) continue; // espaco para a arvore gigante

                // Densidade variavel: cria bosques e clareiras em vez de distribuicao uniforme
                float density = Mathf.PerlinNoise(px * 0.012f + 100f, pz * 0.012f + 100f);
                if (Rand01() > density * 0.95f) continue;

                string name = CanopyTrees[_rng.Next(CanopyTrees.Length)];
                var prefab = LoadModel(name);
                if (prefab == null) continue;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
                go.transform.position = new Vector3(px, 0f, pz);
                go.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);

                float targetHeight = Range(3f, 8f);
                go.transform.localScale = Vector3.one * (targetHeight / NativeHeight[name]);
                MarkStatic(go);
                count++;
            }
        }
        return count;
    }

    private static void PlaceCenterpiece(GameObject parent)
    {
        const string name = "TwistedTree_1";
        var prefab = LoadModel(name);
        if (prefab == null) return;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
        go.name = "GreatTree_Centerpiece";
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
        go.transform.localScale = Vector3.one * (15f / NativeHeight[name]); // exatamente 15 m
        MarkStatic(go);
        Debug.Log("[BuildMap] Arvore gigante de 15 m posicionada no centro do mapa.");
    }

    // ---------- Vegetacao rasteira ----------

    private static int ScatterUndergrowth(GameObject parent)
    {
        var small = new[]
        {
            "Grass_Common_Short", "Grass_Common_Tall", "Grass_Wispy_Short", "Grass_Wispy_Tall",
            "Bush_Common", "Bush_Common_Flowers", "Plant_1", "Plant_7",
            "Flower_3_Group", "Flower_4_Group", "Clover_1", "Clover_2",
            "Mushroom_Common", "Rock_Medium_1", "Rock_Medium_2", "Rock_Medium_3",
            "Pebble_Round_1", "Pebble_Round_3", "Pebble_Square_2", "Pebble_Square_5"
        };

        int count = 0;
        const float cell = 5f;

        for (float z = -FieldHalf + 4f; z <= FieldHalf - 4f; z += cell)
        {
            for (float x = -FieldHalf + 4f; x <= FieldHalf - 4f; x += cell)
            {
                float px = x + Range(-cell * 0.45f, cell * 0.45f);
                float pz = z + Range(-cell * 0.45f, cell * 0.45f);

                if (DistanceToPath(px, pz) < PathHalfWidth + 0.8f) continue;
                if (Rand01() > 0.28f) continue;

                string name = small[_rng.Next(small.Length)];
                var prefab = LoadModel(name);
                if (prefab == null) continue;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
                go.transform.position = new Vector3(px, 0f, pz);
                go.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
                go.transform.localScale = Vector3.one * ScaleForProp(name);
                MarkStatic(go);
                count++;
            }
        }
        return count;
    }

    // Props vem grandes demais no pacote; reduz para um tamanho crivel ao lado de 1,70 m.
    private static float ScaleForProp(string name)
    {
        if (name.StartsWith("Grass")) return Range(0.35f, 0.6f);
        if (name.StartsWith("Bush")) return Range(0.7f, 1.1f);
        if (name.StartsWith("Flower") || name.StartsWith("Clover")) return Range(0.15f, 0.3f);
        if (name.StartsWith("Mushroom")) return Range(0.3f, 0.5f);
        if (name.StartsWith("Rock_Medium")) return Range(0.4f, 0.9f);
        if (name.StartsWith("Pebble")) return Range(0.8f, 1.6f);
        if (name.StartsWith("Plant")) return Range(0.4f, 0.8f);
        return 1f;
    }

    // ---------- Referencia de escala ----------

    private static void AddReferenceCapsule()
    {
        var old = GameObject.Find("ScaleReference_1m70");
        if (old != null) Object.DestroyImmediate(old);

        var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.name = "ScaleReference_1m70";
        // Capsula padrao tem 2 m; 0.85 de escala em Y => 1,70 m
        capsule.transform.localScale = new Vector3(0.45f, 0.85f, 0.45f);
        capsule.transform.position = new Vector3(PathX(0f) + 5f, 0.85f, 0f);

        var mat = new Material(Shader.Find("Standard")) { color = new Color(0.85f, 0.2f, 0.25f) };
        capsule.GetComponent<Renderer>().sharedMaterial = mat;
        Debug.Log("[BuildMap] Capsula de referencia (1,70 m) posicionada perto da trilha.");
    }

    // ---------- Utilidades ----------

    private static readonly Dictionary<string, GameObject> _cache = new Dictionary<string, GameObject>();

    private static GameObject LoadModel(string name)
    {
        if (_cache.TryGetValue(name, out var cached)) return cached;

        var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/{name}.fbx");
        if (go == null) Debug.LogWarning($"[BuildMap] Modelo nao encontrado: {name}");
        _cache[name] = go;
        return go;
    }

    private static void MarkStatic(GameObject go)
    {
        GameObjectUtility.SetStaticEditorFlags(go,
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
    }
}
