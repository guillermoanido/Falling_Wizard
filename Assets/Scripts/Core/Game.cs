using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallingWizard.Core
{
    public static class Game
    {
        public const string MainMenuScene = "Main Menu";
        public const string CutsceneScene = "Cutscene";
        public const string FirstLevelScene = "Level 1";

        public static bool IsPaused { get; private set; }

        public static void SetPaused(bool paused)
        {
            if (IsPaused == paused)
                return;

            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
        }

        public static void LoadMainMenu() => Load(MainMenuScene);

        public static void StartRun()
        {
            Progress.ClearCheckpoint();
            LoadFirstLevel();
        }

        public static void RestartRun()
        {
            Progress.EndRun();
            LoadFirstLevel();
        }

        public static void EraseProgress()
        {
            Progress.ForgetAll();
            LoadFirstLevel();
        }

        public static void LoadCutscene() => Load(CutsceneScene);

        public static void LoadFirstLevel() => Load(FirstLevelScene);

        public static void LoadLevel(string sceneName) => Load(sceneName);

        public static void ReloadCurrentScene() => Load(SceneManager.GetActiveScene().name);

        public static void Quit()
        {
            GameSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static void Load(string sceneName)
        {
            ClearPause();
            SceneManager.LoadScene(sceneName);
        }

        static void ClearPause()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => ClearPause();
    }
}
