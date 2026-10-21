using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// Jogo salvo: o mundo continua como foi deixado.
///
/// Guarda o personagem (posição, vida, SP, nível, XP, atributos, habilidades), o inventário
/// (colheitas e loot caem nele), o equipamento, a barra de atalhos, os baús colocados com o
/// que tem dentro, as construções com o estágio da obra, os pisos pintados e os canteiros com o
/// estágio da plantação.
///
/// Arquivo: Application.persistentDataPath/save.json (um por máquina). Grava sozinho a cada
/// 15 s, ao fechar o jogo e na hora em que algo é construído. Sem arquivo, o jogo começa
/// com o kit inicial do inventário.
///
/// REGRA DO PROJETO: tudo que guarda estado no jogo (sistema, item no mapa, progresso)
/// precisa entrar aqui. Para incluir algo novo: um campo em Data, preencher em Collect(),
/// restaurar em Load() (o objeto expõe um Restore/All, como StorageChest e FarmGrid) e,
/// se for uma ação marcante (construir, destruir), chamar SaveGame.Save() na hora.
public class SaveGame : MonoBehaviour
{
    private const float AutoSaveEvery = 15f;
    private const int Version = 1;

    public static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

    [Serializable]
    private class SkillData { public string id; public int level; }

    [Serializable]
    private class ChestData { public float x, y; public ItemStack[] slots; }

    [Serializable]
    private class PlotData { public int x, y, stage; public bool wet; public float timer; }

    [Serializable]
    private class FloorData { public int x, y; public string id; }

    [Serializable]
    private class DecorData { public int x, y; public string id; }

    [Serializable]
    private class PortalData { public float x, y; public bool active; }

    [Serializable]
    private class BuildingData
    {
        public string type;
        public float x, y;
        public int stage;
        public int[] delivered;   // materiais já entregues no nível atual
        public bool working;      // obra andando
        public float timer;
        public int interior;      // número do interior (onde ficam os móveis de dentro)
    }

    [Serializable]
    private class Data
    {
        public int version;
        public float px, py;
        public int hp, sp;
        public int level, exp, statPoints;
        public int jobLevel, jobExp;   // nível e XP de skill
        public int[] attrs;
        public int skillPoints;
        public SkillData[] skills;
        public ItemStack[] inventory;
        public string[] equipment;
        public string[] bar;
        public ChestData[] chests;
        public PlotData[] plots;
        public BuildingData[] buildings;
        public FloorData[] floors;
        public DecorData[] decor;   // janelas nas paredes
        public PortalData[] portals;
        public int zoom;            // nível de afastamento da câmera
        public int money;           // unidades (dinheiro)
        public bool infiniteHp, infiniteSp;   // chaves de teste da janela DEV
        public bool ignoreEquipLevel;         // equipar sem o nível mínimo
        public int hitBuff;                   // precisão extra de comida (pudim)
        public float hitBuffLeft;             // segundos restantes dela
        public int aspdBuff;                  // ASPD extra de comida (bife)
        public float aspdBuffLeft;
        public int atkBuff;                   // ATQ extra de comida (hambúrguer)
        public float atkBuffLeft;
        public string hitBuffItem, aspdBuffItem, atkBuffItem;   // ícone de cada bônus
        public bool maxedAttributes;          // atributos em 99 pela janela DEV
        public int expRate;                   // multiplicador de XP (0 = jogo salvo antigo: 1x)
        public string skin;                   // aparência do personagem (SkinDB)
        public int damageRate;                // multiplicador de dano de teste (0 = jogo salvo antigo: 1x)
        public int drakeHp;                   // vida do boss Drake (0 = cheia ou morto)
        public float drakeRespawn;            // segundos até o Drake reaparecer (0 = vivo)
        public int lordHp;                    // vida do MVP Lord of Death (0 = cheia ou morto)
        public float lordRespawn;             // segundos até ele reaparecer (0 = vivo)
        public bool inside;              // dentro de uma casa (px, py são do interior)
        public float insideX, insideY;   // posição da casa em que está
    }

    private static SaveGame _instance;
    private GameObject _player;
    private float _timer;
    private bool _loggedWrite;

    // Depois do Awake de tudo na cena e antes de qualquer Start: o kit inicial do inventário
    // já foi montado e é substituído pelo que estava salvo.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        var pc = FindAnyObjectByType<PlayerController2D>();
        Debug.Log($"[Save] início: personagem {(pc != null ? "achado" : "NÃO achado")}, arquivo {FilePath}");
        if (pc == null) return;
        _instance = new GameObject("Jogo Salvo").AddComponent<SaveGame>();
        _instance._player = pc.gameObject;
        _instance.Load();
        Debug.Log($"[Save] carregado: {Building.All.Count} construções, {StorageChest.All.Count} baús");
    }

    /// Grava agora (ex.: logo após colocar um baú).
    public static void Save()
    {
        if (_instance != null) _instance.Write();
    }

    /// Apaga o arquivo: o próximo início começa do zero.
    public static void Delete()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }

    private void Update()
    {
        _timer += Time.unscaledDeltaTime;
        if (_timer < AutoSaveEvery) return;
        _timer = 0f;
        Write();
    }

    private void OnApplicationQuit() => Write();

    private void OnApplicationPause(bool paused)
    {
        if (paused) Write();
    }

    private void Write()
    {
        if (_player == null) { Debug.LogWarning("[Save] sem personagem: não gravou"); return; }
        var d = Collect();
        string tmp = FilePath + ".tmp";
        try
        {
            // Escreve num temporário e só depois troca: se o jogo cair no meio, o save antigo fica inteiro.
            File.WriteAllText(tmp, JsonUtility.ToJson(d, true));
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(tmp, FilePath);
            if (!_loggedWrite) { _loggedWrite = true; Debug.Log("[Save] gravado em " + FilePath); }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Não consegui salvar o jogo: {e.Message}");
        }
    }

    private Data Collect()
    {
        var d = new Data { version = Version };
        var p = _player;
        d.zoom = CameraFollow2D.Zoom;
        d.infiniteHp = DevCheats.InfiniteHp;
        var stBuff = p.GetComponent<CharacterStats>();
        if (stBuff != null) { d.hitBuff = stBuff.HitBuff; d.hitBuffLeft = stBuff.HitBuffLeft; d.aspdBuff = stBuff.AspdBuff; d.aspdBuffLeft = stBuff.AspdBuffLeft; d.atkBuff = stBuff.AtkBuff; d.atkBuffLeft = stBuff.AtkBuffLeft;
                              d.hitBuffItem = stBuff.HitBuffItem; d.aspdBuffItem = stBuff.AspdBuffItem; d.atkBuffItem = stBuff.AtkBuffItem; }
        d.infiniteSp = DevCheats.InfiniteSp;
        d.ignoreEquipLevel = DevCheats.IgnoreEquipLevel;
        d.maxedAttributes = DevCheats.MaxedAttributes;
        d.expRate = ExpTable.Rate;
        d.damageRate = DevCheats.DamageRate;
        d.skin = SkinDB.CurrentId;
        DrakeBoss.Capture(out d.drakeHp, out d.drakeRespawn);
        LordOfDeath.Capture(out d.lordHp, out d.lordRespawn);
        d.px = p.transform.position.x;
        d.py = p.transform.position.y;

        var health = p.GetComponent<Health>();
        if (health != null) d.hp = health.Hp;

        var stats = p.GetComponent<CharacterStats>();
        if (stats != null)
        {
            d.level = stats.level;
            d.exp = stats.exp;
            d.statPoints = stats.statPoints;
            d.jobLevel = stats.jobLevel;
            d.jobExp = stats.jobExp;
            d.attrs = new int[6];
            for (int i = 0; i < 6; i++) d.attrs[i] = stats.Get(i);
        }

        var skills = p.GetComponent<SkillUser>();
        if (skills != null)
        {
            d.sp = skills.Sp;
            d.skillPoints = skills.skillPoints;
            var list = new List<SkillData>();
            foreach (var kv in skills.Levels) list.Add(new SkillData { id = kv.Key, level = kv.Value });
            d.skills = list.ToArray();
        }

        var bag = p.GetComponent<Inventory>();
        if (bag != null) { d.inventory = bag.slots; d.money = bag.money; }
        var equip = p.GetComponent<Equipment>();
        if (equip != null) d.equipment = equip.slots;
        var bar = p.GetComponent<ActionBar>();
        if (bar != null) d.bar = bar.slots;

        var chests = new List<ChestData>();
        foreach (var c in StorageChest.All)
            chests.Add(new ChestData { x = c.transform.position.x, y = c.transform.position.y, slots = c.slots });
        d.chests = chests.ToArray();

        var plots = new List<PlotData>();
        foreach (var kv in FarmGrid.All)
        {
            if (kv.Value == null) continue;
            plots.Add(new PlotData
            {
                x = kv.Key.x, y = kv.Key.y, stage = kv.Value.Stage, wet = kv.Value.Wet, timer = kv.Value.Timer,
            });
        }
        d.plots = plots.ToArray();

        var buildings = new List<BuildingData>();
        foreach (var b in Building.All)
            if (!b.IsCity)   // prédios da cidade fazem parte do mapa
            buildings.Add(new BuildingData
            {
                type = b.type, x = b.transform.position.x, y = b.transform.position.y, stage = b.stage,
                delivered = b.delivered, working = b.working, timer = b.timer, interior = b.interiorId,
            });
        d.buildings = buildings.ToArray();

        var floors = new List<FloorData>();
        foreach (var kv in FloorTiles.All) floors.Add(new FloorData { x = kv.Key.x, y = kv.Key.y, id = kv.Value });
        d.floors = floors.ToArray();

        var decor = new List<DecorData>();
        foreach (var w in WallDecor.All) decor.Add(new DecorData { x = w.origin.x, y = w.origin.y, id = w.id });
        d.decor = decor.ToArray();

        var portals = new List<PortalData>();
        foreach (var pt in Portal.All) portals.Add(new PortalData { x = pt.transform.position.x, y = pt.transform.position.y, active = pt.active });
        d.portals = portals.ToArray();

        if (HouseInterior.Inside != null)
        {
            d.inside = true;
            d.insideX = HouseInterior.Inside.transform.position.x;
            d.insideY = HouseInterior.Inside.transform.position.y;
        }
        return d;
    }

    private void Load()
    {
        if (!File.Exists(FilePath)) return;
        Data d;
        try { d = JsonUtility.FromJson<Data>(File.ReadAllText(FilePath)); }
        catch (Exception e)
        {
            Debug.LogWarning($"Jogo salvo inválido, começando do zero: {e.Message}");
            return;
        }
        if (d == null) return;

        var p = _player;
        Vector3 scenePos = p.transform.position;
        CameraFollow2D.SetZoom(d.zoom);
        DevCheats.InfiniteHp = d.infiniteHp;
        var stLoad = p.GetComponent<CharacterStats>();
        if (stLoad != null && d.hitBuff > 0 && d.hitBuffLeft > 0f) stLoad.AddHitBuff(d.hitBuff, d.hitBuffLeft, d.hitBuffItem);
        if (stLoad != null && d.aspdBuff > 0 && d.aspdBuffLeft > 0f) stLoad.AddAspdBuff(d.aspdBuff, d.aspdBuffLeft, d.aspdBuffItem);
        if (stLoad != null && d.atkBuff > 0 && d.atkBuffLeft > 0f) stLoad.AddAtkBuff(d.atkBuff, d.atkBuffLeft, d.atkBuffItem);
        DevCheats.InfiniteSp = d.infiniteSp;
        DevCheats.IgnoreEquipLevel = d.ignoreEquipLevel;
        DevCheats.MaxedAttributes = d.maxedAttributes;
        ExpTable.Rate = Mathf.Clamp(d.expRate, 1, ExpTable.MaxRate);
        DevCheats.DamageRate = Mathf.Clamp(d.damageRate, 1, DevCheats.MaxDamageRate);
        if (!string.IsNullOrEmpty(d.skin)) SkinDB.Set(d.skin);
        LordOfDeath.Restore(d.lordHp, d.lordRespawn);
        DrakeBoss.Restore(d.drakeHp, d.drakeRespawn);   // início da cena, caso a casa salva não exista mais
        var pos = new Vector3(d.px, d.py, p.transform.position.z);
        // Fora de casa e fora do mapa (ex.: perdido no vazio depois de uma recompilação no meio
        // do Play): volta para o início da cena em vez de nascer no nada.
        if (!d.inside && !FarmGrid.InMap(pos)) pos = scenePos;
        p.transform.position = pos;
        var body = p.GetComponent<Rigidbody2D>();
        if (body != null) body.position = pos;

        // Ordem importa: atributos e equipamento mudam a vida máxima antes de restaurar a vida.
        var stats = p.GetComponent<CharacterStats>();
        if (stats != null)
        {
            stats.jobLevel = Mathf.Max(1, d.jobLevel);
            stats.jobExp = Mathf.Max(0, d.jobExp);
            stats.Restore(d.level, d.exp, d.statPoints, d.attrs);
        }

        var bag = p.GetComponent<Inventory>();
        if (bag != null && d.inventory != null)
        {
            for (int i = 0; i < bag.slots.Length; i++)
                bag.slots[i] = i < d.inventory.Length && d.inventory[i] != null && ItemDB.Get(d.inventory[i].id) != null
                    ? d.inventory[i] : new ItemStack();
            bag.money = Mathf.Max(0, d.money);
            bag.MarkChanged();
        }

        var equip = p.GetComponent<Equipment>();
        if (equip != null && d.equipment != null) equip.Restore(d.equipment);

        var bar = p.GetComponent<ActionBar>();
        if (bar != null && d.bar != null)
            for (int i = 0; i < bar.slots.Length; i++) bar.slots[i] = i < d.bar.Length ? d.bar[i] : null;

        var skills = p.GetComponent<SkillUser>();
        if (skills != null)
        {
            var levels = new List<KeyValuePair<string, int>>();
            if (d.skills != null) foreach (var s in d.skills) levels.Add(new KeyValuePair<string, int>(s.id, s.level));
            skills.Restore(d.skillPoints, d.sp, levels);
        }

        var health = p.GetComponent<Health>();
        if (health != null && d.hp > 0) health.Restore(d.hp);

        if (d.chests != null)
        {
            foreach (var c in d.chests)
            {
                var chest = StorageChest.Create(new Vector2(c.x, c.y));
                for (int i = 0; i < chest.slots.Length; i++)
                    if (c.slots != null && i < c.slots.Length && c.slots[i] != null && ItemDB.Get(c.slots[i].id) != null)
                        chest.slots[i] = c.slots[i];
            }
        }

        if (d.floors != null)
            foreach (var f in d.floors)
                if (ItemDB.Get(f.id) != null) FloorTiles.Set(new Vector2Int(f.x, f.y), f.id);

        if (d.portals != null)
            foreach (var pt in d.portals) Portal.Create(new Vector2(pt.x, pt.y), pt.active);

        if (d.decor != null)
            foreach (var w in d.decor)
                if (ItemDB.Get(w.id) != null) WallDecor.Create(w.id, new Vector2Int(w.x, w.y));

        // Construções antes dos canteiros: a base delas bloqueia arar embaixo.
        if (d.buildings != null)
            foreach (var b in d.buildings)
                if (!string.IsNullOrEmpty(b.type))
                    Building.Create(b.type, new Vector2(b.x, b.y), b.stage).Restore(b.delivered, b.working, b.timer, b.interior);

        if (d.plots != null)
        {
            foreach (var pl in d.plots)
            {
                var plot = FarmGrid.Till(new Vector2Int(pl.x, pl.y));
                if (plot != null) plot.Restore(pl.stage, pl.wet, pl.timer);
            }
        }

        // Estava dentro de uma casa: monta o interior e põe o personagem onde estava.
        if (d.inside)
        {
            Building house = null;
            foreach (var b in Building.All)
                if (Vector2.Distance(b.transform.position, new Vector2(d.insideX, d.insideY)) < 0.01f) house = b;
            if (house != null) HouseInterior.Enter(house, new Vector2(d.px, d.py));
            else
            {
                p.transform.position = scenePos;
                if (body != null) body.position = scenePos;
            }
        }
    }
}
