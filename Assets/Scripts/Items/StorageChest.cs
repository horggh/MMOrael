using UnityEngine;
using UnityEngine.InputSystem;

/// Baú colocado no mapa: guarda 20 pilhas de itens. Clicar nele faz o personagem ir até
/// lá; ao chegar perto ele abre (sprite aberto + janela de armazenamento em ItemUI).
/// Fecha ao se afastar ou pelo X da janela. Só um baú fica aberto por vez.
public class StorageChest : MonoBehaviour
{
    public const int Size = 20;
    public const float Reach = 1.6f;
    // O desenho tem 148 px de largura, bem mais detalhado que o personagem (32 px por
    // unidade): com 112 px por unidade o baú fica com ~1,3 unidade de largura. Ele não segue
    // a grade de 1 quadrado: é posicionado livre e encosta exatamente no vizinho (Placement).
    private const float PPU = 112f;
    /// Fundo da base sólida (de frente para trás).
    public const float Depth = 0.42f;
    /// Largura real do baú no mapa.
    public static float Width => ClosedSprite != null ? ClosedSprite.bounds.size.x : 1.32f;

    public ItemStack[] slots = new ItemStack[Size];

    /// Todos os baús no mapa (para o jogo salvo).
    public static readonly System.Collections.Generic.List<StorageChest> All = new System.Collections.Generic.List<StorageChest>();

    /// Baú aberto agora (null = nenhum).
    public static StorageChest Current { get; private set; }

    private static StorageChest _pending;   // clicado, esperando o personagem chegar
    private static Sprite _closed, _open;
    private SpriteRenderer _sr;
    private Transform _player;

    public static Sprite ClosedSprite => _closed != null ? _closed : _closed = LoadSprite("bau_fechado");
    private static Sprite OpenSprite => _open != null ? _open : _open = LoadSprite("bau_aberto");

    public static StorageChest Create(Vector2 position)
    {
        var go = new GameObject("Baú");
        go.transform.position = position;
        return go.AddComponent<StorageChest>();
    }

    private static Sprite LoadSprite(string name)
    {
        var tex = Resources.Load<Texture2D>("Props/" + name);
        if (tex == null) return null;
        tex.filterMode = FilterMode.Point;
        // Pivô na base: aberto e fechado têm a mesma base, então não "pula" ao abrir.
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), PPU);
    }

    private void Awake()
    {
        for (int i = 0; i < Size; i++) slots[i] ??= new ItemStack();
        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite = ClosedSprite;
        gameObject.AddComponent<YSortRenderer>();
        // Colisor sólido só na base: o personagem esbarra nele e não dá para arar embaixo.
        var col = gameObject.AddComponent<BoxCollider2D>();
        // Base sólida com a largura exata do desenho: baús encostados não se sobrepõem.
        col.size = new Vector2(Width - 0.02f, Depth);
        col.offset = new Vector2(0f, Depth / 2f);
    }

    private void Update()
    {
        if (_player == null)
        {
            var pc = FindAnyObjectByType<PlayerController2D>();
            if (pc == null) return;
            _player = pc.transform;
        }

        var mouse = Mouse.current;
        bool overUI = CombatHUD.PointerOverUI || ItemUI.PointerOverUI || Building.PointerOverUI || Placement.BlocksInput;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && !overUI && Camera.main != null)
        {
            Vector2 p = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
            var b = _sr.bounds;
            if (b.Contains(new Vector3(p.x, p.y, b.center.z))) _pending = this;
            else if (_pending == this) _pending = null;   // clicou em outro lugar: desistiu
        }

        float dist = Vector2.Distance(_player.position, transform.position);
        if (_pending == this && dist <= Reach)
        {
            _pending = null;
            Open();
        }
        if (Current == this && dist > Reach + 1f) Close();
    }

    [System.Serializable]
    private class Contents { public ItemStack[] slots; }

    public bool IsEmpty
    {
        get { foreach (var s in slots) if (!s.Empty) return false; return true; }
    }

    /// Conteúdo como texto, para o baú recolhido levar os itens junto (ItemStack.data).
    public string SaveContents() => IsEmpty ? null : JsonUtility.ToJson(new Contents { slots = slots });

    public void LoadContents(string data)
    {
        if (string.IsNullOrEmpty(data)) return;
        var c = JsonUtility.FromJson<Contents>(data);
        if (c?.slots == null) return;
        for (int i = 0; i < slots.Length; i++)
            slots[i] = i < c.slots.Length && c.slots[i] != null && ItemDB.Get(c.slots[i].id) != null ? c.slots[i] : new ItemStack();
    }

    /// Quantos itens um baú recolhido guarda (para a dica do item).
    public static int CountIn(string data)
    {
        if (string.IsNullOrEmpty(data)) return 0;
        var c = JsonUtility.FromJson<Contents>(data);
        int n = 0;
        if (c?.slots != null) foreach (var s in c.slots) if (s != null && !s.Empty) n += s.count;
        return n;
    }

    /// Esconde enquanto o martelo leva o baú para outro lugar (não bloqueia nem aparece).
    public void SetHidden(bool hidden)
    {
        _sr.enabled = !hidden;
        GetComponent<BoxCollider2D>().enabled = !hidden;
        if (hidden && Current == this) Close();
    }

    public void Open()
    {
        if (Current != null && Current != this) Current.Close();
        Current = this;
        _sr.sprite = OpenSprite;
    }

    public void Close()
    {
        if (Current == this) Current = null;
        _sr.sprite = ClosedSprite;
    }

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void OnDestroy()
    {
        if (Current == this) Current = null;
        if (_pending == this) _pending = null;
    }
}
