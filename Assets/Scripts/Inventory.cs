using System;
using UnityEngine;

[Serializable]
public class ItemStack
{
    public string id;
    public int count;
    public bool Empty => string.IsNullOrEmpty(id) || count <= 0;
}

/// Inventário em slots, com pilhas por item.
public class Inventory : MonoBehaviour
{
    public const int Size = 24;
    public ItemStack[] slots = new ItemStack[Size];
    public int startingPotions = 5;
    [Tooltip("Intervalo entre usos de consumíveis.")]
    public float useCooldown = 0.6f;

    public event Action Changed;
    public event Action<int> Healed;
    public event Action<string, int> Gained;

    private Health _health;
    private float _readyAt;

    private void Awake()
    {
        _health = GetComponent<Health>();
        for (int i = 0; i < Size; i++) slots[i] ??= new ItemStack();
        if (Count("pocao_vermelha") == 0 && startingPotions > 0) Add("pocao_vermelha", startingPotions);
        // Equipamento de teste, até existirem drops e lojas.
        foreach (var id in new[] { "elmo_ferro", "armadura_ferro", "calca_couro", "botas_couro", "espada_ferro",
                                   "escudo_madeira", "colar_esmeralda", "colar_safira", "anel_ouro",
                                   "picareta", "enxada", "regador" })
            Add(id, 1);
        Add("semente_morango", 5);
    }

    public int Count(string id)
    {
        int n = 0;
        foreach (var s in slots) if (s != null && s.id == id) n += s.count;
        return n;
    }

    public int Add(string id, int amount)
    {
        var def = ItemDB.Get(id);
        int max = def != null ? def.maxStack : 99;
        int left = amount;

        // Completa pilhas existentes, depois ocupa slots vazios.
        foreach (var s in slots)
        {
            if (left == 0) break;
            if (s.id == id && s.count < max) { int k = Mathf.Min(left, max - s.count); s.count += k; left -= k; }
        }
        foreach (var s in slots)
        {
            if (left == 0) break;
            if (s.Empty) { int k = Mathf.Min(left, max); s.id = id; s.count = k; left -= k; }
        }

        int added = amount - left;
        if (added > 0) { Gained?.Invoke(id, added); Changed?.Invoke(); }
        return added;
    }

    public void Remove(string id, int amount)
    {
        for (int i = Size - 1; i >= 0 && amount > 0; i--)
        {
            var s = slots[i];
            if (s.id != id) continue;
            int k = Mathf.Min(amount, s.count);
            s.count -= k; amount -= k;
            if (s.count == 0) s.id = null;
        }
        Changed?.Invoke();
    }

    public void Swap(int a, int b)
    {
        (slots[a], slots[b]) = (slots[b], slots[a]);
        Changed?.Invoke();
    }

    public bool Use(string id)
    {
        var def = ItemDB.Get(id);
        if (def == null || Count(id) == 0) return false;

        // "Usar" um equipamento é vesti-lo.
        if (def.kind == ItemKind.Equipment)
        {
            var eq = GetComponent<Equipment>();
            return eq != null && eq.Equip(id);
        }
        if (Time.time < _readyAt) return false;

        if (def.kind == ItemKind.Consumable && def.heal > 0)
        {
            // Com vida cheia o item seria desperdiçado.
            if (_health == null || _health.IsDead || _health.Hp >= _health.maxHp) return false;
            Healed?.Invoke(_health.Heal(def.heal));
        }
        else return false;

        _readyAt = Time.time + useCooldown;
        Remove(id, 1);
        return true;
    }
}
