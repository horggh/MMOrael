using UnityEditor;

/// Peças de interface em pixel art: sem filtro, sem compressão e sem mipmap,
/// para não borrar nem ganhar artefatos quando ampliadas em escala inteira.
public class UITextureImport : AssetPostprocessor
{
    private static readonly string[] Folders =
    {
        "Assets/Resources/UI/Equipamento/", "Assets/Resources/UI/Kit/", "Assets/Resources/Props/", "Assets/Resources/Items/bau.png",
        "Assets/Resources/Buildings/", "Assets/Resources/Items/planta_casa.png", "Assets/Resources/Floors/",
        "Assets/Resources/Items/raspador.png", "Assets/Resources/Items/martelo.png",
        "Assets/Resources/Skills/mover.png", "Assets/Resources/Skills/recolher.png", "Assets/Resources/Items/portal.png", "Assets/Resources/Items/pedra_retorno.png", "Assets/Resources/Walls/", "Assets/Resources/Windows/",
        "Assets/Resources/Characters/Drake/", "Assets/Resources/Characters/Skins/", "Assets/Resources/Items/martelo_gm.png", "Assets/Resources/Items/pedra.png", "Assets/Resources/Items/madeira.png", "Assets/Resources/Items/corda.png",
    };

    // Mudar a versão faz a Unity reimportar as imagens já importadas com a regra antiga
    // (ex.: arquivos que chegaram antes deste script compilar).
    public override uint GetVersion() => 6;

    private void OnPreprocessTexture()
    {
        if (!System.Array.Exists(Folders, f => assetPath.StartsWith(f))) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Default;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.filterMode = UnityEngine.FilterMode.Point;
        ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        // Construções: o clique vale só onde há desenho (Building lê a transparência do pixel).
        if (assetPath.StartsWith("Assets/Resources/Buildings/")) ti.isReadable = true;
    }
}
