using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

/// Recompilar scripts com o Play rodando apaga o que só existe em memória (listas, janelas)
/// e já fez o jogo salvo perder pisos. Ao começar uma compilação durante o Play, para o
/// Play antes: o jogo grava tudo ao sair (OnApplicationQuit) e a recompilação vem depois.
[InitializeOnLoad]
public static class StopPlayOnCompile
{
    static StopPlayOnCompile()
    {
        CompilationPipeline.compilationStarted += _ =>
        {
            if (!EditorApplication.isPlaying) return;
            Debug.LogWarning("Scripts mudaram: parando o Play para recompilar sem perder o jogo salvo.");
            EditorApplication.isPlaying = false;
        };
    }
}
