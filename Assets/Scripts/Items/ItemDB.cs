using System.Collections.Generic;
using UnityEngine;

public enum ItemKind { Consumable, Equipment, Material, Placeable }

public enum EquipSlot { Head, Armor, Gloves, Pants, Boots, Cape, Weapon, Shield, Accessory1, Accessory2 }

/// Tipo de arma, como no Ragnarok: define o atraso base do ataque (ASPD, ver CharacterStats).
public enum WeaponType { Fist, Dagger, Sword1H, Sword2H, Spear1H, Spear2H, Axe1H, Axe2H, Mace, Mace2H, Crossbow, Staff, Book, Instrument, Whip, Knuckle, Katar, Huuma }

public class ItemDef
{
    public string id;
    public string name;
    public ItemKind kind;
    public int maxStack = 99;
    public int heal;
    /// Consumível: SP que recupera.
    public int healSp;
    /// Consumível: precisão extra por um tempo (bônus de comida, como no Ragnarok).
    public int buffHit;
    /// Consumível: ASPD extra pelo mesmo tempo (buffSeconds).
    public int buffAspd;
    /// Consumível: ATQ extra pelo mesmo tempo (buffSeconds).
    public int buffAtk;
    public float buffSeconds;
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
    /// Peso da arma (Ragnarok): entra no dano do Perfurar em Espiral. 0 = 100.
    public int weight;
    /// Alcance do ataque básico em quadrados (Ragnarok: lança 3). 0 = corpo a corpo comum.
    public float range;
    /// Nível mínimo para equipar (0 = qualquer).
    public int equipLevel;
    /// Nível da arma (Ragnarok 1–4) e espaços para cartas: informativos por enquanto.
    public int weaponLevel, slots;
    /// Efeitos que o jogo ainda não tem (mostrados no texto do item).
    public string note;
    /// Grupo do item nas abas da janela DEV ("espada", "lanca", "aljava"...).
    public string category;
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
        if (range > 0f) parts.Add($"Alcance {range:0}");
        if (weight > 0) parts.Add($"Peso {weight}");
        if (weaponLevel > 0) parts.Add($"Nível da arma {weaponLevel}");
        if (slots > 0) parts.Add($"Espaços [{slots}]");
        if (equipLevel > 0) parts.Add($"Requer nível {equipLevel}");
        string text = string.Join("  ", parts);
        return string.IsNullOrEmpty(note) ? text : text + "\n" + note;
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
        // Bestas (Tools/extract_bestas.py): atacam de longe, a 5 quadrados do chão.
        string[] bestas = { "Besta Simples", "Besta de Caça", "Besta de Aço", "Besta Reforçada", "Besta de Estribo",
                            "Besta do Batedor", "Besta Escarlate", "Besta Pesada", "Besta Dourada", "Besta de Repetição",
                            "Besta Real", "Besta Celeste", "Besta do Rubi", "Besta Solar", "Besta Ossuda",
                            "Besta Sombria", "Besta da Floresta", "Besta de Gelo", "Besta Infernal", "Besta do Dragão" };
        for (int i = 0; i < bestas.Length; i++)
        {
            var besta = Equip($"besta_{i + 1:00}", bestas[i], EquipSlot.Weapon, atk: 12 + i * 8, dex: i / 4);
            besta.twoHanded = true;
            besta.weaponType = WeaponType.Crossbow;
        }
        // Armas, escudos e aljavas das folhas de imagens/armas (Tools/extract_armas.py): ícones
        // Items/<grupo>_NN. Ataque sobe com o número (as últimas das folhas são as mais fortes).
        var grupos = new (string id, string nome, int n, WeaponType tipo, bool duasMaos)[]
        {
            ("espada", "Espada", 30, WeaponType.Sword1H, false), ("espada_magica", "Espada Mágica", 50, WeaponType.Sword1H, false),
            ("lanca", "Lança", 15, WeaponType.Spear1H, false), ("alabarda", "Alabarda", 20, WeaponType.Spear2H, true),
            ("machado", "Machado", 15, WeaponType.Axe1H, false), ("martelo_guerra", "Martelo de Guerra", 20, WeaponType.Mace2H, true),
            ("maca", "Maça", 30, WeaponType.Mace, false), ("foice", "Foice", 20, WeaponType.Spear2H, true),
            ("cajado", "Cajado", 30, WeaponType.Staff, false), ("katar", "Katar", 30, WeaponType.Katar, true),
            ("soqueira", "Soqueira", 20, WeaponType.Knuckle, false), ("chicote", "Chicote", 30, WeaponType.Whip, false),
            ("instrumento", "Instrumento", 30, WeaponType.Instrument, false), ("livro", "Livro", 30, WeaponType.Book, false),
            ("shuriken", "Shuriken", 20, WeaponType.Huuma, true),
        };
        foreach (var g in grupos)
            for (int i = 1; i <= g.n; i++)
            {
                var w = Equip($"{g.id}_{i:00}", $"{g.nome} {i}", EquipSlot.Weapon, atk: 10 + i * 160 / g.n);
                w.weaponType = g.tipo;
                w.twoHanded = g.duasMaos;
                w.category = g.id;
                // Lanças: peso como no Ragnarok (lança leve ~100, alabarda ~250) e sobe com a força.
                if (g.tipo == WeaponType.Spear1H || g.tipo == WeaponType.Spear2H)
                    w.weight = (g.duasMaos ? 200 : 100) + i * 10;
            }
        // Alabarda 20 = Lança de Caça (Ragnarok pré-Renewal, item 1422 Hunting_Spear): lança de uma
        // mão, ATQ 180, peso 420, alcance 3, nível de arma 4, 1 espaço, nível 60.
        var lancaCaca = Get("alabarda_20");
        lancaCaca.name = "Lança de Caça";
        lancaCaca.weaponType = WeaponType.Spear1H;
        lancaCaca.twoHanded = false;
        lancaCaca.atk = 180;
        lancaCaca.weight = 420;
        lancaCaca.range = 3f;
        lancaCaca.weaponLevel = 4;
        lancaCaca.slots = 1;
        lancaCaca.equipLevel = 60;
        lancaCaca.note = "Ignora a DEF de feras; 10% de chance de carne ao matar feras; 10% de chance de Golpe Articulado Nv 3 " +
                         "(ainda sem efeito: o jogo não tem raças de monstro nem essa habilidade).";
        for (int i = 1; i <= 30; i++)
            Equip($"escudo_{i:00}", $"Escudo {i}", EquipSlot.Shield, def: 5 + i * 2).category = "escudo";
        for (int i = 1; i <= 20; i++)
            Add(new ItemDef { id = $"aljava_{i:00}", name = $"Aljava {i}", kind = ItemKind.Material, maxStack = 1, category = "aljava" });
        // Consumíveis (Tools/extract_consumiveis.py): poções, bebidas e comidas. (id, nome, HP, SP)
        var consumiveis = new (string id, string nome, int hp, int sp)[]
        {
            ("pocao_vermelha_p", "Poção Vermelha Pequena", 50, 0), ("pocao_vermelha_m", "Poção Vermelha Média", 150, 0),
            ("pocao_vermelha_g", "Poção Vermelha Grande", 300, 0), ("pocao_azul_p", "Poção Azul Pequena", 0, 30),
            ("pocao_azul_m", "Poção Azul Média", 0, 70), ("pocao_azul_g", "Poção Azul Grande", 0, 120),
            ("pocao_verde_p", "Poção Verde Pequena", 80, 10), ("pocao_verde_m", "Poção Verde Média", 200, 20),
            ("pocao_verde_g", "Poção Verde Grande", 400, 40), ("pocao_laranja_p", "Poção de Energia Pequena", 40, 40),
            ("pocao_laranja_m", "Poção de Energia Média", 100, 100), ("pocao_laranja_g", "Poção de Energia Grande", 200, 200),
            ("pocao_roxa_p", "Poção Lunar Pequena", 0, 150), ("pocao_roxa_g", "Poção Lunar Grande", 0, 300),
            ("pocao_rosa_p", "Poção do Coração Pequena", 500, 0), ("pocao_rosa_g", "Poção do Coração Grande", 1000, 0),
            ("pocao_branca", "Poção Branca", 325, 0), ("pocao_dourada", "Poção Dourada", 1000, 300),
            ("cerveja", "Caneca de Cerveja", 20, 30), ("cerveja_metal", "Caneco de Cerveja", 30, 40),
            ("vinho_tinto", "Vinho Tinto", 0, 50), ("vinho_branco", "Vinho Branco", 0, 50),
            ("cafe", "Café", 0, 25), ("cha_verde", "Chá Verde", 10, 35),
            ("frango_assado", "Frango Assado", 250, 0), ("bife", "Bife", 200, 0), ("sopa", "Sopa", 120, 20),
            ("pao", "Pão", 60, 0), ("queijo_frutas", "Queijo e Frutas", 100, 10), ("torta_morango", "Torta de Morango", 90, 15),
            ("prato_carne_arroz", "Arroz com Carne", 150, 0), ("espaguete", "Espaguete", 140, 0),
            ("salmao_grelhado", "Salmão Grelhado", 170, 10), ("salada", "Salada", 60, 10), ("hamburguer", "Hambúrguer", 160, 0),
            ("pizza", "Pizza", 180, 0), ("sopa_abobora", "Sopa de Abóbora", 100, 15), ("frango_brocolis", "Frango com Brócolis", 150, 0),
            ("sushi", "Sushi", 130, 20), ("ovos_bacon", "Ovos com Bacon", 120, 0), ("panquecas", "Panquecas", 110, 10),
            ("taco", "Taco", 120, 0), ("acai", "Açaí", 90, 30), ("omelete", "Omelete", 100, 0), ("feijoada", "Feijoada", 220, 0),
            ("frango_arroz", "Frango com Arroz", 160, 0), ("ramen", "Ramen", 170, 15), ("ensopado", "Ensopado", 180, 10),
            ("lasanha", "Lasanha", 190, 0), ("salada_caprese", "Salada Caprese", 70, 15), ("sanduiche", "Sanduíche", 100, 0),
            ("batata_frita", "Batata Frita", 80, 0), ("risoto", "Risoto", 150, 10), ("burrito", "Burrito", 140, 0),
            ("salada_frutas", "Salada de Frutas", 70, 25), ("bolo_chocolate", "Bolo de Chocolate", 90, 20),
            ("milkshake", "Milk-shake", 50, 30), ("mingau_banana", "Mingau de Banana", 80, 10), ("bife_milho", "Bife com Milho", 200, 0),
            ("pudim", "Pudim", 80, 20),
        };
        foreach (var c in consumiveis)
            Add(new ItemDef { id = c.id, name = c.nome, kind = ItemKind.Consumable, heal = c.hp, healSp = c.sp });
        // Pudim: +100 de precisão por 5 minutos.
        var pudim = Get("pudim");
        pudim.buffHit = 100;
        pudim.buffSeconds = 300f;
        // Bife: +100 de ASPD por 5 minutos (o máximo continua 190).
        var bife = Get("bife");
        bife.buffAspd = 100;
        bife.buffSeconds = 300f;
        // Hambúrguer: +100 de ATQ por 5 minutos.
        var hamburguer = Get("hamburguer");
        hamburguer.buffAtk = 100;
        hamburguer.buffSeconds = 300f;
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
