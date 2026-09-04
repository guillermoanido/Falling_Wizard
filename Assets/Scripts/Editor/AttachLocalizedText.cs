#if UNITY_EDITOR
using System.Collections.Generic;
using FallingWizard.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallingWizard.EditorTools
{
    // A one-shot job: put a LocalizedText on every label that was typed into the Main Menu scene
    // and the Pause Menu prefab, so the menus follow the chosen language.
    //
    // This is a menu command rather than hand-written YAML because a prefab that names a script
    // guid Unity has never imported loads as a missing component - and because there are twenty
    // of these labels across two files, which is exactly the sort of counting a person should not
    // be doing by hand.
    //
    // Labels are matched on the English they currently show. That makes the mapping below
    // readable as a table of what the player sees, and it means running the tool twice is
    // harmless: a label that already has a LocalizedText is left alone.
    //
    // Placeholders the code overwrites - the volume percentage, the dropdown's current value -
    // are deliberately NOT in this table. A key on one of those makes the translation and the
    // code fight over the same words every frame.
    public static class AttachLocalizedText
    {
        const string MainMenuScene = "Assets/Scenes/Main Menu.unity";
        const string PauseMenuPrefab = "Assets/Prefabs/Pause Menu.prefab";

        // What the label reads today -> the key it should read from.
        static readonly Dictionary<string, string> Keys = new Dictionary<string, string>
        {
            { "Falling Wizard", "menu.title" },
            { "Play", "menu.play" },
            { "Exit", "menu.exit" },
            { "Paused", "pause.title" },
            { "Resume", "pause.resume" },
            { "Main Menu", "pause.mainMenu" },
            { "Quit", "pause.quit" },
            { "Settings", "settings.title" },
            { "Resolution", "settings.resolution" },
            { "Fullscreen", "settings.fullscreen" },
            { "Volume", "settings.volume" },
            { "Back", "settings.back" },
        };

        [MenuItem("Falling Wizard/Attach Localized Text To Menus")]
        static void Attach()
        {
            int done = AttachInPrefab() + AttachInScene();

            Debug.Log($"Localized Text: {done} label(s) wired up. Anything already carrying one " +
                      "was left alone, so this is safe to run again. The language dropdown row " +
                      "itself still has to be added by hand - see the Settings Panel component.");
        }

        static int AttachInPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PauseMenuPrefab);

            if (root == null)
            {
                Debug.LogWarning($"No prefab at {PauseMenuPrefab}, so its labels were skipped.");
                return 0;
            }

            int done = Wire(root);

            if (done > 0)
                PrefabUtility.SaveAsPrefabAsset(root, PauseMenuPrefab);

            PrefabUtility.UnloadPrefabContents(root);
            return done;
        }

        static int AttachInScene()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScene, OpenSceneMode.Additive);

            if (!scene.IsValid())
            {
                Debug.LogWarning($"No scene at {MainMenuScene}, so its labels were skipped.");
                return 0;
            }

            int done = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
                done += Wire(root);

            if (done > 0)
                EditorSceneManager.SaveScene(scene);

            EditorSceneManager.CloseScene(scene, true);
            return done;
        }

        static int Wire(GameObject root)
        {
            int done = 0;

            // Include inactive: the settings panel is switched off in both rigs until it is
            // opened, and every one of its labels lives underneath it.
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.GetComponent<LocalizedText>() != null)
                    continue;

                if (!Keys.TryGetValue(label.text.Trim(), out string key))
                    continue;

                label.gameObject.AddComponent<LocalizedText>().key = key;
                done++;
            }

            return done;
        }
    }
}
#endif
