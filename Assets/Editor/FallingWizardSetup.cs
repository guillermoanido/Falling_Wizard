using System.Collections.Generic;
using FallingWizard.Core;
using FallingWizard.Menus;
using FallingWizard.Player;
using FallingWizard.World;
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
        const string AssetsFolder = "Assets";
        const string PrefabFolderName = "Prefabs";
        const string PrefabFolder = AssetsFolder + "/" + PrefabFolderName;
        const string PauseMenuPrefabPath = PrefabFolder + "/Pause Menu.prefab";
        const string ScenesFolder = AssetsFolder + "/Scenes";
        const string SceneExtension = ".unity";

        const string GroundLayerName = "Ground";
        const string PlayerLayerName = "Player";

        const string BoxSpritePath = "UI/Skin/UISprite.psd";

        const int MenuCanvasSortingOrder = 0;
        const int PauseCanvasSortingOrder = 100;

        static readonly Vector2 PlayerSize = new Vector2(0.8f, 1.6f);
        static readonly Vector3 PlayerSpawnPosition = new Vector3(0f, 1f, 0f);
        static readonly Color PlayerColor = new Color(0.58f, 0.45f, 0.88f);
        const float PlayerGravityScale = 3f;
        const int PlayerSortingOrder = 1;

        const float StaffLength = 2.5f;
        const float StaffWidth = 0.12f;
        static readonly Color StaffColor = new Color(0.55f, 0.38f, 0.20f);
        const int StaffSortingOrder = 2;

        static readonly Vector2 PlatformSize = new Vector2(10f, 1f);
        static readonly Color PlatformColor = new Color(0.24f, 0.22f, 0.30f);
        static readonly Color RoughColor = new Color(0.42f, 0.29f, 0.22f);
        const int PlatformSortingOrder = 0;

        const int StairStepCount = 5;
        const float StairStepWidth = 0.7f;
        const float StairStepHeight = 0.5f;
        const float StairStartX = -3f;

        const float GroundCheckSkin = 0.05f;
        const float GroundCheckThickness = 0.1f;
        const float GroundCheckWidthFactor = 0.9f;

        [MenuItem("Tools/Falling Wizard/Build Main Menu In Open Scene", false, 10)]
        static void BuildMainMenu()
        {
            if (!EnsureTextMeshProResources())
                return;

            EnsureEventSystem();

            GameObject canvas = UiFactory.CreateCanvas("Menu Canvas", MenuCanvasSortingOrder);
            Undo.RegisterCreatedObjectUndo(canvas, "Build Main Menu");

            GameObject mainPanel = UiFactory.CreatePanel("Main Panel", canvas.transform);
            UiFactory.CreateTitle("Falling Wizard", mainPanel.transform);
            Button play = UiFactory.CreateButton("Play", mainPanel.transform);
            Button settings = UiFactory.CreateButton("Settings", mainPanel.transform);
            Button exit = UiFactory.CreateButton("Exit", mainPanel.transform);

            SettingsPanel settingsPanel = BuildSettingsPanel(canvas.transform);

            var controller = canvas.AddComponent<MainMenuController>();
            Wire(controller,
                ("panel", mainPanel),
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

            GameObject canvas = UiFactory.CreateCanvas("Pause Menu", PauseCanvasSortingOrder);

            GameObject pausePanel = UiFactory.CreatePanel("Pause Panel", canvas.transform);
            UiFactory.CreateHeading("Paused", pausePanel.transform);
            Button resume = UiFactory.CreateButton("Resume", pausePanel.transform);
            Button settings = UiFactory.CreateButton("Settings", pausePanel.transform);
            Button mainMenu = UiFactory.CreateButton("Main Menu", pausePanel.transform);
            Button quit = UiFactory.CreateButton("Quit", pausePanel.transform);

            SettingsPanel settingsPanel = BuildSettingsPanel(canvas.transform);

            var controller = canvas.AddComponent<PauseMenuController>();
            Wire(controller,
                ("panel", pausePanel),
                ("settingsPanel", settingsPanel),
                ("resumeButton", resume),
                ("settingsButton", settings),
                ("mainMenuButton", mainMenu),
                ("quitButton", quit));

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder(AssetsFolder, PrefabFolderName);

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
            Selection.activeGameObject = SpawnPlayer(PlayerSpawnPosition);
            MarkSceneDirty();
        }

        static GameObject SpawnPlayer(Vector3 position)
        {
            var root = new GameObject("Wizard");
            root.layer = LayerOrDefault(PlayerLayerName);
            Undo.RegisterCreatedObjectUndo(root, "Create Player");

            var body = root.AddComponent<Rigidbody2D>();
            body.freezeRotation = true;
            body.gravityScale = PlayerGravityScale;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = root.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = PlayerSize;

            var player = root.AddComponent<PlayerCharacter>();

            var serialized = new SerializedObject(player);
            serialized.FindProperty("movement.groundLayers").intValue = LayerMask.GetMask(GroundLayerName);
            serialized.FindProperty("movement.groundCheckOffset").vector2Value =
                new Vector2(0f, -(PlayerSize.y / 2f) - GroundCheckSkin);
            serialized.FindProperty("movement.groundCheckSize").vector2Value =
                new Vector2(PlayerSize.x * GroundCheckWidthFactor, GroundCheckThickness);
            serialized.FindProperty("staff").objectReferenceValue = CreateStaff(root.transform);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            CreateSpriteBox("Visual", root.transform, PlayerSize, PlayerColor, PlayerSortingOrder);

            root.transform.position = position;
            return root;
        }

        [MenuItem("Tools/Falling Wizard/Create Ground Platform In Open Scene", false, 31)]
        static void CreateGroundPlatform()
        {
            Selection.activeGameObject = CreatePlatform("Platform", Vector2.zero, PlatformSize, false);
            MarkSceneDirty();
        }

        [MenuItem("Tools/Falling Wizard/Build Test Level In Open Scene", false, 32)]
        static void BuildTestLevel()
        {
            // A run of flat ground, then stairs, then two drops sized around the staff.
            CreatePlatform("Start Ledge", new Vector2(-9f, -0.5f), new Vector2(12f, 1f), false);

            CreateStaircase(new Vector2(StairStartX, 0f));

            CreatePlatform("Mid Ledge", new Vector2(4.5f, -3f), new Vector2(9f, 1f), false);
            CreatePlatform("Lower Ledge", new Vector2(14f, -10f), new Vector2(10f, 1f), false);
            CreatePlatform("Rock", new Vector2(14f, -9f), new Vector2(1.6f, 1f), true);
            CreatePlatform("Bottom", new Vector2(25f, -24.5f), new Vector2(12f, 1f), false);

            GameObject player = SpawnPlayer(new Vector3(-13f, 1f, 0f));
            AttachFollowCamera(player);

            Selection.activeGameObject = player;
            MarkSceneDirty();
            Debug.Log("Test level built. Save the scene to keep it.");
        }

        // One GameObject, one PolygonCollider2D tracing the steps. The child sprites are
        // renderers only, so the whole staircase is a single entity with a single hitbox.
        static GameObject CreateStaircase(Vector2 topLeft)
        {
            var stairs = new GameObject("Staircase");
            stairs.layer = LayerOrDefault(GroundLayerName);
            stairs.transform.position = topLeft;
            Undo.RegisterCreatedObjectUndo(stairs, "Create Staircase");

            float depth = StairStepCount * StairStepHeight + 1f;
            var outline = new List<Vector2> { Vector2.zero };

            for (int step = 0; step < StairStepCount; step++)
            {
                float right = (step + 1) * StairStepWidth;
                float top = -step * StairStepHeight;
                outline.Add(new Vector2(right, top));                          // along the tread
                outline.Add(new Vector2(right, top - StairStepHeight));        // down the riser
            }

            outline.Add(new Vector2(StairStepCount * StairStepWidth, -depth)); // down the far side
            outline.Add(new Vector2(0f, -depth));                              // back along the base

            stairs.AddComponent<PolygonCollider2D>().points = outline.ToArray();
            stairs.AddComponent<RoughGround>();

            for (int step = 0; step < StairStepCount; step++)
            {
                float top = -step * StairStepHeight;
                float height = depth + top;
                var visual = CreateSpriteBox($"Step {step + 1}", stairs.transform,
                    new Vector2(StairStepWidth, height), RoughColor, PlatformSortingOrder);
                visual.transform.localPosition =
                    new Vector3((step + 0.5f) * StairStepWidth, top - height / 2f, 0f);
            }

            return stairs;
        }

        // The staff is its own entity with its own sprite, so growing it never touches the
        // wizard's. Anything with a Rigidbody2D can carry one.
        static Staff CreateStaff(Transform wielder)
        {
            var staffObject = new GameObject("Staff");
            staffObject.transform.SetParent(wielder, false);
            staffObject.transform.localPosition =
                new Vector3(0.3f, StaffLength - PlayerSize.y / 2f, 0f);

            var staff = staffObject.AddComponent<Staff>();

            GameObject visual = CreateSpriteBox("Visual", staffObject.transform,
                new Vector2(StaffWidth, StaffLength), StaffColor, StaffSortingOrder);
            visual.transform.localPosition = new Vector3(0f, -StaffLength / 2f, 0f);

            var serialized = new SerializedObject(staff);
            serialized.FindProperty("length").floatValue = StaffLength;
            serialized.FindProperty("visual").objectReferenceValue = visual.GetComponent<SpriteRenderer>();
            serialized.FindProperty("groundLayers").intValue = LayerMask.GetMask(GroundLayerName);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return staff;
        }

        static GameObject CreatePlatform(string name, Vector2 center, Vector2 size, bool rough)
        {
            var platform = new GameObject(name);
            platform.layer = LayerOrDefault(GroundLayerName);
            platform.transform.position = center;
            Undo.RegisterCreatedObjectUndo(platform, "Create Platform");

            platform.AddComponent<BoxCollider2D>().size = size;
            CreateSpriteBox("Visual", platform.transform, size,
                rough ? RoughColor : PlatformColor, PlatformSortingOrder);

            if (rough)
                platform.AddComponent<RoughGround>();

            return platform;
        }

        static void AttachFollowCamera(GameObject player)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("No Main Camera in this scene, so no follow camera was added.");
                return;
            }

            var follow = camera.GetComponent<FollowCamera>();
            if (follow == null)
                follow = Undo.AddComponent<FollowCamera>(camera.gameObject);

            var serialized = new SerializedObject(follow);
            serialized.FindProperty("target").objectReferenceValue = player.GetComponent<PlayerCharacter>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Tools/Falling Wizard/Add Game Scenes To Build Settings", false, 40)]
        static void ConfigureBuildScenes()
        {
            string[] wanted =
            {
                ScenePath(Game.MainMenuScene),
                ScenePath(Game.CutsceneScene),
                ScenePath(Game.FirstLevelScene),
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

        static string ScenePath(string sceneName) => ScenesFolder + "/" + sceneName + SceneExtension;

        static SettingsPanel BuildSettingsPanel(Transform parent)
        {
            GameObject panel = UiFactory.CreatePanel("Settings Panel", parent);
            UiFactory.CreateHeading("Settings", panel.transform);

            GameObject resolutionRow = UiFactory.CreateRow("Resolution", panel.transform);
            TMP_Dropdown resolution = UiFactory.CreateDropdown(resolutionRow.transform);

            GameObject fullscreenRow = UiFactory.CreateRow("Fullscreen", panel.transform);
            Toggle fullscreen = UiFactory.CreateToggle(fullscreenRow.transform);

            GameObject volumeRow = UiFactory.CreateRow("Volume", panel.transform);
            Slider volume = UiFactory.CreateSlider(volumeRow.transform);
            TextMeshProUGUI volumeValue = UiFactory.CreateValueLabel("100%", volumeRow.transform);
            volumeValue.gameObject.name = "Volume Value";

            Button back = UiFactory.CreateButton("Back", panel.transform);

            var settingsPanel = panel.AddComponent<SettingsPanel>();
            Wire(settingsPanel,
                ("resolutionDropdown", resolution),
                ("fullscreenToggle", fullscreen),
                ("volumeSlider", volume),
                ("volumeValueLabel", volumeValue),
                ("backButton", back));

            WireArray(settingsPanel, "desktopOnlyRows", new Object[] { resolutionRow, fullscreenRow });

            panel.SetActive(false);
            return settingsPanel;
        }

        static GameObject CreateSpriteBox(string name, Transform parent, Vector2 size, Color color,
            int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(BoxSpritePath);
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return go;
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
