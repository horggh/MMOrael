using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Botões no Inspector da sala ("Interior - ..."): guardar a decoração dela como a decoração
/// básica e carregar a básica numa sala (a básica fica num prefab em Assets/Art/Prefabs, com
/// as posições relativas à sala, e serve para qualquer sala do mesmo tamanho). Fica em
/// Resources/DecoracaoBasica: no Play, sala de porta/escada sem decoração usa a básica, e
/// sala nova criada no editor já nasce com ela (PredioInterior).
[CustomEditor(typeof(InteriorMolde))]
public class InteriorMoldeEditor : Editor
{
    private const string Pasta = "Assets/Resources/DecoracaoBasica";

    public static string Caminho(bool corredor) =>
        $"{Pasta}/DecoracaoBasica_{(corredor ? "Corredor" : "Sala")}.prefab";

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        var m = (InteriorMolde)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Decoração básica " + (m.corredor ? "(corredor)" : "(sala)"), EditorStyles.boldLabel);
        bool existe = AssetDatabase.LoadAssetAtPath<GameObject>(Caminho(m.corredor)) != null;
        if (GUILayout.Button("Salvar a decoração desta sala como básica")) Salvar(m);
        GUI.enabled = existe;
        if (GUILayout.Button("Carregar a decoração básica nesta sala")) Carregar(m);
        GUI.enabled = true;
        if (GUILayout.Button("Limpar a decoração desta sala")) Limpar(m);
        if (!existe) EditorGUILayout.HelpBox("Ainda não há decoração básica salva para este tipo de sala.", MessageType.None);
    }

    private static void Salvar(InteriorMolde m)
    {
        var pecas = PredioInterior.Decoracao(m);
        if (pecas.Count == 0)
        {
            EditorUtility.DisplayDialog("Decoração básica", "Esta sala não tem decoração.", "OK");
            return;
        }
        if (!AssetDatabase.IsValidFolder(Pasta)) AssetDatabase.CreateFolder("Assets/Resources", "DecoracaoBasica");
        // Cópia temporária com as posições relativas ao canto de referência da sala.
        Vector3 o = HouseInterior.OriginFor(m.interiorId);
        var raiz = new GameObject("Decoração básica");
        foreach (var t in pecas)
        {
            var c = Object.Instantiate(t.gameObject, raiz.transform);
            c.name = t.name;
            c.transform.localPosition = t.position - o;
            c.transform.localScale = t.lossyScale;
            foreach (var b in c.GetComponentsInChildren<Building>()) Object.DestroyImmediate(b);
        }
        PrefabUtility.SaveAsPrefabAsset(raiz, Caminho(m.corredor));
        Object.DestroyImmediate(raiz);
        EditorUtility.DisplayDialog("Decoração básica", $"{pecas.Count} peças salvas como decoração básica " +
                                    (m.corredor ? "de corredor." : "de sala."), "OK");
    }

    /// Tira a decoração só desta sala (as outras e a básica salva não mudam; Ctrl+Z desfaz).
    public static void Limpar(InteriorMolde m)
    {
        var atuais = PredioInterior.Decoracao(m);
        if (atuais.Count == 0)
        {
            EditorUtility.DisplayDialog("Limpar decoração", "Esta sala não tem decoração.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Limpar decoração", $"Apagar as {atuais.Count} peças desta sala?",
                                         "Apagar", "Cancelar")) return;
        foreach (var t in atuais) Undo.DestroyObjectImmediate(t.gameObject);
        EditorSceneManager.MarkSceneDirty(m.gameObject.scene);
    }

    public static void Carregar(InteriorMolde m, bool perguntar = true)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Caminho(m.corredor));
        if (prefab == null) return;
        var atuais = PredioInterior.Decoracao(m);
        if (atuais.Count > 0 && !perguntar) return;
        if (atuais.Count > 0)
        {
            int r = EditorUtility.DisplayDialogComplex("Decoração básica",
                $"Esta sala já tem {atuais.Count} peças.", "Substituir", "Cancelar", "Somar às atuais");
            if (r == 1) return;
            if (r == 0) foreach (var t in atuais) Undo.DestroyObjectImmediate(t.gameObject);
        }
        Vector3 o = HouseInterior.OriginFor(m.interiorId);
        foreach (Transform t in prefab.transform)
        {
            var c = Object.Instantiate(t.gameObject, o + t.localPosition, t.rotation, m.transform);
            c.name = t.name;
            c.transform.localScale = t.localScale;
            Undo.RegisterCreatedObjectUndo(c, "Carregar decoração básica");
        }
        EditorSceneManager.MarkSceneDirty(m.gameObject.scene);
    }
}
