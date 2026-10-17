using System.Collections.Generic;
using UnityEngine;

public enum ItemKind { Consumable, Equipment, Material, Placeable }

public enum EquipSlot { Head, Armor, Gloves, Pants, Boots, Cape, Weapon, Shield, Accessory1, Accessory2 }

/// Tipo de arma, como no Ragnarok: define o atraso base do ataque (ASPD, ver CharacterStats).
public enum WeaponType { Fist, Dagger, Sword1H, Sword2H, Spear1H, Spear2H, Axe1H, Axe2H, Mace, Mace2H }

public class ItemDef
{
    public string id;
    public string name;
    public ItemKind kind;
    public int maxStack = 99;
    public int heal;
    /// Efeito especial ao usar (consumível): "retorno" = teleporta para o portal ativo.
    public string effect;
    /// Posicionável: o que é construído no mapa ao usar ("bau", "casa", "piso"), ou "raspar"
    /// para a ferramenta que tira pisos.
    public string place;
    /// Piso ou papel de parede: tema (Lajotas, Desgastado, Natureza, Urbano; Clássico, Padrões…).
    public string theme;
    public Texture2D icon;

    // Equipamento. Acessórios usam Accessory1 e cabem em qualquer um dos dois espaços.
    public EquipSlot slot;
    /// Ferramenta que também serve de arma ("picareta", "enxada"); vazio = não é ferramenta.
    public string tool;
    /// Ocupa as duas mãos: tira o escudo ao equipar.
    public bool twoHanded;
    /// Arma: o tipo define a velocidade de ataque base (ASPD).
    public WeaponType weaponType;
    public int atk, def, hp;
    /// Bônus de dano em % (soma ao dano de todos os golpes e habilidades: 1000 = +1000%, 11x).
    public int damagePct;
    /// FOR, AGI, VIT, INT, DES, SOR — mesma ordem de CharacterStats.Names.
    public int[] attrs = new int[6];

    public string BonusText()
    {
        var parts = new List<string>();
        if (slot == EquipSlot.Weapon && kind == ItemKind.Equipment) parts.Add(WeaponName(weaponType));
        if (!string.IsNullOrEmpty(tool)) parts.Add("Ferramenta");
        if (atk != 0) parts.Add($"ATQ +{atk}");
        if (def != 0) parts.Add($"DEF +{def}");
        if (hp != 0) parts.Add($"HP +{hp}");
        if (damagePct != 0) parts.Add($"Dano +{damagePct}%");
        for (int i = 0; i < 6; i++) if (attrs[i] != 0) parts.Add($"{CharacterStats.Names[i]} +{attrs[i]}");
        return string.Join("  ", parts);
    }

    public static string WeaponName(WeaponType t) => t switch
    {
        WeaponType.Dagger => "Adaga",
        WeaponType.Sword1H => "Espada (1 mão)",
        WeaponType.Sword2H => "Espada (2 mãos)",
        WeaponType.Spear1H => "Lança (1 mão)",
        WeaponType.Spear2H => "Lança (2 mãos)",
        WeaponType.Axe1H => "Machado (1 mão)",
        WeaponType.Axe2H => "Machado (2 mãos)",
        WeaponType.Mace => "Maça (1 mão)",
        WeaponType.Mace2H => "Maça (2 mãos)",
        _ => "Mãos vazias",
    };
}

/// Catálogo de itens. Ícones ficam em Resources/Items com o mesmo nome do id.
public static class ItemDB
{
    private static Dictionary<string, ItemDef> _items;
    private static readonly List<ItemDef> _order = new List<ItemDef>();

    /// Todos os itens do jogo, na ordem em que foram cadastrados (usado pela janela de desenvolvedor).
    public static IReadOnlyList<ItemDef> All
    {
        get
        {
            if (_items == null) Build();
            return _order;
        }
    }

    public static ItemDef Get(string id)
    {
        if (_items == null) Build();
        return id != null && _items.TryGetValue(id, out var d) ? d : null;
    }

    private static void Build()
    {
        _items = new Dictionary<string, ItemDef>();
        _order.Clear();
        Add(new ItemDef { id = "pocao_vermelha", name = "Poção Vermelha", kind = ItemKind.Consumable, heal = 20 });
        Add(new ItemDef { id = "pedra_retorno", name = "Pedra de retorno", kind = ItemKind.Consumable, maxStack = 20, effect = "retorno" });

        Equip("elmo_ferro", "Elmo de Ferro", EquipSlot.Head, def: 2);
        Equip("armadura_ferro", "Armadura de Ferro", EquipSlot.Armor, def: 15);
        Equip("calca_couro", "Calça de Couro", EquipSlot.Pants, def: 2);
        Equip("botas_couro", "Botas de Couro", EquipSlot.Boots, agi: 2);
        Equip("espada_ferro", "Espada de Ferro", EquipSlot.Weapon, atk: 15).weaponType = WeaponType.Sword1H;
        Equip("escudo_madeira", "Escudo de Madeira", EquipSlot.Shield, def: 10);
        var picareta = Equip("picareta", "Picareta", EquipSlot.Weapon, atk: 10);
        picareta.tool = "picareta";
        picareta.weaponType = WeaponType.Axe1H;
        var enxada = Equip("enxada", "Enxada", EquipSlot.Weapon, atk: 10);
        enxada.tool = "enxada";
        enxada.weaponType = WeaponType.Spear1H;
        // Equipado, libera as habilidades Mover e Recolher (móveis e decoração).
        var martelo = Equip("martelo", "Martelo de construção", EquipSlot.Weapon, atk: 8);
        martelo.tool = "martelo";
        martelo.weaponType = WeaponType.Mace;
        var regador = Equip("regador", "Regador", EquipSlot.Weapon);
        regador.tool = "regador";
        regador.twoHanded = true;
        regador.weaponType = WeaponType.Mace2H;
        // Arma de teste do GM: martelo de guerra de duas mãos.
        var marteloGm = Equip("martelo_gm", "Martelo de Guerra do GM", EquipSlot.Weapon, atk: 250, str: 100);
        marteloGm.twoHanded = true;
        marteloGm.weaponType = WeaponType.Mace2H;
        marteloGm.damagePct = 1000;
        Add(new ItemDef { id = "morango", name = "Morango", kind = ItemKind.Material });
        Add(new ItemDef { id = "semente_morango", name = "Semente de Morango", kind = ItemKind.Material });
        Equip("colar_esmeralda", "Colar de Esmeralda", EquipSlot.Accessory1, intl: 2);
        Equip("colar_safira", "Colar de Safira", EquipSlot.Accessory1, intl: 2);
        Equip("anel_ouro", "Anel de Ouro", EquipSlot.Accessory1, intl: 2);
        Add(new ItemDef { id = "bau", name = "Baú", kind = ItemKind.Placeable, maxStack = 10, place = "bau" });
        // Materiais de construção.
        Add(new ItemDef { id = "pedra", name = "Pedra", kind = ItemKind.Material });
        Add(new ItemDef { id = "madeira", name = "Madeira", kind = ItemKind.Material });
        Add(new ItemDef { id = "corda", name = "Corda", kind = ItemKind.Material });
        // Pisos: pintam um quadrado do chão (dentro ou fora de casa). Ícone = o próprio piso.
        foreach (var f in FloorTiles.Catalog)
        {
            var piso = new ItemDef { id = f.id, name = "Piso: " + f.name, kind = ItemKind.Placeable, place = "piso", theme = f.theme };
            Add(piso);
            piso.icon = FloorTiles.Texture(f.id);
        }
        // Papéis de parede: pintam um quadrado da parede do fundo da casa.
        foreach (var w in FloorTiles.WallCatalog)
        {
            var papel = new ItemDef { id = w.id, name = "Papel de parede: " + w.name, kind = ItemKind.Placeable, place = "parede", theme = w.theme };
            Add(papel);
            papel.icon = FloorTiles.Texture(w.id);
        }
        // Janelas: vão na parede do fundo da casa, por cima do papel.
        foreach (var w in WallDecor.Catalog)
        {
            var janela = new ItemDef { id = w.id, name = "Janela: " + w.name, kind = ItemKind.Placeable, place = "janela", maxStack = 10 };
            Add(janela);
            janela.icon = WallDecor.Texture(w.id);
        }
        // Ferramenta: tira pisos pintados (não se gasta; o piso volta para a bolsa).
        Add(new ItemDef { id = "raspador", name = "Raspador de piso", kind = ItemKind.Placeable, maxStack = 1, place = "raspar" });
        // Portal: ativado, vira o ponto de retorno ao morrer (no futuro, teleporte entre cidades).
        Add(new ItemDef { id = "portal", name = "Portal de teleporte", kind = ItemKind.Placeable, maxStack = 5, place = "portal" });
        Add(new ItemDef { id = "planta_casa", name = "Planta de projeto - Casa", kind = ItemKind.Placeable, maxStack = 10, place = "casa" });
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
        _order.Add(d);
    }
}
