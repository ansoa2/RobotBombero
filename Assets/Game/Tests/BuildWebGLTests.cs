using NUnit.Framework;
using UnityEditor;

namespace RobotBombero.Game.Tests
{
    /// <summary>La compilación de entrega: sin desarrollo ni capturas, y con los archivos que espera el servidor.</summary>
    public class BuildWebGLTests
    {
        [Test]
        public void Options_AreARelease_IntoTheServerFolder()
        {
            BuildPlayerOptions options = BuildWebGL.Options();
            Assert.AreEqual(BuildOptions.None, options.options);
            Assert.AreEqual(BuildTarget.WebGL, options.target);
            Assert.AreEqual("Server/RobotBombero.Server/wwwroot", options.locationPathName);
            Assert.AreEqual(new[] { "Assets/Scenes/Main.unity" }, options.scenes);
            Assert.IsTrue(options.extraScriptingDefines == null || options.extraScriptingDefines.Length == 0);
        }

        [Test]
        public void Project_DoesNotDefineCapture()
        {
            Assert.IsFalse(BuildWebGL.HasCaptureDefine());
        }

        [Test]
        public void ProjectSettings_UseBrotliWithDecompressionFallback()
        {
            // El servidor sirve los .unityweb sin Content-Encoding; sin la descompresión en el navegador no cargarían.
            Assert.AreEqual(WebGLCompressionFormat.Brotli, PlayerSettings.WebGL.compressionFormat);
            Assert.IsTrue(PlayerSettings.WebGL.decompressionFallback);
        }
    }
}
