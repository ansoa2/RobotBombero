using System.IO;
using NUnit.Framework;

namespace RobotBombero.Game.Tests
{
    public class AndroidPickerPermissionsTests
    {
        [Test]
        public void Patch_RemovesTheStoragePermissionsTheLibraryDeclares()
        {
            string file = Path.Combine(Path.GetTempPath(), "rb-manifest-" + System.Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(file, "<?xml version=\"1.0\" encoding=\"utf-8\"?><manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"><application android:label=\"x\" /></manifest>");
            try
            {
                AndroidPickerPermissions.Patch(file);
                string text = File.ReadAllText(file);
                StringAssert.Contains("xmlns:tools=\"http://schemas.android.com/tools\"", text);
                foreach (string permission in AndroidPickerPermissions.Removed)
                {
                    StringAssert.Contains("android:name=\"" + permission + "\"", text);
                }
                StringAssert.Contains("tools:node=\"remove\"", text);
                StringAssert.Contains("android:label=\"x\"", text);
            }
            finally { File.Delete(file); }
        }
    }
}
