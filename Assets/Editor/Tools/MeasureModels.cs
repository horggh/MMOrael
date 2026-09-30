using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class MeasureModels
{
    private const string Folder = "Assets/Art/Models/Nature_Quaternius";

    public static void Measure()
    {
        var guids = AssetDatabase.FindAssets("t:Model", new[] { Folder });
        Debug.Log($"[Measure] {guids.Length} modelos encontrados em {Folder}");

        var results = new List<string>();

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            var instance = Object.Instantiate(prefab);
            var renderers = instance.GetComponentsInChildren<Renderer>();

            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                results.Add($"{System.IO.Path.GetFileNameWithoutExtension(path)}|{b.size.y:F2}|{b.size.x:F2}|{b.size.z:F2}");
            }

            Object.DestroyImmediate(instance);
        }

        foreach (var line in results.OrderBy(r => r))
        {
            Debug.Log($"[Measure] {line}");
        }

        Debug.Log("[Measure] FIM");
    }
}
