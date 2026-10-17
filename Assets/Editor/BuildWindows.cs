using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// Gera o jogo para Windows em Builds/Windows (RaelMMO.exe) e o zip pronto para jogar em
/// Builds/RaelMMO-<versão>.zip: descompactar e abrir o RaelMMO.exe, sem instalar nada.
/// (O instalador fica para uma versão mais completa: o roteiro dele está em Installer/RaelMMO.iss.)
/// Menu RaelMMO, ou por linha de comando:
/// Unity -batchmode -quit -projectPath <proj> -executeMethod BuildWindows.Run
public static class BuildWindows
{
    private const string Scene = "Assets/Scenes/Campo.unity";
    private const string OutDir = "Builds/Windows";
    private const string Exe = "RaelMMO.exe";
    private const string Version = "0.1.0";

    [MenuItem("RaelMMO/Gerar jogo para Windows (zip)")]
    public static void Menu() => Run();

    public static void Run()
    {
        if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
        var options = new BuildPlayerOptions
        {
            scenes = new[] { Scene },
            locationPathName = Path.Combine(OutDir, Exe),
            target = BuildTarget.StandaloneWindows64,
            // Build de desenvolvimento: mantém a janela DEV (Debug.isDebugBuild) para os testes.
            options = BuildOptions.Development,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"[Build] Falhou: {report.summary.result} ({report.summary.totalErrors} erros)");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        // Pastas de depuração: não vão para o jogador.
        foreach (var dir in Directory.GetDirectories(OutDir, "*DoNotShip"))
            Directory.Delete(dir, true);

        string zip = Path.Combine("Builds", $"RaelMMO-{Version}.zip");
        Zip(OutDir, zip, "RaelMMO");
        Debug.Log($"[Build] Pronto: {Path.GetFullPath(zip)} ({new FileInfo(zip).Length / (1024 * 1024)} MB)");
        if (!Application.isBatchMode) EditorUtility.RevealInFinder(Path.GetFullPath(zip));
    }

    /// Compacta a pasta do jogo dentro de uma pasta-raiz (quem descompacta recebe "RaelMMO/").
    private static void Zip(string dir, string zip, string root)
    {
        if (File.Exists(zip)) File.Delete(zip);
        string full = Path.GetFullPath(dir);
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
            foreach (var f in Directory.GetFiles(full, "*", SearchOption.AllDirectories))
                z.CreateEntryFromFile(f, root + "/" + f.Substring(full.Length + 1).Replace(Path.DirectorySeparatorChar, '/'),
                                      System.IO.Compression.CompressionLevel.Optimal);
    }
}
