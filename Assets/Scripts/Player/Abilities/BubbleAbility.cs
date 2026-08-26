using FallingWizard.Core;
using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Bubble", fileName = "Bubble")]
    public class BubbleAbility : Ability
    {
        [Header("Bubble")]
        [Tooltip("Spawned around the wizard and carried by them. Leave empty and a plain round " +
                 "shape is built from the sprite below.")]
        public GameObject bubblePrefab;

        [Tooltip("Used when there is no prefab. Empty draws a flat tinted block.")]
        public Sprite bubbleArt;

        [Tooltip("Colour of the built bubble. Keep the alpha low - you have to see through it.")]
        public Color tint = new Color(0.6f, 0.85f, 1f, 0.35f);

        [Tooltip("How wide it is, in boxes. A mage is one box, so anything under 1 looks painted on.")]
        [Min(0.2f)] public float size = 1.7f;

        [Tooltip("Drawn in front of the wizard rather than behind them.")]
        public bool drawInFront = true;

        [Header("Drift")]
        [Tooltip("Fall speed inside the bubble, as a fraction of normal. Low numbers hang almost " +
                 "still. This does not forgive the drop the way Feather Fall does - a bubble " +
                 "that pops high up leaves you exactly as high up as you were.")]
        [Range(0f, 1f)] public float fallSpeed = 0.12f;

        [Tooltip("How much harder wind pushes a bubble than a wizard. This is the point of the " +
                 "spell: a gale you would brace against becomes a ride.")]
        [Min(0f)] public float windPull = 3.5f;

        [Tooltip("Steering while floating, against a normal run. Bubbles are meant to be steered " +
                 "badly.")]
        [Min(0f)] public float steering = 0.5f;

        [Header("Skin")]
        [Tooltip("Nothing can hurt the wizard while the bubble holds. Hazards still push them " +
                 "around - a slime under a bubble is a ride, not a wound.")]
        public bool shields = true;

        [Tooltip("Touching down pops it early and starts the cooldown.")]
        public bool popsOnLanding = true;

        public override bool CanCast(PlayerLogic wizard) =>
            wizard.State == PlayerState.Normal || wizard.State == PlayerState.OnVine;

        public override bool OnCast(PlayerLogic wizard)
        {
            Skin skin = wizard.spellbook.StateOf<Skin>(this);

            if (skin.shell != null)
                Destroy(skin.shell);

            skin.shell = Build(wizard);
            return true;
        }

        public override void ModifyStatsWhileLit(PlayerLogic.Modifiers stats)
        {
            stats.FallSpeedMultiplier *= fallSpeed;
            stats.WindMultiplier *= windPull;
            stats.MoveSpeedMultiplier *= steering;

            if (shields)
                stats.Shielded = true;
        }

        public override void OnLit(PlayerLogic wizard, float fixedDeltaTime)
        {
            if (popsOnLanding && wizard.movement.IsGrounded)
                wizard.spellbook.Extinguish(this);
        }

        public override void OnEnded(PlayerLogic wizard) => Pop(wizard);

        public override void OnRunReset(PlayerLogic wizard) => Pop(wizard);

        public override void OnUnequipped(PlayerLogic wizard) => Pop(wizard);

        void Pop(PlayerLogic wizard)
        {
            Skin skin = wizard.spellbook.StateOf<Skin>(this);

            if (skin.shell != null)
                Destroy(skin.shell);

            skin.shell = null;
        }

        GameObject Build(PlayerLogic wizard)
        {
            Transform rig = wizard.Rig;

            if (rig == null)
                return null;

            if (bubblePrefab != null)
            {
                GameObject blown = Instantiate(bubblePrefab, rig);
                blown.name = displayName;
                blown.transform.localPosition = Vector3.zero;
                return blown;
            }

            var shell = new GameObject(displayName);
            shell.transform.SetParent(rig, false);
            shell.transform.localPosition = Vector3.zero;
            shell.transform.localScale = new Vector3(size, size, 1f);

            var art = shell.AddComponent<SpriteRenderer>();
            art.sprite = bubbleArt != null ? bubbleArt : Placeholder.Box;
            art.color = tint;
            art.sortingOrder = drawInFront ? 20 : -20;

            return shell;
        }

        public class Skin
        {
            public GameObject shell;
        }
    }
}
