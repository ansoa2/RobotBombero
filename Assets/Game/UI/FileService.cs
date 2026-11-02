using System;

namespace RobotBombero.Game
{
    /// <summary>Descarga y selección de archivos. Existe para poder sustituir FileBridge en las pruebas.</summary>
    public interface IFileService
    {
        bool Download(string fileName, string content);
        void PickJson(Action<string> onContent, Action<string> onError);
    }

    public sealed class FileBridgeService : IFileService
    {
        public bool Download(string fileName, string content)
        {
            return FileBridge.Download(fileName, content);
        }

        public void PickJson(Action<string> onContent, Action<string> onError)
        {
            FileBridge.PickJson(onContent, onError);
        }
    }
}
