using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FixTexturesAndColliders
{
    private const string ScenePath = "Assets/Scenes/Map_StarterField.unity";

    private static readonly (string models, string textures)[] Folders =
    {
        ("Assets/Art/Models/Nature_Quaternius", "Assets/Art/Textures/Nature"),
        ("Assets/Art/Models/Monsters_Quaternius", "Assets/Art/Textures/Monsters"),
        ("Assets/Art/Models/Characters", "Assets/Art/Textures/Characters"),
    };

    public static void Run()
    {
        ExtractAllTextures();
        AddColliders();
    }

    // ---------- Texturas ----------

    private static void ExtractAllTextures()
    {
        foreach (var (modelFolder, textureFolder) in Folders)
        {
            if (!AssetDatabase.IsValidFolder(modelFolder))
            {
                Debug.LogWarning($"[Fix] Pasta nao encontrada: {modelFolder}");
                continue;
            }

            System.IO.Directory.CreateDirectory(textureFolder);

            var guids = AssetDatabase.FindAssets("t:Model", new[] { modelFolder });
            int extracted = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                if (importer.ExtractTextures(textureFolder)) extracted++;
            }

            Debug.Log($"[Fix] {modelFolder}: texturas extraidas de {extracted}/{guids.Length} modelos para {textureFolder}");
        }

        AssetDatabase.Refresh();

        // Reimporta os modelos para que os materiais reencontrem as texturas ja extraidas.
        foreach (var (modelFolder, _) in Folders)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { modelFolder }))
            {
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            }
        }

        AssetDatabase.SaveAssets();

        int total = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Textures" }).Length;
        Debug.Log($"[Fix] Total de texturas no projeto agora: {total}");
    }

    // ---------- Colisores ----------

    private static void AddColliders()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var props = GameObject.Find("Map_Props");
        if (props == null)
        {
            Debug.LogError("[Fix] Map_Props nao encontrado.");
            return;
        }

        int trees = 0;
        var treeRoot = props.transform.Find("Trees");
        if (treeRoot != null)
        {
            foreach (Transform child in treeRoot)
            {
                if (AddTrunkCollider(child.gameObject, 0.10f)) trees++;
            }
        }

        // Apenas pedras grandes bloqueiam; grama, flores e seixos continuam atravessaveis.
        int rocks = 0;
        var under = props.transform.Find("Undergrowth");
        if (under != null)
        {
            foreach (Transform child in under)
            {
                if (!child.name.StartsWith("Rock_Medium")) continue;
                if (AddTrunkCollider(child.gameObject, 0.40f)) rocks++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[Fix] Colisores adicionados: {trees} arvores, {rocks} pedras.");
    }

    /// Capsula vertical cobrindo o tronco. widthFactor define a grossura em relacao
    /// a largura total do objeto (a copa e larga, o tronco e estreito).
    private static bool AddTrunkCollider(GameObject go, float widthFactor)
    {
        if (go.GetComponent<Collider>() != null) return false;

        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return false;

        Bounds world = renderers[0].bounds;
        foreach (var r in renderers) world.Encapsulate(r.bounds);

        float scale = Mathf.Max(0.0001f, go.transform.localScale.x);

        var capsule = go.AddComponent<CapsuleCollider>();
        capsule.direction = 1; // eixo Y
        capsule.height = world.size.y / scale;
        capsule.radius = Mathf.Clamp(world.size.x * widthFactor, 0.15f, 0.8f) / scale;
        capsule.center = go.transform.InverseTransformPoint(world.center);

        return true;
    }
}
