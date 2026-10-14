using UnityEditor;
using UnityEngine;

/// Toda imagem nova nas pastas de arte entra já configurada para pixel art.
/// Sem isso o Unity aplica filtro bilinear e compressão, que borram e sujam o pixel.
public class PixelArtImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Art/") && !assetPath.StartsWith("Assets/Resources/")) return;

        var im = (TextureImporter)assetImporter;
        im.filterMode = FilterMode.Point;
        im.textureCompression = TextureImporterCompression.Uncompressed;
        im.mipmapEnabled = false;
        im.alphaIsTransparency = true;
    }
}
