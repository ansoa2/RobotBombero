using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RobotBombero.Simulation;
using UnityEngine;

namespace RobotBombero.Game
{
    /// <summary>
    /// Almacén local: profile.json y packs/{localId}.json bajo rootPath (persistentDataPath en todas las plataformas: archivos normales, nada propio de WebGL).
    /// Cada escritura va a un archivo temporal que luego sustituye al definitivo, así un fallo a medias
    /// no destruye lo que ya había. La clave del jugador no se escribe nunca en el log.
    /// </summary>
    public class LocalStore
    {
        private const string ProfileFile = "profile.json";
        private const string PacksDir = "packs";
        private const string TempSuffix = ".tmp";
        private const string PendingFile = "pending-deletes.json";
        private const string PendingFinishFile = "pending-finishes.json";

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
            catch (UnauthorizedAccessException) { }

            // Perfil ilegible: se conserva una copia para no perder la clave por un guardado posterior.
            Debug.LogWarning("RobotBombero: el perfil local es ilegible; se conserva una copia .corrupt.");
            try { File.Copy(path, path + ".corrupt", true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return new Profile();
        }

        public void SaveProfile(Profile profile)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            WriteAtomic(Path.Combine(_root, ProfileFile), JsonUtility.ToJson(profile));
        }

        [Serializable]
        private sealed class PendingDeletes
        {
            public long[] ids;
        }

        /// <summary>Ids de packs borrados en este dispositivo que el servidor aún puede conservar. Un archivo ilegible cuenta como vacío.</summary>
        public List<long> LoadPendingDeletes()
        {
            string path = Path.Combine(_root, PendingFile);
            var result = new List<long>();
            if (!File.Exists(path)) return result;
            try
            {
                var data = JsonUtility.FromJson<PendingDeletes>(File.ReadAllText(path, Encoding.UTF8));
                if (data != null && data.ids != null) result.AddRange(data.ids);
            }
            catch (ArgumentException) { }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return result;
        }

        public void SavePendingDeletes(List<long> ids)
        {
            if (ids == null) throw new ArgumentNullException("ids");
            WriteAtomic(Path.Combine(_root, PendingFile), JsonUtility.ToJson(new PendingDeletes { ids = ids.ToArray() }));
        }

        [Serializable]
        private sealed class PendingFinishes
        {
            public PendingFinish[] items;
        }

        /// <summary>Partidas clasificadas cuyo cierre aún no ha llegado al servidor. Un archivo ilegible cuenta como vacío.</summary>
        public List<PendingFinish> LoadPendingFinishes()
        {
            string path = Path.Combine(_root, PendingFinishFile);
            var result = new List<PendingFinish>();
            if (!File.Exists(path)) return result;
            try
            {
                var data = JsonUtility.FromJson<PendingFinishes>(File.ReadAllText(path, Encoding.UTF8));
                if (data != null && data.items != null)
                {
                    foreach (PendingFinish item in data.items)
                        if (item != null && item.gameId > 0) result.Add(item);
                }
            }
            catch (ArgumentException) { }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return result;
        }

        public void SavePendingFinishes(List<PendingFinish> items)
        {
            if (items == null) throw new ArgumentNullException("items");
            WriteAtomic(Path.Combine(_root, PendingFinishFile), JsonUtility.ToJson(new PendingFinishes { items = items.ToArray() }));
        }

        /// <summary>Anota un cierre pendiente (una vez por partida).</summary>
        public void AddPendingFinish(PendingFinish item)
        {
            if (item == null) throw new ArgumentNullException("item");
            List<PendingFinish> items = LoadPendingFinishes();
            if (items.Exists(p => p.gameId == item.gameId)) return;
            items.Add(item);
            SavePendingFinishes(items);
        }

        public void RemovePendingFinish(long gameId)
        {
            List<PendingFinish> items = LoadPendingFinishes();
            if (items.RemoveAll(p => p.gameId == gameId) > 0) SavePendingFinishes(items);
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
