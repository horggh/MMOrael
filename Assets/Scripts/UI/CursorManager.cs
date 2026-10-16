using UnityEngine;

/// Troca o ponteiro do mouse: espada sobre monstros, padrão no resto.
public class CursorManager : MonoBehaviour
{
    private static Texture2D _sword;
    private static bool _swordOn;

    private void Awake()
    {
        var src = Resources.Load<Texture2D>("UI/cursor_espada");
        if (src != null) _sword = Readable(src);
    }

    /// O Unity só aceita cursor legível pela CPU. Se a importação não marcou, copia
    /// a textura via GPU para uma versão legível.
    private static Texture2D Readable(Texture2D src)
    {
        if (src.isReadable) return src;
        var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(src, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        copy.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        copy.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return copy;
    }

    public static void SetSword(bool on)
    {
        if (on == _swordOn) return;
        _swordOn = on;
        // Ponto ativo na ponta da lâmina (canto superior esquerdo).
        Cursor.SetCursor(on ? _sword : null, Vector2.one, CursorMode.Auto);
    }

    private void OnDisable() => SetSword(false);
}
