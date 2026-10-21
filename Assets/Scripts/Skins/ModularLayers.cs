using System;
using System.Collections.Generic;
using UnityEngine;

/// Arma e escudo como camadas por cima do corpo (skin modular). A cada quadro mostrado pelo
/// KnightAnimator, põe o quadro correspondente da espada e do escudo com a empunhadura em
/// cima da mão marcada naquele quadro do corpo (âncoras em Resources/<modularData>.json,
/// editáveis em RaelMMO > Personagem > Âncoras do modular), na frente ou atrás do corpo.
/// Só aparecem no golpe (animações "attack_*") e se houver arma/escudo equipado.
[DefaultExecutionOrder(1000)]   // depois do YSortRenderer: a ordem do corpo já está certa
public class ModularLayers : MonoBehaviour
{
    // ── Dados (o mesmo formato que Tools/extract_modular.py escreve) ──
    [Serializable] public class BodyFrame
    {
        public int[] mao_arma, mao_escudo;
        // Outras skins (Tools/desarmar_skins.py): qual quadro da espada/escudo da biblioteca
        // usar neste quadro do corpo (vazio = o mesmo quadro desta animação) e a escala dele.
        public string arma_chave, escudo_chave;
        public int arma_quadro = -1, escudo_quadro = -1;
        public float arma_escala;
    }
    [Serializable] public class GripFrame { public int[] empunhadura; }
    [Serializable] public class Layer { public int largura, altura; public GripFrame[] quadros; }
    [Serializable] public class Anim
    {
        public string chave;
        public int largura, altura;
        public BodyFrame[] corpo;
        public Layer espada, escudo;
        public bool arma_na_frente, escudo_na_frente;
    }
    [Serializable] public class Data
    {
        public float ppu = 32f, escala_espada = 1f, escala_escudo = 1f;
        /// Onde estão os desenhos da espada e do escudo (vazio = nesta mesma pasta).
        public string biblioteca;
        public Anim[] anims;
    }

    public static Data Load(string resource)
    {
        var text = Resources.Load<TextAsset>(resource);
        return text != null ? JsonUtility.FromJson<Data>(text.text) : null;
    }

    private SpriteRenderer _body, _weapon, _shield;
    private Equipment _equip;
    private Data _data;
    private string _dataPath;
    private readonly Dictionary<string, Anim> _byKey = new Dictionary<string, Anim>();
    private readonly Dictionary<string, Sprite[]> _sprites = new Dictionary<string, Sprite[]>();
    private Anim _anim;
    private bool _active, _flip;
    private Data _lib;
    private string _libFolder;
    private readonly Dictionary<string, Anim> _libByKey = new Dictionary<string, Anim>();

    private void Awake()
    {
        _body = GetComponent<SpriteRenderer>();
        _equip = GetComponent<Equipment>();
        _weapon = Child("Arma");
        _shield = Child("Escudo");
    }

    private SpriteRenderer Child(string name)
    {
        var t = transform.Find(name);
        var go = t != null ? t.gameObject : new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.enabled = false;
        return sr;
    }

    /// Chamado pelo KnightAnimator a cada quadro mostrado.
    public void Show(SkinDef skin, string key, int frame, bool flip = false)
    {
        _flip = flip;
        _active = skin != null && skin.modular;
        if (!_active) { Hide(); return; }
        if (_data == null || _dataPath != skin.modularData)
        {
            _dataPath = skin.modularData;
            _data = Load(_dataPath);
            _byKey.Clear();
            _sprites.Clear();
            if (_data?.anims != null) foreach (var a in _data.anims) _byKey[a.chave] = a;
            // Biblioteca de espada/escudo: outra skin (a modular) ou esta mesma.
            string libPath = string.IsNullOrEmpty(_data?.biblioteca) ? _dataPath : _data.biblioteca;
            _lib = libPath == _dataPath ? _data : Load(libPath);
            _libFolder = libPath.Substring(0, libPath.LastIndexOf('/') + 1);
            _libByKey.Clear();
            if (_lib?.anims != null) foreach (var a in _lib.anims) _libByKey[a.chave] = a;
        }
        if (!_byKey.TryGetValue(key, out _anim)) { Hide(); return; }

        // Arma e escudo só aparecem no golpe (andando e parado o personagem fica de mãos
        // livres), e só se estiverem equipados.
        bool striking = key.StartsWith("attack");
        bool hasWeapon = striking && _equip != null && !string.IsNullOrEmpty(_equip.Get(EquipSlot.Weapon));
        // Besta não é espada: o golpe com ela não mostra a lâmina (o virote é o efeito).
        if (hasWeapon && ItemDB.Get(_equip.Get(EquipSlot.Weapon))?.weaponType == WeaponType.Crossbow) hasWeapon = false;
        bool hasShield = striking && _equip != null && !string.IsNullOrEmpty(_equip.Get(EquipSlot.Shield));
        Place(_weapon, hasWeapon, frame, true);
        Place(_shield, hasShield, frame, false);
    }

    private void Place(SpriteRenderer sr, bool show, int frame, bool weapon)
    {
        var body = _anim.corpo;
        if (!show || body == null || body.Length == 0) { sr.enabled = false; return; }
        var bf = body[Mathf.Clamp(frame, 0, body.Length - 1)];

        // Qual desenho: o indicado no quadro (biblioteca) ou o mesmo quadro desta animação.
        string refKey = weapon ? bf.arma_chave : bf.escudo_chave;
        int refFrame = weapon ? bf.arma_quadro : bf.escudo_quadro;
        Layer layer;
        string file;
        int idx;
        float extra = 1f;
        if (!string.IsNullOrEmpty(refKey) && _libByKey.TryGetValue(refKey, out var libAnim))
        {
            layer = weapon ? libAnim.espada : libAnim.escudo;
            file = _libFolder + (weapon ? "espada/" : "escudo/") + refKey;
            idx = Mathf.Max(0, refFrame);
            if (weapon && bf.arma_escala > 0f) extra = bf.arma_escala;
        }
        else
        {
            layer = weapon ? _anim.espada : _anim.escudo;
            file = _dataPath.Substring(0, _dataPath.LastIndexOf('/') + 1) + (weapon ? "espada/" : "escudo/") + _anim.chave;
            idx = frame;
        }
        if (layer == null || layer.quadros == null) { sr.enabled = false; return; }
        var sprites = SpritesFor(file, layer);
        if (sprites == null) { sr.enabled = false; return; }

        int[] hand = weapon ? bf.mao_arma : bf.mao_escudo;
        float ppu = _data.ppu;
        // Pixel da mão no quadro do corpo (de cima para baixo) -> posição em relação aos pés
        // (o pivô do corpo fica no meio, 2 px acima da base). Corpo espelhado: tudo espelha.
        float x = (hand[0] - _anim.largura / 2f) / ppu;
        sr.transform.localPosition = new Vector3(_flip ? -x : x, (_anim.altura - 2 - hand[1]) / ppu, 0f);
        sr.sprite = sprites[Mathf.Clamp(idx, 0, sprites.Length - 1)];
        sr.flipX = _flip;
        float scale = (weapon ? _data.escala_espada : _data.escala_escudo) * extra;
        if (scale <= 0f) scale = 1f;
        sr.transform.localScale = new Vector3(scale, scale, 1f);
        sr.enabled = true;
    }

    /// Quadros da tira com o pivô na empunhadura de cada um (assim ela cai na mão).
    private Sprite[] SpritesFor(string file, Layer layer)
    {
        if (_sprites.TryGetValue(file, out var cached)) return cached;
        var tex = Resources.Load<Texture2D>(file);
        Sprite[] result = null;
        if (tex != null)
        {
            tex.filterMode = FilterMode.Point;
            int n = layer.quadros.Length, w = layer.largura, h = layer.altura;
            result = new Sprite[n];
            for (int k = 0; k < n; k++)
            {
                int[] g = layer.quadros[k].empunhadura;
                var pivot = new Vector2((g[0] + 0.5f) / w, (h - g[1] - 0.5f) / h);
                // Os desenhos da biblioteca têm a densidade dela (não a da skin do corpo).
                float lppu = _lib != null ? _lib.ppu : _data.ppu;
                result[k] = Sprite.Create(tex, new Rect(k * w, 0, w, h), pivot, lppu, 0, SpriteMeshType.FullRect);
            }
        }
        _sprites[file] = result;
        return result;
    }

    private void Hide()
    {
        if (_weapon != null) _weapon.enabled = false;
        if (_shield != null) _shield.enabled = false;
    }

    private void LateUpdate()
    {
        if (!_active || _anim == null) return;
        // Na frente ou atrás do corpo, conforme a direção (o corpo muda de ordem pela altura).
        int order = _body.sortingOrder;
        _weapon.sortingOrder = order + (_anim.arma_na_frente ? 1 : -1);
        _shield.sortingOrder = order + (_anim.escudo_na_frente ? 2 : -2);
        _weapon.sortingLayerID = _shield.sortingLayerID = _body.sortingLayerID;
        var c = _body.color;
        _weapon.color = _shield.color = c;
    }

    /// O editor de âncoras salvou: relê os dados no próximo quadro.
    public static void ReloadAll()
    {
        foreach (var l in FindObjectsByType<ModularLayers>()) { l._data = null; }
    }
}
