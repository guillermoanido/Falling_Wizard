using System;
using UnityEngine;

namespace FallingWizard.Core
{
    public static class GamePause
    {
        public static bool IsPaused { get; private set; }

        public static event Action<bool> Changed;

        public static void SetPaused(bool paused)
        {
            if (IsPaused == paused)
                return;

            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            Changed?.Invoke(paused);
        }

        public static void Clear()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Changed?.Invoke(false);
        }

        // Static fields survive "Enter Play Mode (no domain reload)", so wipe them on every play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            IsPaused = false;
            Changed = null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }
}
