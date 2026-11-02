#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;

namespace RobotBombero.Game
{
    /// <summary>
    /// Diálogos Abrir y Guardar de Windows (comdlg32, versión Unicode). Las cadenas se pasan como memoria no administrada
    /// y se leen como UTF-16, de modo que las rutas con tildes, ñ o emojis llegan intactas. Bloquea el hilo hasta cerrar el diálogo.
    /// </summary>
    internal static class WindowsFileDialog
    {
        private const int OFN_OVERWRITEPROMPT = 0x00000002;
        private const int OFN_NOCHANGEDIR = 0x00000008;
        private const int OFN_PATHMUSTEXIST = 0x00000800;
        private const int OFN_FILEMUSTEXIST = 0x00001000;
        private const int OFN_EXPLORER = 0x00080000;
        private const int BufferChars = 32768;

        // OPENFILENAMEW. Todas las cadenas son IntPtr a memoria UTF-16 propia.
        [StructLayout(LayoutKind.Sequential)]
        private struct OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public IntPtr lpstrFilter;
            public IntPtr lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public IntPtr lpstrFileTitle;
            public int nMaxFileTitle;
            public IntPtr lpstrInitialDir;
            public IntPtr lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public IntPtr lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public IntPtr lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetOpenFileName(ref OpenFileName ofn);

        [DllImport("comdlg32.dll", EntryPoint = "GetSaveFileNameW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetSaveFileName(ref OpenFileName ofn);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        /// <summary>Ruta elegida, o nulo si se cancela.</summary>
        public static string Open(string title)
        {
            return Show(false, title, "");
        }

        /// <summary>Ruta elegida (con la extensión .json si no la lleva), o nulo si se cancela.</summary>
        public static string Save(string title, string suggestedName)
        {
            return Show(true, title, suggestedName ?? "");
        }

        private static string Show(bool save, string title, string initialName)
        {
            IntPtr filter = Marshal.StringToHGlobalUni("Packs de reglas (*.json)\0*.json\0Todos los archivos (*.*)\0*.*\0\0");
            IntPtr titlePtr = Marshal.StringToHGlobalUni(title);
            IntPtr defExt = Marshal.StringToHGlobalUni("json");
            IntPtr file = Marshal.AllocHGlobal(BufferChars * 2);
            try
            {
                // Búfer inicial: nombre sugerido y terminador; el resto a cero.
                for (int i = 0; i < BufferChars; i++) Marshal.WriteInt16(file, i * 2, 0);
                int n = Math.Min(initialName.Length, 255);
                for (int i = 0; i < n; i++) Marshal.WriteInt16(file, i * 2, initialName[i]);

                var ofn = new OpenFileName
                {
                    lStructSize = Marshal.SizeOf(typeof(OpenFileName)),
                    hwndOwner = GetActiveWindow(),
                    lpstrFilter = filter,
                    nFilterIndex = save ? 1 : 2,
                    lpstrFile = file,
                    nMaxFile = BufferChars,
                    lpstrTitle = titlePtr,
                    lpstrDefExt = save ? defExt : IntPtr.Zero,
                    Flags = OFN_EXPLORER | OFN_NOCHANGEDIR | OFN_PATHMUSTEXIST | (save ? OFN_OVERWRITEPROMPT : OFN_FILEMUSTEXIST)
                };
                bool ok = save ? GetSaveFileName(ref ofn) : GetOpenFileName(ref ofn);
                return ok ? Marshal.PtrToStringUni(file) : null;
            }
            finally
            {
                Marshal.FreeHGlobal(filter);
                Marshal.FreeHGlobal(titlePtr);
                Marshal.FreeHGlobal(defExt);
                Marshal.FreeHGlobal(file);
            }
        }
    }
}
#endif
