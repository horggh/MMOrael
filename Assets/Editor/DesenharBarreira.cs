using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Desenhar barreiras com o mouse na Scene: ligue em RaelMMO > Mapa > Desenhar barreira
/// (atalho Ctrl+Shift+B), segure o botão esquerdo e arraste por cima do mapa (borda da
/// caverna, beira do rio, muro). Ao soltar, a linha vira uma barreira (BarreiraDesenhada, com
/// EdgeCollider2D) no grupo "Barreiras". Shift ao soltar fecha a linha (volta ao começo).
/// Esc ou o mesmo menu desliga. Ctrl+Z desfaz a última linha.
[InitializeOnLoad]
public static class DesenharBarreira
{
    private static bool _on;
    private static readonly List<Vector2> _pts = new List<Vector2>();
    private const float Step = 0.25f;       // distância mínima entre pontos ao arrastar
    private const float Simplify = 0.08f;   // tira pontos que quase não mudam a linha

    static DesenharBarreira() => SceneView.duringSceneGui += OnScene;

    [MenuItem("RaelMMO/Mapa/Desenhar barreira %#b")]
    private static void Toggle()
    {
        _on = !_on;
        _pts.Clear();
        SceneView.RepaintAll();
        if (_on) SceneView.lastActiveSceneView?.ShowNotification(new GUIContent("Desenhando barreira: arraste com o botão esquerdo (Esc sai)"));
    }

    [MenuItem("RaelMMO/Mapa/Desenhar barreira %#b", true)]
    private static bool ToggleCheck()
    {
        Menu.SetChecked("RaelMMO/Mapa/Desenhar barreira", _on);
        return !EditorApplication.isPlaying;
    }

    private static Vector2 Mouse(Event e) => HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;

    private static void OnScene(SceneView view)
    {
        if (!_on) return;
        var e = Event.current;
        // O clique é do desenho (não seleciona objetos).
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { _on = false; _pts.Clear(); e.Use(); view.Repaint(); return; }

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            _pts.Clear();
            _pts.Add(Mouse(e));
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && e.button == 0 && _pts.Count > 0)
        {
            Vector2 p = Mouse(e);
            if (Vector2.Distance(p, _pts[_pts.Count - 1]) >= Step) _pts.Add(p);
            e.Use();
        }
        else if (e.type == EventType.MouseUp && e.button == 0 && _pts.Count > 0)
        {
            _pts.Add(Mouse(e));
            if (e.shift && _pts.Count > 2) _pts.Add(_pts[0]);
            Create(SimplifyLine(_pts));
            _pts.Clear();
            e.Use();
        }

        // Linha sendo desenhada e o aviso.
        if (e.type == EventType.Repaint)
        {
            Handles.color = new Color(1f, 0.3f, 0.2f);
            for (int i = 1; i < _pts.Count; i++) Handles.DrawLine(_pts[i - 1], _pts[i], 3f);
            Handles.BeginGUI();
            GUI.Label(new Rect(10, 10, 420, 20), "Desenhando barreira — arraste com o botão esquerdo; Shift ao soltar fecha; Esc sai",
                      EditorStyles.whiteLargeLabel);
            Handles.EndGUI();
        }
        view.Repaint();
    }

    private static void Create(List<Vector2> pts)
    {
        if (pts.Count < 2) return;
        var group = GameObject.Find("Barreiras");
        if (group == null)
        {
            group = new GameObject("Barreiras");
            Undo.RegisterCreatedObjectUndo(group, "Barreira");
        }
        var go = new GameObject("Barreira");
        Undo.RegisterCreatedObjectUndo(go, "Desenhar barreira");
        go.transform.SetParent(group.transform, true);
        go.transform.position = Vector3.zero;
        var edge = go.AddComponent<EdgeCollider2D>();
        edge.points = pts.ToArray();
        edge.edgeRadius = 0.08f;   // um pouco de espessura: o personagem não passa raspando
        go.AddComponent<BarreiraDesenhada>();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
    }

    /// Ramer-Douglas-Peucker: menos pontos com o mesmo traço.
    private static List<Vector2> SimplifyLine(List<Vector2> pts)
    {
        if (pts.Count < 3) return new List<Vector2>(pts);
        var keep = new bool[pts.Count];
        keep[0] = keep[pts.Count - 1] = true;
        var stack = new Stack<(int, int)>();
        stack.Push((0, pts.Count - 1));
        while (stack.Count > 0)
        {
            var (a, b) = stack.Pop();
            float best = 0f; int idx = -1;
            for (int i = a + 1; i < b; i++)
            {
                float d = HandleUtility.DistancePointLine(pts[i], pts[a], pts[b]);
                if (d > best) { best = d; idx = i; }
            }
            if (idx >= 0 && best > Simplify)
            {
                keep[idx] = true;
                stack.Push((a, idx));
                stack.Push((idx, b));
            }
        }
        var outPts = new List<Vector2>();
        for (int i = 0; i < pts.Count; i++) if (keep[i]) outPts.Add(pts[i]);
        return outPts;
    }
}
