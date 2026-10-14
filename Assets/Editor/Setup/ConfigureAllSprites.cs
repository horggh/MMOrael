using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ConfigureAllSprites
{
    public const int PPU = 32;

    /// O lobo foi desenhado numa grade duas vezes mais fina que o cavaleiro: com o
    /// mesmo PPU ele ficaria com o dobro da altura dele.
    public const int MonsterPPU = 64;

    private struct Sheet
    {
        public string path;
        public int frameW, frameH, ppu;
        public float pivotY;    // fração da altura: onde ficam os pés dentro do quadro
    }

    private static readonly Sheet[] Sheets =
    {
        Knight("idle"), Knight("walk"), Knight("run"), Knight("attack"),
        Wolf("idle"), Wolf("walk"), Wolf("run"), Wolf("attack"),
    };

    private static Sheet Knight(string anim) => new Sheet
    {
        path = $"Assets/Art/Sprites/Characters/cavaleiro_{anim}.png",
        frameW = 48, frameH = 48, ppu = PPU, pivotY = 0f,
    };

    // Os quadros do lobo têm 4px de folga embaixo (margem do gerador de animação).
    private static Sheet Wolf(string anim) => new Sheet
    {
        path = $"Assets/Art/Sprites/Monsters/lobo_{anim}.png",
        frameW = 128, frameH = 81, ppu = MonsterPPU, pivotY = 4f / 81f,
    };

    public static void Run()
    {
        foreach (var s in Sheets)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(s.path) == null)
            {
                Debug.LogWarning($"[Sprites] nao encontrado: {s.path}");
                continue;
            }
            Base(s.path, SpriteAlignment.Custom, multiple: true, ppu: s.ppu);
            Slice(s);
        }

        Single("Assets/Art/Sprites/Characters/cavaleiro.png", SpriteAlignment.BottomCenter, PPU);
        Single("Assets/Art/Sprites/Monsters/lobo.png", SpriteAlignment.BottomCenter, MonsterPPU);

        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Sprites/Environment" }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            bool ground = p.EndsWith("grama_base.png");
            // O chão é desenhado em modo Tiled, que exige malha retangular inteira
            // e pivô central; os demais são objetos apoiados no solo.
            Single(p, ground ? SpriteAlignment.Center : SpriteAlignment.BottomCenter, PPU, fullRect: ground);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Sprites] configuracao concluida");
    }

    private static void Single(string path, SpriteAlignment align, int ppu, bool fullRect = false)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null) return;
        Base(path, align, multiple: false, ppu: ppu, fullRect: fullRect);
    }

    private static void Base(string path, SpriteAlignment align, bool multiple, int ppu, bool fullRect = false)
    {
        var im = (TextureImporter)AssetImporter.GetAtPath(path);
        im.textureType = TextureImporterType.Sprite;
        im.spriteImportMode = multiple ? SpriteImportMode.Multiple : SpriteImportMode.Single;
        im.spritePixelsPerUnit = ppu;
        im.filterMode = FilterMode.Point;                                    // sem isso o pixel borra
        im.textureCompression = TextureImporterCompression.Uncompressed;     // compressão suja a cor
        im.mipmapEnabled = false;
        im.alphaIsTransparency = true;
        im.wrapMode = fullRect ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

        var s = new TextureImporterSettings();
        im.ReadTextureSettings(s);
        s.spriteAlignment = (int)align;
        s.spriteMeshType = fullRect ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
        im.SetTextureSettings(s);
        im.SaveAndReimport();
    }

    /// Corta a tira horizontal em quadros do tamanho informado.
    private static void Slice(Sheet sheet)
    {
        var im = (TextureImporter)AssetImporter.GetAtPath(sheet.path);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(sheet.path);
        int count = Mathf.Max(1, tex.width / sheet.frameW);

        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(im);
        provider.InitSpriteEditorDataProvider();

        string baseName = System.IO.Path.GetFileNameWithoutExtension(sheet.path);
        var rects = new List<SpriteRect>();
        for (int i = 0; i < count; i++)
        {
            rects.Add(new SpriteRect
            {
                name = $"{baseName}_{i}",
                spriteID = GUID.Generate(),
                rect = new Rect(i * sheet.frameW, 0, sheet.frameW, sheet.frameH),
                alignment = SpriteAlignment.Custom,
                pivot = new Vector2(0.5f, sheet.pivotY),
            });
        }

        provider.SetSpriteRects(rects.ToArray());
        provider.Apply();
        im.SaveAndReimport();

        Debug.Log($"[Sprites] {baseName}: {count} quadros");
    }
}
