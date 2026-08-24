using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallingWizard.Core
{
    public static class Progress
    {
        const string Prefix = "FallingWizard.";
        const string SceneKey = Prefix + "Scene";
        const string PointKey = Prefix + "Point";
        const string SpellsKey = Prefix + "Spells";
        const char Separator = ';';

        static readonly HashSet<string> learned = new HashSet<string>();
        static readonly HashSet<string> banked = new HashSet<string>();

        public static bool HasCheckpoint { get; private set; }
        public static Vector2 CheckpointPoint { get; private set; }
        public static string CheckpointScene { get; private set; } = string.Empty;

        public static bool CheckpointIsHere =>
            HasCheckpoint && CheckpointScene == SceneManager.GetActiveScene().name;

        public static bool Knows(string key) => !string.IsNullOrEmpty(key) && learned.Contains(key);

        public static void Learn(string key)
        {
            if (!string.IsNullOrEmpty(key))
                learned.Add(key);
        }

        public static void Forget(string key) => learned.Remove(key);

        public static void MarkCheckpoint(Vector2 point)
        {
            banked.Clear();
            banked.UnionWith(learned);

            CheckpointPoint = point;
            CheckpointScene = SceneManager.GetActiveScene().name;
            HasCheckpoint = true;

            Save();
        }

        public static void Rewind()
        {
            learned.Clear();
            learned.UnionWith(banked);
        }

        public static void ForgetAll()
        {
            learned.Clear();
            banked.Clear();

            HasCheckpoint = false;
            CheckpointScene = string.Empty;
            CheckpointPoint = Vector2.zero;

            DeleteSave();
        }

        public static bool HasSave => PlayerPrefs.HasKey(SceneKey);

        public static void Save()
        {
            if (!HasCheckpoint)
                return;

            PlayerPrefs.SetString(SceneKey, CheckpointScene);
            PlayerPrefs.SetString(PointKey, $"{CheckpointPoint.x}{Separator}{CheckpointPoint.y}");
            PlayerPrefs.SetString(SpellsKey, string.Join(Separator.ToString(), banked));
            PlayerPrefs.Save();
        }

        public static bool Load()
        {
            if (!HasSave)
                return false;

            string[] point = PlayerPrefs.GetString(PointKey, string.Empty).Split(Separator);

            if (point.Length != 2 ||
                !float.TryParse(point[0], out float x) ||
                !float.TryParse(point[1], out float y))
                return false;

            CheckpointScene = PlayerPrefs.GetString(SceneKey, string.Empty);
            CheckpointPoint = new Vector2(x, y);
            HasCheckpoint = !string.IsNullOrEmpty(CheckpointScene);

            banked.Clear();

            foreach (string key in PlayerPrefs.GetString(SpellsKey, string.Empty).Split(Separator))
                if (!string.IsNullOrEmpty(key))
                    banked.Add(key);

            learned.Clear();
            learned.UnionWith(banked);

            return HasCheckpoint;
        }

        public static void DeleteSave()
        {
            PlayerPrefs.DeleteKey(SceneKey);
            PlayerPrefs.DeleteKey(PointKey);
            PlayerPrefs.DeleteKey(SpellsKey);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            learned.Clear();
            banked.Clear();

            HasCheckpoint = false;
            CheckpointScene = string.Empty;
            CheckpointPoint = Vector2.zero;
        }
    }
}
