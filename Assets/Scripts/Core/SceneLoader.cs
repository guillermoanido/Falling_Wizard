using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallingWizard.Core
{
    public static class SceneLoader
    {
        public static void LoadMainMenu() => Load(GameScenes.MainMenu);

        // Play button. Change this to LoadCutscene() once the intro exists and the cutscene
        // will play first, then hand off to level 1 by itself.
        public static void StartNewGame() => LoadFirstLevel();

        public static void LoadCutscene() => Load(GameScenes.Cutscene);

        public static void LoadFirstLevel() => Load(GameScenes.Level1);

        public static void ReloadCurrentScene() => Load(SceneManager.GetActiveScene().name);

        public static void QuitGame()
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
            GamePause.Clear();
            SceneManager.LoadScene(sceneName);
        }
    }
}
