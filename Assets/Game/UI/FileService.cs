using System;

namespace RobotBombero.Game
{
    /// <summary>Descarga y selección de archivos. Existe para poder sustituir FileBridge en las pruebas.</summary>
    public interface IFileService
    {
        /// <summary>Falso donde no hay selector ni guardado de archivos: la interfaz oculta importar y exportar.</summary>
        bool IsAvailable { get; }
        /// <summary>Verdadero solo en WebGL: el selector debe armarse al pulsar (pointer-down); en el resto basta un clic.</summary>
        bool PickNeedsPointerDown { get; }
        void Download(string fileName, string content, Action onDone, Action<string> onError);
        void PickJson(Action<string> onContent, Action<string> onError);
    }

    public sealed class FileBridgeService : IFileService
    {
        public bool IsAvailable { get { return FileBridge.IsAvailable; } }
        public bool PickNeedsPointerDown { get { return FileBridge.PickNeedsPointerDown; } }

        public void Download(string fileName, string content, Action onDone, Action<string> onError)
        {
            FileBridge.Download(fileName, content, onDone, onError);
        }

        public void PickJson(Action<string> onContent, Action<string> onError)
        {
            FileBridge.PickJson(onContent, onError);
        }
    }
}
