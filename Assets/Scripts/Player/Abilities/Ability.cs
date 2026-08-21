using UnityEngine;

namespace FallingWizard.Player
{
    // A spell the wizard can learn. Once learned it is theirs for good - dying does not take it
    // away, only the timers reset.
    //
    // These assets are STATELESS. Every wizard in the scene shares the one asset, so anything
    // that changes while playing lives in PlayerLogic.Spellbook.Slot instead. Adding a mutable
    // field here would leak between play sessions and, worse, into the built game.
    //
    // Passive or active is decided by one thing: a spell with no actionName has no button, shows
    // no key on the HUD, and simply applies for as long as it is owned.
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

        // ---- hooks. Override what you need and ignore the rest. ----

        // Learned, either by walking into a shrine or by starting the game with it.
        public virtual void OnLearned(PlayerLogic wizard) { }

        // The wizard died and the level is restarting. Ownership is not affected.
        public virtual void OnRunReset(PlayerLogic wizard) { }

        // Applied every physics step for as long as the spell is OWNED. This is where a passive
        // lives. Multiply, never assign, so two spells touching one stat stack instead of
        // clobbering each other.
        public virtual void ModifyStats(PlayerLogic.Modifiers stats) { }

        // Applied every physics step only while the spell is LIT, for the seconds after a cast.
        public virtual void ModifyStatsWhileLit(PlayerLogic.Modifiers stats) { }

        // Whether the button would do anything right now. Only greys the HUD slot; it does not
        // stop the press being buffered.
        public virtual bool CanCast(PlayerLogic wizard) => true;

        // Do the thing. Return false to say "not yet" - the press stays buffered and will be
        // retried next step, which is what lets you press the staff button before the ledge.
        public virtual bool OnCast(PlayerLogic wizard) => false;

        // Every physics step while lit.
        public virtual void OnLit(PlayerLogic wizard, float fixedDeltaTime) { }

        // The lit window ran out, or the wizard died mid-spell.
        public virtual void OnEnded(PlayerLogic wizard) { }
    }
}
