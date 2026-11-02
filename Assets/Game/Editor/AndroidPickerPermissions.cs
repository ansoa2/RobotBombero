using System;
using System.IO;
using System.Xml;
using UnityEditor.Android;

/// <summary>
/// NativeFilePicker declara READ_EXTERNAL_STORAGE y WRITE_EXTERNAL_STORAGE en su biblioteca. El selector y el guardado del sistema
/// (Storage Access Framework) no las necesitan, así que se quitan del manifiesto de la aplicación con tools:node="remove".
/// El código pone NativeFilePicker.PermissionFreeMode para que el complemento no pida ni compruebe esos permisos.
/// </summary>
public sealed class AndroidPickerPermissions : IPostGenerateGradleAndroidProject
{
    private const string AndroidNs = "http://schemas.android.com/apk/res/android";
    private const string ToolsNs = "http://schemas.android.com/tools";
    public static readonly string[] Removed =
    {
        "android.permission.READ_EXTERNAL_STORAGE",
        "android.permission.WRITE_EXTERNAL_STORAGE"
    };

    public int callbackOrder { get { return 1; } }

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string manifest = Path.GetFullPath(Path.Combine(path, "..", "launcher", "src", "main", "AndroidManifest.xml"));
        if (!File.Exists(manifest)) throw new FileNotFoundException("No se encuentra el manifiesto del launcher para quitar permisos de almacenamiento.", manifest);
        Patch(manifest);
    }

    public static void Patch(string manifest)
    {
        var doc = new XmlDocument();
        doc.Load(manifest);
        XmlElement root = doc.DocumentElement;
        if (root == null || root.Name != "manifest") throw new InvalidOperationException("El manifiesto no es válido.");
        if (root.GetAttribute("xmlns:tools") == "") root.SetAttribute("xmlns:tools", ToolsNs);

        foreach (string permission in Removed)
        {
            XmlElement element = doc.CreateElement("uses-permission");
            XmlAttribute name = doc.CreateAttribute("android", "name", AndroidNs);
            name.Value = permission;
            element.Attributes.Append(name);
            XmlAttribute node = doc.CreateAttribute("tools", "node", ToolsNs);
            node.Value = "remove";
            element.Attributes.Append(node);
            root.AppendChild(element);
        }
        doc.Save(manifest);
    }
}
