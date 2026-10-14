using System.Collections.Generic;
using UnityEngine;

public enum ItemKind { Consumable, Equipment, Material }

public class ItemDef
{
    public string id;
    public string name;
    public ItemKind kind;
    public int maxStack = 99;
    public int heal;
    public Texture2D icon;
}

/// Catálogo de itens. Ícones ficam em Resources/Items com o mesmo nome do id.
public static class ItemDB
{
    private static Dictionary<string, ItemDef> _items;

    public static ItemDef Get(string id)
    {
        if (_items == null) Build();
        return id != null && _items.TryGetValue(id, out var d) ? d : null;
    }

    private static void Build()
    {
        _items = new Dictionary<string, ItemDef>();
        Add(new ItemDef { id = "pocao_vermelha", name = "Poção Vermelha", kind = ItemKind.Consumable, heal = 20 });
    }

    private static void Add(ItemDef d)
    {
        d.icon = Resources.Load<Texture2D>($"Items/{d.id}");
        if (d.icon != null) d.icon.filterMode = FilterMode.Point;
        _items[d.id] = d;
    }
}
