using System;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;

/// <summary>
/// Con "Allow downloads over HTTP" = Always Allowed (ver BuildAndroid.ApplySettings) añade android:usesCleartextTraffic="true"
/// al manifiesto del APK. Unity no lo escribe solo, y Android 9 o posterior bloquea HTTP sin cifrar si falta.
/// El servidor de red local usa HTTP; para publicar hará falta HTTPS y quitar esto.
/// </summary>
public sealed class AndroidCleartext : IPostGenerateGradleAndroidProject
{
    private const string AndroidNs = "http://schemas.android.com/apk/res/android";

    public int callbackOrder { get { return 0; } }

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        if (PlayerSettings.insecureHttpOption != InsecureHttpOption.AlwaysAllowed) return;

        // path es la carpeta unityLibrary; la aplicación (y su manifiesto principal) está en launcher.
        string manifest = Path.GetFullPath(Path.Combine(path, "..", "launcher", "src", "main", "AndroidManifest.xml"));
        if (!File.Exists(manifest)) throw new FileNotFoundException("No se encuentra el manifiesto del launcher para permitir HTTP.", manifest);

        Patch(manifest);
    }

    /// <summary>Añade usesCleartextTraffic="true" a la etiqueta application del manifiesto.</summary>
    public static void Patch(string manifest)
    {
        var doc = new XmlDocument();
        doc.Load(manifest);
        XmlNode application = doc.SelectSingleNode("/manifest/application");
        if (application == null) throw new InvalidOperationException("El manifiesto no tiene <application>.");

        XmlAttribute attribute = doc.CreateAttribute("android", "usesCleartextTraffic", AndroidNs);
        attribute.Value = "true";
        application.Attributes.SetNamedItem(attribute);
        doc.Save(manifest);
    }
}
