using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RobotBombero.Simulation;
using UnityEngine;

namespace RobotBombero.Game
{
    /// <summary>
    /// Almacén local: profile.json y packs/{localId}.json bajo rootPath (en WebGL, persistentDataPath).
    /// Cada escritura va a un archivo temporal que luego sustituye al definitivo, así un fallo a medias
    /// no destruye lo que ya había. La clave del jugador no se escribe nunca en el log.
    /// </summary>
    public class LocalStore
    {
        private const string ProfileFile = "profile.json";
        private const string PacksDir = "packs";
        private const string TempSuffix = ".tmp";

        private readonly string _root;

        public LocalStore(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath)) throw new ArgumentException("rootPath vacío.", "rootPath");
            _root = rootPath;
        }

        public Profile LoadProfile()
        {
            string path = Path.Combine(_root, ProfileFile);
            if (!File.Exists(path)) return new Profile();
            try
            {
                var profile = JsonUtility.FromJson<Profile>(File.ReadAllText(path, Encoding.UTF8));
                if (profile != null) return profile;
            }
            catch (ArgumentException) { }
            catch (IOException) { }

            // Perfil ilegible: se conserva una copia para no perder la clave por un guardado posterior.
            Debug.LogWarning("RobotBombero: el perfil local es ilegible; se conserva una copia .corrupt.");
            try { File.Copy(path, path + ".corrupt", true); } catch (IOException) { }
            return new Profile();
        }

        public void SaveProfile(Profile profile)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            WriteAtomic(Path.Combine(_root, ProfileFile), JsonUtility.ToJson(profile));
        }

        public List<LocalPack> LoadPacks()
        {
            var result = new List<LocalPack>();
            string dir = Path.Combine(_root, PacksDir);
            if (!Directory.Exists(dir)) return result;

            string[] files = Directory.GetFiles(dir, "*.json");
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string file in files)
            {
                LocalPack pack = TryReadPack(file);
                if (pack == null)
                {
                    Debug.LogWarning("RobotBombero: se omite el pack ilegible " + Path.GetFileName(file));
                    continue;
                }
                result.Add(pack);
            }
            return result;
        }

        public void SavePack(LocalPack pack)
        {
            if (pack == null) throw new ArgumentNullException("pack");
            string path = PackPath(pack.localId);
            WriteAtomic(path, JsonUtility.ToJson(pack));
        }

        public void DeletePack(string localId)
        {
            string path = PackPath(localId);
            if (File.Exists(path)) File.Delete(path);
        }

        private string PackPath(string localId)
        {
            if (!IsSafeId(localId)) throw new ArgumentException("localId no válido.", "localId");
            return Path.Combine(Path.Combine(_root, PacksDir), localId + ".json");
        }

        private static bool IsSafeId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 64) return false;
            foreach (char c in id)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
                if (!ok) return false;
            }
            return true;
        }

        private static LocalPack TryReadPack(string file)
        {
            try
            {
                var pack = JsonUtility.FromJson<LocalPack>(File.ReadAllText(file, Encoding.UTF8));
                if (pack == null || !IsSafeId(pack.localId) || pack.content == null) return null;
                if (!PackValidator.Validate(pack.content).IsValid) return null;
                if (pack.stats == null) pack.stats = new PackStats();
                return pack;
            }
            catch (ArgumentException) { return null; }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        private static void WriteAtomic(string path, string content)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string temp = path + TempSuffix;
            File.WriteAllText(temp, content, new UTF8Encoding(false));
            if (File.Exists(path))
            {
                try
                {
                    File.Replace(temp, path, null);
                }
                catch (PlatformNotSupportedException)
                {
                    // Algunos sistemas de archivos (p. ej. el de WebGL) no implementan Replace.
                    File.Copy(temp, path, true);
                    File.Delete(temp);
                }
            }
            else
            {
                File.Move(temp, path);
            }
        }

        // En WebGL la plantilla activa autoSyncPersistentDataPath, que vuelca a IndexedDB sin llamadas extra.
    }
}
