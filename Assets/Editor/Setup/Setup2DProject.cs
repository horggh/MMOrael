using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class Setup2DProject
{
    private const int Size = 48;
    private const string SpriteDir = "Assets/Art/Sprites/Characters";
    private const string SpritePath = SpriteDir + "/knight_placeholder.png";
    private const string SettingsDir = "Assets/Settings";

    public static void Run()
    {
        ConfigureUrp2D();
        GenerateKnightSprite();
    }

    // ---------- URP com Renderer 2D ----------

    private static void ConfigureUrp2D()
    {
        Directory.CreateDirectory(SettingsDir);

        var rendererData = ScriptableObject.CreateInstance<Renderer2DData>();
        AssetDatabase.CreateAsset(rendererData, SettingsDir + "/Renderer2D.asset");

        var pipeline = UniversalRenderPipelineAsset.Create(rendererData);
        AssetDatabase.CreateAsset(pipeline, SettingsDir + "/URP2D.asset");
        AssetDatabase.SaveAssets();

        // Precisa estar nos dois lugares: sem isso o pipeline fica instalado mas inativo,
        // que é exatamente o que produz os materiais rosa.
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;

        AssetDatabase.SaveAssets();
        Debug.Log($"[Setup2D] URP 2D ativa. defaultRenderPipeline={GraphicsSettings.defaultRenderPipeline?.name}");
    }

    // ---------- Sprite placeholder ----------

    private static Color32[] _px;

    private static void Set(int x, int y, Color32 c)
    {
        if (x < 0 || x >= Size || y < 0 || y >= Size) return;
        _px[(Size - 1 - y) * Size + x] = c; // y invertido: desenho de cima para baixo
    }

    private static void Rect(int x0, int y0, int x1, int y1, Color32 c)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                Set(x, y, c);
    }

    private static void Line(float x0, float y0, float x1, float y1, Color32 c, int w)
    {
        int steps = Mathf.RoundToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * 3f);
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            float cx = Mathf.Lerp(x0, x1, t);
            float cy = Mathf.Lerp(y0, y1, t);
            for (int a = 0; a < w; a++)
                for (int b = 0; b < w; b++)
                    Set(Mathf.RoundToInt(cx + a - (w - 1) / 2f), Mathf.RoundToInt(cy + b - (w - 1) / 2f), c);
        }
    }

    private static void GenerateKnightSprite()
    {
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 o = new Color32(0x0d, 0x0d, 0x14, 255);
        Color32 A = new Color32(0x9d, 0xb6, 0xd6, 255);
        Color32 B = new Color32(0x6c, 0x86, 0xad, 255);
        Color32 C = new Color32(0x45, 0x58, 0x7c, 255);
        Color32 D = new Color32(0x2a, 0x37, 0x52, 255);
        Color32 R = new Color32(0xbb, 0x2f, 0x2f, 255);
        Color32 S = new Color32(0x85, 0x1f, 0x1f, 255);
        Color32 T = new Color32(0x5e, 0x14, 0x14, 255);
        Color32 E = new Color32(0xdf, 0xf2, 0xff, 255);
        Color32 F = new Color32(0xa6, 0xd6, 0xf4, 255);
        Color32 G = new Color32(0x5e, 0xa0, 0xcc, 255);
        Color32 H = new Color32(0x7a, 0x5a, 0x3a, 255);
        Color32 I = new Color32(0xc8, 0xa0, 0x40, 255);
        Color32 J = new Color32(0x8f, 0xe4, 0xff, 255);

        _px = new Color32[Size * Size];
        for (int i = 0; i < _px.Length; i++) _px[i] = clear;

        int[,] cape =
        {
            {18,14,21},{19,13,21},{20,12,21},{21,11,21},{22,10,20},{23,9,20},
            {24,8,20},{25,8,20},{26,7,19},{27,7,19},{28,6,19},{29,6,19},
            {30,5,19},{31,5,19},{32,5,20},{33,4,20},{34,4,20},{35,4,19},
            {36,5,19},{37,5,18},{38,6,17},{39,7,16},{40,8,15},{41,10,14}
        };
        for (int i = 0; i < cape.GetLength(0); i++)
        {
            int y = cape[i, 0], a = cape[i, 1], b = cape[i, 2];
            Rect(a, y, b, y, R);
            Set(a, y, o);
            Set(a + 1, y, S);
            if (y > 30) Rect(a + 1, y, a + 3, y, S);
            if (y > 36) Rect(a + 1, y, b - 1, y, T);
        }

        Rect(19, 34, 24, 44, C); Rect(26, 34, 31, 44, C);
        Rect(19, 34, 20, 44, D); Rect(26, 34, 27, 44, D);
        Rect(17, 44, 25, 46, C); Rect(25, 44, 33, 46, C);
        Rect(17, 46, 25, 46, D); Rect(25, 46, 33, 46, D);
        Rect(16, 44, 16, 46, o); Rect(34, 44, 34, 46, o);
        Rect(17, 47, 33, 47, o);
        for (int y = 34; y <= 43; y++) { Set(18, y, o); Set(32, y, o); Set(25, y, o); }

        Rect(19, 22, 31, 33, B); Rect(21, 24, 29, 31, A); Rect(22, 25, 28, 30, B);
        Rect(19, 32, 31, 33, H); Rect(23, 32, 27, 33, I);
        for (int y = 22; y <= 33; y++) { Set(18, y, o); Set(32, y, o); }
        Rect(19, 21, 31, 21, o);

        Rect(15, 19, 35, 23, B); Rect(15, 19, 19, 21, A); Rect(31, 19, 35, 21, A);
        Rect(15, 23, 35, 23, C);
        for (int x = 15; x <= 35; x++) Set(x, 18, o);
        for (int y = 19; y <= 23; y++) { Set(14, y, o); Set(36, y, o); }
        Rect(15, 24, 18, 27, C); Rect(32, 24, 35, 27, C);
        Rect(15, 27, 18, 30, B); Rect(32, 27, 35, 30, B);
        for (int y = 24; y <= 30; y++) { Set(14, y, o); Set(36, y, o); }
        Rect(15, 31, 18, 31, o); Rect(32, 31, 35, 31, o);

        Rect(20, 19, 30, 20, S); Rect(21, 20, 29, 20, R);

        Rect(21, 6, 29, 7, o); Rect(20, 8, 30, 8, o);
        Rect(21, 7, 29, 7, B); Rect(20, 8, 30, 17, B);
        Rect(22, 8, 28, 16, A); Rect(23, 9, 27, 15, B);
        Rect(24, 6, 26, 8, A);
        for (int y = 8; y <= 17; y++) { Set(19, y, o); Set(31, y, o); }
        Rect(20, 18, 30, 18, o);
        Rect(21, 13, 29, 14, D);
        Rect(22, 13, 24, 13, J); Rect(26, 13, 28, 13, J);
        Rect(22, 14, 24, 14, G); Rect(26, 14, 28, 14, G);
        Rect(24, 9, 26, 17, A); Rect(25, 9, 25, 17, E);
        Rect(20, 17, 30, 17, C);

        Line(23, 30, 27, 27, H, 3);
        Rect(22, 28, 30, 29, o); Rect(23, 28, 29, 28, I);
        Line(27, 27, 43, 9, o, 5);
        Line(27, 27, 43, 9, G, 3);
        Line(27, 26, 42, 9, F, 2);
        Line(28, 25, 41, 9, E, 1);
        Rect(42, 6, 44, 7, o); Set(43, 7, F); Set(43, 8, E);

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.SetPixels32(_px);
        tex.Apply();

        Directory.CreateDirectory(SpriteDir);
        File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);
        ApplyPixelArtImportSettings(SpritePath);

        Debug.Log($"[Setup2D] Sprite gerado: {SpritePath} ({Size}x{Size})");
    }

    /// Ajustes obrigatórios para pixel art: sem filtro, sem compressão, sem mipmap.
    /// Qualquer um deles ligado borra ou suja o pixel.
    private static void ApplyPixelArtImportSettings(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = Size;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
    }
}
