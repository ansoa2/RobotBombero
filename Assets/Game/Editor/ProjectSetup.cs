using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace RobotBombero.Game.Editor
{
    /// <summary>
    /// Deja el proyecto listo de forma repetible: importación de imágenes, PanelSettings, Main.unity,
    /// lista de escenas y ajustes del reproductor. Se ejecuta con -executeMethod.
    /// </summary>
    public static class ProjectSetup
    {
        private const string UiPath = "Assets/Game/UI/";
        private const string ArtPath = "Assets/Game/Art/";
        private const string MainScene = "Assets/Scenes/Main.unity";
        private static readonly Color Night = new Color32(0x09, 0x0C, 0x16, 0xFF);

        [MenuItem("RobotBombero/Preparar proyecto")]
        public static void Run()
        {
            AssetDatabase.Refresh();
            ConfigureImages();
            PanelSettings panel = CreatePanelSettings();
            CreateMainScene(panel);
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureImages()
        {
            // Crunch reduce mucho la descarga en WebGL. Los fondos no llevan transparencia.
            Image(ArtPath + "fondo-vertical.png", 1920, true, false);
            Image(ArtPath + "fondo-horizontal.png", 1920, true, false);
            Image(ArtPath + "robot.png", 512, true, true);
            Image(ArtPath + "fuego.png", 512, true, true);
            // Degradados pequeños de interfaz: sin compresión para que no aparezcan bandas.
            Image(UiPath + "Images/btn-main.png", 256, false, false);
            Image(UiPath + "Images/panel-fade.png", 256, false, true);
        }

        private static void Image(string path, int maxSize, bool crunch, bool alpha)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = alpha;
            importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = crunch ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = crunch;
            importer.compressionQuality = 60;
            importer.SaveAndReimport();
        }

        private static PanelSettings CreatePanelSettings()
        {
            string path = UiPath + "PanelSettings.asset";
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, path);
            }
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(UiPath + "Theme.tss");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            // La escala es una mezcla lineal de ancho y alto. Con estos valores, un móvil en cualquier orientación
            // queda cerca de escala 1 (16 lógicos se leen como 16 puntos) y una ventana 16:9 da unos 790 x 440 lógicos.
            panel.referenceResolution = new Vector2Int(420, 780);
            panel.match = 0.67f;
            panel.clearColor = false;
            EditorUtility.SetDirty(panel);
            return panel;
        }

        private static void CreateMainScene(PanelSettings panel)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Night;
            camera.orthographic = true;
            camera.cullingMask = 0;

            var app = new GameObject("App");
            var document = app.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPath + "Root.uxml");

            var serialized = new SerializedObject(app.AddComponent<GameApp>());
            SerializedProperty assets = serialized.FindProperty("assets");
            assets.FindPropertyRelative("login").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPath + "Screens/Login.uxml");
            assets.FindPropertyRelative("mainMenu").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPath + "Screens/MainMenu.uxml");
            foreach (string[] screen in new[] { new[] { "rules", "Rules" }, new[] { "ruleEditor", "RuleEditor" }, new[] { "packSelect", "PackSelect" }, new[] { "game", "Game" }, new[] { "result", "Result" }, new[] { "stats", "Stats" }, new[] { "leaderboard", "Leaderboard" } })
                assets.FindPropertyRelative(screen[0]).objectReferenceValue = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPath + "Screens/" + screen[1] + ".uxml");
            assets.FindPropertyRelative("backgroundPortrait").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath + "fondo-vertical.png");
            assets.FindPropertyRelative("backgroundLandscape").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath + "fondo-horizontal.png");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, MainScene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScene, true) };
        }

        private static void ConfigurePlayer()
        {
            // Sin pantalla de inicio de Unity ni destello blanco antes del primer fotograma.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = Night;
        }

        /// <summary>Reproductor de Windows de desarrollo para ver las pantallas de verdad. Va a Build/, fuera del repositorio.</summary>
        public static void BuildWindowsCapture()
        {
            Report(BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = "Build/CapturaT11/RobotBombero.exe",
                target = BuildTarget.StandaloneWindows64,
                // Sin Development: así el reproductor no abre el puerto del perfilador (y no aparece el aviso del cortafuegos).
                // DevFlags se activa con RB_CAPTURE, que nunca se define en una compilación de entrega.
                extraScriptingDefines = new[] { "RB_CAPTURE" },
                options = BuildOptions.None
            }));
        }

        private static void Report(BuildReport report)
        {
            BuildSummary summary = report.summary;
            Debug.Log("RobotBombero build: " + summary.result + ", " + summary.totalSize / 1024 + " KB, " + summary.totalErrors + " errores, en " + Path.GetFullPath(summary.outputPath));
            if (summary.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
