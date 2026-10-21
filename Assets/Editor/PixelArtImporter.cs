using UnityEditor;
using UnityEngine;

/// Toda imagem nova nas pastas de arte entra já configurada para pixel art.
/// Sem isso o Unity aplica filtro bilinear e compressão, que borram e sujam o pixel.
public class PixelArtImporter : AssetPostprocessor
{
    // Subir o número reimporta as imagens com as regras novas.
    public override uint GetVersion() => 6;

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Art/") && !assetPath.StartsWith("Assets/Resources/")) return;

        var im = (TextureImporter)assetImporter;
        im.filterMode = FilterMode.Point;
        im.textureCompression = TextureImporterCompression.Uncompressed;
        im.mipmapEnabled = false;
        im.alphaIsTransparency = true;

        // Papéis de parede e janelas: sprites de 64 px por unidade (papel = 1 quadrado), pivô no
        // meio, para dar para arrastar da aba Project para a cena. O jogo continua lendo como
        // textura (Resources.Load<Texture2D>).
        if (assetPath.StartsWith("Assets/Resources/Walls/") || assetPath.StartsWith("Assets/Resources/Windows/") ||
            assetPath.Contains("/Environment/PapelDeParede/") || assetPath.Contains("/Environment/Janelas/"))
        {
            im.textureType = TextureImporterType.Sprite;
            im.spriteImportMode = SpriteImportMode.Single;
            im.spritePixelsPerUnit = 64;
            var ws = new TextureImporterSettings();
            im.ReadTextureSettings(ws);
            ws.spriteAlignment = (int)SpriteAlignment.Center;
            ws.spriteMeshType = SpriteMeshType.FullRect;
            im.SetTextureSettings(ws);
        }

        // Peças de borda do mapa (Tools/extract_bordas.py): sprite de pixel art, 64 px por
        // unidade, pivô na base. Os ladrilhos repetem: malha retangular e textura em Repeat,
        // para o SpriteRenderer em modo Tiled esticar sem emenda (menu RaelMMO > Borda).
        if (assetPath.Contains("/Environment/Borda/"))
        {
            im.textureType = TextureImporterType.Sprite;
            im.spriteImportMode = SpriteImportMode.Single;
            im.spritePixelsPerUnit = 64;
            var st = new TextureImporterSettings();
            im.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect;
            st.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            im.SetTextureSettings(st);
            im.wrapMode = assetPath.Contains("_ladrilho") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        }

        // Fundos de mapa inteiros (Tools/gen_fundo_cruzamento.py): imagem grande (passa de
        // 2048 px, que a Unity reduziria pela metade), 32 px por unidade, pivô no centro.
        if (assetPath.Contains("/Environment/Mapas/"))
        {
            im.textureType = TextureImporterType.Sprite;
            im.spriteImportMode = SpriteImportMode.Single;
            im.spritePixelsPerUnit = 32;
            im.maxTextureSize = 4096;
            var st = new TextureImporterSettings();
            im.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect;
            st.spriteAlignment = (int)SpriteAlignment.Center;
            im.SetTextureSettings(st);
        }

        // Texturas de chão para pintar o mapa (Tools/extract_pisos_natureza.py): 64 px por
        // unidade = 1 ladrilho por quadrado da grade, como os pisos. Pivô no canto de baixo à
        // esquerda (os ladrilhos começam no canto da área, alinhados à grade) e Repeat para a
        // área esticar sem emenda.
        if (assetPath.Contains("/Environment/Texturas/"))
        {
            im.textureType = TextureImporterType.Sprite;
            im.spriteImportMode = SpriteImportMode.Single;
            im.spritePixelsPerUnit = 64;
            im.wrapMode = TextureWrapMode.Repeat;
            var st = new TextureImporterSettings();
            im.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect;
            st.spriteAlignment = (int)SpriteAlignment.BottomLeft;
            im.SetTextureSettings(st);
        }

        // Peças da masmorra (Tools/extract_masmorra.py): na escala dos chãos, 64 px = 1 quadrado
        // da grade (as paredes encaixam no piso). Pivô no meio da base.
        if (assetPath.Contains("/Environment/Masmorra/"))
        {
            im.textureType = TextureImporterType.Sprite;
            im.spriteImportMode = SpriteImportMode.Single;
            im.spritePixelsPerUnit = 64;
            var st = new TextureImporterSettings();
            im.ReadTextureSettings(st);
            st.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            im.SetTextureSettings(st);
        }

        // Prédios e decoração do mapa (Tools/extract_predio_comercial.py, extract_decoracao.py):
        // sprite de 64 px por unidade (padrão do jogo), pivô no meio da base (ordem de
        // desenho pelos pés).
        if (assetPath.Contains("/Environment/Predios/") || assetPath.Contains("/Environment/Decoracao/") ||
            assetPath.Contains("/Sprites/NPCs/"))
        {
            im.textureType = TextureImporterType.Sprite;
            im.spriteImportMode = SpriteImportMode.Single;
            im.spritePixelsPerUnit = 64;
            var st = new TextureImporterSettings();
            im.ReadTextureSettings(st);
            st.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            im.SetTextureSettings(st);
        }

        // Peças de terreno do pincel (Tools/gen_terrenos.py): 64 px = 1 quadrado, pivô no meio
        // (cada peça ocupa exatamente uma célula do tilemap). O formato da colisão da água vem
        // da transparência do desenho.
        if (assetPath.Contains("/Environment/Terrenos/"))
        {
            im.textureType = TextureImporterType.Sprite;
            im.spriteImportMode = SpriteImportMode.Single;
            im.spritePixelsPerUnit = 64;
            var st = new TextureImporterSettings();
            im.ReadTextureSettings(st);
            st.spriteAlignment = (int)SpriteAlignment.Center;
            st.spriteMeshType = SpriteMeshType.FullRect;
            st.spriteGenerateFallbackPhysicsShape = true;
            im.SetTextureSettings(st);
        }

        // Plantações: o canteiro molhado é gerado escurecendo os pixels em tempo de jogo.
        if (assetPath.Contains("/Crops/")) im.isReadable = true;

        // Cursor precisa ser legível pela CPU, senão o Unity recusa ao trocar o ponteiro.
        if (assetPath.Contains("/UI/cursor"))
        {
            im.textureType = TextureImporterType.Cursor;
            im.isReadable = true;
        }
    }
}
