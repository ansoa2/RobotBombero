using System.IO;
using NUnit.Framework;

namespace RobotBombero.Game.Tests
{
    public class AndroidCleartextTests
    {
        [Test]
        public void Patch_AddsUsesCleartextTraffic_ToTheApplication()
        {
            string file = Path.Combine(Path.GetTempPath(), "rb-manifest-" + System.Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(file, "<?xml version=\"1.0\" encoding=\"utf-8\"?><manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"><application android:label=\"x\" /></manifest>");
            try
            {
                AndroidCleartext.Patch(file);
                string text = File.ReadAllText(file);
                StringAssert.Contains("android:usesCleartextTraffic=\"true\"", text);
                StringAssert.Contains("android:label=\"x\"", text);
            }
            finally { File.Delete(file); }
        }
    }
}
