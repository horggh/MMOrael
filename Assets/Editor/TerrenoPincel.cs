using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// Pincel de terrenos: RaelMMO > Mapa > Pincel de terreno.
/// Escolha o terreno (água, areia, grama, trilha, calçamento...), ligue "Pintar" e arraste o
/// mouse na Scene: as bordas e os cantos se ajustam sozinhos (TerrenoMapa, grade dupla).
/// Botão direito (ou Shift) apaga. Ctrl+Z desfaz.
/// As peças vêm de Tools/gen_terrenos.py (Environment/Terrenos); os tiles do Unity são
/// criados aqui (Assets/Art/Tiles/Terrenos).
public class TerrenoPincel : EditorWindow
{
    private const string Pecas = "Assets/Art/Sprites/Environment/Terrenos";
    private const string Tiles = "Assets/Art/Tiles/Terrenos";
    private const int OrdemBase = -9994;   // acima do fundo do mapa e dos pisos soltos

    private TerrenoMapa _mapa;
    private int _terreno = 4;              // grama
    private int _tamanho = 2;
    private bool _pintar;
    private Vector2 _scroll;
    private string[] _ids = new string[0];
    private readonly HashSet<Vector3Int> _feitos = new HashSet<Vector3Int>();

    [MenuItem("RaelMMO/Mapa/Pincel de terreno")]
    private static void Open() => GetWindow<TerrenoPincel>("Pincel de terreno");

    private void OnEnable()
    {
        _ids = LerIds();
        SceneView.duringSceneGui += OnScene;
        Achar();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnScene;
        _pintar = false;
    }

    private static string[] LerIds()
    {
        string f = Path.Combine(Pecas, "ordem.txt");
        return File.Exists(f) ? File.ReadAllLines(f).Where(l => l.Trim().Length > 0).ToArray() : new string[0];
    }

    private void Achar()
    {
        if (_mapa != null) return;
        var sel = Selection.activeGameObject;
        _mapa = sel != null ? sel.GetComponentInParent<TerrenoMapa>() : null;
        if (_mapa == null) _mapa = Object.FindAnyObjectByType<TerrenoMapa>();
    }

    private void OnGUI()
    {
        if (_ids.Length == 0)
        {
            EditorGUILayout.HelpBox("Faltam as peças: rode python Tools/gen_terrenos.py.", MessageType.Warning);
            return;
        }
        EditorGUILayout.HelpBox("Escolha o terreno, ligue Pintar e arraste na Scene: botão esquerdo pinta, botão direito apaga (ou Shift). " +
                                "Ctrl+Z desfaz. Esc desliga o pincel.", MessageType.Info);
        _mapa = (TerrenoMapa)EditorGUILayout.ObjectField("Terreno do mapa", _mapa, typeof(TerrenoMapa), true);
        if (_mapa == null)
        {
            if (GUILayout.Button("Criar terreno no meio da Scene")) _mapa = Criar(null, Centro());
            return;
        }

        _tamanho = EditorGUILayout.IntSlider("Tamanho do pincel", _tamanho, 1, 10);
        var cor = GUI.backgroundColor;
        GUI.backgroundColor = _pintar ? new Color(1f, 0.6f, 0.3f) : cor;
        if (GUILayout.Button(_pintar ? "Pintando… (clique para parar)" : "Pintar", GUILayout.Height(28))) _pintar = !_pintar;
        GUI.backgroundColor = cor;

        const float cell = 76f;
        int perRow = Mathf.Max(1, (int)((position.width - 10) / cell));
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < _ids.Length; i += perRow)
        {
            EditorGUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + perRow, _ids.Length); j++)
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(cell - 4));
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Pecas}/{_ids[j]}/{_ids[j]}_15.png");
                var r = GUILayoutUtility.GetRect(cell - 8, cell - 8);
                if (GUI.Button(r, GUIContent.none)) { _terreno = j; _pintar = true; }
                if (tex != null) GUI.DrawTexture(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), tex, ScaleMode.ScaleToFit);
                if (j == _terreno) EditorGUI.DrawRect(new Rect(r.x, r.yMax - 4, r.width, 4), new Color(1f, 0.6f, 0.2f));
                GUILayout.Label(_ids[j].Replace('_', ' '), EditorStyles.miniLabel, GUILayout.Width(cell - 8));
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        if (GUILayout.Button($"Preencher a área dos limites com {_ids[_terreno].Replace('_', ' ')}")) Preencher();
        if (GUILayout.Button("Atualizar as peças (depois de gerar de novo)")) { AtualizarPecas(_mapa); _mapa.RedesenharTudo(); }
    }

    private static Vector3 Centro()
    {
        var v = SceneView.lastActiveSceneView;
        return v != null ? new Vector3(Mathf.Round(v.pivot.x), Mathf.Round(v.pivot.y), 0f) : Vector3.zero;
    }

    // ---------- pintar na Scene ----------

    private void OnScene(SceneView view)
    {
        if (!_pintar || _mapa == null || !_mapa.Valido) return;
        var e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(id);
        // O botão direito é do pincel (apagar): sem o menu de contexto da Scene.
        if (e.type == EventType.ContextClick) { e.Use(); return; }
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { _pintar = false; e.Use(); Repaint(); return; }

        Vector2 w = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
        var c = _mapa.dados.WorldToCell(w);
        // Pincel quadrado, alinhado à grade: tamanho N = N x N quadrados (o do mouse no meio;
        // com N par, o do mouse fica no canto de baixo à esquerda do meio).
        int ini = -(_tamanho - 1) / 2, fim = ini + _tamanho - 1;

        // Contorno do pincel.
        if (e.type == EventType.Repaint)
        {
            var cor = e.shift ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.75f, 0.2f);
            Vector3 a0 = _mapa.dados.CellToWorld(new Vector3Int(c.x + ini, c.y + ini, 0));
            Vector3 a1 = _mapa.dados.CellToWorld(new Vector3Int(c.x + fim + 1, c.y + fim + 1, 0));
            Handles.DrawSolidRectangleWithOutline(new[] { a0, new Vector3(a1.x, a0.y), a1, new Vector3(a0.x, a1.y) },
                                                  new Color(cor.r, cor.g, cor.b, 0.12f), cor);
            Handles.BeginGUI();
            GUI.Label(new Rect(10, 10, 420, 20), $"Pincel: {_ids[_terreno]} (tamanho {_tamanho}) — Shift/botão direito apaga, Esc sai",
                      EditorStyles.whiteLargeLabel);
            Handles.EndGUI();
        }

        bool apagar = e.shift || e.button == 1;
        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && (e.button == 0 || e.button == 1) && !e.alt)
        {
            // Todos os terrenos da cena: a borracha apaga em qualquer um, e a pintura substitui o
            // que houver (não importa em qual terreno ou em que momento foi pintado).
            var todos = Object.FindObjectsByType<TerrenoMapa>().Where(t => t.Valido).ToArray();
            if (e.type == EventType.MouseDown)
            {
                GUIUtility.hotControl = id;   // segura o arrasto (esquerdo ou direito) até soltar
                _feitos.Clear();
                var objs = new List<Object>();
                foreach (var t in todos) { objs.Add(t.dados); objs.AddRange(t.camadas.Where(l => l != null)); }
                Undo.RegisterCompleteObjectUndo(objs.ToArray(), apagar ? "Apagar terreno" : "Pintar terreno");
            }
            var pontos = new List<Vector3>();
            for (int dy = ini; dy <= fim; dy++)
                for (int dx = ini; dx <= fim; dx++)
                {
                    var p = new Vector3Int(c.x + dx, c.y + dy, 0);
                    if (_feitos.Add(p)) pontos.Add(_mapa.dados.GetCellCenterWorld(p));
                }
            if (pontos.Count > 0)
            {
                foreach (var t in todos)
                {
                    var cells = new List<Vector3Int>();
                    foreach (var w2 in pontos)
                    {
                        var cc = t.dados.WorldToCell(w2);
                        if (t == _mapa || t.TerrenoEm(cc) >= 0) cells.Add(cc);
                    }
                    if (cells.Count == 0) continue;
                    // Pintando: só o terreno escolhido recebe; nos outros o quadrado é limpo.
                    t.Pintar(cells, !apagar && t == _mapa ? _terreno : -1);
                    EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
                }
            }
            e.Use();
        }
        if (e.type == EventType.MouseUp && (e.button == 0 || e.button == 1))
        {
            _feitos.Clear();
            if (GUIUtility.hotControl == id) GUIUtility.hotControl = 0;
            e.Use();
        }
        view.Repaint();
    }

    private void Preencher()
    {
        var bounds = Object.FindObjectsByType<MapBounds>()
            .FirstOrDefault(b => b.playable.Contains((Vector2)_mapa.transform.position) || b.transform.IsChildOf(_mapa.transform.parent ?? _mapa.transform));
        Rect area = bounds != null ? bounds.playable : new Rect(Centro().x - 20, Centro().y - 15, 40, 30);
        var objs = new List<Object> { _mapa.dados };
        objs.AddRange(_mapa.camadas.Where(l => l != null));
        Undo.RegisterCompleteObjectUndo(objs.ToArray(), "Preencher terreno");
        var a = _mapa.dados.WorldToCell(area.min);
        var b2 = _mapa.dados.WorldToCell(area.max);
        var cells = new List<Vector3Int>();
        for (int y = a.y - 1; y <= b2.y + 1; y++)
            for (int x = a.x - 1; x <= b2.x + 1; x++)
                cells.Add(new Vector3Int(x, y, 0));
        _mapa.Pintar(cells, _terreno);
        EditorSceneManager.MarkSceneDirty(_mapa.gameObject.scene);
    }

    // ---------- criar o terreno e os tiles ----------

    /// Cria o objeto Terreno (grade, dados invisíveis e uma camada por terreno).
    public static TerrenoMapa Criar(Transform parent, Vector3 pos)
    {
        var ids = LerIds();
        var root = new GameObject("Terreno", typeof(Grid));
        Undo.RegisterCreatedObjectUndo(root, "Criar terreno");
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(Mathf.Round(pos.x), Mathf.Round(pos.y), 0f);
        var mapa = root.AddComponent<TerrenoMapa>();
        mapa.ids = ids;

        var dados = new GameObject("Dados", typeof(Tilemap), typeof(TilemapRenderer));
        dados.transform.SetParent(root.transform, false);
        dados.GetComponent<TilemapRenderer>().enabled = false;   // só guarda o terreno de cada quadrado
        mapa.dados = dados.GetComponent<Tilemap>();

        mapa.camadas = new Tilemap[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            var go = new GameObject("Camada " + ids[i], typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(-0.5f, -0.5f, 0f);   // grade dupla
            go.GetComponent<TilemapRenderer>().sortingOrder = OrdemBase + i;
            mapa.camadas[i] = go.GetComponent<Tilemap>();
            if (ids[i] == "agua")
            {
                // Água: colisão no formato do desenho; o personagem não entra.
                var body = go.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Static;
                go.AddComponent<CompositeCollider2D>();
                go.AddComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
            }
        }
        AtualizarPecas(mapa);
        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        return mapa;
    }

    /// Tiles do Unity a partir das peças (cria o que faltar) e liga no terreno.
    public static void AtualizarPecas(TerrenoMapa mapa)
    {
        var ids = LerIds();
        mapa.ids = ids;
        mapa.marcadores = new TileBase[ids.Length];
        mapa.pecas = new TileBase[ids.Length * 16];
        for (int i = 0; i < ids.Length; i++)
        {
            string dir = $"{Tiles}/{ids[i]}";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }
            for (int k = 1; k < 16; k++)
                mapa.pecas[i * 16 + k] = TileDe($"{Pecas}/{ids[i]}/{ids[i]}_{k:00}.png", $"{dir}/{ids[i]}_{k:00}.asset",
                                                 ids[i] == "agua" ? Tile.ColliderType.Sprite : Tile.ColliderType.None);
            mapa.marcadores[i] = TileDe($"{Pecas}/{ids[i]}/{ids[i]}_15.png", $"{dir}/marcador_{ids[i]}.asset", Tile.ColliderType.None);
        }
        EditorUtility.SetDirty(mapa);
        AssetDatabase.SaveAssets();
    }

    private static Tile TileDe(string spritePath, string tilePath, Tile.ColliderType col)
    {
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
        if (tile == null)
        {
            tile = CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, tilePath);
        }
        tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        tile.colliderType = col;
        EditorUtility.SetDirty(tile);
        return tile;
    }
}
