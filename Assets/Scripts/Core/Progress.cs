using System.Collections.Generic;
using UnityEngine;

namespace FallingWizard.Core
{
    // What the wizard has learned, kept outside the wizard. Dying reloads the scene and builds a
    // brand new PlayerCharacter, so anything stored on the player would be forgotten on death -
    // which is the one thing a permanent spell must not do.
    //
    // This lasts the play session: it survives dying and walking into the next level, and it
    // starts empty every time you press Play. Swap the HashSet for PlayerPrefs the day the game
    // grows a save file, and nothing else has to change.
    public static class Progress
    {
        static readonly HashSet<string> Learned = new HashSet<string>();

        public static bool Knows(string key) => !string.IsNullOrEmpty(key) && Learned.Contains(key);

        public static void Learn(string key)
        {
            if (!string.IsNullOrEmpty(key))
                Learned.Add(key);
        }

        public static void Forget(string key) => Learned.Remove(key);

        public static void ForgetAll() => Learned.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Learned.Clear();
    }
}
