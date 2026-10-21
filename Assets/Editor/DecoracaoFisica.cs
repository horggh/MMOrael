using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Toda decoração da cena (pastas Decoracao, Masmorra e NPCs) tem colisão na base, a "pegada"
/// dela no chão: o personagem esbarra no pé do móvel e passa por trás da parte de cima. E tem
/// YSortRenderer, para ficar na frente ou atrás do personagem conforme a altura no mapa.
/// Feito ao arrastar da aba Project, ao salvar a cena e ao apertar Play; quem já tem colisor
/// fica como está (dá para ajustar o tamanho no Inspector). Peças pequenas (prato, colher,
/// vela: menos de meio quadrado) ficam sem colisão, para dar para pôr em cima das mesas.
[InitializeOnLoad]
public static class DecoracaoFisica
{
    static DecoracaoFisica()
    {
        EditorSceneManager.sceneSaving += (scene, path) => ApplyAll();
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.ExitingEditMode) ApplyAll();
        };
        EditorApplication.hierarchyChanged += () =>
        {
            if (EditorApplication.isPlaying) return;
            foreach (var go in Selection.gameObjects) Apply(go.GetComponent<SpriteRenderer>());
        };
    }

    /// Base do desenho (pixel opaco mais baixo de cada faixa da largura), em unidades a partir
    /// do pivô (meio da base). Só vale para peça cuja base sobe ou desce mais de 0,3 unidade
    /// de uma ponta à outra (na diagonal); as retas ficam com a caixa simples.
    private static bool BaseProfile(Sprite s, out Vector2[] profile, out float meanBase)
    {
        profile = null; meanBase = 0f;
        string path = AssetDatabase.GetAssetPath(s);
        if (!System.IO.File.Exists(path)) return false;
        var tex = new Texture2D(2, 2);
        if (!tex.LoadImage(System.IO.File.ReadAllBytes(path))) return false;
        int w = tex.width, h = tex.height;
        var px = tex.GetPixels32();
        Object.DestroyImmediate(tex);
        float ppu = s.pixelsPerUnit;
        Vector2 pivot = s.pivot;
        const int Steps = 8;
        var list = new System.Collections.Generic.List<Vector2>();
        for (int k = 0; k <= Steps; k++)
        {
            int x0 = Mathf.Clamp(Mathf.RoundToInt(k * (w - 1f) / Steps) - 1, 0, w - 1);
            int x1 = Mathf.Clamp(x0 + 2, 0, w - 1);
            int bottom = -1;
            for (int y = 0; y < h && bottom < 0; y++)
                for (int x = x0; x <= x1; x++)
                    if (px[y * w + x].a > 0) { bottom = y; break; }
            if (bottom < 0) continue;
            list.Add(new Vector2((Mathf.Round(k * (w - 1f) / Steps) - pivot.x) / ppu, (bottom - pivot.y) / ppu));
        }
        if (list.Count < 3) return false;
        // Reta ajustada à base. Só é diagonal se a base acompanha a reta (correlação alta):
        // mesa com pernas nas duas pontas (baixo-alto-baixo) não conta.
        int n = list.Count;
        float mx = 0f, my = 0f;
        foreach (var p in list) { mx += p.x; my += p.y; }
        mx /= n; my /= n;
        float sxy = 0f, sxx = 0f, syy = 0f;
        foreach (var p in list) { sxy += (p.x - mx) * (p.y - my); sxx += (p.x - mx) * (p.x - mx); syy += (p.y - my) * (p.y - my); }
        if (sxx < 1e-6f || syy < 1e-6f) return false;
        float slope = sxy / sxx, r = sxy / Mathf.Sqrt(sxx * syy);
        float xa = list[0].x, xb = list[n - 1].x;
        // Só móveis (1 quadrado ou mais de largura): faca, cutelo e panela tortos ficam de fora.
        if (xb - xa < 1f || Mathf.Abs(r) < 0.85f || Mathf.Abs(slope * (xb - xa)) < 0.3f) return false;
        // A reta passa pelo ponto mais baixo (o pé não fica fora da colisão).
        float shift = 0f;
        foreach (var p in list) shift = Mathf.Min(shift, p.y - (my + slope * (p.x - mx)));
        profile = new Vector2[n];
        for (int i = 0; i < n; i++)
            profile[i] = new Vector2(list[i].x, my + shift + slope * (list[i].x - mx));
        meanBase = my + shift;
        return true;
    }

    private static bool IsDecor(string path) =>
        path.Contains("/Environment/Decoracao/") || path.Contains("/Environment/Masmorra/") || path.Contains("/Sprites/NPCs/");

    [MenuItem("RaelMMO/Mapa/Colisão na base das decorações")]
    private static void Run()
    {
        int n = ApplyAll();
        EditorUtility.DisplayDialog("Decoração", n > 0 ? $"{n} decorações ganharam colisão na base. Salve a cena (Ctrl+S)."
                                                       : "Todas as decorações já têm colisão.", "OK");
    }

    private static int ApplyAll()
    {
        int n = 0;
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
            if (Apply(sr)) n++;
        return n;
    }

    private static bool Apply(SpriteRenderer sr)
    {
        if (sr == null || sr.sprite == null || sr.GetComponent<Building>() != null) return false;
        if (!IsDecor(AssetDatabase.GetAssetPath(sr.sprite))) return false;
        var go = sr.gameObject;
        bool changed = false;
        // Portal entre mapas e entradas de caverna: componente próprio (gatilho no meio, lados sólidos).
        bool caverna = sr.sprite.name.StartsWith("entrada_caverna");
        if ((sr.sprite.name.StartsWith("portal_mapa") || caverna) && go.GetComponent<MapPortal>() == null)
        {
            foreach (var c in go.GetComponents<Collider2D>()) Undo.DestroyObjectImmediate(c);
            var portal = Undo.AddComponent<MapPortal>(go);
            if (caverna) portal.ComoEntradaDeCaverna();
            changed = true;
        }
        if (go.GetComponent<YSortRenderer>() == null)
        {
            Undo.AddComponent<YSortRenderer>(go);
            changed = true;
        }
        Vector2 size = sr.sprite.bounds.size;
        // Peça na diagonal (balcão em perspectiva): colisão seguindo a base do desenho e a
        // linha de frente/trás na altura média dela (a perna mais baixa não decide sozinha).
        var auto = go.GetComponent<BoxCollider2D>();
        bool autoBox = auto != null && go.GetComponents<Collider2D>().Length == 1 &&
                       Mathf.Abs(auto.size.x - size.x * 0.8f) < 0.01f;
        if ((autoBox || go.GetComponent<Collider2D>() == null) && (size.x >= 0.5f || size.y >= 0.5f) &&
            BaseProfile(sr.sprite, out var profile, out float meanBase))
        {
            if (auto != null) Undo.DestroyObjectImmediate(auto);
            var poly = Undo.AddComponent<PolygonCollider2D>(go);
            var pts = new Vector2[profile.Length * 2];
            for (int i = 0; i < profile.Length; i++)
            {
                pts[i] = profile[i];
                pts[pts.Length - 1 - i] = profile[i] + new Vector2(0f, 0.35f);
            }
            poly.SetPath(0, pts);
            var ys = go.GetComponent<YSortRenderer>();
            Undo.RecordObject(ys, "Decoração");
            ys.sortOffset = meanBase;
            EditorSceneManager.MarkSceneDirty(go.scene);
            return true;
        }
        if (go.GetComponent<Collider2D>() == null && (size.x >= 0.5f || size.y >= 0.5f))
        {
            // Só a faixa de baixo (a "pegada" da peça no chão), como a janela Decoração faz.
            var col = Undo.AddComponent<BoxCollider2D>(go);
            col.size = new Vector2(size.x * 0.8f, Mathf.Min(0.4f, size.y * 0.35f));
            col.offset = new Vector2(0f, col.size.y / 2f);
            changed = true;
        }
        if (changed) EditorSceneManager.MarkSceneDirty(go.scene);
        return changed;
    }
}
