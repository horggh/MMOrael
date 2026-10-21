using System;
using System.Collections.Generic;
using UnityEngine;

/// Ficha e drops de um monstro: nome mostrado na ficha (MonsterSheet) e a lista de itens que
/// ele pode deixar ao morrer, com a chance de cada um (%). Os drops ainda não foram definidos:
/// a lista fica vazia e é editável no Inspector de cada monstro; ao morrer, cada item da lista
/// é sorteado e vai para a bolsa de quem o matou (Roll).
public class MonsterDrops : MonoBehaviour
{
    [Serializable]
    public class Drop
    {
        [Tooltip("Id do item (ItemDB), ex.: pocao_vermelha.")]
        public string item;
        [Tooltip("Chance em % (0,01 a 100).")]
        public float chance = 1f;
        public int amount = 1;
    }

    public string displayName;
    public bool mvp;
    public List<Drop> drops = new List<Drop>();

    /// Garante o componente no monstro (com nome e se é MVP) e o devolve.
    public static MonsterDrops Ensure(GameObject go, string name, bool mvp)
    {
        var d = go.GetComponent<MonsterDrops>();
        if (d == null) d = go.AddComponent<MonsterDrops>();
        if (string.IsNullOrEmpty(d.displayName)) d.displayName = name;
        d.mvp = mvp;
        return d;
    }

    /// Sorteia os drops e põe na bolsa (os que caírem). Devolve quantos itens saíram.
    public int Roll(Inventory bag)
    {
        if (bag == null) return 0;
        int n = 0;
        foreach (var d in drops)
        {
            if (string.IsNullOrEmpty(d.item) || ItemDB.Get(d.item) == null) continue;
            if (UnityEngine.Random.value * 100f >= d.chance) continue;
            bag.Add(d.item, Mathf.Max(1, d.amount));
            n++;
        }
        return n;
    }
}
