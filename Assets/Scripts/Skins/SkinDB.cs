using System;
using System.Collections.Generic;
using UnityEngine;

/// Uma animação de uma skin: tira horizontal de quadros em Resources.
public class SkinAnim
{
    public string file;
    public int frames;
    /// Desenhada olhando para a esquerda (as outras olham para a direita).
    public bool facesLeft;
    public Sprite[] sprites;
}

/// Uma aparência do personagem. Animações conhecidas pelo KnightAnimator:
/// idle_down, walk_down, walk_side, walk_up, run_side, run_up, attack_side, attack_down,
/// attack_up, death. Só walk_down/side/up e attack_side são obrigatórias; as outras caem
/// nessas quando faltam.
public class SkinDef
{
    public string id, name;
    public float ppu = 32f;
    public readonly Dictionary<string, SkinAnim> anims = new Dictionary<string, SkinAnim>();

    public SkinAnim Get(string anim) => anims.TryGetValue(anim, out var a) && a.sprites != null && a.sprites.Length > 0 ? a : null;

    /// Quadro para mostrar na lista de skins (parado, de frente).
    public Sprite Preview => (Get("idle_down") ?? Get("walk_down"))?.sprites[0];
}

/// Catálogo de skins do personagem e a skin em uso (fica no jogo salvo).
public static class SkinDB
{
    private static List<SkinDef> _all;
    private static string _current = "cavaleiro";

    public static event Action Changed;

    public static IReadOnlyList<SkinDef> All { get { if (_all == null) Build(); return _all; } }

    public static string CurrentId => _current;
    public static SkinDef Current => Get(_current) ?? All[0];

    public static SkinDef Get(string id)
    {
        foreach (var s in All) if (s.id == id) return s;
        return null;
    }

    public static void Set(string id)
    {
        if (Get(id) == null || id == _current) return;
        _current = id;
        Changed?.Invoke();
    }

    private static void Build()
    {
        _all = new List<SkinDef>();

        // Cavaleiro: a arte original (Resources/Characters/cav_*), quadros de 64x48.
        var knight = new SkinDef { id = "cavaleiro", name = "Cavaleiro", ppu = 32f };
        Add(knight, "walk_down", "Characters/cav_walk_down", 4, false);
        Add(knight, "walk_side", "Characters/cav_walk_side", 4, true);
        Add(knight, "walk_up", "Characters/cav_walk_up", 4, false);
        Add(knight, "run_side", "Characters/cav_run_side", 4, false);
        Add(knight, "attack_side", "Characters/cav_attack_side", 4, false);
        _all.Add(knight);

        // Andarilho: cabelo branco e cachecol (Tools/build_skin_andarilho.py). A arte é mais
        // detalhada: 40 px por unidade deixa o tamanho perto do cavaleiro.
        var wanderer = new SkinDef { id = "andarilho", name = "Andarilho", ppu = 40f };
        const string W = "Characters/Skins/andarilho/";
        Add(wanderer, "idle_down", W + "idle_down", 1, false);
        Add(wanderer, "walk_down", W + "walk_down", 4, false);
        Add(wanderer, "walk_side", W + "walk_side", 4, true);
        Add(wanderer, "walk_up", W + "walk_up", 4, false);
        Add(wanderer, "run_side", W + "run_side", 8, false);
        Add(wanderer, "run_up", W + "run_up", 3, false);
        Add(wanderer, "attack_down", W + "attack_down", 3, false);
        Add(wanderer, "attack_side", W + "attack_side", 4, false);
        Add(wanderer, "attack_up", W + "attack_up", 4, false);
        Add(wanderer, "death", W + "death", 6, false);
        _all.Add(wanderer);
    }

    private static void Add(SkinDef skin, string anim, string file, int frames, bool facesLeft)
    {
        var a = new SkinAnim { file = file, frames = frames, facesLeft = facesLeft };
        var tex = Resources.Load<Texture2D>(file);
        if (tex == null) Debug.LogError($"[SkinDB] falta Resources/{file}");
        else
        {
            tex.filterMode = FilterMode.Point;
            int fw = tex.width / frames, fh = tex.height;
            // Pivô nos pés (2 px acima da base), igual à extração.
            var pivot = new Vector2(0.5f, 2f / fh);
            a.sprites = new Sprite[frames];
            for (int i = 0; i < frames; i++)
                a.sprites[i] = Sprite.Create(tex, new Rect(i * fw, 0, fw, fh), pivot, skin.ppu, 0, SpriteMeshType.FullRect);
        }
        skin.anims[anim] = a;
    }
}
