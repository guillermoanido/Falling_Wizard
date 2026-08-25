using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallingWizard.Core
{
    public enum StaysTaken
    {
        ForThisRun,
        OnceBanked,
        ForGood,
    }

    public static class Progress
    {
        public const int SlotCount = 4;

        const string Prefix = "FallingWizard.";
        const string WispsKey = Prefix + "Wisps";
        const string RanksKey = Prefix + "Ranks";
        const string LoadoutKey = Prefix + "Loadout";
        const string SpentKey = Prefix + "Spent";
        const string HeartsKey = Prefix + "Hearts";
        const char Separator = ';';
        const char Pair = ':';

        static readonly Dictionary<string, int> ranks = new Dictionary<string, int>();
        static readonly string[] equipped = new string[SlotCount];

        static readonly HashSet<string> found = new HashSet<string>();
        static readonly HashSet<string> carrying = new HashSet<string>();
        static readonly HashSet<string> spent = new HashSet<string>();

        public static int Wisps { get; private set; }

        public static int CarriedWisps { get; private set; }

        public static int BonusHearts { get; private set; }

        public static bool HasCheckpoint { get; private set; }
        public static Vector2 CheckpointPoint { get; private set; }
        public static string CheckpointScene { get; private set; } = string.Empty;

        public static bool CheckpointIsHere =>
            HasCheckpoint && CheckpointScene == SceneManager.GetActiveScene().name;

        public static int Rank(string key) =>
            !string.IsNullOrEmpty(key) && ranks.TryGetValue(key, out int rank) ? rank : 0;

        public static bool Owns(string key) => Rank(key) > 0;

        public static void Grant(string key)
        {
            if (string.IsNullOrEmpty(key) || Owns(key))
                return;

            ranks[key] = 1;
            Save();
        }

        public static bool CanAfford(int cost) => Wisps >= cost;

        public static bool Buy(string key, int cost)
        {
            if (string.IsNullOrEmpty(key) || Owns(key) || Wisps < cost)
                return false;

            Wisps -= cost;
            ranks[key] = 1;
            Save();
            return true;
        }

        public static string EquippedIn(int slot) =>
            (uint)slot < SlotCount ? equipped[slot] : string.Empty;

        public static int SlotHolding(string key)
        {
            if (string.IsNullOrEmpty(key))
                return -1;

            for (int i = 0; i < SlotCount; i++)
                if (equipped[i] == key)
                    return i;

            return -1;
        }

        public static void Equip(int slot, string key)
        {
            if ((uint)slot >= SlotCount)
                return;

            key ??= string.Empty;

            if (key.Length > 0)
                for (int i = 0; i < SlotCount; i++)
                    if (equipped[i] == key)
                        equipped[i] = string.Empty;

            equipped[slot] = key;
            Save();
        }

        public static int FirstEmptySlot()
        {
            for (int i = 0; i < SlotCount; i++)
                if (string.IsNullOrEmpty(equipped[i]))
                    return i;

            return -1;
        }

        public static bool IsGone(string id) =>
            !string.IsNullOrEmpty(id) && (found.Contains(id) || spent.Contains(id));

        public static void MarkFound(string id, StaysTaken staysTaken)
        {
            if (string.IsNullOrEmpty(id))
                return;

            found.Add(id);

            switch (staysTaken)
            {
                case StaysTaken.OnceBanked:
                    carrying.Add(id);
                    break;

                case StaysTaken.ForGood:
                    spent.Add(id);
                    Save();
                    break;
            }
        }

        public static void CarryWisps(int amount)
        {
            if (amount > 0)
                CarriedWisps += amount;
        }

        public static void TakeHearts(int amount)
        {
            if (amount <= 0)
                return;

            BonusHearts += amount;
            Save();
        }

        public static void LoseCarried()
        {
            CarriedWisps = 0;
            carrying.Clear();
        }

        public static void BankCarried()
        {
            Wisps += CarriedWisps;
            CarriedWisps = 0;

            spent.UnionWith(carrying);
            carrying.Clear();

            Save();
        }

        public static void MarkCheckpoint(Vector2 point)
        {
            CheckpointPoint = point;
            CheckpointScene = SceneManager.GetActiveScene().name;
            HasCheckpoint = true;
        }

        public static void ClearCheckpoint()
        {
            HasCheckpoint = false;
            CheckpointScene = string.Empty;
            CheckpointPoint = Vector2.zero;
        }

        public static void EndRun()
        {
            BankCarried();

            found.Clear();
            ClearCheckpoint();
        }

        public static bool HasSave => PlayerPrefs.HasKey(WispsKey);

        public static void Save()
        {
            PlayerPrefs.SetInt(WispsKey, Wisps);
            PlayerPrefs.SetString(RanksKey, PackRanks());
            PlayerPrefs.SetString(LoadoutKey, string.Join(Separator.ToString(), equipped));
            PlayerPrefs.SetString(SpentKey, string.Join(Separator.ToString(), spent));
            PlayerPrefs.SetInt(HeartsKey, BonusHearts);
            PlayerPrefs.Save();
        }

        public static void Load()
        {
            Wisps = PlayerPrefs.GetInt(WispsKey, 0);
            BonusHearts = PlayerPrefs.GetInt(HeartsKey, 0);

            ranks.Clear();

            foreach (string entry in Split(PlayerPrefs.GetString(RanksKey, string.Empty)))
            {
                int split = entry.LastIndexOf(Pair);

                if (split <= 0 || !int.TryParse(entry.Substring(split + 1), out int rank))
                    continue;

                ranks[entry.Substring(0, split)] = Mathf.Max(1, rank);
            }

            string[] slots = PlayerPrefs.GetString(LoadoutKey, string.Empty).Split(Separator);

            for (int i = 0; i < SlotCount; i++)
                equipped[i] = i < slots.Length ? slots[i] : string.Empty;

            spent.Clear();

            foreach (string id in Split(PlayerPrefs.GetString(SpentKey, string.Empty)))
                spent.Add(id);
        }

        public static void ForgetAll()
        {
            PlayerPrefs.DeleteKey(WispsKey);
            PlayerPrefs.DeleteKey(RanksKey);
            PlayerPrefs.DeleteKey(LoadoutKey);
            PlayerPrefs.DeleteKey(SpentKey);
            PlayerPrefs.DeleteKey(HeartsKey);
            PlayerPrefs.Save();

            Clear();
        }

        static string PackRanks()
        {
            var packed = new List<string>(ranks.Count);

            foreach (KeyValuePair<string, int> rank in ranks)
                packed.Add($"{rank.Key}{Pair}{rank.Value}");

            return string.Join(Separator.ToString(), packed);
        }

        static IEnumerable<string> Split(string packed)
        {
            foreach (string piece in packed.Split(Separator))
                if (!string.IsNullOrEmpty(piece))
                    yield return piece;
        }

        static void Clear()
        {
            ranks.Clear();
            found.Clear();
            carrying.Clear();
            spent.Clear();

            for (int i = 0; i < SlotCount; i++)
                equipped[i] = string.Empty;

            Wisps = 0;
            CarriedWisps = 0;
            BonusHearts = 0;

            ClearCheckpoint();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void LoadOnPlay() => Load();
    }
}
