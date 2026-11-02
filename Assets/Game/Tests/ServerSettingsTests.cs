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

    /// <summary>Dónde se guarda la dirección del servidor y cómo llega al cliente de red.</summary>
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
        public void LocalStore_ServerAddress_RoundTrips_AndDefaultsToNull()
        {
            Assert.IsNull(_store.LoadServerAddress());
            _store.SaveServerAddress("http://10.0.0.5:5080");
            Assert.AreEqual("http://10.0.0.5:5080", new LocalStore(_root).LoadServerAddress());
        }

        [Test]
        public void LocalStore_ServerAddress_SurvivesProfileSaves()
        {
            _store.SaveServerAddress("http://10.0.0.5:5080");
            _store.SaveProfile(new Profile());
            Assert.AreEqual("http://10.0.0.5:5080", _store.LoadServerAddress());
        }

        [Test]
        public void LocalStore_CorruptSettings_CountAsEmpty()
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, "settings.json"), "{no es json");
            Assert.IsNull(_store.LoadServerAddress());
        }

        [Test]
        public void Context_WithoutServerConfig_DoesNotUseAddresses()
        {
            var api = new TargetApi();
            var context = new GameContext(_store, api, Drain);
            Assert.IsFalse(context.UsesServerAddress);
            Assert.IsNull(api.BaseUrl, "WebGL: rutas relativas");
        }

        [Test]
        public void Context_CleanInstall_UsesTheBuildDefault()
        {
            var api = new TargetApi();
            var context = new GameContext(_store, api, Drain, null, new ServerConfig(true, "192.168.1.149:5080"));
            Assert.IsTrue(context.UsesServerAddress);
            Assert.AreEqual("http://192.168.1.149:5080", context.ServerUrl);
            Assert.AreEqual("http://192.168.1.149:5080", api.BaseUrl);
        }

        [Test]
        public void Context_StoredAddress_WinsOverTheBuildDefault()
        {
            _store.SaveServerAddress("http://10.0.0.5:5080");
            var api = new TargetApi();
            var context = new GameContext(_store, api, Drain, null, new ServerConfig(true, "192.168.1.149:5080"));
            Assert.AreEqual("http://10.0.0.5:5080", context.ServerUrl);
            Assert.AreEqual("http://10.0.0.5:5080", api.BaseUrl);
        }

        [Test]
        public void Context_NoDefault_StartsEmpty()
        {
            var context = new GameContext(_store, new TargetApi(), Drain, null, new ServerConfig(true, null));
            Assert.AreEqual("", context.ServerUrl);
        }

        [Test]
        public void SetServerAddress_Valid_NormalizesStoresAndRetargetsTheClient()
        {
            var api = new TargetApi();
            var context = new GameContext(_store, api, Drain, null, new ServerConfig(true, null));
            Assert.IsNull(context.SetServerAddress(" 192.168.1.20 "));
            Assert.AreEqual("http://192.168.1.20:5080", context.ServerUrl);
            Assert.AreEqual("http://192.168.1.20:5080", api.BaseUrl);
            Assert.AreEqual("http://192.168.1.20:5080", _store.LoadServerAddress());
        }

        [Test]
        public void SetServerAddress_Invalid_ReturnsTheError_AndChangesNothing()
        {
            var api = new TargetApi();
            var context = new GameContext(_store, api, Drain, null, new ServerConfig(true, "192.168.1.149"));
            string error = context.SetServerAddress("192.168.1.999");
            Assert.IsNotEmpty(error);
            Assert.AreEqual("http://192.168.1.149:5080", context.ServerUrl);
            Assert.AreEqual("http://192.168.1.149:5080", api.BaseUrl);
            Assert.IsNull(_store.LoadServerAddress());
        }

        [Test]
        public void ExpireSession_KeepsTheServerAddress()
        {
            var context = new GameContext(_store, new TargetApi(), Drain, null, new ServerConfig(true, "192.168.1.149"));
            context.SetServerAddress("10.0.0.5");
            context.ExpireSession();
            Assert.AreEqual("http://10.0.0.5:5080", context.ServerUrl);
            Assert.AreEqual("http://10.0.0.5:5080", _store.LoadServerAddress());
        }
    }
}
