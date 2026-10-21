using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// RaelMMO > Personagem > Âncoras do modular: ajusta onde a espada e o escudo encaixam em
/// cada quadro do corpo da skin modular, vendo o personagem montado.
/// - Clique ESQUERDO no desenho: mão da espada naquele quadro.
/// - Clique DIREITO: mão do escudo.
/// - Setas do teclado: move a mão da espada 1 pixel (com Shift, a do escudo).
/// - "Espada na frente"/"Escudo na frente": ordem das camadas nessa animação/direção.
/// Salvar grava em Resources/Characters/Skins/modular/ancoras.json (o jogo relê sozinho).
public class ModularAnchorsWindow : EditorWindow
{
    private const string Json = "Assets/Resources/Characters/Skins/modular/ancoras.json";
    private const string Dir = "Assets/Resources/Characters/Skins/modular/";
    private ModularLayers.Data _data;
    private int _anim, _frame;
    private float _zoom = 6f;
    private bool _showWeapon = true, _showShield = true, _dirty;

    [MenuItem("RaelMMO/Personagem/Âncoras do modular")]
    private static void Open() => GetWindow<ModularAnchorsWindow>("Âncoras do modular");

    private void OnEnable() => Load();

    private void Load()
    {
        _data = File.Exists(Json) ? JsonUtility.FromJson<ModularLayers.Data>(File.ReadAllText(Json)) : null;
        _dirty = false;
    }

    private void Save()
    {
        File.WriteAllText(Json, JsonUtility.ToJson(_data, true));
        AssetDatabase.ImportAsset(Json);
        ModularLayers.ReloadAll();
        _dirty = false;
    }

    private static Texture2D Tex(string path)
    {
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (t != null) t.filterMode = FilterMode.Point;
        return t;
    }

    private void OnGUI()
    {
        if (_data?.anims == null || _data.anims.Length == 0)
        {
            EditorGUILayout.HelpBox($"Não achei {Json}. Rode: python Tools/extract_modular.py", MessageType.Warning);
            if (GUILayout.Button("Recarregar")) Load();
            return;
        }
        var names = _data.anims.Select(a => a.chave).ToArray();
        EditorGUILayout.BeginHorizontal();
        int anim = EditorGUILayout.Popup("Animação", _anim, names);
        if (anim != _anim) { _anim = anim; _frame = 0; }
        _zoom = EditorGUILayout.Slider(_zoom, 2f, 12f, GUILayout.Width(160));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        float ws = EditorGUILayout.Slider("Tamanho da espada", _data.escala_espada, 0.3f, 1.5f);
        float ss = EditorGUILayout.Slider("Tamanho do escudo", _data.escala_escudo, 0.3f, 1.5f);
        if (!Mathf.Approximately(ws, _data.escala_espada) || !Mathf.Approximately(ss, _data.escala_escudo))
        { _data.escala_espada = ws; _data.escala_escudo = ss; _dirty = true; }
        EditorGUILayout.EndHorizontal();

        var a = _data.anims[_anim];
        int count = a.corpo.Length;
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("◀", GUILayout.Width(30))) _frame = (_frame + count - 1) % count;
        _frame = EditorGUILayout.IntSlider("Quadro", _frame, 0, count - 1);
        if (GUILayout.Button("▶", GUILayout.Width(30))) _frame = (_frame + 1) % count;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        _showWeapon = EditorGUILayout.ToggleLeft("Mostrar espada", _showWeapon, GUILayout.Width(120));
        _showShield = EditorGUILayout.ToggleLeft("Mostrar escudo", _showShield, GUILayout.Width(120));
        bool wf = EditorGUILayout.ToggleLeft("Espada na frente", a.arma_na_frente, GUILayout.Width(130));
        bool sf = EditorGUILayout.ToggleLeft("Escudo na frente", a.escudo_na_frente, GUILayout.Width(130));
        if (wf != a.arma_na_frente || sf != a.escudo_na_frente) { a.arma_na_frente = wf; a.escudo_na_frente = sf; _dirty = true; }
        EditorGUILayout.EndHorizontal();

        var body = a.corpo[_frame];
        EditorGUILayout.LabelField($"Mão da espada: {body.mao_arma[0]}, {body.mao_arma[1]}    Mão do escudo: {body.mao_escudo[0]}, {body.mao_escudo[1]}");
        EditorGUILayout.HelpBox("Clique esquerdo: mão da espada. Clique direito: mão do escudo. " +
                                "Setas: move a espada 1 px (Shift+setas: o escudo).", MessageType.None);

        // Tela: corpo + camadas, ampliados.
        float pad = 60f;
        Rect area = GUILayoutUtility.GetRect(a.largura * _zoom + pad * 2, a.altura * _zoom + pad * 2);
        EditorGUI.DrawRect(area, new Color(0.24f, 0.35f, 0.24f));
        var origin = new Vector2(area.x + pad, area.y + pad);
        var bodyTex = Tex(Dir + "corpo/" + a.chave + ".png");
        var swordTex = Tex(Dir + "espada/" + a.chave + ".png");
        var shieldTex = Tex(Dir + "escudo/" + a.chave + ".png");

        if (_showShield && !a.escudo_na_frente) DrawLayer(shieldTex, a.escudo, _frame, body.mao_escudo, origin, _data.escala_escudo);
        if (_showWeapon && !a.arma_na_frente) DrawLayer(swordTex, a.espada, _frame, body.mao_arma, origin, _data.escala_espada);
        if (bodyTex != null)
        {
            float u = (float)a.largura / bodyTex.width;
            GUI.DrawTextureWithTexCoords(new Rect(origin.x, origin.y, a.largura * _zoom, a.altura * _zoom), bodyTex,
                                         new Rect(_frame * u, 0f, u, 1f));
        }
        if (_showWeapon && a.arma_na_frente) DrawLayer(swordTex, a.espada, _frame, body.mao_arma, origin, _data.escala_espada);
        if (_showShield && a.escudo_na_frente) DrawLayer(shieldTex, a.escudo, _frame, body.mao_escudo, origin, _data.escala_escudo);

        Mark(origin, body.mao_arma, new Color(1f, 0.3f, 0.2f));
        Mark(origin, body.mao_escudo, new Color(0.3f, 0.6f, 1f));

        var e = Event.current;
        if (e.type == EventType.MouseDown && area.Contains(e.mousePosition))
        {
            int px = Mathf.FloorToInt((e.mousePosition.x - origin.x) / _zoom);
            int py = Mathf.FloorToInt((e.mousePosition.y - origin.y) / _zoom);
            if (e.button == 0) body.mao_arma = new[] { px, py };
            else if (e.button == 1) body.mao_escudo = new[] { px, py };
            _dirty = true;
            e.Use();
        }
        if (e.type == EventType.KeyDown)
        {
            int dx = e.keyCode == KeyCode.LeftArrow ? -1 : e.keyCode == KeyCode.RightArrow ? 1 : 0;
            int dy = e.keyCode == KeyCode.UpArrow ? -1 : e.keyCode == KeyCode.DownArrow ? 1 : 0;
            if (dx != 0 || dy != 0)
            {
                var p = e.shift ? body.mao_escudo : body.mao_arma;
                p[0] += dx; p[1] += dy;
                _dirty = true;
                e.Use();
            }
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Copiar as mãos deste quadro para todos desta animação"))
        {
            foreach (var f in a.corpo)
            {
                f.mao_arma = (int[])body.mao_arma.Clone();
                f.mao_escudo = (int[])body.mao_escudo.Clone();
            }
            _dirty = true;
        }
        GUI.enabled = _dirty;
        if (GUILayout.Button(_dirty ? "Salvar *" : "Salvo", GUILayout.Width(100))) Save();
        GUI.enabled = true;
        if (GUILayout.Button("Recarregar", GUILayout.Width(100))) Load();
        EditorGUILayout.EndHorizontal();
        if (GUI.changed) Repaint();
    }

    /// Quadro da camada com a empunhadura em cima da mão.
    private void DrawLayer(Texture2D tex, ModularLayers.Layer layer, int frame, int[] hand, Vector2 origin, float scale)
    {
        if (scale <= 0f) scale = 1f;
        if (tex == null || layer?.quadros == null || layer.quadros.Length == 0) return;
        int k = Mathf.Clamp(frame, 0, layer.quadros.Length - 1);
        int[] g = layer.quadros[k].empunhadura;
        float u = (float)layer.largura / tex.width;
        float z = _zoom * scale;
        var r = new Rect(origin.x + hand[0] * _zoom - g[0] * z, origin.y + hand[1] * _zoom - g[1] * z,
                         layer.largura * z, layer.altura * z);
        GUI.DrawTextureWithTexCoords(r, tex, new Rect(k * u, 0f, u, 1f));
    }

    private void Mark(Vector2 origin, int[] p, Color c)
    {
        var r = new Rect(origin.x + p[0] * _zoom, origin.y + p[1] * _zoom, _zoom, _zoom);
        EditorGUI.DrawRect(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), Color.black);
        EditorGUI.DrawRect(r, c);
    }
}
