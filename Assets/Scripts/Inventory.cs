using System;
using UnityEngine;

[Serializable]
public class ItemStack
{
    public string id;
    public int count;
    /// Conteúdo próprio deste item (ex.: baú recolhido com o que tinha dentro). Pilha com
    /// dados é única: não junta com outras e não perde os dados ao mudar de lugar.
    public string data;
    public bool Empty => string.IsNullOrEmpty(id) || count <= 0;
    public bool HasData => !string.IsNullOrEmpty(data);

    public void Clear() { id = null; count = 0; data = null; }
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
    /// Aviso curto sobre o personagem (ex.: "Nenhum portal ativo").
    public event Action<string> Notice;
    /// Aviso na tela (ex.: equipamento que pede nível maior).
    public void Say(string msg) => Notice?.Invoke(msg);

    /// Dinheiro do personagem, em unidades (a moeda do jogo). Monstros não dão dinheiro:
    /// ele vem da venda de itens para NPCs (a fazer).
    public int money;
    /// Dinheiro ganho (para o aviso "+N unidades").
    public event Action<int> MoneyGained;

    public void AddMoney(int amount)
    {
        if (amount <= 0) return;
        money += amount;
        MoneyGained?.Invoke(amount);
        Changed?.Invoke();
    }

    /// Paga se tiver o suficiente.
    public bool SpendMoney(int amount)
    {
        if (amount < 0 || money < amount) return false;
        money -= amount;
        Changed?.Invoke();
        return true;
    }

    /// "1 unidade", "12.450 unidades".
    public static string MoneyText(int amount) =>
        amount.ToString("#,0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) + (amount == 1 ? " unidade" : " unidades");

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
        Add("bau", 2);
        Add("planta_casa", 1);
    }

    public int Count(string id)
    {
        int n = 0;
        foreach (var s in slots) if (s != null && s.id == id) n += s.count;
        return n;
    }

    public int Add(string id, int amount)
    {
        int added = AddTo(slots, id, amount);
        if (added > 0) { Gained?.Invoke(id, added); Changed?.Invoke(); }
        return added;
    }

    /// Avisa a interface depois de mexer direto em slots (ex.: troca com o baú).
    public void MarkChanged() => Changed?.Invoke();

    /// Põe itens numa lista de espaços: completa pilhas existentes, depois ocupa vazios.
    /// Serve ao inventário e ao baú. Devolve quanto coube.
    public static int AddTo(ItemStack[] slots, string id, int amount)
    {
        var def = ItemDB.Get(id);
        int max = def != null ? def.maxStack : 99;
        int left = amount;
        foreach (var s in slots)
        {
            if (left == 0) break;
            if (s.id == id && !s.HasData && s.count < max) { int k = Mathf.Min(left, max - s.count); s.count += k; left -= k; }
        }
        foreach (var s in slots)
        {
            if (left == 0) break;
            if (s.Empty) { int k = Mathf.Min(left, max); s.id = id; s.count = k; s.data = null; left -= k; }
        }
        return amount - left;
    }

    /// Põe uma pilha inteira (com os dados dela) no primeiro espaço vazio. false = sem espaço.
    public static bool PutStack(ItemStack[] slots, ItemStack stack)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].Empty) continue;
            slots[i] = new ItemStack { id = stack.id, count = stack.count, data = stack.data };
            return true;
        }
        return false;
    }

    /// Item com dados próprios na bolsa (ex.: baú com itens dentro). false = bolsa cheia.
    public bool AddStack(ItemStack stack)
    {
        if (!PutStack(slots, stack)) return false;
        Gained?.Invoke(stack.id, stack.count);
        Changed?.Invoke();
        return true;
    }

    /// Arrastar uma pilha para outro espaço (no mesmo lugar ou entre inventário e baú):
    /// mesmo item junta até o limite da pilha; item diferente troca de lugar.
    public static void Move(ItemStack[] from, int fi, ItemStack[] to, int ti)
    {
        var a = from[fi];
        var b = to[ti];
        if (a.Empty || (from == to && fi == ti)) return;
        if (!b.Empty && b.id == a.id && !a.HasData && !b.HasData)
        {
            int max = ItemDB.Get(a.id)?.maxStack ?? 99;
            int k = Mathf.Min(a.count, max - b.count);
            b.count += k;
            a.count -= k;
            if (a.count == 0) a.Clear();
            return;
        }
        from[fi] = b;
        to[ti] = a;
    }

    public void Remove(string id, int amount)
    {
        // Pilhas comuns primeiro: um baú guardado com itens dentro só sai se não houver outro.
        for (int pass = 0; pass < 2; pass++)
            for (int i = Size - 1; i >= 0 && amount > 0; i--)
            {
                var s = slots[i];
                if (s.id != id || s.HasData != (pass == 1)) continue;
                int k = Mathf.Min(amount, s.count);
                s.count -= k; amount -= k;
                if (s.count == 0) s.Clear();
            }
        Changed?.Invoke();
    }

    /// Tira 1 do espaço indicado e devolve os dados dele (null = item comum).
    public string TakeOne(int index)
    {
        var s = slots[index];
        string data = s.data;
        s.count--;
        if (s.count <= 0) s.Clear();
        Changed?.Invoke();
        return data;
    }

    public void Swap(int a, int b)
    {
        (slots[a], slots[b]) = (slots[b], slots[a]);
        Changed?.Invoke();
    }

    /// Usar o item de um espaço específico: para móveis, o próprio espaço sai ao colocar
    /// (importa para baú recolhido com itens dentro).
    public bool UseSlot(int index)
    {
        var s = slots[index];
        if (s.Empty) return false;
        var def = ItemDB.Get(s.id);
        if (def != null && def.kind == ItemKind.Placeable)
        {
            Placement.Begin(this, s.id, index);
            return true;
        }
        return Use(s.id);
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
        // Móvel (baú): escolhe o lugar no mapa; o item só sai da bolsa ao ser colocado.
        if (def.kind == ItemKind.Placeable)
        {
            Placement.Begin(this, id, -1);
            return true;
        }
        if (Time.time < _readyAt) return false;

        if (def.effect == "retorno")
        {
            // Teleporta para o portal ativo; sem portal, avisa e não gasta a pedra.
            if (!Portal.TeleportPlayer()) { Notice?.Invoke("Nenhum portal ativo"); return false; }
        }
        else if (def.kind == ItemKind.Consumable && (def.heal > 0 || def.healSp > 0 || def.buffHit > 0 || def.buffAspd > 0 || def.buffAtk > 0))
        {
            // Com vida (e SP) cheios o item seria desperdiçado.
            var sk = GetComponent<SkillUser>();
            bool hpFull = _health == null || _health.Hp >= _health.maxHp;
            bool spFull = sk == null || sk.Sp >= sk.maxSp;
            if (_health == null || _health.IsDead) return false;
            if ((def.heal <= 0 || hpFull) && (def.healSp <= 0 || spFull) && def.buffHit <= 0 && def.buffAspd <= 0 && def.buffAtk <= 0) return false;
            if (def.buffHit > 0)
            {
                var st = GetComponent<CharacterStats>();
                if (st != null) st.AddHitBuff(def.buffHit, def.buffSeconds, def.id);
                Notice?.Invoke($"+{def.buffHit} de precisão por {Mathf.RoundToInt(def.buffSeconds / 60f)} min");
            }
            if (def.buffAspd > 0)
            {
                var st = GetComponent<CharacterStats>();
                if (st != null) st.AddAspdBuff(def.buffAspd, def.buffSeconds, def.id);
                Notice?.Invoke($"+{def.buffAspd} de ASPD por {Mathf.RoundToInt(def.buffSeconds / 60f)} min");
            }
            if (def.buffAtk > 0)
            {
                var st = GetComponent<CharacterStats>();
                if (st != null) st.AddAtkBuff(def.buffAtk, def.buffSeconds, def.id);
                Notice?.Invoke($"+{def.buffAtk} de ATQ por {Mathf.RoundToInt(def.buffSeconds / 60f)} min");
            }
            if (def.heal > 0) Healed?.Invoke(_health.Heal(def.heal));
            if (def.healSp > 0 && sk != null) sk.AddSp(def.healSp);
        }
        else return false;

        _readyAt = Time.time + useCooldown;
        Remove(id, 1);
        return true;
    }
}
