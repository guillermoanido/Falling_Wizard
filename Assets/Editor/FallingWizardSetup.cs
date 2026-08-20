using System.Collections.Generic;
using FallingWizard.Core;
using FallingWizard.Menus;
using FallingWizard.Player;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FallingWizard.EditorTools
{
    static class FallingWizardSetup
    {
        const string PrefabFolder = "Assets/Prefabs";
        const string PauseMenuPrefabPath = PrefabFolder + "/Pause Menu.prefab";

        [MenuItem("Tools/Falling Wizard/Build Main Menu In Open Scene", false, 10)]
        static void BuildMainMenu()
        {
            if (!EnsureTextMeshProResources())
                return;

            EnsureEventSystem();

            GameObject canvas = UiFactory.CreateCanvas("Menu Canvas", 0);
            Undo.RegisterCreatedObjectUndo(canvas, "Build Main Menu");

            GameObject mainPanel = UiFactory.CreatePanel("Main Panel", canvas.transform);
            UiFactory.CreateLabel("Falling Wizard", mainPanel.transform, 86f, 1000f, 130f);
            Button play = UiFactory.CreateButton("Play", mainPanel.transform);
            Button settings = UiFactory.CreateButton("Settings", mainPanel.transform);
            Button exit = UiFactory.CreateButton("Exit", mainPanel.transform);

            SettingsPanel settingsPanel = BuildSettingsPanel(canvas.transform);

            var controller = canvas.AddComponent<MainMenuController>();
            Wire(controller,
                ("mainPanel", mainPanel),
                ("settingsPanel", settingsPanel),
                ("playButton", play),
                ("settingsButton", settings),
                ("exitButton", exit));

            Selection.activeGameObject = canvas;
            MarkSceneDirty();
            Debug.Log("Main menu built. Save the scene to keep it.");
        }

        [MenuItem("Tools/Falling Wizard/Create Pause Menu Prefab", false, 20)]
        static void CreatePauseMenuPrefab()
        {
            if (!EnsureTextMeshProResources())
                return;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PauseMenuPrefabPath) != null &&
                !EditorUtility.DisplayDialog("Replace pause menu prefab?",
                    PauseMenuPrefabPath + " already exists. Rebuilding it throws away any changes you made.",
                    "Replace", "Cancel"))
                return;

            GameObject canvas = UiFactory.CreateCanvas("Pause Menu", 100);

            GameObject pausePanel = UiFactory.CreatePanel("Pause Panel", canvas.transform);
            UiFactory.CreateLabel("Paused", pausePanel.transform, 68f, 1000f, 110f);
            Button resume = UiFactory.CreateButton("Resume", pausePanel.transform);
            Button settings = UiFactory.CreateButton("Settings", pausePanel.transform);
            Button mainMenu = UiFactory.CreateButton("Main Menu", pausePanel.transform);
            Button quit = UiFactory.CreateButton("Quit", pausePanel.transform);

            SettingsPanel settingsPanel = BuildSettingsPanel(canvas.transform);

            var controller = canvas.AddComponent<PauseMenuController>();
            Wire(controller,
                ("pausePanel", pausePanel),
                ("settingsPanel", settingsPanel),
                ("resumeButton", resume),
                ("settingsButton", settings),
                ("mainMenuButton", mainMenu),
                ("quitButton", quit));

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            PrefabUtility.SaveAsPrefabAsset(canvas, PauseMenuPrefabPath);
            Object.DestroyImmediate(canvas);

            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PauseMenuPrefabPath);
            Debug.Log("Pause menu prefab saved to " + PauseMenuPrefabPath);
        }

        [MenuItem("Tools/Falling Wizard/Add Pause Menu To Open Scene", false, 21)]
        static void AddPauseMenuToScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PauseMenuPrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("No pause menu prefab",
                    "Run Tools > Falling Wizard > Create Pause Menu Prefab first.", "OK");
                return;
            }

            EnsureEventSystem();

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Add Pause Menu");
            Selection.activeGameObject = instance;
            MarkSceneDirty();
        }

        [MenuItem("Tools/Falling Wizard/Create Player In Open Scene", false, 30)]
        static void CreatePlayer()
        {
            var root = new GameObject("Wizard");
            root.layer = LayerOrDefault("Player");
            Undo.RegisterCreatedObjectUndo(root, "Create Player");

            var body = root.AddComponent<Rigidbody2D>();
            body.freezeRotation = true;
            body.gravityScale = 3f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = root.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.8f, 1.6f);

            root.AddComponent<PlayerInputReader>();
            root.AddComponent<PlayerPowerUps>();
            root.AddComponent<Health>();
            var motor = root.AddComponent<PlayerMotor>();
            root.AddComponent<FallDamage>();
            root.AddComponent<PlayerCharacter>();

            // AddComponent does not run Reset, so the ground check is set up here instead.
            var serialized = new SerializedObject(motor);
            serialized.FindProperty("groundLayers").intValue = LayerMask.GetMask("Ground");
            serialized.FindProperty("groundCheckOffset").vector2Value = new Vector2(0f, -0.85f);
            serialized.FindProperty("groundCheckSize").vector2Value = new Vector2(0.72f, 0.1f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            CreateSpriteBox("Visual", root.transform, new Vector2(0.8f, 1.6f),
                new Color(0.58f, 0.45f, 0.88f), sortingOrder: 1);

            root.transform.position = new Vector3(0f, 1f, 0f);
            Selection.activeGameObject = root;
            MarkSceneDirty();
        }

        [MenuItem("Tools/Falling Wizard/Create Ground Platform In Open Scene", false, 31)]
        static void CreateGroundPlatform()
        {
            var platform = new GameObject("Platform");
            platform.layer = LayerOrDefault("Ground");
            Undo.RegisterCreatedObjectUndo(platform, "Create Platform");

            var size = new Vector2(10f, 1f);
            platform.AddComponent<BoxCollider2D>().size = size;
            CreateSpriteBox("Visual", platform.transform, size, new Color(0.24f, 0.22f, 0.30f), sortingOrder: 0);

            Selection.activeGameObject = platform;
            MarkSceneDirty();
        }

        [MenuItem("Tools/Falling Wizard/Add Game Scenes To Build Settings", false, 40)]
        static void ConfigureBuildScenes()
        {
            string[] wanted =
            {
                "Assets/Scenes/" + GameScenes.MainMenu + ".unity",
                "Assets/Scenes/" + GameScenes.Cutscene + ".unity",
                "Assets/Scenes/" + GameScenes.Level1 + ".unity",
            };

            var scenes = new List<EditorBuildSettingsScene>();
            foreach (string path in wanted)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    Debug.LogWarning("Scene not found, skipping: " + path);
                    continue;
                }

                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"Build Settings now lists {scenes.Count} scene(s), starting with the main menu.");
        }

        static SettingsPanel BuildSettingsPanel(Transform parent)
        {
            GameObject panel = UiFactory.CreatePanel("Settings Panel", parent);
            UiFactory.CreateLabel("Settings", panel.transform, 68f, 1000f, 110f);

            GameObject resolutionRow = UiFactory.CreateRow("Resolution", panel.transform);
            TMP_Dropdown resolution = UiFactory.CreateDropdown(resolutionRow.transform);

            GameObject fullscreenRow = UiFactory.CreateRow("Fullscreen", panel.transform);
            Toggle fullscreen = UiFactory.CreateToggle(fullscreenRow.transform);

            GameObject qualityRow = UiFactory.CreateRow("Quality", panel.transform);
            TMP_Dropdown quality = UiFactory.CreateDropdown(qualityRow.transform);

            GameObject volumeRow = UiFactory.CreateRow("Volume", panel.transform);
            Slider volume = UiFactory.CreateSlider(volumeRow.transform);
            TextMeshProUGUI volumeValue =
                UiFactory.CreateLabel("100%", volumeRow.transform, 26f, 100f, 44f);
            volumeValue.gameObject.name = "Volume Value";

            Button back = UiFactory.CreateButton("Back", panel.transform);

            var settingsPanel = panel.AddComponent<SettingsPanel>();
            Wire(settingsPanel,
                ("resolutionDropdown", resolution),
                ("qualityDropdown", quality),
                ("fullscreenToggle", fullscreen),
                ("volumeSlider", volume),
                ("volumeValueLabel", volumeValue),
                ("backButton", back));

            WireArray(settingsPanel, "desktopOnlyRows", new Object[] { resolutionRow, fullscreenRow });

            panel.SetActive(false);
            return settingsPanel;
        }

        static void CreateSpriteBox(string name, Transform parent, Vector2 size, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            renderer.drawMode = SpriteDrawMode.Sliced;   // lets the sprite be sized in world units
            renderer.size = size;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        static bool EnsureTextMeshProResources()
        {
            if (TMP_Settings.instance != null)
                return true;

            bool import = EditorUtility.DisplayDialog("TextMeshPro resources needed",
                "The menus use TextMeshPro, which needs its essential resources imported once per project.\n\n" +
                "Import them now, then run this command again.", "Import", "Cancel");

            if (import)
                EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources");

            return false;
        }

        static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
                return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        }

        static int LayerOrDefault(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
                return layer;

            Debug.LogWarning($"Layer '{layerName}' does not exist. Add it in Project Settings > Tags and Layers.");
            return 0;
        }

        static void Wire(Object target, params (string field, Object value)[] links)
        {
            var serialized = new SerializedObject(target);

            foreach ((string field, Object value) in links)
            {
                SerializedProperty property = serialized.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError($"{target.GetType().Name} has no field called '{field}'.");
                    continue;
                }

                property.objectReferenceValue = value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireArray(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            property.arraySize = values.Length;

            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void MarkSceneDirty() => EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
