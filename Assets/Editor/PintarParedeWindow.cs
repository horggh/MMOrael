using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// Janela RaelMMO > Chão > Pintar parede: um botão por papel de parede (Assets/Art/Sprites/
/// Environment/PapelDeParede, os 60). Como o Pintar chão: clicar põe na cena uma área desse
/// papel no centro da vista, encaixada na grade (cada papel = 1 quadrado), na camada da parede
/// (atrás dos móveis, quadros e janelas). Para mudar o tamanho, mude o Size no Inspector (ou a
/// ferramenta Rect, tecla T), em números inteiros.
/// Com uma sala de prédio selecionada (RaelMMO > Prédios > Editar interior), a área já entra
/// na parede do fundo dela, do tamanho da parede.
public class PintarParedeWindow : EditorWindow
{
    private const string Dir = "Assets/Art/Sprites/Environment/PapelDeParede";
    private Sprite[] _sprites;
    private Vector2 _scroll;
    private Vector2 _size = new Vector2(6f, 4f);
    private string _filter = "";

    [MenuItem("RaelMMO/Chão/Pintar parede")]
    private static void Open() => GetWindow<PintarParedeWindow>("Pintar parede");

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
        EditorGUILayout.HelpBox("Clique num papel de parede para pôr uma área dele no centro da vista da cena " +
                                "(com a sala de um prédio selecionada, ele cobre a parede do fundo dela). " +
                                "Depois ajuste o Size no Inspector.", MessageType.Info);
        _size = EditorGUILayout.Vector2Field("Tamanho inicial (quadrados)", _size);
        _filter = EditorGUILayout.TextField("Procurar", _filter);
        if (GUILayout.Button("Recarregar papéis")) Load();
        if (_sprites == null || _sprites.Length == 0)
        {
            EditorGUILayout.LabelField("Nenhum papel de parede em " + Dir + ".");
            return;
        }
        var list = string.IsNullOrEmpty(_filter) ? _sprites
                 : _sprites.Where(s => s.name.Replace('_', ' ').Contains(_filter.ToLowerInvariant())).ToArray();
        const float cell = 84f;
        int perRow = Mathf.Max(1, (int)((position.width - 10) / cell));
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < list.Length; i += perRow)
        {
            EditorGUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + perRow, list.Length); j++)
            {
                var s = list[j];
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
        // Sala de prédio selecionada: a parede do fundo inteira.
        var sel = Selection.activeGameObject;
        var molde = sel != null ? sel.GetComponentInParent<InteriorMolde>() : null;
        Vector2 min, size;
        Transform parent;
        if (molde != null)
        {
            var room = HouseInterior.SizeFor(molde.corredor);
            Vector2 o = HouseInterior.OriginFor(molde.interiorId);
            min = new Vector2(Mathf.Round(o.x - room.x / 2f), Mathf.Round(o.y + room.y - room.z));
            size = new Vector2(room.x, room.z);
            parent = molde.transform;
        }
        else
        {
            var view = SceneView.lastActiveSceneView;
            Vector3 at = view != null ? view.pivot : Vector3.zero;
            size = new Vector2(Mathf.Max(1f, Mathf.Round(_size.x)), Mathf.Max(1f, Mathf.Round(_size.y)));
            min = new Vector2(Mathf.Round(at.x - size.x / 2f), Mathf.Round(at.y - size.y / 2f));
            parent = (GameObject.Find("Parede pintada") ?? new GameObject("Parede pintada")).transform;
        }

        var go = new GameObject("Parede " + s.name);
        go.transform.SetParent(parent);
        // O pivô do papel é o meio: a área fica centrada no retângulo de quadrados inteiros.
        go.transform.position = new Vector3(min.x + size.x / 2f, min.y + size.y / 2f, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = size;
        sr.sortingOrder = FloorTiles.WallOrder;   // camada da parede (atrás de móveis e janelas)
        Undo.RegisterCreatedObjectUndo(go, "Pintar parede");
        Selection.activeGameObject = go;
    }
}
