using UnityEditor;
using UnityEngine;

/// RaelMMO > Chão > Alinhar selecionados à grade (Ctrl+Shift+G): todo chão selecionado
/// (e o que estiver dentro dele) passa a ter 1 ladrilho = 1 quadrado, com o canto num canto
/// de quadrado e tamanho inteiro, e ganha o encaixe automático (ChaoGrade) para continuar
/// alinhado quando for movido.
public static class AlinharChao
{
    // Também no botão direito da Hierarchy (menu GameObject), em cima da seleção.
    [MenuItem("GameObject/Alinhar pisos à grade", false, 0)]
    private static void AlignFromHierarchy(MenuCommand cmd)
    {
        // O Unity chama uma vez por objeto selecionado: alinha tudo só na primeira.
        if (cmd.context != null && cmd.context != Selection.activeObject && Selection.objects.Length > 1) return;
        Align();
    }

    [MenuItem("RaelMMO/Chão/Alinhar selecionados à grade %#g")]
    private static void Align()
    {
        int count = 0;
        foreach (var go in Selection.gameObjects)
        {
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.sprite == null) continue;
                Undo.RecordObject(sr.transform, "Alinhar chão à grade");
                Undo.RecordObject(sr, "Alinhar chão à grade");
                var snap = sr.GetComponent<ChaoGrade>();
                if (snap == null) snap = Undo.AddComponent<ChaoGrade>(sr.gameObject);
                snap.Snap();
                EditorUtility.SetDirty(sr);
                count++;
            }
        }
        Debug.Log(count > 0 ? $"[Chão] {count} peça(s) alinhada(s) à grade."
                            : "[Chão] Selecione os pisos na Hierarchy ou na cena antes.");
    }

    [MenuItem("RaelMMO/Chão/Alinhar selecionados à grade %#g", true)]
    private static bool CanAlign() => Selection.gameObjects.Length > 0;
}
