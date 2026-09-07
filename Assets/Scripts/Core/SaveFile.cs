using System;
using System.IO;
using UnityEngine;

namespace FallingWizard.Core
{
    public enum SaveRead
    {
        Missing,
        Loaded,
        Unreadable,
    }

    public static class SaveFile
    {
        public const string FolderName = "Saves";

        const string TempExtension = ".tmp";
        const string BackupExtension = ".bak";
        const string CorruptExtension = ".corrupt";
        const string ProbeName = "write.probe";

        static string folder;
        static bool announced;

        public static string Folder => folder ??= Resolve();

        public static string PathFor(string fileName) => Path.Combine(Folder, fileName);

        public static void Announce()
        {
            if (announced)
                return;

            announced = true;
            Debug.Log($"Falling Wizard saves live in {Folder}");
        }

        public static bool Exists(string fileName)
        {
            try
            {
                return File.Exists(PathFor(fileName));
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Could not look for the save file '{fileName}' in {Folder}, so " +
                                 $"the game will act as though there is no save there. {error.Message}");
                return false;
            }
        }

        public static SaveRead Read<T>(string fileName, out T data) where T : class
        {
            data = null;

            string path = PathFor(fileName);
            string text;

            try
            {
                if (!File.Exists(path))
                    return SaveRead.Missing;

                text = File.ReadAllText(path);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"The save file at {path} exists but could not be opened, so " +
                                 $"nothing will be written over it this session and the game has " +
                                 $"started from a blank save. Close whatever is holding the file " +
                                 $"and relaunch. {error.Message}");
                return SaveRead.Unreadable;
            }

            if (string.IsNullOrWhiteSpace(text))
                return SaveRead.Missing;

            try
            {
                data = JsonUtility.FromJson<T>(text);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"The save file at {path} is not readable JSON. {error.Message}");
                data = null;
            }

            if (data != null)
                return SaveRead.Loaded;

            Quarantine(path);
            return SaveRead.Missing;
        }

        public static bool Write<T>(string fileName, T data) where T : class
        {
            if (data == null)
            {
                Debug.LogWarning($"Refusing to write nothing to '{fileName}'. Something asked to " +
                                 "save before it had anything to save, which would have emptied the file.");
                return false;
            }

            string path = PathFor(fileName);
            string temp = path + TempExtension;

            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));

                if (File.Exists(path))
                    ReplaceOrMove(temp, path);
                else
                    File.Move(temp, path);

                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Could not save to {path}, so anything earned this session will " +
                                 $"be gone when the game closes. {error.Message}");
                TryDelete(temp);
                return false;
            }
        }

        public static bool Delete(string fileName)
        {
            string path = PathFor(fileName);

            try
            {
                File.Delete(path);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Could not delete the save file at {path}, so the progress it " +
                                 $"holds will come back on the next launch. {error.Message}");
                return false;
            }
        }

        static void ReplaceOrMove(string temp, string path)
        {
            try
            {
                File.Replace(temp, path, null);
                return;
            }
            catch (Exception)
            {
            }

            string backup = path + BackupExtension;

            TryDelete(backup);
            File.Move(path, backup);

            try
            {
                File.Move(temp, path);
            }
            catch (Exception)
            {
                File.Move(backup, path);
                throw;
            }

            TryDelete(backup);
        }

        static void Quarantine(string path)
        {
            string kept = path + CorruptExtension;

            try
            {
                TryDelete(kept);
                File.Move(path, kept);

                Debug.LogWarning($"The save file at {path} could not be understood, so it has been " +
                                 $"set aside as {kept} and the game has started from a blank save. " +
                                 "Open that file if you want to see what went wrong, then delete it.");
            }
            catch (Exception error)
            {
                Debug.LogWarning($"The save file at {path} could not be understood and could not " +
                                 $"be moved aside either. It will be written over. {error.Message}");
            }
        }

        static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception)
            {
            }
        }

        static string Resolve()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", FolderName));
#else
            if (Application.platform != RuntimePlatform.OSXPlayer)
            {
                string beside = Path.GetFullPath(Path.Combine(Application.dataPath, "..", FolderName));

                if (IsWritable(beside))
                    return beside;

                Debug.LogWarning($"Cannot write to {beside} - the game is installed somewhere " +
                                 "read-only, which usually means Program Files. Saving to the " +
                                 "operating system's save folder instead.");
            }

            return Path.GetFullPath(Path.Combine(Application.persistentDataPath, FolderName));
#endif
        }

        static bool IsWritable(string directory)
        {
            string probe = Path.Combine(directory, ProbeName);

            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            folder = null;
            announced = false;
        }
    }
}
