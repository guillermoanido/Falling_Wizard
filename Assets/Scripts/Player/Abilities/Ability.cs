using System;
using UnityEngine;
using UnityEngine.Serialization;

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

        [Header("Learning")]
        [Tooltip("Wisps spent to learn this at the skill screen.")]
        [Min(0)] public int cost = 1;

        [Tooltip("Cannot be moved out of its slot at the skill screen. Only the Staff wants this.")]
        public bool locked = false;

        [Tooltip("The slot a locked spell always sits in. 0 is the first button, 1 the second, " +
                 "and so on. -1 for anything the player is free to place.")]
        [Range(-1, 3)] public int fixedSlot = -1;

        [Header("Input")]
        [Tooltip("A passive has no button. It still takes up one of the four slots, so bringing " +
                 "one is a real choice.")]
        public bool passive = false;

        [Tooltip("A press this many seconds early still counts, so you can hit the button just " +
                 "before reaching a ledge.")]
        [Min(0f)] public float pressBuffer = 0.12f;

        [Tooltip("Winds up while the button is DOWN and goes off when it is let go, instead of " +
                 "firing on the press. A spell with this on never uses the press buffer - set " +
                 "that to 0, or it complains to the console every tenth of a second of charging.")]
        public bool chargesOnHold = false;

        [Header("Timing")]
        [Tooltip("Seconds the spell stays lit after casting. 0 means it acts instantly and does " +
                 "not linger.")]
        [Min(0f)] public float activeDuration = 0f;

        [Tooltip("Seconds before it can be cast again, counted from when it ends.")]
        [Min(0f)] public float cooldown = 0f;

        [Tooltip("Casts allowed per level. 0 is unlimited. Use it for something powerful enough " +
                 "that a cooldown alone would not hold it back - the decision then becomes WHEN " +
                 "in the level to spend it. Refilled whenever the level is entered, which is the " +
                 "same scene load that rebuilds the spellbook, so dying and coming back refills " +
                 "it too.")]
        [FormerlySerializedAs("usesPerRun")]
        [Min(0)] public int usesPerLevel = 0;

        [Header("Pickup")]
        [Tooltip("Optional prefab spawned where the shrine was, for a puff of sparkles.")]
        public GameObject collectedEffect;

        [Header("Upgrades")]
        [Tooltip("One entry per rank past the first. Element 0 is what rank 2 costs and what it " +
                 "buys, in the player's words. Leave it empty for a spell that never upgrades. " +
                 "The NUMBERS a rank actually changes live on the spell's own script - this list " +
                 "is the shop window.")]
        public Upgrade[] upgrades = new Upgrade[0];

        public bool IsPassive => passive;

        // Deliberately no separate maxRank field: two numbers that can disagree is a bug waiting
        // to be authored. The list IS the cap.
        public int MaxRank => upgrades != null ? upgrades.Length + 1 : 1;

        public bool HasUpgrades => upgrades != null && upgrades.Length > 0;

        public Upgrade NextUpgrade(int rank) =>
            upgrades != null && rank >= 1 && rank <= upgrades.Length ? upgrades[rank - 1] : null;

        public string Key => string.IsNullOrEmpty(id) ? name : id;

        public virtual void OnEquipped(PlayerLogic wizard) { }

        public virtual void OnUnequipped(PlayerLogic wizard) { }

        public virtual void OnRunReset(PlayerLogic wizard) { }

        public virtual void ModifyStats(PlayerLogic wizard, PlayerLogic.Modifiers stats) { }

        public virtual void ModifyStatsWhileLit(PlayerLogic wizard, PlayerLogic.Modifiers stats) { }

        // What the HUD should draw for this spell right now. Telekinesis answers with the icon of
        // whatever it is carrying, which is the only way a player can tell a stored slime from a
        // stored rock without opening a menu.
        public virtual Sprite IconFor(PlayerLogic wizard) => icon;

        public virtual Color IconTintFor(PlayerLogic wizard) => Color.white;

        // 0..1 to draw a meter of the spell's own, or below zero for "I have nothing to show and
        // the slot should fall back to its cooldown wipe".
        public virtual float ChargeFor(PlayerLogic wizard) => -1f;

        public virtual void OnHeld(PlayerLogic wizard, float heldSeconds, float fixedDeltaTime) { }

        public virtual void OnReleased(PlayerLogic wizard, float heldSeconds) { }

        public virtual bool CanCast(PlayerLogic wizard) => true;

        // Why the press just now did nothing, in the player's words and without a full stop.
        // Null says nothing at all. Only ever reaches the console, and only in the editor - it
        // exists because a spell that silently refuses is a spell you cannot debug.
        public virtual string WhyNot(PlayerLogic wizard) => null;

        public virtual bool OnCast(PlayerLogic wizard) => false;

        public virtual void OnLit(PlayerLogic wizard, float fixedDeltaTime) { }

        public virtual void OnEnded(PlayerLogic wizard) { }

        // Pick the block of per-rank numbers for a rank, clamped both ways. A tier list shorter
        // than the upgrade list is a designer half way through filling it in: the top ranks repeat
        // the last block, which is wrong but visible, rather than throwing at runtime.
        protected static T TierFor<T>(T[] tiers, int rank) where T : class =>
            tiers == null || tiers.Length == 0
                ? null
                : tiers[Mathf.Clamp(rank - 1, 0, tiers.Length - 1)];

        protected void CheckTiers(int count)
        {
            if (count > 0 && count < MaxRank)
                Debug.LogWarning($"'{name}' reaches rank {MaxRank} but has only {count} block(s) " +
                                 $"of numbers, so rank {count + 1} and up repeat the last one.",
                                 this);
        }

        // Unity delivers OnValidate to the MOST-DERIVED type only. A subclass declaring its own
        // would hide this one and every clamp on the chain would quietly stop running, with
        // nothing in the compiler to catch it. A spell overrides Validate() and never writes an
        // OnValidate of its own.
        void OnValidate()
        {
            cost = Mathf.Max(0, cost);

            if (upgrades != null)
                foreach (Upgrade step in upgrades)
                    step?.Validate();

            Validate();
        }

        protected virtual void Validate() { }

        [Serializable]
        public class Upgrade
        {
            [Tooltip("Wisps to raise the spell to this rank.")]
            [Min(0)] public int cost = 1;

            [Tooltip("Short name for the rank: 'Longer staff', 'Up and down', 'Wider stone'.")]
            public string title = "";

            [TextArea]
            [Tooltip("What it buys, in the player's words. One sentence.")]
            public string description = "";

            // OnValidate does not reach into a nested class, so the block clamps itself.
            public void Validate() => cost = Mathf.Max(0, cost);
        }
    }
}
