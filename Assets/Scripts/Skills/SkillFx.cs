using System.Collections;
using UnityEngine;

/// Efeitos visuais simples de habilidade, gerados em código (sem arte ainda).
public class SkillFx : MonoBehaviour
{
    private static Sprite _ring;

    /// Anel laranja que se expande até o raio da área e some.
    public static void Ring(Vector2 center, float radius)
    {
        var go = new GameObject("FxAnel");
        go.transform.position = center;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = RingSprite();
        sr.sortingOrder = 30000;   // por cima de tudo
        go.AddComponent<SkillFx>().StartCoroutine(Expand(go, sr, radius));
    }

    private static IEnumerator Expand(GameObject go, SpriteRenderer sr, float radius)
    {
        const float dur = 0.35f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = t / dur;
            // O sprite tem 1 unidade de diâmetro: escala = diâmetro desejado.
            go.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, radius * 2f, Mathf.Sqrt(k));
            sr.color = new Color(1f, 0.55f, 0.15f, 1f - k);
            yield return null;
        }
        Destroy(go);
    }

    private static Sprite RingSprite()
    {
        if (_ring != null) return _ring;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        float c = (n - 1) / 2f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = d > 1f ? 0f : d > 0.85f ? 1f : d * d * 0.35f;   // borda forte, miolo leve
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        _ring = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return _ring;
    }
}
