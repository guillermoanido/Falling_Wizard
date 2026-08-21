using System.Collections.Generic;
using UnityEngine;

namespace FallingWizard.Player
{
    public abstract class PowerUp : ScriptableObject
    {
        [Tooltip("Collecting a power up of the same type again refreshes it instead of stacking it.")]
        [SerializeField] string displayName = "Power Up";

        [Tooltip("Seconds the effect lasts. 0 means instant: OnCollected runs and nothing is tracked.")]
        [SerializeField] float duration;

        public string DisplayName => displayName;
        public float Duration => duration;
        public bool IsTimed => duration > 0f;

        public virtual void OnCollected(PlayerLogic player) { }

        public virtual void ModifyStats(PlayerStats stats) { }

        public virtual void OnExpired(PlayerLogic player) { }
    }

    public class ActivePowerUps
    {
        readonly List<Entry> entries = new List<Entry>();
        readonly PlayerStats stats = new PlayerStats();
        readonly PlayerLogic player;

        public ActivePowerUps(PlayerLogic owner) => player = owner;

        public PlayerStats Stats => stats;

        public void Add(PowerUp powerUp)
        {
            if (powerUp == null)
                return;

            powerUp.OnCollected(player);

            if (!powerUp.IsTimed)
                return;

            Entry existing = entries.Find(entry => entry.PowerUp.DisplayName == powerUp.DisplayName);
            if (existing != null)
            {
                existing.TimeLeft = powerUp.Duration;
                return;
            }

            entries.Add(new Entry { PowerUp = powerUp, TimeLeft = powerUp.Duration });
            Recalculate();
        }

        public void Tick(float deltaTime)
        {
            bool anyExpired = false;

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                entries[i].TimeLeft -= deltaTime;
                if (entries[i].TimeLeft > 0f)
                    continue;

                entries[i].PowerUp.OnExpired(player);
                entries.RemoveAt(i);
                anyExpired = true;
            }

            if (anyExpired)
                Recalculate();
        }

        public void Clear()
        {
            if (entries.Count == 0)
                return;

            foreach (Entry entry in entries)
                entry.PowerUp.OnExpired(player);

            entries.Clear();
            Recalculate();
        }

        void Recalculate()
        {
            stats.Reset();

            foreach (Entry entry in entries)
                entry.PowerUp.ModifyStats(stats);
        }

        class Entry
        {
            public PowerUp PowerUp;
            public float TimeLeft;
        }
    }
}
