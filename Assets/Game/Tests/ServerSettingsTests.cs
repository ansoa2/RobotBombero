using System;
using System.Collections;
using System.IO;
using NUnit.Framework;

namespace RobotBombero.Game.Tests
{
    internal sealed class TargetApi : FakeApi, IServerTarget
    {
        public string BaseUrl { get; set; }
    }

    /// <summary>La dirección del servidor es la de la compilación: no se guarda, no se elige y no llega a ningún texto.</summary>
    public class ServerSettingsTests
    {
        private string _root;
        private LocalStore _store;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rb-srv-" + Guid.NewGuid().ToString("N"));
            _store = new LocalStore(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private static void Drain(IEnumerator e) { while (e.MoveNext()) { } }

        [Test]
        public void Context_WithoutServerConfig_UsesRelativePaths()
        {
            var api = new TargetApi();
            new GameContext(_store, api, Drain);
            Assert.IsNull(api.BaseUrl, "WebGL: rutas relativas");
        }

        [Test]
        public void Context_UsesOnlyTheBuildAddress()
        {
            var api = new TargetApi();
            new GameContext(_store, api, Drain, null, new ServerConfig(true, "192.168.1.149:5080"));
            Assert.AreEqual("http://192.168.1.149:5080", api.BaseUrl);
        }

        [Test]
        public void Context_IgnoresAnyOldStoredAddress()
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, "settings.json"), "{\"serverAddress\":\"http://10.0.0.5:5080\"}");
            var api = new TargetApi();
            new GameContext(_store, api, Drain, null, new ServerConfig(true, "192.168.1.149:5080"));
            Assert.AreEqual("http://192.168.1.149:5080", api.BaseUrl);
        }

        [Test]
        public void Context_NoBuildAddress_StartsEmpty()
        {
            var api = new TargetApi { BaseUrl = "x" };
            new GameContext(_store, api, Drain, null, new ServerConfig(true, null));
            Assert.AreEqual("", api.BaseUrl);
        }

        [Test]
        public void ExpireSession_KeepsTheBuildAddress()
        {
            var api = new TargetApi();
            var context = new GameContext(_store, api, Drain, null, new ServerConfig(true, "192.168.1.149"));
            context.ExpireSession();
            Assert.AreEqual("http://192.168.1.149:5080", api.BaseUrl);
        }

        [Test]
        public void LocalStore_HasNoAddressHandling()
        {
            Assert.IsNull(typeof(LocalStore).GetMethod("LoadServerAddress"));
            Assert.IsNull(typeof(LocalStore).GetMethod("SaveServerAddress"));
            Assert.IsNull(typeof(GameContext).GetMethod("SetServerAddress"));
        }
    }
}
