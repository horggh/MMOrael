using UnityEditor;

/// Atalhos de teste para o jogo salvo.
public static class SaveMenu
{
    [MenuItem("MMO_RAEL/Apagar jogo salvo")]
    private static void Delete()
    {
        if (!EditorUtility.DisplayDialog("Apagar jogo salvo",
                "O próximo Play começa do zero, com o kit inicial. Continuar?", "Apagar", "Cancelar"))
            return;
        SaveGame.Delete();
    }

    // Durante o Play não: ao parar, o jogo gravaria de novo por cima.
    [MenuItem("MMO_RAEL/Apagar jogo salvo", true)]
    private static bool CanDelete() => !EditorApplication.isPlaying;

    // Com save existente o kit inicial não roda: estes atalhos entregam itens novos para testar.
    [MenuItem("MMO_RAEL/Dar item/Planta de projeto - Casa")]
    private static void GiveBlueprint() => Give("planta_casa");

    [MenuItem("MMO_RAEL/Dar item/Baú")]
    private static void GiveChest() => Give("bau");

    [MenuItem("MMO_RAEL/Dar item/Planta de projeto - Casa", true)]
    [MenuItem("MMO_RAEL/Dar item/Baú", true)]
    private static bool CanGive() => EditorApplication.isPlaying;

    private static void Give(string id)
    {
        var pc = UnityEngine.Object.FindAnyObjectByType<PlayerController2D>();
        var bag = pc != null ? pc.GetComponent<Inventory>() : null;
        if (bag != null) bag.Add(id, 1);
    }

    [MenuItem("MMO_RAEL/Abrir pasta do jogo salvo")]
    private static void Reveal() => EditorUtility.RevealInFinder(SaveGame.FilePath);
}
