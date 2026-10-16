using System.Collections.Generic;
using UnityEngine;

/// Grade de plantio: cada quadrado de 1x1 unidade pode virar um canteiro.
public static class FarmGrid
{
    public const float Cell = 1f;
    // Limite do mapa Campo (40x30), com 1 de margem.
    private static readonly Rect Bounds = new Rect(-19f, -14f, 38f, 28f);

    private static readonly Dictionary<Vector2Int, FarmPlot> Plots = new Dictionary<Vector2Int, FarmPlot>();

    public static Vector2Int CellOf(Vector2 world) =>
        new Vector2Int(Mathf.FloorToInt(world.x / Cell), Mathf.FloorToInt(world.y / Cell));

    public static Vector2 Center(Vector2Int c) => new Vector2((c.x + 0.5f) * Cell, (c.y + 0.5f) * Cell);

    public static FarmPlot At(Vector2Int c) => Plots.TryGetValue(c, out var p) && p != null ? p : null;

    /// Dá para arar: dentro do mapa, ainda não arado e sem obstáculo sólido (tronco).
    public static bool CanTill(Vector2Int c)
    {
        Vector2 center = Center(c);
        if (!Bounds.Contains(center) || At(c) != null) return false;
        foreach (var col in Physics2D.OverlapBoxAll(center, Vector2.one * Cell * 0.8f, 0f))
        {
            // Árvores têm colisor sólido sem Rigidbody; personagens e monstros não bloqueiam.
            if (!col.isTrigger && col.attachedRigidbody == null) return false;
        }
        return true;
    }

    /// A habilidade de ferramenta pode agir neste quadrado agora?
    public static bool CanAct(string skill, Vector2Int c, Inventory bag)
    {
        switch (skill)
        {
            case "arar": return CanTill(c);
            case "semear": return At(c) != null && !At(c).Planted && bag != null && bag.Count("semente_morango") > 0;
            case "colher": return At(c) != null && At(c).Ripe;
            case "regar": return Near(Center(c), 2f, 10).Count > 0;
            default: return false;
        }
    }

    public static void Act(string skill, Vector2Int c, Inventory bag)
    {
        switch (skill)
        {
            case "arar": Till(c); break;
            case "semear":
                bag.Remove("semente_morango", 1);
                At(c).Plant();
                break;
            case "colher":
                bag.Add("morango", FarmPlot.Yield);
                At(c).Harvest();
                break;
            case "regar":
                foreach (var p in Near(Center(c), 2f, 10)) p.Water();
                break;
        }
    }

    /// Até max canteiros mais próximos do ponto, dentro do raio.
    public static List<FarmPlot> Near(Vector2 point, float radius, int max)
    {
        var list = new List<FarmPlot>();
        foreach (var p in Plots.Values)
            if (p != null && Vector2.Distance(p.transform.position, point) <= radius) list.Add(p);
        list.Sort((a, b) => Vector2.Distance(a.transform.position, point)
                            .CompareTo(Vector2.Distance(b.transform.position, point)));
        if (list.Count > max) list.RemoveRange(max, list.Count - max);
        return list;
    }

    /// Canteiro abandonado: some e o chão volta a ser grama.
    public static void Remove(FarmPlot p)
    {
        Plots.Remove(CellOf(p.transform.position));
        Object.Destroy(p.gameObject);
    }

    public static FarmPlot Till(Vector2Int c)
    {
        if (!CanTill(c)) return null;
        var go = new GameObject($"Canteiro {c.x},{c.y}");
        go.transform.position = Center(c);
        var plot = go.AddComponent<FarmPlot>();
        Plots[c] = plot;
        return plot;
    }
}
