using UnityEditor;
using UnityEditor.SceneManagement;

/// Abre a cena de teste e entra em Play. Usado ao abrir o editor por linha de comando:
/// Unity -projectPath <proj> -executeMethod AutoPlay.Run
public static class AutoPlay
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Campo.unity");
        EditorApplication.delayCall += EditorApplication.EnterPlaymode;
    }
}
