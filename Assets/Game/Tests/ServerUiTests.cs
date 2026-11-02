using UnityEngine.UIElements;
using NUnit.Framework;

namespace RobotBombero.Game.Tests
{
    /// <summary>Los botones de archivos según la plataforma.</summary>
    public class ServerUiTests : UiFixture
    {
        [Test]
        public void Rules_FilesAvailable_ShowsImportAndExport()
        {
            Start();
            AddPack("p1", "Uno");
            Click(Btn("menu-rules"));
            Assert.IsTrue(IsShown(Btn("rules-import")));
            Assert.IsNotNull(Root.Q<Button>("pack-export-0"));
            Assert.IsFalse(IsShown(Lbl("rules-files-note")));
        }

        [Test]
        public void Rules_FilesUnavailable_HidesBothButtons_AndSaysComingSoon()
        {
            Files.Available = false;
            Start();
            AddPack("p1", "Uno");
            Click(Btn("menu-rules"));
            Assert.IsFalse(IsShown(Btn("rules-import")));
            Assert.IsNull(Root.Q<Button>("pack-export-0"));
            Assert.IsTrue(IsShown(Lbl("rules-files-note")));
            StringAssert.Contains("Próximamente en esta versión", Lbl("rules-files-note").text);
        }

        [Test]
        public void FileBridgeService_IsAvailableOnlyWhereItWorks()
        {
#if UNITY_EDITOR || UNITY_WEBGL
            Assert.IsTrue(new FileBridgeService().IsAvailable);
#else
            Assert.IsFalse(new FileBridgeService().IsAvailable);
#endif
        }
    }
}
