using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Compilación de entrega para navegador, en la carpeta que sirve el servidor.
/// Se ejecuta con: Unity -batchmode -quit -buildTarget WebGL -executeMethod BuildWebGL.Build
/// Sin Development ni RB_CAPTURE: DevFlags y DevSamples no llegan a compilarse.
/// </summary>
public static class BuildWebGL
{
    public const string OutputPath = "Server/RobotBombero.Server/wwwroot";
    private const string MainScene = "Assets/Scenes/Main.unity";
    private const string CaptureDefine = "RB_CAPTURE";

    /// <summary>Opciones de la compilación de entrega. Son públicas para que las pruebas las comprueben.</summary>
    public static BuildPlayerOptions Options()
    {
        return new BuildPlayerOptions
        {
            scenes = new[] { MainScene },
            locationPathName = OutputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
    }

    /// <summary>
    /// Brotli con descompresión en el navegador: la red local usa HTTP y los navegadores solo aceptan
    /// Content-Encoding: br por HTTPS. El cargador de Unity descomprime los .unityweb en JavaScript.
    /// </summary>
    public static void ApplySettings()
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.decompressionFallback = true;
    }

    /// <summary>True si los símbolos del proyecto activarían el código de capturas en esta compilación.</summary>
    public static bool HasCaptureDefine()
    {
        string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL);
        return Array.IndexOf(defines.Split(';'), CaptureDefine) >= 0;
    }

    [MenuItem("RobotBombero/Compilar para navegador")]
    public static void Build()
    {
        if (HasCaptureDefine())
        {
            Fail("El proyecto define " + CaptureDefine + " para WebGL: una compilación de entrega no puede llevarlo.");
            return;
        }

        ApplySettings();
        AssetDatabase.SaveAssets();

        // Otra compilación puede dejar archivos con otros nombres: wwwroot solo contiene lo generado aquí.
        if (Directory.Exists(OutputPath)) Directory.Delete(OutputPath, true);

        BuildSummary summary = BuildPipeline.BuildPlayer(Options()).summary;
        Debug.Log("RobotBombero build: " + summary.result + ", " + summary.totalSize / 1024 + " KB, " + summary.totalErrors + " errores, en " + Path.GetFullPath(summary.outputPath));

        if (summary.result != BuildResult.Succeeded) Fail("La compilación ha fallado: " + summary.result);
        else if (!File.Exists(Path.Combine(OutputPath, "index.html"))) Fail("La compilación no ha generado index.html en " + OutputPath);
    }

    private static void Fail(string message)
    {
        Debug.LogError("RobotBombero build: " + message);
        if (Application.isBatchMode) EditorApplication.Exit(1);
    }
}
