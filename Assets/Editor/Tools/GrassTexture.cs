using System.IO;
using UnityEditor;
using UnityEngine;

/// Gera proceduralmente uma textura de grama que se repete sem emenda (tileable),
/// evitando depender de arquivos de imagem externos.
public static class GrassTexture
{
    private const string Path = "Assets/Art/Textures/grass_tile.png";
    private const int Size = 512;

    public static Texture2D CreateAndSave()
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGB24, true);

        // Tons de verde: do mais escuro/frio ao mais claro/amarelado.
        var dark = new Color(0.16f, 0.30f, 0.12f);
        var mid = new Color(0.27f, 0.45f, 0.17f);
        var light = new Color(0.45f, 0.60f, 0.24f);
        var dry = new Color(0.55f, 0.57f, 0.27f);

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                // Ruido seamless: amostra num toro para que as bordas encaixem.
                float patches = TileableNoise(x, y, 3f);
                float detail = TileableNoise(x, y, 14f);
                float blades = TileableNoise(x, y, 48f);

                float t = Mathf.Clamp01(patches * 0.55f + detail * 0.3f + blades * 0.15f);

                Color c = t < 0.45f
                    ? Color.Lerp(dark, mid, t / 0.45f)
                    : Color.Lerp(mid, light, (t - 0.45f) / 0.55f);

                // Manchas secas ocasionais, para quebrar a uniformidade.
                float dryMask = Mathf.InverseLerp(0.68f, 0.85f, patches);
                c = Color.Lerp(c, dry, dryMask * 0.5f);

                // Granulado fino: sugere folhas de grama a curta distancia.
                float grain = (Mathf.PerlinNoise(x * 0.9f, y * 0.9f) - 0.5f) * 0.08f;
                c += new Color(grain, grain * 1.2f, grain * 0.6f);

                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();

        Directory.CreateDirectory("Assets/Art/Textures");
        File.WriteAllBytes(Path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();

        Debug.Log($"[GrassTexture] Textura de grama gerada em {Path}");
        return AssetDatabase.LoadAssetAtPath<Texture2D>(Path);
    }

    /// Perlin amostrado num toro 4D aproximado, garantindo continuidade nas bordas.
    private static float TileableNoise(int x, int y, float frequency)
    {
        float u = (float)x / Size;
        float v = (float)y / Size;

        float a = Mathf.PerlinNoise(u * frequency, v * frequency);
        float b = Mathf.PerlinNoise((u - 1f) * frequency, v * frequency);
        float c = Mathf.PerlinNoise(u * frequency, (v - 1f) * frequency);
        float d = Mathf.PerlinNoise((u - 1f) * frequency, (v - 1f) * frequency);

        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }
}
