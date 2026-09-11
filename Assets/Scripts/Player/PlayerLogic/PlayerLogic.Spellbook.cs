using System;
using System.Collections.Generic;
using FallingWizard.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FallingWizard.Player
{
    public partial class PlayerLogic
    {
        [Serializable]
        public class Spellbook
        {
            public const int SlotCount = Progress.SlotCount;

            const float ExplainAfter = 0.35f;

            public static readonly string[] SlotActions =
                { "Spell1", "Spell2", "Spell3", "Spell4" };

            [Header("Spells")]
            [Tooltip("The catalogue every slot draws from. Leave empty and the wizard loads " +
                     "Assets/Resources/Spellbook.asset.")]
            public AbilityBook book;

            [Tooltip("Print a line to the console whenever a press comes to nothing, saying " +
                     "which spell refused and why. Editor only. Leave it on while building a " +
                     "level - a spell that silently does nothing is the hardest kind to chase.")]
            public bool explainRefusals = true;

            [NonSerialized] public Modifiers stats = new Modifiers();
            [NonSerialized] Slot[] slots = Array.Empty<Slot>();
            [NonSerialized] PlayerLogic owner;

            [NonSerialized] readonly Dictionary<Ability, object> scratch =
                new Dictionary<Ability, object>();

            public event Action Changed;

            public int Version { get; private set; }

            public IReadOnlyList<Slot> Slots => slots;

            public AbilityBook Book => book;

            public void Attach(PlayerLogic player)
            {
                owner = player;

                if (book == null)
                    book = Resources.Load<AbilityBook>(AbilityBook.ResourcePath);

                if (book == null)
                {
                    Debug.LogError("No spellbook found. Create Assets/Resources/Spellbook.asset " +
                                   "from Assets > Create > Falling Wizard > Spellbook, and put " +
                                   "the Staff in both of its lists.");
                    slots = Array.Empty<Slot>();
                    return;
                }

                slots = new Slot[SlotCount];

                for (int i = 0; i < SlotCount; i++)
                    slots[i] = new Slot { Action = Controls.Player(SlotActions[i]) };

                Seed(book);
                Reload();
            }

            public static void Seed(AbilityBook book)
            {
                if (book == null)
                    return;

                foreach (Ability spell in book.known)
                    if (spell != null)
                        Progress.Grant(spell.Key);

                foreach (Ability spell in book.spells)
                    if (spell != null && spell.locked && spell.fixedSlot >= 0 &&
                        Progress.Owns(spell.Key) && Progress.SlotHolding(spell.Key) < 0)
                        Progress.Equip(spell.fixedSlot, spell.Key);
            }

            public void Reload()
            {
                if (slots.Length == 0)
                    return;

                Seed(book);

                for (int i = 0; i < SlotCount; i++)
                {
                    Slot slot = slots[i];
                    Ability next = book.Find(Progress.EquippedIn(i));

                    if (next != null && !Progress.Owns(next.Key))
                        next = null;

                    slot.Rank = next != null ? Progress.Rank(next.Key) : 0;

                    if (slot.Ability == next)
                        continue;

                    if (slot.Ability != null)
                    {
                        if (slot.IsLit)
                            slot.Ability.OnEnded(owner);

                        slot.Ability.OnUnequipped(owner);
                    }

                    slot.Fill(next);
                    next?.OnEquipped(owner);
                }

                Version++;
                Changed?.Invoke();
            }

            public bool Equip(Ability spell, int slot)
            {
                if ((uint)slot >= SlotCount)
                    return false;

                if (spell != null && (!Progress.Owns(spell.Key) || spell.locked))
                    return false;

                Ability leaving = book.Find(Progress.EquippedIn(slot));

                if (leaving != null && leaving.locked)
                    return false;

                Progress.Place(slot, spell != null ? spell.Key : string.Empty);
                Reload();
                return true;
            }

            public T StateOf<T>(Ability spell) where T : class, new()
            {
                if (spell == null)
                    return null;

                if (scratch.TryGetValue(spell, out object held) && held is T kept)
                    return kept;

                var fresh = new T();
                scratch[spell] = fresh;
                return fresh;
            }

            public void Extinguish(Ability spell)
            {
                Slot slot = Array.Find(slots, s => s.Ability == spell);

                if (slot == null || !slot.IsLit)
                    return;

                PutOut(slot);
            }

            void PutOut(Slot slot)
            {
                slot.LitLeft = 0f;
                slot.CooldownLeft = slot.Ability.cooldown;
                slot.Ability.OnEnded(owner);
            }

            public bool Knows(Ability spell) => spell != null && Progress.Owns(spell.Key);

            public Slot SlotOf(Ability spell) =>
                spell == null ? null : Array.Find(slots, s => s.Ability == spell);

            public int RankOf(Ability spell)
            {
                Slot slot = SlotOf(spell);
                return slot != null ? slot.Rank : 0;
            }

            public bool IsEquipped(Ability spell) =>
                spell != null && Array.Exists(slots, s => s.Ability == spell);

            public void Observe(float deltaTime)
            {
                bool paused = Game.IsPaused;

                for (int i = 0; i < slots.Length; i++)
                {
                    Slot slot = slots[i];

                    if (paused || slot.Action == null)
                    {
                        slot.Buffer = 0f;

                        if (slot.HeldFor > 0f && slot.Ability != null)
                        {
                            slot.Ability.OnChargeLost(owner);
                            slot.DropCharge();
                        }

                        continue;
                    }

                    bool pressed = slot.Action.WasPressedThisFrame();

                    if (slot.Ability == null)
                    {
                        if (pressed)
                            Explain(i, null, "there is nothing in that slot");

                        slot.Buffer = 0f;
                        continue;
                    }

                    slot.Held = slot.Action.IsPressed();

                    if (slot.Action.WasReleasedThisFrame() && slot.HeldFor > 0f)
                        slot.ReleasedAfter = slot.HeldFor;

                    if (pressed)
                    {
                        slot.Buffer = slot.Ability.pressBuffer;
                        slot.Fired = false;
                        slot.Explained = false;
                        continue;
                    }

                    if (slot.Ability.chargesOnHold)
                        continue;

                    float had = slot.Buffer;
                    slot.Buffer -= deltaTime;

                    if (had > 0f && slot.Buffer <= 0f && !slot.Fired)
                        Explain(i, slot.Ability, Refusal(slot));
                }
            }

            string Refusal(Slot slot)
            {
                if (slot.CooldownLeft > 0f)
                    return $"it is still cooling down, {slot.CooldownLeft:0.0}s to go";

                if (!slot.HasUsesLeft)
                    return "it has no casts left in this level";

                return slot.Ability.WhyNot(owner);
            }

            void Explain(int slot, Ability spell, string reason)
            {
#if UNITY_EDITOR
                if (!explainRefusals || string.IsNullOrEmpty(reason))
                    return;

                string named = spell != null ? spell.Name : $"Slot {slot + 1}";

                Debug.LogWarning($"{named} did not cast: {reason}.");
#endif
            }

            public void TryCast(float fixedDeltaTime)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    Slot slot = slots[i];

                    if (slot.Ability == null)
                        continue;

                    if (slot.Ability.chargesOnHold)
                    {
                        AdvanceCharge(i, slot, fixedDeltaTime);
                        continue;
                    }

                    if (slot.Buffer <= 0f || !slot.IsReady)
                        continue;

                    if (!slot.Ability.CanCast(owner))
                        continue;

                    if (!slot.Ability.OnCast(owner))
                        continue;

                    slot.Buffer = 0f;
                    slot.BeginCast();
                }
            }

            void AdvanceCharge(int index, Slot slot, float fixedDeltaTime)
            {
                if (slot.ReleasedAfter >= 0f)
                {
                    float held = slot.ReleasedAfter;

                    slot.ReleasedAfter = -1f;
                    slot.HeldFor = 0f;

                    slot.Ability.OnReleased(owner, held);
                    return;
                }

                if (!slot.Held || !slot.IsReady)
                {
                    slot.HeldFor = 0f;
                    return;
                }

                slot.HeldFor += fixedDeltaTime;
                slot.Ability.OnHeld(owner, slot.HeldFor, fixedDeltaTime);

                if (slot.Explained || slot.HeldFor < ExplainAfter || slot.Ability.CanCast(owner))
                    return;

                slot.Explained = true;
                Explain(index, slot.Ability, Refusal(slot));
            }

            public bool Fire(Ability spell)
            {
                Slot slot = SlotOf(spell);

                if (slot == null || !slot.IsReady)
                    return false;

                slot.BeginCast();
                return true;
            }

            public void Rebuild()
            {
                stats.Reset();

                foreach (Slot slot in slots)
                    if (slot.Ability != null)
                        slot.Ability.ModifyStats(owner, stats);

                foreach (Slot slot in slots)
                    if (slot.IsLit)
                        slot.Ability.ModifyStatsWhileLit(owner, stats);
            }

            public void TickTimers(float fixedDeltaTime)
            {
                foreach (Slot slot in slots)
                {
                    if (slot.CooldownLeft > 0f)
                        slot.CooldownLeft = Mathf.Max(0f, slot.CooldownLeft - fixedDeltaTime);

                    if (!slot.IsLit)
                        continue;

                    slot.Ability.OnLit(owner, fixedDeltaTime);

                    if (!slot.IsLit)
                        continue;

                    slot.LitLeft -= fixedDeltaTime;

                    if (slot.LitLeft <= 0f)
                        PutOut(slot);
                }
            }

            public void ResetForRun()
            {
                foreach (Slot slot in slots)
                {
                    if (slot.IsLit)
                        slot.Ability.OnEnded(owner);

                    slot.Fill(slot.Ability);
                    slot.Ability?.OnRunReset(owner);
                }
            }

            public class Slot
            {
                public Ability Ability;
                public InputAction Action;
                public float Buffer;
                public bool Fired;

                public int Rank;

                public bool Held;
                public float HeldFor;

                public bool Explained;

                public float ReleasedAfter = -1f;
                public float LitLeft;
                public float CooldownLeft;
                public int UsesLeft;

                public void Fill(Ability spell)
                {
                    Ability = spell;
                    Buffer = 0f;
                    LitLeft = 0f;
                    CooldownLeft = 0f;
                    UsesLeft = spell != null ? spell.usesPerLevel : 0;

                    DropCharge();
                }

                public void DropCharge()
                {
                    Held = false;
                    HeldFor = 0f;
                    ReleasedAfter = -1f;
                    Explained = false;
                }

                public void BeginCast()
                {
                    Fired = true;
                    LitLeft = Ability.activeDuration;

                    if (Ability.usesPerLevel > 0)
                        UsesLeft = Mathf.Max(0, UsesLeft - 1);

                    if (LitLeft <= 0f)
                        CooldownLeft = Ability.cooldown;
                }

                public bool IsEmpty => Ability == null;

                public bool IsLit => LitLeft > 0f;

                public bool HasUsesLeft =>
                    Ability == null || Ability.usesPerLevel <= 0 || UsesLeft > 0;

                public bool IsReady => Ability != null && CooldownLeft <= 0f && HasUsesLeft;

                public float CooldownProgress =>
                    Ability == null || Ability.cooldown <= 0f ? 0f : CooldownLeft / Ability.cooldown;

                public float LitProgress =>
                    Ability == null || Ability.activeDuration <= 0f ? 0f : LitLeft / Ability.activeDuration;
            }
        }
    }
}
