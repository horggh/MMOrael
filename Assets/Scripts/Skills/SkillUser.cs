using System;
using System.Collections.Generic;
using UnityEngine;

/// SP, habilidades aprendidas, pontos de habilidade e recarga.
public class SkillUser : MonoBehaviour, ISerializationCallbackReceiver
{
    /// SP máximo: vem de CharacterStats.MaxSp (fórmula do Ragnarok).
    public int maxSp = 100;
    [SerializeField] private int _sp;
    public int Sp { get => _sp; private set => _sp = value; }
    public int skillPoints = 2;   // dá para aprender as duas habilidades já no teste

    public event Action Changed;
    /// Disparado ao usar uma habilidade (para aviso visual).
    public event Action<SkillDef> Used;

    private readonly Dictionary<string, int> _levels = new Dictionary<string, int>();
    // Cópia em listas: dicionário o Unity não guarda quando recompila no meio do Play, e as
    // habilidades aprendidas sumiam (e o salvamento automático gravava isso).
    [SerializeField] private List<string> _levelIds = new List<string>();
    [SerializeField] private List<int> _levelValues = new List<int>();

    public void OnBeforeSerialize()
    {
        _levelIds.Clear();
        _levelValues.Clear();
        foreach (var kv in _levels) { _levelIds.Add(kv.Key); _levelValues.Add(kv.Value); }
    }

    public void OnAfterDeserialize()
    {
        _levels.Clear();
        for (int i = 0; i < _levelIds.Count && i < _levelValues.Count; i++) _levels[_levelIds[i]] = _levelValues[i];
    }
    private readonly Dictionary<string, float> _readyAt = new Dictionary<string, float>();
    private PlayerController2D _ctrl;

    private void Awake()
    {
        _ctrl = GetComponent<PlayerController2D>();
        foreach (var d in SkillDB.All) if (d.innate) _levels[d.id] = 1;
        var stats = GetComponent<CharacterStats>();
        if (stats != null)
        {
            maxSp = Mathf.Max(1, stats.MaxSp);
            // Subir de nível enche o SP (como o HP); mudar INT só ajusta o máximo.
            stats.Changed += leveled =>
            {
                maxSp = Mathf.Max(1, stats.MaxSp);
                Sp = leveled ? maxSp : Mathf.Min(Sp, maxSp);
                Changed?.Invoke();
            };
        }
        Sp = maxSp;
        // Pontos de habilidade vêm do nível de skill (como o nível de classe do Ragnarok).
        if (stats != null) stats.JobLeveled += ups => { skillPoints += ups; Changed?.Invoke(); };
    }

    /// Regeneração natural (NaturalRegen) e afins.
    public void AddSp(int amount)
    {
        if (amount <= 0 || Sp >= maxSp) return;
        Sp = Mathf.Min(maxSp, Sp + amount);
        Changed?.Invoke();
    }

    /// SP cheio (DEV: ao ligar o SP infinito).
    public void RefillSp() { Sp = maxSp; Changed?.Invoke(); }

    public int Level(string id) => _levels.TryGetValue(id, out var l) ? l : 0;

    /// Habilidades aprendidas (id, nível), para o jogo salvo.
    public IEnumerable<KeyValuePair<string, int>> Levels => _levels;

    public void Restore(int points, int sp, IEnumerable<KeyValuePair<string, int>> levels)
    {
        skillPoints = points;
        Sp = Mathf.Clamp(sp, 0, maxSp);
        foreach (var kv in levels)
            if (SkillDB.Get(kv.Key) != null) _levels[kv.Key] = kv.Value;
        Changed?.Invoke();
    }

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
        float cd = def != null ? def.CooldownAt(Level(id)) : 0f;
        if (def == null || cd <= 0f || !_readyAt.TryGetValue(id, out var t)) return 0f;
        return Mathf.Clamp01((t - Time.time) / cd);
    }

    /// A ferramenta que a habilidade pede está na mão (ou ela não pede nenhuma).
    public bool HasTool(SkillDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.requiredTool)) return true;
        var eq = GetComponent<Equipment>();
        var weapon = eq != null ? ItemDB.Get(eq.Get(EquipSlot.Weapon)) : null;
        return weapon != null && weapon.tool == def.requiredTool;
    }

    /// Aprendida, com SP e com a ferramenta na mão (ignora a recarga).
    public bool Usable(string id)
    {
        var def = SkillDB.Get(id);
        if (def == null || Level(id) == 0 || (Sp < def.SpAt(Level(id)) && !DevCheats.InfiniteSp)) return false;
        if (def.requiredWeapons != null && def.requiredWeapons.Length > 0)
        {
            var eqW = GetComponent<Equipment>();
            var w = eqW != null ? ItemDB.Get(eqW.Get(EquipSlot.Weapon)) : null;
            if (w == null || System.Array.IndexOf(def.requiredWeapons, w.weaponType) < 0) return false;
        }
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
        if (!DevCheats.InfiniteSp) Sp -= def.SpAt(Level(def.id));   // DEV: SP infinito
        _readyAt[def.id] = Time.time + def.CooldownAt(Level(def.id));
        Used?.Invoke(def);
        Changed?.Invoke();
        return true;
    }
}
