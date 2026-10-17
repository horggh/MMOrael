using UnityEngine;

/// Troca o ponteiro do mouse: espada sobre monstros, círculo de seleção ao mirar uma
/// habilidade (vermelho sobre o monstro), padrão no resto.
public class CursorManager : MonoBehaviour
{
    private enum Mode { Default, Sword, Target, TargetHot }

    private static Texture2D _sword, _target, _targetHot;
    private static Mode _mode;

    private void Awake()
    {
        var src = Resources.Load<Texture2D>("UI/cursor_espada");
        if (src != null) _sword = Readable(src);
        _target = Ring(new Color32(255, 214, 90, 255));
        _targetHot = Ring(new Color32(255, 72, 56, 255));
    }

    private const int RingSize = 32;

    /// Círculo de seleção em pixel art: anel com contorno escuro, quatro marcas e ponto central.
    private static Texture2D Ring(Color32 color)
    {
        var tex = new Texture2D(RingSize, RingSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px = new Color32[RingSize * RingSize];
        var dark = new Color32(30, 18, 10, 255);
        float c = (RingSize - 1) / 2f;
        for (int y = 0; y < RingSize; y++)
        for (int x = 0; x < RingSize; x++)
        {
            float dx = x - c, dy = y - c, d = Mathf.Sqrt(dx * dx + dy * dy);
            bool ring = d >= 10f && d < 12f;
            bool ringEdge = (d >= 9f && d < 10f) || (d >= 12f && d < 13f);
            // Marcas nas quatro direções, por dentro e por fora do anel.
            bool tick = (Mathf.Abs(dx) < 1f || Mathf.Abs(dy) < 1f) && ((d >= 5f && d < 8f) || (d >= 13f && d < 15.5f));
            bool tickEdge = (Mathf.Abs(dx) < 2f || Mathf.Abs(dy) < 2f) && ((d >= 4f && d < 9f) || (d >= 12f && d < 16f));
            bool dot = d < 1.5f, dotEdge = d < 2.5f;
            Color32 col = new Color32(0, 0, 0, 0);
            if (ringEdge || tickEdge || dotEdge) col = dark;
            if (ring || tick || dot) col = color;
            px[y * RingSize + x] = col;
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
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

    public static void SetSword(bool on) => Set(on ? Mode.Sword : Mode.Default);

    /// Mirando uma habilidade: círculo de seleção, vermelho quando está sobre um monstro.
    public static void SetTarget(bool overMonster) => Set(overMonster ? Mode.TargetHot : Mode.Target);

    private static void Set(Mode mode)
    {
        if (mode == _mode) return;
        _mode = mode;
        switch (mode)
        {
            // Ponto ativo na ponta da lâmina (canto superior esquerdo).
            case Mode.Sword: Cursor.SetCursor(_sword, Vector2.one, CursorMode.Auto); break;
            // Ponto ativo no centro do círculo.
            case Mode.Target: Cursor.SetCursor(_target, Vector2.one * (RingSize / 2), CursorMode.Auto); break;
            case Mode.TargetHot: Cursor.SetCursor(_targetHot, Vector2.one * (RingSize / 2), CursorMode.Auto); break;
            default: Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); break;
        }
    }

    private void OnDisable() => Set(Mode.Default);
}
