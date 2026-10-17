using System.Collections.Generic;
using UnityEngine;

public enum SkillKind
{
    /// Golpe corpo a corpo no alvo (anda até ele se precisar).
    Melee,
    /// Área em volta do próprio personagem; dispara na hora, sem alvo.
    AreaSelf,
    /// Ação de ferramenta num quadrado do chão (anda até ele se precisar).
    Tool,
    /// Martelo: abre o modo de mexer em móveis e decoração (Placement).
    Furniture,
}

public class SkillDef
{
    public SkillKind kind = SkillKind.Melee;
    /// Dano fixo (ignora ATQ, esquiva e defesa). 0 = usa damageMult sobre o ATQ.
    public int fixedDamage;
    public float radius;
    /// Ferramenta que precisa estar equipada na mão ("enxada"); vazio = nenhuma.
    public string requiredTool;
    /// Item gasto a cada uso ("semente_morango"); vazio = nenhum.
    public string requiredItem;
    /// Já vem aprendida, sem gastar ponto.
    public bool innate;
    /// Ícone fora de Resources/Skills (ex.: "Items/enxada").
    public string iconPath;
    public string id;
    public string name;
    public string description;
    public int spCost;
    public float cooldown;
    /// Multiplicador sobre o ATQ base.
    public float damageMult = 1f;
    public int maxLevel = 1;
    public Texture2D icon;
}

/// Catálogo de habilidades. Ícones em Resources/Skills com o mesmo nome do id.
public static class SkillDB
{
    private static Dictionary<string, SkillDef> _skills;
    private static List<SkillDef> _order;

    public static IReadOnlyList<SkillDef> All { get { if (_order == null) Build(); return _order; } }

    public static SkillDef Get(string id)
    {
        if (_skills == null) Build();
        return id != null && _skills.TryGetValue(id, out var d) ? d : null;
    }

    private static void Build()
    {
        _skills = new Dictionary<string, SkillDef>();
        _order = new List<SkillDef>();
        Add(new SkillDef
        {
            id = "golpe_fulminante",
            name = "Golpe Fulminante",
            description = "Golpe concentrado que causa 5x o ATQ base.",
            spCost = 8,   // como o Golpe Fulminante (Bash) nível 1 do Ragnarok: cabe no SP do nível 1
            cooldown = 1.2f,
            damageMult = 5f,
        });
        Add(new SkillDef
        {
            id = "impacto_explosivo",
            name = "Impacto Explosivo",
            description = "Explosão em volta do personagem: 50 de dano em raio 2.",
            kind = SkillKind.AreaSelf,
            spCost = 25,
            cooldown = 3f,
            fixedDamage = 50,
            radius = 2f,
        });
        Add(new SkillDef
        {
            id = "arar",
            name = "Arar",
            description = "Prepara o quadrado de chão sob o mouse para o plantio. Requer Enxada equipada.",
            kind = SkillKind.Tool,
            requiredTool = "enxada",
            innate = true,
            spCost = 0,
            cooldown = 0.4f,
        });
        Add(new SkillDef
        {
            id = "semear",
            name = "Semear",
            description = "Planta uma Semente de Morango no canteiro arado sob o mouse. Requer Enxada equipada.",
            kind = SkillKind.Tool,
            requiredTool = "enxada",
            requiredItem = "semente_morango",
            innate = true,
            cooldown = 0.4f,
        });
        Add(new SkillDef
        {
            id = "colher",
            name = "Colher",
            description = "Colhe o morango maduro sob o mouse: rende 5 Morangos. Requer Enxada equipada.",
            kind = SkillKind.Tool,
            requiredTool = "enxada",
            innate = true,
            cooldown = 0.4f,
        });
        Add(new SkillDef
        {
            id = "regar",
            name = "Regar",
            description = "Molha até 10 canteiros arados em volta do cursor (raio 2). Requer Regador equipado.",
            kind = SkillKind.Tool,
            requiredTool = "regador",
            innate = true,
            cooldown = 0.6f,
            radius = 2f,
        });
        Add(new SkillDef
        {
            id = "mover",
            name = "Mover",
            description = "Clique num móvel (baú, janela) e leve para outro lugar, com tudo dentro. Requer Martelo equipado.",
            kind = SkillKind.Furniture,
            requiredTool = "martelo",
            innate = true,
            cooldown = 0.2f,
        });
        Add(new SkillDef
        {
            id = "recolher",
            name = "Recolher",
            description = "Clique num móvel para guardá-lo na bolsa; o baú vai com os itens dentro. Requer Martelo equipado.",
            kind = SkillKind.Furniture,
            requiredTool = "martelo",
            innate = true,
            cooldown = 0.2f,
        });
    }

    private static void Add(SkillDef d)
    {
        d.icon = Resources.Load<Texture2D>(d.iconPath ?? $"Skills/{d.id}");
        if (d.icon != null) d.icon.filterMode = FilterMode.Point;
        _skills[d.id] = d;
        _order.Add(d);
    }
}
