using System;
using System.IO;
using UnityEngine;

namespace FallingWizard.Core
{
    // What Read() found on disk. THREE answers, not two, and the third one is the point:
    // "there is nothing saved" and "there IS a save and I could not open it" must never lead to
    // the same place. If an unreadable file reads back as an empty one, the game starts with a
    // blank purse and the very next Save() - which fires on every purchase, every equip, every
    // banked wisp - writes that blank straight over the real save. Unreadable is the answer that
    // tells a caller to keep its hands off the file for the rest of the session.
    public enum SaveRead
    {
        Missing,
        Loaded,
        Unreadable,
    }

    // Every value the game remembers between launches goes through here, as pretty-printed JSON
    // in a folder you can open, read and diff. It replaces PlayerPrefs, which on Windows is a
    // handful of registry values under HKCU\Software\DefaultCompany\Falling Wizard: nothing can
    // diff them, no repo can track them, and you cannot hand one to somebody to reproduce a bug.
    public static class SaveFile
    {
        public const string FolderName = "Saves";

        const string TempExtension = ".tmp";
        const string BackupExtension = ".bak";
        const string CorruptExtension = ".corrupt";
        const string ProbeName = "write.probe";

        static string folder;
        static bool announced;

        // The folder every save file lives in, worked out once and remembered. In the editor it
        // is the project folder next to Assets, so the file lands in the repo where you can see
        // it; in a build it is next to the executable when that is writable, and the operating
        // system's save folder when it is not. Log it at startup with Announce().
        public static string Folder => folder ??= Resolve();

        public static string PathFor(string fileName) => Path.Combine(Folder, fileName);

        // Says where the saves are, once per play session. A path in the Console beats hunting
        // for it, and it is the first thing you want when somebody reports lost progress.
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
                // NOT Missing. The file is sitting right there and we could not open it -
                // antivirus holding it, a cloud sync client half way through copying it, a
                // permission that changed. Reporting "nothing saved" here is precisely how a
                // real save gets erased by the next write.
                Debug.LogWarning($"The save file at {path} exists but could not be opened, so " +
                                 $"nothing will be written over it this session and the game has " +
                                 $"started from a blank save. Close whatever is holding the file " +
                                 $"and relaunch. {error.Message}");
                return SaveRead.Unreadable;
            }

            // Nothing in it, so there is nothing worth setting aside - and quarantining an empty
            // file would leave a .corrupt beside the save every time one was created and not yet
            // written to.
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

            // Opened fine, made no sense. Whatever was in it is already gone, so keep the bytes
            // where somebody can look at them and let the game start fresh - unlike the case
            // above, there is nothing left here that a write could destroy.
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

                // Write to a scratch file and swap it in; never write over the live save. A crash,
                // an Alt+F4 or a pulled power lead half way through WriteAllText leaves a truncated
                // file that parses as nothing at all - which is to say, a wiped save. This way the
                // old save stays whole right up to the instant the new one is complete.
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
                // File.Replace is the properly atomic one, and it is refused outright on some
                // file systems - a network share, a Dropbox or OneDrive folder, a FAT stick. Fall
                // through and do it the long way.
            }

            // Rename the live save out of the way; NEVER delete it first. Deleting first is how
            // the fallback that exists to protect the save becomes the thing that eats it: if the
            // Move below throws - antivirus grabbing the freed name, a sync client mid-copy - the
            // old file is already gone, and Write's cleanup then deletes the scratch file as
            // well, leaving nothing at all on disk.
            string backup = path + BackupExtension;

            TryDelete(backup);
            File.Move(path, backup);

            try
            {
                File.Move(temp, path);
            }
            catch (Exception)
            {
                // Put it back and let the caller report the failure. A save one write out of
                // date beats no save at all.
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
                // Nothing to do about it and nothing worth saying: this only ever runs as tidying
                // up after a failure that has already been reported.
            }
        }

        static string Resolve()
        {
#if UNITY_EDITOR
            // Application.dataPath is <project>/Assets in the editor, so one level up is the
            // project folder itself - the folder git has checked out, where you will actually
            // find the file. Beside Assets and never inside it: a file inside Assets gets an
            // imported .meta, shows up in the Project window, and ships inside the built game.
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", FolderName));
#else
            // In a Windows or Linux player Application.dataPath is <exe folder>/<Product>_Data,
            // so one level up is the folder the player double-clicks - "the game files" as far as
            // anyone playing is concerned. A macOS .app puts dataPath INSIDE the bundle, and
            // writing into a signed bundle breaks its signature, so that platform goes straight
            // to the operating system's save folder.
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

        // Asks the file system rather than guessing from the path. Whether a folder is writable
        // depends on where the game was installed, which account is running it and whether the
        // launcher is elevated, and no amount of reading the path can tell you any of that.
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
            // Statics survive pressing Play when domain reloading is off, and a build made from a
            // moved project would otherwise keep pointing at yesterday's folder.
            folder = null;
            announced = false;
        }
    }
}
