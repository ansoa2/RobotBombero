using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace RobotBombero.Game.Tests
{
    /// <summary>Pantalla de acceso y menú con dirección de servidor, y los botones de archivos según la plataforma.</summary>
    public class ServerUiTests : UiFixture
    {
        private TargetApi _target;

        private void StartNative(string defaultAddress, bool loggedIn = false)
        {
            _target = new TargetApi();
            if (loggedIn) Store.SaveProfile(new Profile { playerName = "Ana", playerKey = "k", playerId = 3 });
            Context = new GameContext(Store, _target, Drain, Files, new ServerConfig(true, defaultAddress));
            Router = AppScreens.Compose(Root, Assets(new Texture2D(2, 4), new Texture2D(4, 2)), Context);
            Router.ShowStart();
        }

        private TextField ServerField() { return Root.Q<TextField>("login-server"); }

        [Test]
        public void Login_WithoutServerAddress_HidesTheServerField()
        {
            Start(false);
            Assert.IsFalse(IsShown(Root.Q("login-server-block")));
        }

        [Test]
        public void Login_Native_ShowsTheFieldPrefilledWithTheDefault()
        {
            StartNative("192.168.1.149:5080");
            Assert.IsNotNull(ServerField());
            Assert.IsTrue(IsShown(Root.Q("login-server-block")));
            Assert.AreEqual("192.168.1.149:5080", ServerField().value);
            Assert.AreEqual("Dirección del servidor", ServerField().label);
        }

        [Test]
        public void Login_Native_InvalidAddress_ShowsMessage_AndDoesNotCallTheServer()
        {
            StartNative("192.168.1.149:5080");
            ServerField().value = "192.168.1.999";
            Root.Q<TextField>("login-name").value = "Bombera_7";
            Click(Btn("login-enter"));
            Assert.AreEqual(0, _target.CreatePlayerCalls);
            StringAssert.Contains("dirección", Lbl("login-message").text);
        }

        [Test]
        public void Login_Native_ValidAddress_IsStoredAndUsed()
        {
            StartNative("");
            ServerField().value = "10.0.0.5";
            Root.Q<TextField>("login-name").value = "Bombera_7";
            Click(Btn("login-enter"));
            Assert.AreEqual("http://10.0.0.5:5080", _target.BaseUrl);
            Assert.AreEqual("http://10.0.0.5:5080", Store.LoadServerAddress());
            Assert.AreEqual(1, _target.CreatePlayerCalls);
        }

        [Test]
        public void Login_Native_ConnectionFailure_NamesTheAddressTried()
        {
            StartNative("192.168.1.149:5080");
            Root.Q<TextField>("login-name").value = "Bombera_7";
            Click(Btn("login-enter"));
            string text = Lbl("login-message").text;
            StringAssert.Contains("http://192.168.1.149:5080", text);
            StringAssert.Contains("wifi", text);
        }

        [Test]
        public void Menu_Native_OffersToChangeTheServer()
        {
            StartNative("192.168.1.149:5080", true);
            Assert.IsTrue(IsShown(Btn("menu-server")));
            Click(Btn("menu-server"));
            Assert.IsTrue(Dialog.IsOpen(Root));
            Root.Q<TextField>("dialog-input").value = "10.0.0.7:6000";
            Click(Btn("dialog-ok"));
            Assert.AreEqual("http://10.0.0.7:6000", Context.ServerUrl);
            Assert.AreEqual("http://10.0.0.7:6000", _target.BaseUrl);
        }

        [Test]
        public void Menu_WithoutServerAddress_HidesTheServerButton()
        {
            Start();
            Assert.IsFalse(IsShown(Btn("menu-server")));
        }

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
