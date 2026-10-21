using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Janela RaelMMO > Mapa > Monstros: todos os monstros em abas (Monstros e MVPs). Clique num
/// deles, diga quantos e eles são espalhados pela área do mapa onde a Scene está (dentro dos
/// limites dela, longe de paredes e de outros objetos sólidos). Cada um vira um objeto normal
/// da cena: dá para mover, apagar e editar no Inspector.
///
/// Monstro novo: uma linha em Catalog (nome, aba, desenho da prévia e como montar).
public class MonstrosWindow : EditorWindow
{
    private class Entry
    {
        public string name;
        public bool mvp;
        public Func<(Texture2D tex, Rect uv)> preview;
        public Action<GameObject> build;
    }

    private static readonly Entry[] Catalog =
    {
        new Entry { name = "Lobo", mvp = false, preview = WolfPreview, build = BuildWolf },
        new Entry { name = "Bloody Murderer", mvp = false,
                    preview = () => Strip("SerialKiller/andar_frente", 3), build = go => BloodyMurderer.Build(go) },
        new Entry { name = "Drake", mvp = true,
                    preview = () => Strip("Drake/parado", 1), build = go => DrakeBoss.Build(go) },
        new Entry { name = "Lord of Death", mvp = true,
                    preview = () => Strip("CavaleiroSangue/parado_frente", 1), build = go => LordOfDeath.Build(go) },
    };

    private static readonly string[] Tabs = { "Monstros", "MVPs" };
    private int _tab;
    private Entry _selected;
    private int _amount = 5;
    private Vector2 _scroll;

    [MenuItem("RaelMMO/Mapa/Monstros")]
    private static void Open() => GetWindow<MonstrosWindow>("Monstros");

    private void OnGUI()
    {
        int tab = GUILayout.Toolbar(_tab, Tabs);
        if (tab != _tab) { _tab = tab; _selected = null; }
        EditorGUILayout.HelpBox("Clique num monstro e diga quantos espalhar. Eles vão para a área do mapa que está no meio da Scene.",
                                MessageType.Info);

        const float cell = 96f;
        var list = Catalog.Where(e => e.mvp == (_tab == 1)).ToArray();
        int perRow = Mathf.Max(1, (int)((position.width - 10) / cell));
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < list.Length; i += perRow)
        {
            EditorGUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + perRow, list.Length); j++)
            {
                var e = list[j];
                EditorGUILayout.BeginVertical(GUILayout.Width(cell - 4));
                var r = GUILayoutUtility.GetRect(cell - 8, cell - 8);
                if (GUI.Button(r, GUIContent.none)) _selected = e;
                var (tex, uv) = e.preview();
                if (tex != null)
                {
                    float s = Mathf.Min((r.width - 8) / (tex.width * uv.width), (r.height - 8) / (tex.height * uv.height));
                    var size = new Vector2(tex.width * uv.width * s, tex.height * uv.height * s);
                    GUI.DrawTextureWithTexCoords(new Rect(r.center - size / 2f, size), tex, uv);
                }
                if (_selected == e) EditorGUI.DrawRect(new Rect(r.x, r.yMax - 3, r.width, 3), new Color(1f, 0.75f, 0.2f));
                GUILayout.Label(e.name, EditorStyles.miniLabel, GUILayout.Width(cell - 8));
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        if (_selected == null) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(_selected.name, EditorStyles.boldLabel);
        _amount = Mathf.Clamp(EditorGUILayout.IntField("Quantos espalhar no mapa?", _amount), 1, 200);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Espalhar")) Scatter(_selected, _amount);
        if (GUILayout.Button("Cancelar")) _selected = null;
        EditorGUILayout.EndHorizontal();
    }

    /// Espalha na área (MapBounds) que contém o meio da Scene; sem área, num raio de 8 em volta.
    private static void Scatter(Entry e, int n)
    {
        var view = SceneView.lastActiveSceneView;
        Vector2 at = view != null ? (Vector2)view.pivot : Vector2.zero;
        Rect area = new Rect(at.x - 8f, at.y - 8f, 16f, 16f);
        foreach (var b in UnityEngine.Object.FindObjectsByType<MapBounds>())
            if (b.playable.Contains(at)) { area = b.playable; break; }

        var group = GameObject.Find("Monstros") ?? new GameObject("Monstros");
        var sub = group.transform.Find(e.name);
        if (sub == null)
        {
            sub = new GameObject(e.name).transform;
            sub.SetParent(group.transform);
            Undo.RegisterCreatedObjectUndo(sub.gameObject, "Monstros");
        }
        Physics2D.SyncTransforms();
        int placed = 0;
        for (int i = 0; i < n; i++)
        {
            Vector2 p = area.center;
            for (int tries = 0; tries < 40; tries++)
            {
                p = new Vector2(UnityEngine.Random.Range(area.xMin + 1f, area.xMax - 1f), UnityEngine.Random.Range(area.yMin + 1f, area.yMax - 1f));
                if (Physics2D.OverlapCircle(p, 0.8f) == null) break;
            }
            var go = new GameObject(n > 1 ? $"{e.name} {i + 1}" : e.name);
            go.transform.SetParent(sub, false);
            go.transform.position = new Vector3(Mathf.Round(p.x * 2f) / 2f, Mathf.Round(p.y * 2f) / 2f, 0f);
            e.build(go);
            MonsterDrops.Ensure(go, e.name, e.mvp);   // drops editáveis no Inspector
            Undo.RegisterCreatedObjectUndo(go, "Espalhar " + e.name);
            placed++;
        }
        Selection.activeGameObject = sub.gameObject;
        EditorSceneManager.MarkSceneDirty(sub.gameObject.scene);
        Debug.Log($"[Monstros] {placed} x {e.name} espalhados. Salve a cena (Ctrl+S).");
    }

    // ---------- prévias ----------

    private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

    private static (Texture2D, Rect) Strip(string res, int frames)
    {
        if (!Cache.TryGetValue(res, out var t) || t == null) Cache[res] = t = Resources.Load<Texture2D>(res);
        return (t, new Rect(0f, 0f, 1f / frames, 1f));
    }

    private const string WolfArt = "Assets/Art/Sprites/Environment/Monsters/lobo_idle.png";

    private static Sprite WolfSprite() =>
        AssetDatabase.LoadAllAssetsAtPath(WolfArt).OfType<Sprite>().OrderBy(s => s.name).FirstOrDefault();

    private static (Texture2D, Rect) WolfPreview()
    {
        var s = WolfSprite();
        if (s == null) return (null, default);
        var r = s.textureRect;
        var t = s.texture;
        return (t, new Rect(r.x / t.width, r.y / t.height, r.width / t.width, r.height / t.height));
    }

    /// Lobo como o BuildScene monta.
    private static void BuildWolf(GameObject w)
    {
        w.AddComponent<SpriteRenderer>().sprite = WolfSprite();
        w.AddComponent<Animator>().runtimeAnimatorController =
            AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Art/Animation/Lobo.controller");
        var wb = w.AddComponent<Rigidbody2D>();
        wb.gravityScale = 0f;
        wb.freezeRotation = true;
        wb.mass = 1.5f;
        var wc = w.AddComponent<CapsuleCollider2D>();
        wc.direction = CapsuleDirection2D.Horizontal;
        wc.size = new Vector2(1.1f, 0.3f);
        wc.offset = new Vector2(0f, 0.15f);
        w.AddComponent<CharacterStats>();
        w.AddComponent<Health>();
        w.AddComponent<WolfAI>();
        w.AddComponent<YSortRenderer>();
    }
}
