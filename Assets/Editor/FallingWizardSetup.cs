using System.Collections.Generic;
using FallingWizard.Core;
using FallingWizard.Menus;
using FallingWizard.Player;
using FallingWizard.UI;
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
        const string HazardLayerName = "Hazard";

        const string BoxSpritePath = "UI/Skin/UISprite.psd";

        const int MenuCanvasSortingOrder = 0;
        const int PauseCanvasSortingOrder = 100;

        // Matched to the art: 25x35 px of mage on a 32 px grid.
        static readonly Vector2 PlayerSize = new Vector2(0.78125f, 1.09375f);
        static readonly Color PlayerColor = new Color(0.58f, 0.45f, 0.88f);
        const float PlayerGravityScale = 3f;
        const int PlayerSortingOrder = 1;

        // Likewise 14x34 px of staff. The pole's height is its reach, so this is a real number.
        const float StaffLength = 1.0625f;
        const float StaffWidth = 0.4375f;
        static readonly Color StaffColor = new Color(0.55f, 0.38f, 0.20f);
        const int StaffSortingOrder = 2;
        static readonly Vector2 StaffGripOffset = new Vector2(0.3f, 0f);

        static readonly Vector2 PlatformSize = new Vector2(10f, 1f);
        static readonly Color PlatformColor = new Color(0.24f, 0.22f, 0.30f);
        static readonly Color RoughColor = new Color(0.42f, 0.29f, 0.22f);
        const int PlatformSortingOrder = 0;

        // Above the level geometry, so a new hazard is never hidden behind a platform that
        // happens to sit closer to the camera.
        const int HazardSortingOrder = 3;
        static readonly Vector2 CheckpointSize = new Vector2(1f, 2f);

        const string UiLayerName = "UI";
        const int HudSortingOrder = 10;                 // above the game, below the pause menu
        const float HudPixelsPerUnit = 32f;             // must match the art, or icons come out small
        const float HudHeartSize = 40f;
        const float HudHeartSpacing = 6f;
        const float HudSlotSize = 64f;
        const float HudSlotSpacing = 14f;
        const float HudSlotPadding = 6f;
        const float HudButtonTextSize = 22f;
        static readonly Vector2 HudReferenceResolution = new Vector2(1920f, 1080f);
        static readonly Vector2 HudMargin = new Vector2(48f, 48f);
        static readonly Color HudSlotFrameColor = new Color(0.12f, 0.10f, 0.16f, 0.75f);
        static readonly Color HudButtonTextColor = new Color(0.92f, 0.90f, 0.86f);

        const int StairStepCount = 5;
        const float StairStepWidth = 1f;
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
            Selection.activeGameObject = SpawnPlayer(SpawnPoint());
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
            serialized.FindProperty("logic.movement.groundLayers").intValue = LayerMask.GetMask(GroundLayerName);
            serialized.FindProperty("logic.movement.groundCheckOffset").vector2Value =
                new Vector2(0f, -(PlayerSize.y / 2f) - GroundCheckSkin);
            serialized.FindProperty("logic.movement.groundCheckSize").vector2Value =
                new Vector2(PlayerSize.x * GroundCheckWidthFactor, GroundCheckThickness);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // The wizard's own sprite first, so it is the one that gets flipped, then the staff
            // as a child: the player finds it by hierarchy rather than by a wired reference.
            CreateSpriteBox("Visual", root.transform, PlayerSize, PlayerColor, PlayerSortingOrder);
            CreateStaff(root.transform);

            root.transform.position = position;
            return root;
        }

        [MenuItem("Tools/Falling Wizard/Create Ground Platform In Open Scene", false, 31)]
        static void CreateGroundPlatform()
        {
            Selection.activeGameObject = CreatePlatform("Platform", SpawnPoint(), PlatformSize);
            MarkSceneDirty();
        }

        [MenuItem("Tools/Falling Wizard/Build Test Level In Open Scene", false, 32)]
        static void BuildTestLevel()
        {
            // Laid out in boxes: one box is 32 px, one world unit, and about one mage. A run of
            // flat ground, then stairs, then two drops either side of what the staff is worth.
            CreatePlatform("Start Ledge", new Vector2(-9f, -0.5f), new Vector2(12f, 1f));

            CreateStaircase(new Vector2(StairStartX, 0f));

            CreatePlatform("Mid Ledge", new Vector2(4.5f, -3f), new Vector2(9f, 1f));

            // Four boxes down: one heart taken bare, free if you climb down the staff first.
            CreatePlatform("Lower Ledge", new Vector2(14f, -7f), new Vector2(10f, 1f));
            CreatePlatform("Rock", new Vector2(14f, -6f), new Vector2(2f, 1f));

            // Seven boxes down: four hearts bare, two off the staff. Survivable either way.
            CreatePlatform("Bottom", new Vector2(25f, -14f), new Vector2(12f, 1f));

            GameObject player = SpawnPlayer(new Vector3(-13f, 1f, 0f));
            AttachFollowCamera();

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

        // The staff is its own entity with its own sprite and its own hitbox, so growing it
        // never touches the wizard's. Anything with a Rigidbody2D can carry one.
        static Staff CreateStaff(Transform wielder)
        {
            var staffObject = new GameObject("Staff");
            staffObject.layer = wielder.gameObject.layer;
            staffObject.transform.SetParent(wielder, false);
            staffObject.transform.localPosition = new Vector3(StaffGripOffset.x,
                StaffLength - PlayerSize.y / 2f + StaffGripOffset.y, 0f);

            // This collider is the mechanic: the wizard travels its height and no further, so
            // a taller box is a longer climb down with nothing else to change.
            var hitbox = staffObject.AddComponent<BoxCollider2D>();
            hitbox.isTrigger = true;
            hitbox.size = new Vector2(StaffWidth, StaffLength);
            hitbox.offset = new Vector2(0f, -StaffLength / 2f);

            var staff = staffObject.AddComponent<Staff>();

            GameObject visual = CreateSpriteBox("Visual", staffObject.transform,
                new Vector2(StaffWidth, StaffLength), StaffColor, StaffSortingOrder);
            visual.transform.localPosition = new Vector3(0f, -StaffLength / 2f, 0f);

            // The plank you stand on when the staff is a bridge. It has to be SOLID and on
            // the Ground layer - the staff itself is on Player, which the ground check ignores,
            // so a collider on the staff would be one the wizard falls straight through.
            var bridgeObject = new GameObject("Bridge");
            bridgeObject.layer = LayerOrDefault(GroundLayerName);
            bridgeObject.transform.SetParent(staffObject.transform, false);

            var bridge = bridgeObject.AddComponent<BoxCollider2D>();
            bridge.size = hitbox.size;
            bridge.offset = hitbox.offset;
            bridge.enabled = false;

            var serialized = new SerializedObject(staff);
            serialized.FindProperty("hitbox").objectReferenceValue = hitbox;
            serialized.FindProperty("bridgeCollider").objectReferenceValue = bridge;
            serialized.FindProperty("visual").objectReferenceValue = visual.GetComponent<SpriteRenderer>();
            serialized.FindProperty("pole.groundLayers").intValue = LayerMask.GetMask(GroundLayerName);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return staff;
        }

        static GameObject CreatePlatform(string name, Vector2 center, Vector2 size)
        {
            var platform = new GameObject(name);
            platform.layer = LayerOrDefault(GroundLayerName);
            platform.transform.position = center;
            Undo.RegisterCreatedObjectUndo(platform, "Create Platform");

            platform.AddComponent<BoxCollider2D>().size = size;
            CreateSpriteBox("Visual", platform.transform, size, PlatformColor, PlatformSortingOrder);

            return platform;
        }

        static void AttachFollowCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("No Main Camera in this scene, so no follow camera was added.");
                return;
            }

            // Nothing to wire up: FollowCamera finds the wizard through the singleton.
            if (camera.GetComponent<FollowCamera>() == null)
                Undo.AddComponent<FollowCamera>(camera.gameObject);
        }

        [MenuItem("Tools/Falling Wizard/Add HUD To Open Scene", false, 33)]
        static void AddHud()
        {
            if (Object.FindFirstObjectByType<PlayerHud>() != null)
            {
                Debug.LogWarning("This scene already has a HUD. Delete it first to rebuild.");
                return;
            }

            // A plain screen-space Canvas, so the whole HUD is ordinary UI you can open up and
            // restyle. Deliberately no GraphicRaycaster: the HUD must never swallow a pause-menu
            // click or steal controller focus from the menus.
            var canvas = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(PlayerHud));
            canvas.layer = LayerOrDefault(UiLayerName);
            Undo.RegisterCreatedObjectUndo(canvas, "Add HUD");

            var surface = canvas.GetComponent<Canvas>();
            surface.renderMode = RenderMode.ScreenSpaceOverlay;
            surface.sortingOrder = HudSortingOrder;

            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = HudReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // The art is 32 pixels per unit. Left at the default 100 every icon comes out a
            // third of the size it should be.
            scaler.referencePixelsPerUnit = HudPixelsPerUnit;

            RectTransform hearts = CreateHudRow("Hearts", canvas.transform,
                new Vector2(0f, 1f), new Vector2(HudMargin.x, -HudMargin.y), HudHeartSpacing);

            RectTransform spells = CreateHudRow("Spell Bar", canvas.transform,
                new Vector2(0f, 0f), new Vector2(HudMargin.x, HudMargin.y), HudSlotSpacing);

            var hud = canvas.GetComponent<PlayerHud>();
            hud.heartRow = hearts;
            hud.spellBar = spells;
            hud.heartTemplate = CreateHeartTemplate(hearts);
            hud.slotTemplate = CreateSlotTemplate(spells);

            Selection.activeGameObject = canvas;
            MarkSceneDirty();
            Debug.Log("HUD built. Hearts and spell slots are copies of the two templates under " +
                      "it - restyle a template and every heart or slot follows.");
        }

        static RectTransform CreateHudRow(string name, Transform parent, Vector2 corner,
                                          Vector2 offset, float spacing)
        {
            var host = new GameObject(name, typeof(RectTransform));
            var rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            // Pinned to one corner and sized by its contents, so it never has to be re-laid-out
            // when the wizard gains a heart or learns a spell.
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.anchoredPosition = offset;
            rect.sizeDelta = Vector2.zero;

            var layout = host.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childAlignment = corner.y > 0.5f ? TextAnchor.UpperLeft : TextAnchor.LowerLeft;

            var fitter = host.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return rect;
        }

        static Image CreateHeartTemplate(Transform parent)
        {
            Image heart = CreateHudImage("Heart", parent, Vector2.one * HudHeartSize);
            heart.gameObject.SetActive(false);
            return heart;
        }

        static HudSlot CreateSlotTemplate(Transform parent)
        {
            Image frame = CreateHudImage("Spell Slot", parent, Vector2.one * HudSlotSize);
            frame.color = HudSlotFrameColor;

            Image icon = CreateHudImage("Icon", frame.transform, Vector2.zero);
            StretchHud(icon.rectTransform, HudSlotPadding);

            Image charge = CreateHudImage("Charge", frame.transform, Vector2.zero);
            StretchHud(charge.rectTransform, HudSlotPadding);
            charge.type = Image.Type.Filled;
            charge.fillMethod = Image.FillMethod.Radial360;
            charge.fillOrigin = (int)Image.Origin360.Top;
            charge.fillClockwise = false;

            var labelObject = new GameObject("Button", typeof(RectTransform));
            labelObject.transform.SetParent(frame.transform, false);

            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = HudButtonTextSize;
            label.color = HudButtonTextColor;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.text = "E";

            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.sizeDelta = new Vector2(0f, HudButtonTextSize * 1.4f);
            labelRect.anchoredPosition = new Vector2(0f, -2f);

            var slot = frame.gameObject.AddComponent<HudSlot>();
            slot.icon = icon;
            slot.charge = charge;
            slot.button = label;

            frame.gameObject.SetActive(false);
            return slot;
        }

        static Image CreateHudImage(string name, Transform parent, Vector2 size)
        {
            var host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(parent, false);

            var image = host.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(BoxSpritePath);
            image.raycastTarget = false;

            if (size != Vector2.zero)
                host.GetComponent<RectTransform>().sizeDelta = size;

            return image;
        }

        static void StretchHud(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = Vector2.one * -inset;
        }

        [MenuItem("Tools/Falling Wizard/Create Hazard In Open Scene/Rock", false, 34)]
        static void CreateRock() => Finish(CreateHazard<Rock>("Rock", new Vector2(2f, 1f),
            new Color(0.42f, 0.29f, 0.22f)));

        [MenuItem("Tools/Falling Wizard/Create Hazard In Open Scene/Slime", false, 35)]
        static void CreateSlime() => Finish(CreateHazard<Slime>("Slime", new Vector2(2f, 1f),
            new Color(0.35f, 0.78f, 0.42f)));

        [MenuItem("Tools/Falling Wizard/Create Hazard In Open Scene/Wind", false, 36)]
        static void CreateWind() => Finish(CreateHazard<WindZone2D>("Wind", new Vector2(8f, 6f),
            new Color(0.55f, 0.80f, 0.95f, 0.18f)));

        [MenuItem("Tools/Falling Wizard/Create Ability Shrine In Open Scene", false, 37)]
        static void CreateShrine()
        {
            var shrine = new GameObject("Shrine");
            shrine.transform.position = SpawnPoint();
            Undo.RegisterCreatedObjectUndo(shrine, "Create Shrine");

            shrine.AddComponent<BoxCollider2D>().isTrigger = true;
            shrine.AddComponent<AbilityShrine>();
            CreateSpriteBox("Visual", shrine.transform, Vector2.one,
                new Color(0.95f, 0.86f, 0.45f), HazardSortingOrder);

            Debug.Log("Shrine created. Drag a spell asset onto it, and make sure that spell is " +
                      "also listed in Assets/Resources/Spellbook.asset.");
            Finish(shrine);
        }

        // Hazards go at the scene root on purpose: several platforms in the test level carry
        // non-uniform scales that would squash anything parented under them.
        static GameObject CreateHazard<T>(string name, Vector2 size, Color color) where T : Component
        {
            var hazard = new GameObject(name);
            hazard.layer = LayerOrDefault(HazardLayerName);
            hazard.transform.position = SpawnPoint();
            Undo.RegisterCreatedObjectUndo(hazard, "Create " + name);

            // Every hazard is something you pass through; Hazard applies that on Awake, and
            // this makes it true in the editor too, so the scene view matches what you get.
            var box = hazard.AddComponent<BoxCollider2D>();
            box.size = size;
            box.isTrigger = true;

            hazard.AddComponent<T>();
            CreateSpriteBox("Visual", hazard.transform, size, color, HazardSortingOrder);

            return hazard;
        }

        // Where a newly created object should appear. The middle of the Scene view, the way
        // Unity's own Create menu behaves - dropping things at the world origin buries them in
        // whatever happens to be built there, and a solid one becomes an invisible wall.
        //
        // z is forced to 0 because that is the plane the wizard lives on. Nothing in a 2D game
        // should be built at another depth, however the Scene view was angled at the time.
        static Vector3 SpawnPoint()
        {
            SceneView view = SceneView.lastActiveSceneView;
            Vector3 point = view != null ? view.pivot : Vector3.zero;

            return new Vector3(point.x, point.y, 0f);
        }

        static void Finish(GameObject created)
        {
            Selection.activeGameObject = created;
            MarkSceneDirty();
        }

        [MenuItem("Tools/Falling Wizard/Create Checkpoint In Open Scene", false, 38)]
        static void CreateCheckpoint()
        {
            var post = new GameObject("Checkpoint");
            post.transform.position = SpawnPoint();
            Undo.RegisterCreatedObjectUndo(post, "Create Checkpoint");

            var trigger = post.AddComponent<BoxCollider2D>();
            trigger.size = CheckpointSize;
            trigger.isTrigger = true;

            GameObject visual = CreateSpriteBox("Visual", post.transform, CheckpointSize,
                Color.white, HazardSortingOrder);

            var checkpoint = post.AddComponent<Checkpoint>();
            checkpoint.visual = visual.GetComponent<SpriteRenderer>();

            // Half its own height, so the wizard reappears standing on the floor rather than
            // buried in whatever the checkpoint is sitting on.
            checkpoint.respawnOffset = new Vector2(0f, CheckpointSize.y * 0.5f);

            Finish(post);
        }

        [MenuItem("Tools/Falling Wizard/Clear Saved Progress", false, 39)]
        static void ClearSavedProgress()
        {
            // Qualified: UnityEditor has a Progress class of its own, so the bare name is
            // ambiguous in any editor script.
            Core.Progress.ForgetAll();
            Debug.Log("Saved progress cleared. The next run starts from the top with only the " +
                      "starting spells.");
        }

        [MenuItem("Tools/Falling Wizard/Add Game Scenes To Build Settings", false, 50)]
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
