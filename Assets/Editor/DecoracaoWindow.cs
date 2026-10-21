using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// Janela RaelMMO > Mapa > Decoração, com duas abas:
/// - Decoração: peças de Assets/Art/Sprites/Environment/Decoracao (barris, caixotes, móveis);
///   colisor baixo na base (o personagem esbarra, mas passa atrás da parte de cima).
/// - Prédios: Assets/Art/Sprites/Environment/Predios; colisor no corpo do prédio e a linha
///   da profundidade na base das paredes (ajustável: prédio com calçada desenhada na frente
///   precisa de uma linha mais alta).
/// - NPCs: Assets/Art/Sprites/NPCs; personagens parados (por enquanto só aparência), com
///   colisor pequeno nos pés.
/// - Masmorra: paredes, cantos, estátua, grade, tochas... na escala dos chãos (1 quadrado =
///   64 px), encaixando na grade: posição de meio em meio quadrado e colisor na base.
/// Clicar põe a peça no centro da vista da cena; depois é arrastar com W.
public class DecoracaoWindow : EditorWindow
{
    private static readonly string[] Dirs = { "Assets/Art/Sprites/Environment/Decoracao", "Assets/Art/Sprites/Environment/Predios", "Assets/Art/Sprites/NPCs", "Assets/Art/Sprites/Environment/Masmorra", "Assets/Resources/Walls", "Assets/Resources/Windows" };
    private static readonly string[] Tabs = { "Decoração", "Prédios", "NPCs", "Masmorra", "Paredes", "Janelas" };
    // Abas de papel de parede e janela: texturas do jogo, encaixadas na grade da parede.
    private bool WallTab => _tab >= 4;
    private Texture2D[] _textures;
    private int _tab;
    private float _sortOffset = 0.3f;
    private string Dir => Dirs[_tab];
    private Sprite[] _sprites;
    private Vector2 _scroll;
    private bool _collider = true;

    [MenuItem("RaelMMO/Mapa/Decoração")]
    private static void Open() => GetWindow<DecoracaoWindow>("Decoração");

    private void OnEnable() => Load();

    private void Load()
    {
        if (WallTab)
        {
            _textures = AssetDatabase.FindAssets("t:Texture2D", new[] { Dir })
                .Select(g => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(t => t != null && !t.name.EndsWith("_ceu") && !t.name.StartsWith("luz_"))
                .OrderBy(t => t.name).ToArray();
            return;
        }
        _sprites = Directory.Exists(Dir)
            ? AssetDatabase.FindAssets("t:Sprite", new[] { Dir })
                .Select(g => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null).OrderBy(s => s.name).ToArray()
            : new Sprite[0];
    }

    private void OnGUI()
    {
        int tab = GUILayout.Toolbar(_tab, Tabs);
        if (tab != _tab) { _tab = tab; Load(); }
        EditorGUILayout.HelpBox("Clique numa peça para pô-la no centro da vista da cena. Depois arraste com W.", MessageType.Info);
        _collider = EditorGUILayout.Toggle(_tab == 0 ? "Com colisão na base" : "Com colisão no prédio", _collider);
        if (_tab == 1)
            _sortOffset = EditorGUILayout.FloatField(new GUIContent("Linha da profundidade",
                "Altura (unidades) da base das paredes acima do pé do desenho. Prédio de frente: ~0,3. " +
                "Com calçada desenhada na frente (prédio comercial): ~1,4."), _sortOffset);
        if (GUILayout.Button("Recarregar")) Load();
        if (WallTab)
        {
            DrawTextures();
            return;
        }
        if (_sprites == null || _sprites.Length == 0)
        {
            EditorGUILayout.LabelField("Nenhuma peça em " + Dir + " (rode Tools/extract_decoracao.py).");
            return;
        }
        const float cell = 76f;
        int perRow = Mathf.Max(1, (int)((position.width - 10) / cell));
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < _sprites.Length; i += perRow)
        {
            EditorGUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + perRow, _sprites.Length); j++)
            {
                var s = _sprites[j];
                EditorGUILayout.BeginVertical(GUILayout.Width(cell - 4));
                var tex = AssetPreview.GetAssetPreview(s);
                if (tex == null) tex = s.texture;
                if (GUILayout.Button(tex, GUILayout.Width(cell - 8), GUILayout.Height(cell - 8))) Place(s);
                GUILayout.Label(s.name.Replace('_', ' '), EditorStyles.miniLabel, GUILayout.Width(cell - 8));
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawTextures()
    {
        EditorGUILayout.HelpBox("Para a parede do fundo dos interiores (menu RaelMMO > Prédios > Editar interior). " +
                                "Encaixa na grade: papel de parede = 1 quadrado; janela = o tamanho dela.", MessageType.None);
        if (_textures == null || _textures.Length == 0) { EditorGUILayout.LabelField("Nada em " + Dir); return; }
        var molde = TargetRoom();
        EditorGUILayout.LabelField(molde != null ? "Sala: " + molde.name : "Sala: nenhuma (use RaelMMO > Prédios > Editar interior)", EditorStyles.boldLabel);
        if (_tab == 4) _fillWall = EditorGUILayout.Toggle("Cobrir a parede inteira", _fillWall);
        const float cell = 76f;
        int perRow = Mathf.Max(1, (int)((position.width - 10) / cell));
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < _textures.Length; i += perRow)
        {
            EditorGUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + perRow, _textures.Length); j++)
            {
                var t = _textures[j];
                EditorGUILayout.BeginVertical(GUILayout.Width(cell - 4));
                if (GUILayout.Button(t, GUILayout.Width(cell - 8), GUILayout.Height(cell - 8))) PlaceWall(t.name);
                GUILayout.Label(t.name.Replace('_', ' '), EditorStyles.miniLabel, GUILayout.Width(cell - 8));
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private bool _fillWall = true;

    /// Sala que recebe a peça: a selecionada (ou a dona do objeto selecionado); senão a mais
    /// perto do meio da Scene.
    private static InteriorMolde TargetRoom()
    {
        var sel = Selection.activeGameObject;
        var m = sel != null ? sel.GetComponentInParent<InteriorMolde>() : null;
        if (m != null) return m;
        var view = SceneView.lastActiveSceneView;
        if (view == null) return null;
        float best = 30f;
        foreach (var r in Object.FindObjectsByType<InteriorMolde>())
        {
            float d = Vector2.Distance(view.pivot, HouseInterior.RoomRect(r.interiorId, r.corredor).center);
            if (d < best) { best = d; m = r; }
        }
        return m;
    }

    private void PlaceWall(string id)
    {
        bool janela = _tab == 5;
        var molde = TargetRoom();
        Transform parent = molde != null ? molde.transform : (GameObject.Find(Tabs[_tab]) ?? new GameObject(Tabs[_tab])).transform;
        var view = SceneView.lastActiveSceneView;
        Vector3 at = view != null ? view.pivot : Vector3.zero;
        var origin = new Vector2Int(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y));
        if (molde != null)
        {
            // Na parede do fundo da sala (a faixa azul), na coluna do meio da Scene.
            Vector2 o = HouseInterior.OriginFor(molde.interiorId);
            var room = HouseInterior.SizeFor(molde.corredor);
            int left = Mathf.RoundToInt(o.x - room.x / 2f);
            int wallY = Mathf.RoundToInt(o.y + room.y - room.z);
            int top = Mathf.RoundToInt(o.y + room.y) - 1;
            if (!janela && _fillWall)
            {
                // Troca o papel da parede toda (as janelas ficam).
                foreach (var old in molde.GetComponentsInChildren<PecaDeParede>())
                    if (!old.janela) Undo.DestroyObjectImmediate(old.gameObject);
                for (int y = wallY; y <= top; y++)
                    for (int x = left; x < left + (int)room.x; x++)
                        NewWallPiece(id, false, new Vector2(x + 0.5f, y + 0.5f), molde.transform);
                return;
            }
            var size = janela ? WallDecor.SizeOf(id) : Vector2Int.one;
            origin.x = Mathf.Clamp(origin.x, left, left + (int)room.x - size.x);
            origin.y = Mathf.Clamp(origin.y, wallY, top - size.y + 1);
        }
        Vector2 pos = janela ? WallDecor.CenterOf(origin, WallDecor.SizeOf(id)) : new Vector2(origin.x + 0.5f, origin.y + 0.5f);
        Selection.activeGameObject = NewWallPiece(id, janela, pos, parent);
    }

    private static GameObject NewWallPiece(string id, bool janela, Vector2 pos, Transform parent)
    {
        var go = new GameObject((janela ? "Janela " : "Parede ") + id);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.AddComponent<SpriteRenderer>();
        var p = go.AddComponent<PecaDeParede>();
        p.id = id;
        p.janela = janela;
        p.SendMessage("OnValidate", SendMessageOptions.DontRequireReceiver);
        Undo.RegisterCreatedObjectUndo(go, "Colocar " + (janela ? "janela" : "parede"));
        return go;
    }

    private void Place(Sprite s)
    {
        string group = Tabs[_tab];
        var parent = GameObject.Find(group) ?? new GameObject(group);
        var go = new GameObject(s.name);
        go.transform.SetParent(parent.transform);
        var view = SceneView.lastActiveSceneView;
        Vector3 at = view != null ? view.pivot : Vector3.zero;
        // Meio quadrado: dá para encostar as peças umas nas outras com precisão.
        go.transform.position = new Vector3(Mathf.Round(at.x * 2f) / 2f, Mathf.Round(at.y * 2f) / 2f, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        float offset = _tab == 1 ? _sortOffset : 0f;
        go.AddComponent<YSortRenderer>().sortOffset = offset;
        sr.sortingOrder = YSortRenderer.OrderFor(go.transform.position.y + offset);
        if (_tab == 1)
        {
            // Prédio: construção pronta com a área da casa (6x2) e o mesmo interior; clicar
            // nele abre a janela com "Entrar" (Building).
            var b = go.AddComponent<Building>();
            b.art = s;
            b.displayName = Building.CityName(s.name);
            b.type = s.name;
            int next = 1000;   // longe dos números das casas construídas pelo jogador
            foreach (var other in Object.FindObjectsByType<Building>())
                if (other != b) next = Mathf.Max(next, other.interiorId);
            b.interiorId = next + 1;
        }
        else if (_collider && _tab == 1)
        {
            // Corpo do prédio: 70% da largura, 1,4 de altura logo acima da linha da profundidade.
            Vector2 bs = s.bounds.size;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(bs.x * 0.7f, 1.4f);
            col.offset = new Vector2(0f, offset + 0.7f);
        }
        else if (_collider)
        {
            // Só a faixa de baixo (a "pegada" da peça no chão).
            Vector2 size = s.bounds.size;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(size.x * 0.8f, Mathf.Min(0.4f, size.y * 0.35f));
            col.offset = new Vector2(0f, col.size.y / 2f);
        }
        Undo.RegisterCreatedObjectUndo(go, "Colocar decoração");
        Selection.activeGameObject = go;
    }
}
