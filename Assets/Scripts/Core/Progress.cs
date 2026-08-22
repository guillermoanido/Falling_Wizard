using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallingWizard.Core
{
    // What the wizard has learned and where they came back from, kept outside the wizard. Dying
    // reloads the scene and builds a brand new PlayerCharacter, so anything stored on the player
    // would be forgotten on death - which is the one thing progress must not do.
    //
    // Two sets, and the difference between them is the whole checkpoint system:
    //   learned  what the wizard knows right now
    //   banked   what they knew when they last touched a checkpoint
    // Reaching a checkpoint copies learned into banked. Dying copies banked back over learned, so
    // a spell picked up after the checkpoint is lost - and the shrine that granted it comes back.
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

        // Is the wizard due to come back here, in the scene that is loaded right now?
        public static bool CheckpointIsHere =>
            HasCheckpoint && CheckpointScene == SceneManager.GetActiveScene().name;

        public static bool Knows(string key) => !string.IsNullOrEmpty(key) && learned.Contains(key);

        public static void Learn(string key)
        {
            if (!string.IsNullOrEmpty(key))
                learned.Add(key);
        }

        public static void Forget(string key) => learned.Remove(key);

        // Reached a checkpoint: bank everything known so far, and remember the way back.
        public static void MarkCheckpoint(Vector2 point)
        {
            banked.Clear();
            banked.UnionWith(learned);

            CheckpointPoint = point;
            CheckpointScene = SceneManager.GetActiveScene().name;
            HasCheckpoint = true;

            Save();
        }

        // Died. Roll back to the last checkpoint. Anything learned since is gone.
        public static void Rewind()
        {
            learned.Clear();
            learned.UnionWith(banked);
        }

        // A new game. Everything goes, including the save on disk.
        public static void ForgetAll()
        {
            learned.Clear();
            banked.Clear();

            HasCheckpoint = false;
            CheckpointScene = string.Empty;
            CheckpointPoint = Vector2.zero;

            DeleteSave();
        }

        // ------------------------------------------------------------------------ the save ----
        // Written at every checkpoint. Nothing reads it back on its own: Load is there for a
        // Continue button, so pressing Play in the editor always starts you where you are rather
        // than teleporting you to wherever you last got to.

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

        // Statics outlive a scene load, which is exactly what makes this work - but they also
        // outlive leaving play mode when domain reloading is off.
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
