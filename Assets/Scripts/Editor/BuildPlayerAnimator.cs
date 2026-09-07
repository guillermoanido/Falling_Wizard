#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using FallingWizard.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallingWizard.EditorTools
{
    public static class BuildPlayerAnimator
    {
        const string ClipFolder = "Assets/Animations";
        const string ControllerPath = ClipFolder + "/Player.controller";
        const string PlayerPrefab = "Assets/Prefabs/PLAYER.prefab";

        const string BodyPath = "Visual";

        class Clip
        {
            public string State;
            public string Sheet;
            public int Fps = 12;
            public bool Loops;

            public string Path => $"{ClipFolder}/{State.Replace(" ", string.Empty)}.anim";
        }

        static readonly Clip[] Clips =
        {
            new Clip { State = "Idle",       Sheet = "Assets/Assets/Mage 2.png",     Fps = 1,  Loops = true },
            new Clip { State = "Walk",       Sheet = "",                             Fps = 12, Loops = true },
            new Clip { State = "Run",        Sheet = "Assets/Assets/PJ Run 1.png",   Fps = 12, Loops = true },
            new Clip { State = "Falling",    Sheet = "",                             Fps = 12, Loops = true },
            new Clip { State = "Climb Up",   Sheet = "",                             Fps = 12, Loops = false },
            new Clip { State = "Climb Down", Sheet = "",                             Fps = 12, Loops = false },
            new Clip { State = "Climb",      Sheet = "",                             Fps = 12, Loops = true },
            new Clip { State = "Take Damage",Sheet = "",                             Fps = 12, Loops = false },
            new Clip { State = "Death",      Sheet = "",                             Fps = 12, Loops = false },
        };

        const string RagdollClipPrefix = "Ragdoll";

        [MenuItem("Falling Wizard/Build Player Animator")]
        public static void Build()
        {
            int variants = ReadVariantCount();

            EnsureFolder();

            var motions = new Dictionary<string, AnimationClip>();

            foreach (Clip clip in Clips)
                motions[clip.State] = FillClip(clip);

            for (int i = 0; i < variants; i++)
            {
                var ragdoll = new Clip { State = $"{RagdollClipPrefix} {i}", Sheet = "", Loops = false };
                motions[ragdoll.State] = FillClip(ragdoll);
            }

            AnimatorController controller = BuildController(motions, variants);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AttachTo(controller, variants);

            Debug.Log($"Player Animator built: {motions.Count} clips and a controller at " +
                      $"{ControllerPath}, with {variants} ragdoll reaction(s). Clips with no art " +
                      "yet are empty and wired in - draw into them and press play.");
        }

        static int ReadVariantCount()
        {
            PlayerAnimator existing = Object.FindFirstObjectByType<PlayerAnimator>(FindObjectsInactive.Include);

            return existing != null ? Mathf.Max(1, existing.ragdollVariants) : 4;
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(ClipFolder))
                AssetDatabase.CreateFolder("Assets", "Animations");
        }

        static AnimationClip FillClip(Clip spec)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(spec.Path);

            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, spec.Path);
            }

            clip.frameRate = Mathf.Max(1, spec.Fps);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = spec.Loops;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (!string.IsNullOrEmpty(spec.Sheet))
            {
                Sprite[] frames = FramesOf(spec.Sheet);

                if (frames.Length == 0)
                    Debug.LogWarning($"'{spec.Sheet}' has no sprites, so the {spec.State} clip was " +
                                     "left empty. Set the texture's Sprite Mode and slice it first.");
                else
                    WriteFrames(clip, frames);
            }

            EditorUtility.SetDirty(clip);
            return clip;
        }

        static Sprite[] FramesOf(string sheet)
        {
            return AssetDatabase.LoadAllAssetsAtPath(sheet)
                .OfType<Sprite>()
                .OrderByDescending(s => s.rect.y)
                .ThenBy(s => s.rect.x)
                .ToArray();
        }

        static void WriteFrames(AnimationClip clip, Sprite[] frames)
        {
            var keys = new ObjectReferenceKeyframe[frames.Length];

            for (int i = 0; i < frames.Length; i++)
                keys[i] = new ObjectReferenceKeyframe
                {
                    time = i / clip.frameRate,
                    value = frames[i],
                };

            EditorCurveBinding binding =
                EditorCurveBinding.PPtrCurve(BodyPath, typeof(SpriteRenderer), "m_Sprite");

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        }

        static AnimatorController BuildController(Dictionary<string, AnimationClip> motions, int variants)
        {
            AssetDatabase.DeleteAsset(ControllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter("Mode", AnimatorControllerParameterType.Int);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Walking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("ClimbRate", AnimatorControllerParameterType.Float);
            controller.AddParameter("ClimbUp", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("ClimbDown", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("RagdollVariant", AnimatorControllerParameterType.Int);
            controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine graph = controller.layers[0].stateMachine;

            AnimatorState idle = AddState(graph, motions, "Idle", new Vector3(300, 0));
            AnimatorState walk = AddState(graph, motions, "Walk", new Vector3(560, -80), "Speed");
            AnimatorState run = AddState(graph, motions, "Run", new Vector3(560, 80), "Speed");
            AnimatorState falling = AddState(graph, motions, "Falling", new Vector3(300, 180));
            AnimatorState climbUp = AddState(graph, motions, "Climb Up", new Vector3(-40, -140));
            AnimatorState climbDown = AddState(graph, motions, "Climb Down", new Vector3(-40, -60));
            AnimatorState climb = AddState(graph, motions, "Climb", new Vector3(180, -100), "ClimbRate");
            AnimatorState hurt = AddState(graph, motions, "Take Damage", new Vector3(300, 300));
            AnimatorState death = AddState(graph, motions, "Death", new Vector3(300, 380));

            graph.defaultState = idle;

            Go(idle, walk, ("Moving", true), ("Walking", true));
            Go(idle, run, ("Moving", true), ("Walking", false));
            Go(walk, run, ("Walking", false));
            Go(run, walk, ("Walking", true));
            Go(walk, idle, ("Moving", false));
            Go(run, idle, ("Moving", false));

            Go(idle, falling, ("Grounded", false));
            Go(walk, falling, ("Grounded", false));
            Go(run, falling, ("Grounded", false));

            GoOnLanding(falling, idle, motions, ("Moving", false));
            GoOnLanding(falling, walk, motions, ("Moving", true), ("Walking", true));
            GoOnLanding(falling, run, motions, ("Moving", true), ("Walking", false));

            OnFinishing(climbUp, climb);
            OnFinishing(climbDown, climb);

            AnimatorStateTransition offThePole = climb.AddTransition(idle);
            offThePole.hasExitTime = false;
            offThePole.duration = 0f;
            offThePole.AddCondition(AnimatorConditionMode.NotEqual, PlayerAnimator.ModeStaff, "Mode");

            OnFinishing(hurt, idle);

            Interrupt(graph, death, t =>
                t.AddCondition(AnimatorConditionMode.Equals, PlayerAnimator.ModeDead, "Mode"));

            for (int i = 0; i < variants; i++)
            {
                AnimatorState pose = AddState(graph, motions, $"{RagdollClipPrefix} {i}",
                    new Vector3(760, -100 + i * 70));

                int variant = i;
                Interrupt(graph, pose, t =>
                {
                    t.AddCondition(AnimatorConditionMode.Equals, PlayerAnimator.ModeRagdoll, "Mode");
                    t.AddCondition(AnimatorConditionMode.Equals, variant, "RagdollVariant");
                });

                AnimatorStateTransition backUp = pose.AddTransition(idle);
                backUp.hasExitTime = false;
                backUp.duration = 0f;
                backUp.AddCondition(AnimatorConditionMode.Equals, PlayerAnimator.ModeGround, "Mode");
            }

            Interrupt(graph, climbUp, t => t.AddCondition(AnimatorConditionMode.If, 0f, "ClimbUp"));
            Interrupt(graph, climbDown, t => t.AddCondition(AnimatorConditionMode.If, 0f, "ClimbDown"));

            Interrupt(graph, hurt, t =>
            {
                t.AddCondition(AnimatorConditionMode.If, 0f, "Hurt");
                t.AddCondition(AnimatorConditionMode.Equals, PlayerAnimator.ModeGround, "Mode");
            });

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimatorState AddState(AnimatorStateMachine graph,
            Dictionary<string, AnimationClip> motions, string name, Vector3 at, string speedParameter = null)
        {
            AnimatorState state = graph.AddState(name, at);

            state.motion = motions.TryGetValue(name, out AnimationClip clip) ? clip : null;

            state.writeDefaultValues = false;

            if (!string.IsNullOrEmpty(speedParameter))
            {
                state.speedParameterActive = true;
                state.speedParameter = speedParameter;
            }

            return state;
        }

        static void Go(AnimatorState from, AnimatorState to, params (string Parameter, bool Wanted)[] conditions)
        {
            AnimatorStateTransition transition = from.AddTransition(to);

            transition.hasExitTime = false;
            transition.duration = 0f;

            foreach ((string parameter, bool wanted) in conditions)
                transition.AddCondition(wanted ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                    0f, parameter);
        }

        static void GoOnLanding(AnimatorState from, AnimatorState to,
            Dictionary<string, AnimationClip> motions, params (string Parameter, bool Wanted)[] conditions)
        {
            AnimatorStateTransition transition = from.AddTransition(to);

            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            transition.AddCondition(AnimatorConditionMode.Equals, PlayerAnimator.ModeGround, "Mode");

            foreach ((string parameter, bool wanted) in conditions)
                transition.AddCondition(wanted ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                    0f, parameter);
        }

        static void OnFinishing(AnimatorState from, AnimatorState to)
        {
            AnimatorStateTransition transition = from.AddTransition(to);

            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.duration = 0f;
        }

        static void Interrupt(AnimatorStateMachine graph, AnimatorState to,
            System.Action<AnimatorStateTransition> conditions)
        {
            AnimatorStateTransition transition = graph.AddAnyStateTransition(to);

            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.canTransitionToSelf = false;

            conditions(transition);
        }

        static void AttachTo(AnimatorController controller, int variants)
        {
            GameObject prefab = PrefabUtility.LoadPrefabContents(PlayerPrefab);

            if (prefab != null)
            {
                Wire(prefab, controller, variants);
                PrefabUtility.SaveAsPrefabAsset(prefab, PlayerPrefab);
                PrefabUtility.UnloadPrefabContents(prefab);
                Debug.Log($"Wired the Animator into {PlayerPrefab}.");
            }

            Scene scene = SceneManager.GetActiveScene();
            PlayerCharacter inScene = Object.FindFirstObjectByType<PlayerCharacter>(FindObjectsInactive.Include);

            if (inScene == null)
                return;

            Wire(inScene.gameObject, controller, variants);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Wired the Animator into '{inScene.name}' in {scene.name}. Save the scene to keep it.");
        }

        static void Wire(GameObject wizard, AnimatorController controller, int variants)
        {
            var animator = wizard.GetComponent<Animator>();

            if (animator == null)
                animator = wizard.AddComponent<Animator>();

            animator.runtimeAnimatorController = controller;

            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var driver = wizard.GetComponent<PlayerAnimator>();

            if (driver == null)
                driver = wizard.AddComponent<PlayerAnimator>();

            driver.ragdollVariants = variants;

            EditorUtility.SetDirty(wizard);
        }
    }
}
#endif
