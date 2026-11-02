using System;
using System.IO;
using RobotBombero.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Lo que comparten las compilaciones nativas (Android y Windows): escena, símbolos, dirección por defecto y salida.</summary>
public static class BuildCommon
{
    public const string MainScene = "Assets/Scenes/Main.unity";
    public const string CaptureDefine = "RB_CAPTURE";

    /// <summary>Parámetro de línea de órdenes con la dirección del servidor que llevará por defecto una instalación limpia.</summary>
    public const string ServerArg = "-rbServer";

    private const string ResourcesDir = "Assets/Game/Resources";
    public static readonly string ServerDefaultPath = ResourcesDir + "/" + ServerConfig.DefaultResource + ".txt";

    public static bool HasCaptureDefine(NamedBuildTarget target)
    {
        string defines = PlayerSettings.GetScriptingDefineSymbols(target);
        return Array.IndexOf(defines.Split(';'), CaptureDefine) >= 0;
    }

    public static string GetArg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }

    /// <summary>
    /// Escribe la dirección por defecto como recurso de texto (Assets/Game/Resources, fuera de git) para que entre en la compilación.
    /// Devuelve la ruta, o nulo si no se indicó dirección. Lanza ArgumentException si no es válida.
    /// </summary>
    public static string WriteServerDefault(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        ServerAddressResult result = ServerAddress.Normalize(address);
        if (!result.IsValid) throw new ArgumentException("Dirección del servidor no válida: " + result.Error);

        Directory.CreateDirectory(ResourcesDir);
        File.WriteAllText(ServerDefaultPath, result.Url);
        AssetDatabase.ImportAsset(ServerDefaultPath, ImportAssetOptions.ForceSynchronousImport);
        return ServerDefaultPath;
    }

    /// <summary>Quita el recurso y su carpeta (con los .meta) para que no quede nada generado en el proyecto.</summary>
    public static void RemoveServerDefault()
    {
        if (!Directory.Exists(ResourcesDir)) return;
        AssetDatabase.DeleteAsset(ServerDefaultPath);
        if (Directory.GetFileSystemEntries(ResourcesDir).Length == 0) AssetDatabase.DeleteAsset(ResourcesDir);
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Compila con las opciones dadas: la dirección por defecto entra como recurso y se retira al terminar, y el destino
    /// activo del proyecto vuelve a ser el que había. Devuelve un mensaje de error o nulo si salió bien.
    /// </summary>
    public static string Run(string label, BuildPlayerOptions options, string expectedFile)
    {
        string outputDir = Path.GetDirectoryName(options.locationPathName);
        BuildTarget previous = EditorUserBuildSettings.activeBuildTarget;
        BuildTargetGroup previousGroup = BuildPipeline.GetBuildTargetGroup(previous);
        Debug.Log("RobotBombero " + label + ": destino activo antes de compilar: " + previous);
        try
        {
            WriteServerDefault(GetArg(ServerArg));
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            Directory.CreateDirectory(outputDir);

            BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
            Debug.Log("RobotBombero " + label + ": " + summary.result + ", " + summary.totalSize / 1024 + " KB, " + summary.totalErrors + " errores, en " + Path.GetFullPath(summary.outputPath));
            if (summary.result != BuildResult.Succeeded) return "La compilación ha fallado: " + summary.result;
            if (!File.Exists(expectedFile)) return "La compilación no ha generado " + expectedFile;
            return null;
        }
        catch (ArgumentException e)
        {
            return e.Message;
        }
        finally
        {
            RemoveServerDefault();
            // BuildPlayer cambia el destino activo; se devuelve al de antes (WebGL, normalmente) para no dejar el editor en otra plataforma.
            if (EditorUserBuildSettings.activeBuildTarget != previous)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(previousGroup, previous);
                Debug.Log("RobotBombero " + label + ": destino activo restaurado a " + previous);
            }
        }
    }

    public static void Fail(string label, string message)
    {
        Debug.LogError("RobotBombero " + label + ": " + message);
        if (Application.isBatchMode) EditorApplication.Exit(1);
    }
}
