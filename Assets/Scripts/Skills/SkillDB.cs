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
    /// Alcance em quadrados (0 = corpo a corpo): anda até essa distância e usa dali.
    public float range;
    /// Por nível (índice = nível − 1); vazio = usa spCost / cooldown / sem conjuração.
    public int[] spByLevel;
    public float[] cooldownByLevel;
    public float[] castByLevel;
    /// Tipos de arma exigidos na mão (vazio = qualquer).
    public WeaponType[] requiredWeapons;

    public int SpAt(int lv) => spByLevel != null && spByLevel.Length > 0 ? spByLevel[Mathf.Clamp(lv, 1, spByLevel.Length) - 1] : spCost;
    public float CooldownAt(int lv) => cooldownByLevel != null && cooldownByLevel.Length > 0 ? cooldownByLevel[Mathf.Clamp(lv, 1, cooldownByLevel.Length) - 1] : cooldown;
    public float CastAt(int lv) => castByLevel != null && castByLevel.Length > 0 ? castByLevel[Mathf.Clamp(lv, 1, castByLevel.Length) - 1] : 0f;
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
            id = "perfurar_espiral",
            name = "Perfurar em Espiral",
            // Spiral Pierce (Ragnarok pré-Renewal, rAthena skill 397): só com lança; alcance 4;
            // 5 golpes que ignoram a DEF; dano = peso da arma × 0,8 × (1 + 0,5 × Nv) + (FOR/10)²
            // por golpe; prende o alvo 1 s (não funciona em chefes); conjuração 0,3→1 s;
            // espera depois 1,2→2 s; SP 18→30.
            description = "Investida com a lança girando: 5 golpes que ignoram a DEF, a até 4 quadrados. " +
                          "Dano por golpe: peso da arma × 0,8 × (1 + 0,5×Nv) + (FOR/10)². Prende o alvo por 1 s. Precisa de lança.",
            kind = SkillKind.Melee,
            maxLevel = 5,
            range = 4f,
            spCost = 18,
            spByLevel = new[] { 18, 21, 24, 27, 30 },
            cooldown = 1.2f,
            cooldownByLevel = new[] { 1.2f, 1.4f, 1.6f, 1.8f, 2f },
            castByLevel = new[] { 0.3f, 0.5f, 0.7f, 0.9f, 1f },
            requiredWeapons = new[] { WeaponType.Spear1H, WeaponType.Spear2H },
        });
        Add(new SkillDef
        {
            id = "impacto_explosivo",
            name = "Impacto Explosivo",
            // Magnum Break (Ragnarok pré-Renewal, rAthena skill 7): área 5x5 em volta, golpe de
            // arma de fogo; centro 3x3 = ATQ × (100 + 20×Nv)%, borda = ATQ × (100 + 10×Nv)%;
            // precisão +10×Nv %; empurra 2 quadrados; 30 SP e 20→16 HP; espera 2 s depois.
            // Por 10 s, os golpes normais causam +20% (dano de fogo).
            description = "Explosão de fogo em volta (5x5). Centro: ATQ × (100 + 20×Nv)%; borda: ATQ × (100 + 10×Nv)%; Nv 10: 500% em toda a área. " +
                          "Precisão +10×Nv %. Depois, golpes normais com +20% de fogo por 10 s. Custa HP.",
            kind = SkillKind.AreaSelf,
            spCost = 30,
            cooldown = 2f,
            radius = 2.5f,
            maxLevel = 10,
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
