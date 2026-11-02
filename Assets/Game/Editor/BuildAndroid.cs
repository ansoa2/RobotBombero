using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Compilación de entrega para Android: Build/Android/RobotBombero.apk (fuera de git).
/// Se ejecuta con: Unity -batchmode -quit -executeMethod BuildAndroid.Build [-rbServer 192.168.1.149:5080]
/// Sin Development ni RB_CAPTURE. La firma es la automática de desarrollo de Unity (clave de depuración generada
/// en la carpeta del usuario): no hay ninguna clave ni contraseña en el proyecto. Para publicar hará falta una clave propia.
/// </summary>
public static class BuildAndroid
{
    public const string OutputPath = "Build/Android/RobotBombero.apk";
    public const string ApplicationId = "com.robotbombero.juego";
    public const string ProductName = "Robot Bombero";
    public const string Version = "0.1.0";

    private const string IconPath = "Assets/Game/Art/Icon/";

    public static BuildPlayerOptions Options()
    {
        return new BuildPlayerOptions
        {
            scenes = new[] { BuildCommon.MainScene },
            locationPathName = OutputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };
    }

    /// <summary>
    /// Identidad, IL2CPP con ARM64 y ARMv7, giro libre, HTTP permitido (red local) e iconos.
    /// Solo toca ajustes de Android y de identidad: nada de lo que usa la compilación WebGL.
    /// </summary>
    public static void ApplySettings()
    {
        PlayerSettings.companyName = ProductName;
        PlayerSettings.productName = ProductName;
        PlayerSettings.bundleVersion = Version;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
        PlayerSettings.Android.bundleVersionCode = 1;

        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
        // Android 8.0 como mínimo; el destino es el SDK más reciente instalado.
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        // El servidor local usa HTTP; sin esto Android bloquea el tráfico sin cifrar. Para publicar hará falta HTTPS.
        PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;

        // APK (no paquete de tienda) y clave de desarrollo automática, sin almacén propio.
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        PlayerSettings.Android.useCustomKeystore = false;

        ApplyIcons();
        AssetDatabase.SaveAssets();
    }

    private static Texture2D Icon(string file)
    {
        string path = IconPath + file;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureCompression != TextureImporterCompression.Uncompressed || importer.mipmapEnabled || !importer.alphaIsTransparency)
        {
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void ApplyIcons()
    {
        Texture2D background = Icon("icon-adaptive-background.png");
        Texture2D foreground = Icon("icon-adaptive-foreground.png");

        PlatformIcon[] adaptive = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive);
        foreach (PlatformIcon icon in adaptive) icon.SetTextures(background, foreground);
        PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive, adaptive);

    }

    [MenuItem("RobotBombero/Compilar para Android")]
    public static void Build()
    {
        if (BuildCommon.HasCaptureDefine(NamedBuildTarget.Android))
        {
            BuildCommon.Fail("Android", "El proyecto define " + BuildCommon.CaptureDefine + " para Android: una compilación de entrega no puede llevarlo.");
            return;
        }

        ApplySettings();
        string error = BuildCommon.Run("Android", Options(), OutputPath);
        if (error != null) BuildCommon.Fail("Android", error);
    }
}
