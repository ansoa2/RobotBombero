using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace RobotBombero.Game.Tests
{
    /// <summary>Las compilaciones nativas (Android y Windows): de entrega, sin capturas, con la identidad pedida.</summary>
    public class BuildNativeTests
    {
        [Test]
        public void AndroidOptions_AreARelease_IntoTheBuildFolder()
        {
            BuildPlayerOptions options = BuildAndroid.Options();
            Assert.AreEqual(BuildOptions.None, options.options);
            Assert.AreEqual(BuildTarget.Android, options.target);
            Assert.AreEqual("Build/Android/RobotBombero.apk", options.locationPathName);
            Assert.AreEqual(new[] { "Assets/Scenes/Main.unity" }, options.scenes);
            Assert.IsTrue(options.extraScriptingDefines == null || options.extraScriptingDefines.Length == 0);
        }

        [Test]
        public void WindowsOptions_AreARelease_IntoTheBuildFolder()
        {
            BuildPlayerOptions options = BuildWindows.Options();
            Assert.AreEqual(BuildOptions.None, options.options);
            Assert.AreEqual(BuildTarget.StandaloneWindows64, options.target);
            Assert.AreEqual("Build/Windows/RobotBombero.exe", options.locationPathName);
            Assert.IsTrue(options.extraScriptingDefines == null || options.extraScriptingDefines.Length == 0);
        }

        [Test]
        public void Project_DoesNotDefineCaptureForNativeTargets()
        {
            Assert.IsFalse(BuildCommon.HasCaptureDefine(NamedBuildTarget.Android));
            Assert.IsFalse(BuildCommon.HasCaptureDefine(NamedBuildTarget.Standalone));
        }

        [Test]
        public void AndroidSettings_HaveTheRequestedIdentityAndOptions()
        {
            BuildAndroid.ApplySettings();
            Assert.AreEqual("com.robotbombero.juego", PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
            Assert.AreEqual("Robot Bombero", PlayerSettings.productName);
            Assert.AreEqual("Robot Bombero", PlayerSettings.companyName);
            Assert.AreEqual("0.1.0", PlayerSettings.bundleVersion);
            Assert.AreEqual(ScriptingImplementation.IL2CPP, PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android));
            Assert.AreEqual(AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7, PlayerSettings.Android.targetArchitectures);
            Assert.AreEqual(UIOrientation.AutoRotation, PlayerSettings.defaultInterfaceOrientation);
            Assert.IsTrue(PlayerSettings.allowedAutorotateToPortrait);
            Assert.IsTrue(PlayerSettings.allowedAutorotateToPortraitUpsideDown);
            Assert.IsTrue(PlayerSettings.allowedAutorotateToLandscapeLeft);
            Assert.IsTrue(PlayerSettings.allowedAutorotateToLandscapeRight);
            Assert.AreEqual(InsecureHttpOption.AlwaysAllowed, PlayerSettings.insecureHttpOption);
            Assert.IsFalse(PlayerSettings.Android.useCustomKeystore, "Sin clave propia: la firma de desarrollo es automática y no hay secretos en el repositorio");
            Assert.IsFalse(EditorUserBuildSettings.buildAppBundle);
        }

        [Test]
        public void AndroidSettings_UseTheAppIcons()
        {
            BuildAndroid.ApplySettings();
            var adaptive = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, UnityEditor.Android.AndroidPlatformIconKind.Adaptive);
            Assert.IsNotEmpty(adaptive);
            foreach (PlatformIcon icon in adaptive)
            {
                Assert.IsNotNull(icon.GetTexture(0), "Falta el fondo del icono adaptable");
                Assert.IsNotNull(icon.GetTexture(1), "Falta el primer plano del icono adaptable");
            }
        }

        [Test]
        public void ServerDefault_Valid_IsNormalizedIntoAResource_AndRemovedAfterwards()
        {
            string file = BuildCommon.WriteServerDefault("192.168.1.149");
            try
            {
                Assert.IsTrue(File.Exists(file));
                Assert.AreEqual("http://192.168.1.149:5080", File.ReadAllText(file).Trim());
                Assert.IsNotNull(UnityEngine.Resources.Load<UnityEngine.TextAsset>(ServerConfig.DefaultResource));
            }
            finally { BuildCommon.RemoveServerDefault(); }
            Assert.IsFalse(File.Exists(file));
            Assert.IsNull(UnityEngine.Resources.Load<UnityEngine.TextAsset>(ServerConfig.DefaultResource));
        }

        [Test]
        public void ServerDefault_Invalid_IsRejected_AndWritesNothing()
        {
            Assert.Throws<System.ArgumentException>(() => BuildCommon.WriteServerDefault("no es una dirección"));
            Assert.IsFalse(File.Exists(BuildCommon.ServerDefaultPath));
        }

        [Test]
        public void ServerDefault_Empty_WritesNothing()
        {
            Assert.IsNull(BuildCommon.WriteServerDefault(null));
            Assert.IsFalse(File.Exists(BuildCommon.ServerDefaultPath));
        }
    }
}
