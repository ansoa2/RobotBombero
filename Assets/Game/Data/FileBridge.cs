using System;
using System.IO;
using System.Text;
using RobotBombero.Simulation;
using UnityEngine;

namespace RobotBombero.Game
{
    /// <summary>
    /// Descarga y selección de archivos JSON con la misma API en todas las plataformas.
    /// Android: selector y guardado del sistema (NativeFilePicker). Windows: diálogos comdlg32 (WindowsFileDialog).
    /// Editor: EditorUtility. Otras plataformas: no disponible, se avisa por onError.
    /// </summary>
    public static class FileBridge
    {
        public const string ErrorCancelled = "cancelled";
        public const string ErrorTooLarge = "too_large";
        public const string ErrorUnavailable = "unavailable";
        public const string ErrorRead = "read_error";
        public const string ErrorWrite = "write_error";

        /// <summary>Verdadero donde hay selector y guardado de archivos.</summary>
        public static bool IsAvailable
        {
            get
            {
#if UNITY_EDITOR || UNITY_ANDROID || UNITY_STANDALONE_WIN
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Guarda el contenido como archivo. onDone se llama al guardarlo; onError (opcional) recibe un código
        /// (cancelled, write_error, unavailable). Cancelar el guardado no es un error de la aplicación.
        /// </summary>
        public static void Download(string fileName, string content, Action onDone, Action<string> onError = null)
        {
            if (string.IsNullOrEmpty(fileName)) fileName = "pack.json";
            if (content == null) content = "";
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.SaveFilePanel("Exportar pack", "", fileName, "json");
            SaveTo(path, content, onDone, onError);
#elif UNITY_STANDALONE_WIN
            SaveTo(WindowsFileDialog.Save("Exportar pack", fileName), content, onDone, onError);
#elif UNITY_ANDROID
            DownloadAndroid(fileName, content, onDone, onError);
#else
            if (onError != null) onError(ErrorUnavailable);
#endif
        }

        /// <summary>
        /// Abre el selector de archivos (cualquier archivo: el formato se decide por el contenido). onContent recibe el texto;
        /// onError (opcional) recibe un código (cancelled, too_large, read_error, unavailable). Nunca se lee un archivo mayor
        /// que PackLimits.MaxFileBytes.
        /// Se llama desde un clic normal del botón.
        /// </summary>
        public static void PickJson(Action<string> onContent, Action<string> onError = null)
        {
            if (onContent == null) throw new ArgumentNullException("onContent");
#if UNITY_EDITOR
            ReadFrom(UnityEditor.EditorUtility.OpenFilePanel("Importar pack", "", ""), onContent, onError);
#elif UNITY_STANDALONE_WIN
            ReadFrom(WindowsFileDialog.Open("Importar pack"), onContent, onError);
#elif UNITY_ANDROID
            SkipStoragePermissions();
            if (NativeFilePicker.IsFilePickerBusy()) { if (onError != null) onError(ErrorCancelled); return; }
            // "*/*": el archivo del juego antiguo no tiene extensión. NativeFilePicker entrega una copia en caché (o nulo si se cancela o se deniega).
            NativeFilePicker.PickFile(path => ReadFrom(path, onContent, onError, true), "*/*");
#else
            if (onError != null) onError(ErrorUnavailable);
#endif
        }

        /// <summary>Lee el archivo elegido con los mismos límites en todas las plataformas. Ruta vacía = cancelado.</summary>
        private static void ReadFrom(string path, Action<string> onContent, Action<string> onError, bool deleteAfter = false)
        {
            if (string.IsNullOrEmpty(path)) { if (onError != null) onError(ErrorCancelled); return; }
            string text;
            try
            {
                if (new FileInfo(path).Length > PackLimits.MaxFileBytes) { if (onError != null) onError(ErrorTooLarge); return; }
                text = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception e)
            {
                Debug.LogWarning("RobotBombero: no se pudo leer el archivo: " + e.Message);
                if (onError != null) onError(ErrorRead);
                return;
            }
            finally
            {
                if (deleteAfter) TryDelete(path);
            }
            onContent(text);
        }

        private static void SaveTo(string path, string content, Action onDone, Action<string> onError)
        {
            if (string.IsNullOrEmpty(path)) { if (onError != null) onError(ErrorCancelled); return; }
            try
            {
                File.WriteAllText(path, content, new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogWarning("RobotBombero: no se pudo guardar el archivo: " + e.Message);
                if (onError != null) onError(ErrorWrite);
                return;
            }
            if (onDone != null) onDone();
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); } catch (Exception) { }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // El selector y el guardado del sistema no necesitan permisos de almacenamiento: el complemento no debe pedirlos.
        private static void SkipStoragePermissions()
        {
            using (var picker = new AndroidJavaClass("com.yasirkula.unity.NativeFilePicker")) picker.SetStatic("PermissionFreeMode", true);
        }

        private static void DownloadAndroid(string fileName, string content, Action onDone, Action<string> onError)
        {
            SkipStoragePermissions();
            if (NativeFilePicker.IsFilePickerBusy() || !NativeFilePicker.CanExportFiles()) { if (onError != null) onError(ErrorUnavailable); return; }
            // El sistema ofrece el lugar donde guardar el archivo; se parte de una copia temporal con el nombre sugerido.
            string temp = Path.Combine(Application.temporaryCachePath, fileName);
            try
            {
                File.WriteAllText(temp, content, new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogWarning("RobotBombero: no se pudo preparar el archivo: " + e.Message);
                if (onError != null) onError(ErrorWrite);
                return;
            }
            // Falso significa cancelado o denegado: no se muestra ningún error.
            NativeFilePicker.ExportFile(temp, success =>
            {
                TryDelete(temp);
                if (success) { if (onDone != null) onDone(); }
                else if (onError != null) onError(ErrorCancelled);
            });
        }
#endif
    }
}
