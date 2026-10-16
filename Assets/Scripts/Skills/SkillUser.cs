using System;
using System.Collections.Generic;
using UnityEngine;

/// SP, habilidades aprendidas, pontos de habilidade e recarga.
public class SkillUser : MonoBehaviour
{
    public int maxSp = 100;
    public int Sp { get; private set; }
    [Tooltip("SP recuperado por segundo, parado ou andando.")]
    public float spRegenPerSecond = 0.7f;
    public int skillPoints = 2;   // dá para aprender as duas habilidades já no teste

    public event Action Changed;
    /// Disparado ao usar uma habilidade (para aviso visual).
    public event Action<SkillDef> Used;

    private readonly Dictionary<string, int> _levels = new Dictionary<string, int>();
    private readonly Dictionary<string, float> _readyAt = new Dictionary<string, float>();
    private PlayerController2D _ctrl;
    private float _regen;

    private void Awake()
    {
        Sp = maxSp;
        _ctrl = GetComponent<PlayerController2D>();
        foreach (var d in SkillDB.All) if (d.innate) _levels[d.id] = 1;
        var stats = GetComponent<CharacterStats>();
        if (stats != null) stats.Changed += leveled => { if (leveled) { skillPoints++; Changed?.Invoke(); } };
    }

    private void Update()
    {
        if (Sp >= maxSp) return;
        _regen += spRegenPerSecond * Time.deltaTime;
        if (_regen >= 1f) { int k = (int)_regen; _regen -= k; Sp = Mathf.Min(maxSp, Sp + k); }
    }

    public int Level(string id) => _levels.TryGetValue(id, out var l) ? l : 0;

    public bool Learn(string id)
    {
        var def = SkillDB.Get(id);
        if (def == null || skillPoints <= 0 || Level(id) >= def.maxLevel) return false;
        skillPoints--;
        _levels[id] = Level(id) + 1;
        Changed?.Invoke();
        return true;
    }

    /// 0 = pronta, 1 = acabou de ser usada.
    public float CooldownFraction(string id)
    {
        var def = SkillDB.Get(id);
        if (def == null || def.cooldown <= 0f || !_readyAt.TryGetValue(id, out var t)) return 0f;
        return Mathf.Clamp01((t - Time.time) / def.cooldown);
    }

    /// Aprendida, com SP e com a ferramenta na mão (ignora a recarga).
    public bool Usable(string id)
    {
        var def = SkillDB.Get(id);
        if (def == null || Level(id) == 0 || Sp < def.spCost) return false;
        if (!string.IsNullOrEmpty(def.requiredItem))
        {
            var bag = GetComponent<Inventory>();
            if (bag == null || bag.Count(def.requiredItem) == 0) return false;
        }
        if (string.IsNullOrEmpty(def.requiredTool)) return true;
        var eq = GetComponent<Equipment>();
        var weapon = eq != null ? ItemDB.Get(eq.Get(EquipSlot.Weapon)) : null;
        return weapon != null && weapon.tool == def.requiredTool;
    }

    public bool CanUse(string id) => Usable(id) && CooldownFraction(id) <= 0f;

    /// Pedido vindo da barra de atalhos. O controlador decide o alvo e quando o golpe sai.
    public void Request(string id)
    {
        if (CanUse(id) && _ctrl != null) _ctrl.UseSkill(SkillDB.Get(id));
    }

    /// Chamado pelo controlador no instante em que a habilidade é executada.
    public bool Spend(SkillDef def)
    {
        if (!CanUse(def.id)) return false;
        Sp -= def.spCost;
        _readyAt[def.id] = Time.time + def.cooldown;
        Used?.Invoke(def);
        Changed?.Invoke();
        return true;
    }
}
