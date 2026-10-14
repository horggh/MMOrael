using UnityEditor;
using UnityEngine;

public static class ConfigureSprites
{
    private const string Folder = "Assets/Art/Sprites";

    public static void Run()
    {
        AssetDatabase.DeleteAsset(Folder + "/Characters/knight_placeholder.png");

        AssetDatabase.Refresh();

        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Apply(path);
            Debug.Log($"[Sprites] Configurado: {path}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Sprites] FIM");
    }

    /// Sem estes ajustes o Unity borra e comprime o sprite, destruindo a pixel art.
    private static void Apply(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 48;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 2048;

        var s = new TextureImporterSettings();
        importer.ReadTextureSettings(s);
        s.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        importer.SetTextureSettings(s);

        importer.SaveAndReimport();
    }
}
