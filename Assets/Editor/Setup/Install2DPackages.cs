using System.Threading;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

public static class Install2DPackages
{
    private static readonly string[] Packages =
    {
        "com.unity.render-pipelines.universal", // Renderer 2D: permite luz dinâmica nos sprites
        "com.unity.inputsystem",
        "com.unity.2d.sprite",
        "com.unity.2d.tilemap.extras",          // Rule Tiles: essencial para montar mapas de tiles
        "com.unity.2d.animation",
        "com.unity.2d.pixel-perfect",           // Mantém o pixel nítido, sem borrar ou tremer
    };

    public static void Install()
    {
        // Faz o editor tratar o projeto como 2D: sprites, câmera ortográfica, gizmos 2D.
        EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
        Debug.Log("[Install2D] Editor configurado para modo 2D.");

        foreach (var pkg in Packages)
        {
            Debug.Log($"[Install2D] Instalando {pkg} ...");
            AddRequest request = Client.Add(pkg);

            while (!request.IsCompleted)
            {
                Thread.Sleep(500);
            }

            if (request.Status == StatusCode.Success)
            {
                Debug.Log($"[Install2D] OK: {request.Result.packageId}");
            }
            else
            {
                Debug.LogError($"[Install2D] FALHOU {pkg}: {request.Error?.message}");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Install2D] FIM");
    }
}
