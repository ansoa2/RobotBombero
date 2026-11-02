using System;

namespace RobotBombero.Game
{
    /// <summary>Descarga y selección de archivos. Existe para poder sustituir FileBridge en las pruebas.</summary>
    public interface IFileService
    {
        /// <summary>Falso donde no hay selector ni descarga de archivos (apps nativas por ahora): la interfaz oculta importar y exportar.</summary>
        bool IsAvailable { get; }
        bool Download(string fileName, string content);
        void PickJson(Action<string> onContent, Action<string> onError);
    }

    public sealed class FileBridgeService : IFileService
    {
        public bool IsAvailable
        {
            get
            {
#if UNITY_WEBGL || UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

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
