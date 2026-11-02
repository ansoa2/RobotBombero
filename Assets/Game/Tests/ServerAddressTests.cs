using NUnit.Framework;

namespace RobotBombero.Game.Tests
{
    /// <summary>La dirección del servidor que escribe el jugador: con o sin http://, con o sin puerto (por defecto 5080).</summary>
    public class ServerAddressTests
    {
        [TestCase("192.168.1.149:5080", "http://192.168.1.149:5080")]
        [TestCase("192.168.1.149", "http://192.168.1.149:5080")]
        [TestCase("http://192.168.1.149", "http://192.168.1.149:5080")]
        [TestCase("http://192.168.1.149:5080", "http://192.168.1.149:5080")]
        [TestCase("  HTTP://192.168.1.149:6000/  ", "http://192.168.1.149:6000")]
        [TestCase("https://mipc.local", "https://mipc.local:5080")]
        [TestCase("mipc", "http://mipc:5080")]
        [TestCase("Servidor-1.casa:80", "http://servidor-1.casa:80")]
        [TestCase("127.0.0.1:5080", "http://127.0.0.1:5080")]
        public void Normalize_AcceptsValidAddresses(string input, string expected)
        {
            ServerAddressResult result = ServerAddress.Normalize(input);
            Assert.IsTrue(result.IsValid, result.Error);
            Assert.AreEqual(expected, result.Url);
            Assert.IsNull(result.Error);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("http://")]
        [TestCase("ftp://192.168.1.149")]
        [TestCase("192.168.1.149:abc")]
        [TestCase("192.168.1.149:0")]
        [TestCase("192.168.1.149:70000")]
        [TestCase("192.168.1.149:")]
        [TestCase("192.168.1")]
        [TestCase("192.168.1.300")]
        [TestCase("192.168.1.149/api")]
        [TestCase("192.168.1.149?x=1")]
        [TestCase("mi pc")]
        [TestCase("-pc")]
        [TestCase("pc..local")]
        [TestCase("pc_1")]
        [TestCase("[::1]:5080")]
        [TestCase("http://user@host")]
        public void Normalize_RejectsInvalidAddresses_WithAMessage(string input)
        {
            ServerAddressResult result = ServerAddress.Normalize(input);
            Assert.IsFalse(result.IsValid);
            Assert.IsNull(result.Url);
            Assert.IsNotEmpty(result.Error);
            StringAssert.Contains("192.168.1.149:5080", result.Error, "El mensaje debe dar un ejemplo");
        }

        [Test]
        public void Display_DropsTheHttpScheme()
        {
            Assert.AreEqual("192.168.1.149:5080", ServerAddress.Display("http://192.168.1.149:5080"));
            Assert.AreEqual("https://mipc:5080", ServerAddress.Display("https://mipc:5080"));
            Assert.AreEqual("", ServerAddress.Display(null));
        }

        [Test]
        public void OfflineMessage_NamesTheAddress_AndRemindsServerAndWifi()
        {
            string text = ServerAddress.OfflineMessage("http://192.168.1.149:5080");
            StringAssert.Contains("http://192.168.1.149:5080", text);
            StringAssert.Contains("servidor", text);
            StringAssert.Contains("wifi", text);
        }
    }
}
