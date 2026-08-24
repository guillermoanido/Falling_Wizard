using UnityEngine;

namespace FallingWizard.Player
{
    public abstract class Ability : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Key this spell is remembered by. Leave empty to use the asset's file name. " +
                 "Once a save exists, changing it forgets the spell.")]
        public string id = "";

        [Tooltip("Name shown to the player.")]
        public string displayName = "Spell";

        [TextArea]
        [Tooltip("What it does, in the player's words.")]
        public string description = "";

        [Tooltip("HUD icon. Same import settings as the rest of the art: 32 pixels per unit, " +
                 "point filter.")]
        public Sprite icon;

        [Header("Input")]
        [Tooltip("Action name inside the Player map - 'Staff', 'Glide', 'Bridge'. LEAVE EMPTY " +
                 "for a passive spell: it always applies and shows no button on the bar.")]
        public string actionName = "";

        [Tooltip("A press this many seconds early still counts, so you can hit the button just " +
                 "before reaching a ledge.")]
        [Min(0f)] public float pressBuffer = 0.12f;

        [Header("Timing")]
        [Tooltip("Seconds the spell stays lit after casting. 0 means it acts instantly and does " +
                 "not linger.")]
        [Min(0f)] public float activeDuration = 0f;

        [Tooltip("Seconds before it can be cast again, counted from when it ends.")]
        [Min(0f)] public float cooldown = 0f;

        [Header("Pickup")]
        [Tooltip("Optional prefab spawned where the shrine was, for a puff of sparkles.")]
        public GameObject collectedEffect;

        public bool IsPassive => string.IsNullOrEmpty(actionName);

        public string Key => string.IsNullOrEmpty(id) ? name : id;

        public virtual void OnLearned(PlayerLogic wizard) { }

        public virtual void OnRunReset(PlayerLogic wizard) { }

        public virtual void ModifyStats(PlayerLogic.Modifiers stats) { }

        public virtual void ModifyStatsWhileLit(PlayerLogic.Modifiers stats) { }

        public virtual bool CanCast(PlayerLogic wizard) => true;

        public virtual bool OnCast(PlayerLogic wizard) => false;

        public virtual void OnLit(PlayerLogic wizard, float fixedDeltaTime) { }

        public virtual void OnEnded(PlayerLogic wizard) { }
    }
}
