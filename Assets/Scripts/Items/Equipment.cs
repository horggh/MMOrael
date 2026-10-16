using System;
using UnityEngine;

/// Itens vestidos. Equipar tira o item do inventário; desequipar devolve.
public class Equipment : MonoBehaviour
{
    public const int Size = 10;
    public string[] slots = new string[Size];

    public static readonly string[] SlotNames =
    {
        "Cabeça", "Armadura", "Luvas", "Calça", "Botas", "Capa", "Arma", "Escudo", "Acessório", "Acessório",
    };

    public event Action Changed;

    private Inventory _bag;
    private CharacterStats _stats;

    private void Awake()
    {
        _bag = GetComponent<Inventory>();
        _stats = GetComponent<CharacterStats>();
    }

    public string Get(EquipSlot s) => slots[(int)s];

    public static bool Fits(ItemDef d, EquipSlot s)
    {
        if (d == null || d.kind != ItemKind.Equipment) return false;
        if (d.slot == EquipSlot.Accessory1) return s == EquipSlot.Accessory1 || s == EquipSlot.Accessory2;
        return d.slot == s;
    }

    /// Equipa no espaço do item (acessório: primeiro espaço livre).
    public bool Equip(string id)
    {
        var d = ItemDB.Get(id);
        if (d == null || d.kind != ItemKind.Equipment) return false;
        EquipSlot target = d.slot;
        if (d.slot == EquipSlot.Accessory1 && !string.IsNullOrEmpty(Get(EquipSlot.Accessory1))
            && string.IsNullOrEmpty(Get(EquipSlot.Accessory2)))
        {
            target = EquipSlot.Accessory2;
        }
        return EquipTo(id, target);
    }

    public bool EquipTo(string id, EquipSlot s)
    {
        var d = ItemDB.Get(id);
        if (!Fits(d, s) || _bag == null || _bag.Count(id) == 0) return false;

        _bag.Remove(id, 1);
        string old = slots[(int)s];
        if (!string.IsNullOrEmpty(old)) _bag.Add(old, 1);   // o que estava vestido volta à bolsa
        slots[(int)s] = id;
        // Duas mãos: arma de duas mãos derruba o escudo, e escudo derruba a arma de duas mãos.
        EquipSlot other = s == EquipSlot.Weapon ? EquipSlot.Shield : EquipSlot.Weapon;
        bool clash = s == EquipSlot.Weapon ? d.twoHanded
                   : s == EquipSlot.Shield && ItemDB.Get(Get(EquipSlot.Weapon))?.twoHanded == true;
        if (clash && !string.IsNullOrEmpty(Get(other)))
        {
            _bag.Add(Get(other), 1);
            slots[(int)other] = null;
        }
        Apply();
        return true;
    }

    public void Unequip(EquipSlot s)
    {
        string id = slots[(int)s];
        if (string.IsNullOrEmpty(id) || _bag == null) return;
        if (_bag.Add(id, 1) == 0) return;   // bolsa cheia: fica vestido
        slots[(int)s] = null;
        Apply();
    }

    /// Soma os bônus de tudo que está vestido e entrega aos atributos.
    private void Apply()
    {
        int atk = 0, def = 0, hp = 0;
        var attrs = new int[6];
        foreach (var id in slots)
        {
            var d = ItemDB.Get(id);
            if (d == null) continue;
            atk += d.atk; def += d.def; hp += d.hp;
            for (int i = 0; i < 6; i++) attrs[i] += d.attrs[i];
        }
        if (_stats != null) _stats.SetEquipBonus(attrs, atk, def, hp);
        Changed?.Invoke();
    }
}
