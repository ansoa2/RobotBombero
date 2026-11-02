using UnityEditor;
using UnityEditor.Build;

/// <summary>
/// Compilación de entrega para Windows (la app del PC), en Build/Windows (fuera de git).
/// Se ejecuta con: Unity -batchmode -quit -executeMethod BuildWindows.Build [-rbServer 192.168.1.149:5080]
/// Sin Development ni RB_CAPTURE: DevFlags y DevSamples no llegan a compilarse.
/// </summary>
public static class BuildWindows
{
    public const string OutputPath = "Build/Windows/RobotBombero.exe";

    public static BuildPlayerOptions Options()
    {
        return new BuildPlayerOptions
        {
            scenes = new[] { BuildCommon.MainScene },
            locationPathName = OutputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
    }

    [MenuItem("RobotBombero/Compilar para Windows")]
    public static void Build()
    {
        if (BuildCommon.HasCaptureDefine(NamedBuildTarget.Standalone))
        {
            BuildCommon.Fail("Windows", "El proyecto define " + BuildCommon.CaptureDefine + " para Windows: una compilación de entrega no puede llevarlo.");
            return;
        }

        string error = BuildCommon.Run("Windows", Options(), OutputPath);
        if (error != null) BuildCommon.Fail("Windows", error);
    }
}
