using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// Janela RaelMMO > Chão > Pintar chão: um botão por textura (Assets/Art/Sprites/Environment/
/// Texturas). Clicar põe na cena uma área dessa textura no centro da vista; para mudar o
/// tamanho, mude o Size no Inspector (ou use a ferramenta Rect, tecla T). Cada área é um
/// objeto só, do tamanho que quiser, logo acima do fundo do mapa. Cada ladrilho ocupa
/// exatamente 1 quadrado da grade, e a área se encaixa sozinha na grade (ChaoGrade).
public class PintarChaoWindow : EditorWindow
{
    private const string Dir = "Assets/Art/Sprites/Environment/Texturas";
    private Sprite[] _sprites;
    private Vector2 _scroll;
    private Vector2 _size = new Vector2(6f, 6f);

    [MenuItem("RaelMMO/Chão/Pintar chão")]
    private static void Open() => GetWindow<PintarChaoWindow>("Pintar chão");

    private void OnEnable() => Load();

    private void Load()
    {
        _sprites = Directory.Exists(Dir)
            ? AssetDatabase.FindAssets("t:Sprite", new[] { Dir })
                .Select(g => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null).OrderBy(s => s.name).ToArray()
            : new Sprite[0];
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Clique numa textura para pôr uma área dela no centro da vista da cena. " +
                                "Depois ajuste o Size no Inspector.", MessageType.Info);
        _size = EditorGUILayout.Vector2Field("Tamanho inicial (unidades)", _size);
        if (GUILayout.Button("Recarregar texturas")) Load();
        if (_sprites == null || _sprites.Length == 0)
        {
            EditorGUILayout.LabelField("Nenhuma textura em " + Dir + " (rode Tools/extract_pisos_natureza.py).");
            return;
        }
        const float cell = 84f;
        int perRow = Mathf.Max(1, (int)((position.width - 10) / cell));
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < _sprites.Length; i += perRow)
        {
            EditorGUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + perRow, _sprites.Length); j++)
            {
                var s = _sprites[j];
                EditorGUILayout.BeginVertical(GUILayout.Width(cell - 4));
                var tex = AssetPreview.GetAssetPreview(s) ?? s.texture;
                if (GUILayout.Button(tex, GUILayout.Width(cell - 8), GUILayout.Height(cell - 8))) Place(s);
                GUILayout.Label(s.name.Replace('_', ' '), EditorStyles.miniLabel, GUILayout.Width(cell - 8));
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private void Place(Sprite s)
    {
        var parent = GameObject.Find("Chão pintado") ?? new GameObject("Chão pintado");
        var go = new GameObject(s.name);
        go.transform.SetParent(parent.transform);
        var view = SceneView.lastActiveSceneView;
        Vector3 at = view != null ? view.pivot : Vector3.zero;
        // Canto de baixo à esquerda num canto de quadrado da grade (o pivô da textura é ali).
        go.transform.position = new Vector3(Mathf.Round(at.x - _size.x / 2f), Mathf.Round(at.y - _size.y / 2f), 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = new Vector2(Mathf.Max(1f, Mathf.Round(_size.x)), Mathf.Max(1f, Mathf.Round(_size.y)));
        // Acima da grama (-10000) e do fundo do mapa (-9999), abaixo de tudo o mais.
        sr.sortingOrder = -9998;
        go.AddComponent<ChaoGrade>().Snap();
        Undo.RegisterCreatedObjectUndo(go, "Pintar chão");
        Selection.activeGameObject = go;
    }
}
