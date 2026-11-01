using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using RobotBombero.Simulation;
using UnityEngine;

namespace RobotBombero.Game
{
    /// <summary>
    /// Descarga y selección de archivos JSON. WebGL: Blob y input[type=file] vía FileBridge.jslib
    /// (resultado devuelto con SendMessage a un GameObject con nombre). Editor: EditorUtility.
    /// Otras plataformas: no disponible, se avisa por onError.
    /// </summary>
    public static class FileBridge
    {
        public const string ReceiverName = "RobotBomberoFileBridge";
        public const string ErrorCancelled = "cancelled";
        public const string ErrorTooLarge = "too_large";
        public const string ErrorUnavailable = "unavailable";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void RB_DownloadFile(string fileName, string content);
        [DllImport("__Internal")] private static extern void RB_PickJson(int maxBytes, string receiverName);
#endif

        private static Action<string> _onContent;
        private static Action<string> _onError;

        /// <summary>Devuelve false si la plataforma no puede descargar archivos.</summary>
        public static bool Download(string fileName, string content)
        {
            if (string.IsNullOrEmpty(fileName)) fileName = "pack.json";
            if (content == null) content = "";
#if UNITY_WEBGL && !UNITY_EDITOR
            RB_DownloadFile(fileName, content);
            return true;
#elif UNITY_EDITOR
            string path = UnityEditor.EditorUtility.SaveFilePanel("Exportar pack", "", fileName, "json");
            if (string.IsNullOrEmpty(path)) return false;
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return true;
#else
            Debug.LogWarning("RobotBombero: la descarga de archivos solo está disponible en WebGL.");
            return false;
#endif
        }

        /// <summary>
        /// Abre el selector de archivos. onContent recibe el texto; onError (opcional) recibe un código
        /// (cancelled, too_large, read_error, unavailable). Nunca se lee un archivo mayor que PackLimits.MaxFileBytes.
        /// REQUISITO WebGL: en WebGL este método solo "arma" el selector; el navegador lo abre en el siguiente
        /// pointerup/touchend. Por tanto la UI debe llamarlo desde el callback de pointer-down del botón, para
        /// que la liberación del mismo toque abra el selector dentro de un gesto real del usuario. Llamarlo
        /// desde un click de Unity (frame posterior) o sin un toque en curso lo dejaría esperando al siguiente toque.
        /// Iniciar una selección nueva sustituye a la pendiente. En el editor abre el diálogo directamente.
        /// </summary>
        public static void PickJson(Action<string> onContent, Action<string> onError = null)
        {
            if (onContent == null) throw new ArgumentNullException("onContent");
#if UNITY_WEBGL && !UNITY_EDITOR
            _onContent = onContent;
            _onError = onError;
            EnsureReceiver();
            RB_PickJson(PackLimits.MaxFileBytes, ReceiverName);
#elif UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("Importar pack", "", "json");
            if (string.IsNullOrEmpty(path)) { if (onError != null) onError(ErrorCancelled); return; }
            if (new FileInfo(path).Length > PackLimits.MaxFileBytes) { if (onError != null) onError(ErrorTooLarge); return; }
            onContent(File.ReadAllText(path, Encoding.UTF8));
#else
            if (onError != null) onError(ErrorUnavailable);
#endif
        }

        internal static void DeliverContent(string content)
        {
            Action<string> cb = _onContent;
            _onContent = null;
            _onError = null;
            if (cb != null) cb(content);
        }

        internal static void DeliverError(string code)
        {
            Action<string> cb = _onError;
            _onContent = null;
            _onError = null;
            if (cb != null) cb(code);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private static void EnsureReceiver()
        {
            if (GameObject.Find(ReceiverName) != null) return;
            var go = new GameObject(ReceiverName);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<FileBridgeReceiver>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }
#endif
    }

    /// <summary>Destino de los SendMessage del .jslib. Solo se crea en WebGL.</summary>
    public class FileBridgeReceiver : MonoBehaviour
    {
        public void OnFilePicked(string content) { FileBridge.DeliverContent(content); }
        public void OnFileError(string code) { FileBridge.DeliverError(code); }
    }
}
