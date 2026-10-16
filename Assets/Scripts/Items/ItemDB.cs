using System.Collections.Generic;
using UnityEngine;

public enum ItemKind { Consumable, Equipment, Material }

public enum EquipSlot { Head, Armor, Gloves, Pants, Boots, Cape, Weapon, Shield, Accessory1, Accessory2 }

public class ItemDef
{
    public string id;
    public string name;
    public ItemKind kind;
    public int maxStack = 99;
    public int heal;
    public Texture2D icon;

    // Equipamento. Acessórios usam Accessory1 e cabem em qualquer um dos dois espaços.
    public EquipSlot slot;
    /// Ferramenta que também serve de arma ("picareta", "enxada"); vazio = não é ferramenta.
    public string tool;
    /// Ocupa as duas mãos: tira o escudo ao equipar.
    public bool twoHanded;
    public int atk, def, hp;
    /// FOR, AGI, VIT, INT, DES, SOR — mesma ordem de CharacterStats.Names.
    public int[] attrs = new int[6];

    public string BonusText()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(tool)) parts.Add("Ferramenta");
        if (atk != 0) parts.Add($"ATQ +{atk}");
        if (def != 0) parts.Add($"DEF +{def}");
        if (hp != 0) parts.Add($"HP +{hp}");
        for (int i = 0; i < 6; i++) if (attrs[i] != 0) parts.Add($"{CharacterStats.Names[i]} +{attrs[i]}");
        return string.Join("  ", parts);
    }
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

        Equip("elmo_ferro", "Elmo de Ferro", EquipSlot.Head, def: 2);
        Equip("armadura_ferro", "Armadura de Ferro", EquipSlot.Armor, def: 15);
        Equip("calca_couro", "Calça de Couro", EquipSlot.Pants, def: 2);
        Equip("botas_couro", "Botas de Couro", EquipSlot.Boots, agi: 2);
        Equip("espada_ferro", "Espada de Ferro", EquipSlot.Weapon, atk: 15);
        Equip("escudo_madeira", "Escudo de Madeira", EquipSlot.Shield, def: 10);
        Equip("picareta", "Picareta", EquipSlot.Weapon, atk: 10).tool = "picareta";
        Equip("enxada", "Enxada", EquipSlot.Weapon, atk: 10).tool = "enxada";
        var regador = Equip("regador", "Regador", EquipSlot.Weapon);
        regador.tool = "regador";
        regador.twoHanded = true;
        Add(new ItemDef { id = "morango", name = "Morango", kind = ItemKind.Material });
        Add(new ItemDef { id = "semente_morango", name = "Semente de Morango", kind = ItemKind.Material });
        Equip("colar_esmeralda", "Colar de Esmeralda", EquipSlot.Accessory1, intl: 2);
        Equip("colar_safira", "Colar de Safira", EquipSlot.Accessory1, intl: 2);
        Equip("anel_ouro", "Anel de Ouro", EquipSlot.Accessory1, intl: 2);
    }

    private static ItemDef Equip(string id, string name, EquipSlot slot, int atk = 0, int def = 0, int hp = 0,
                                 int str = 0, int agi = 0, int vit = 0, int intl = 0, int dex = 0, int luk = 0)
    {
        var d = new ItemDef
        {
            id = id, name = name, kind = ItemKind.Equipment, maxStack = 1, slot = slot,
            atk = atk, def = def, hp = hp, attrs = new[] { str, agi, vit, intl, dex, luk },
        };
        Add(d);
        return d;
    }

    private static void Add(ItemDef d)
    {
        d.icon = Resources.Load<Texture2D>($"Items/{d.id}");
        if (d.icon != null) d.icon.filterMode = FilterMode.Point;
        _items[d.id] = d;
    }
}
