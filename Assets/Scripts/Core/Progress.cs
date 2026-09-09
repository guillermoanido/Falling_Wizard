using System;
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

        const string FileName = "progress.json";

        const int Format = 1;

        const string Prefix = "FallingWizard.";
        const string LegacyWispsKey = Prefix + "Wisps";
        const string LegacyRanksKey = Prefix + "Ranks";
        const string LegacyLoadoutKey = Prefix + "Loadout";
        const string LegacySpentKey = Prefix + "Spent";
        const string LegacyHeartsKey = Prefix + "Hearts";
        const char Separator = ';';
        const char Pair = ':';

        static readonly Dictionary<string, int> ranks = new Dictionary<string, int>();
        static readonly string[] equipped = new string[SlotCount];

        static readonly HashSet<string> found = new HashSet<string>();
        static readonly HashSet<string> carrying = new HashSet<string>();
        static readonly HashSet<string> spent = new HashSet<string>();

        static bool saveIsUnreadable;
        static bool warnedAboutUnreadable;

        public static bool Sandbox { get; private set; }

        public static bool SandboxSeeded { get; private set; }

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

        public static bool FreeSpending { get; set; }

        public static bool CanAfford(int cost) => FreeSpending || Wisps >= cost;

        public static bool Buy(string key, int cost)
        {
            if (string.IsNullOrEmpty(key) || Owns(key) || (!FreeSpending && Wisps < cost))
                return false;

            if (!FreeSpending)
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

        public static void Place(int slot, string key)
        {
            if ((uint)slot >= SlotCount)
                return;

            key ??= string.Empty;

            int from = SlotHolding(key);

            if (key.Length > 0 && from >= 0 && from != slot)
            {
                (equipped[from], equipped[slot]) = (equipped[slot], equipped[from]);
                Save();
                return;
            }

            Equip(slot, key);
        }

        public static int FirstEmptySlot()
        {
            for (int i = 0; i < SlotCount; i++)
                if (string.IsNullOrEmpty(equipped[i]))
                    return i;

            return -1;
        }

        public static bool Upgrade(string key, int cost, int cap)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            int rank = Rank(key);

            if (rank < 1 || rank >= Mathf.Max(1, cap) || (!FreeSpending && Wisps < cost))
                return false;

            if (!FreeSpending)
                Wisps -= cost;

            ranks[key] = rank + 1;
            Save();
            return true;
        }

        public static void SetRank(string key, int rank)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (rank <= 0)
                ranks.Remove(key);
            else
                ranks[key] = rank;

            Save();
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

        public static void GiveWisps(int amount)
        {
            if (amount > 0)
                Wisps += amount;
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

        public static void SetHearts(int amount) => BonusHearts = Mathf.Max(0, amount);

        public static void LoseCarried()
        {
            CarriedWisps = 0;

            found.ExceptWith(carrying);
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

        public static void ResetSave()
        {
            Clear();
            SaveFile.Delete(FileName);
            DeleteLegacyPlayerPrefs();
        }

        public static bool HasSave => SaveFile.Exists(FileName) || PlayerPrefs.HasKey(LegacyWispsKey);

        public static void BeginSandbox(bool reseed = false)
        {
            if (Sandbox && SandboxSeeded && !reseed)
                return;

            Clear();
            Sandbox = true;
            SandboxSeeded = true;
        }

        public static void Save()
        {
            if (Sandbox)
                return;

            if (saveIsUnreadable)
            {
                if (!warnedAboutUnreadable)
                {
                    warnedAboutUnreadable = true;
                    Debug.LogWarning($"Not saving: the save file at {SaveFile.PathFor(FileName)} " +
                                     "could not be read when the game started, and writing a fresh " +
                                     "one over it would throw away whatever is in there. Nothing " +
                                     "earned this session will be kept. Relaunch once whatever is " +
                                     "holding that file has let go of it.");
                }

                return;
            }

            SaveFile.Write(FileName, Pack());
        }

        public static void Load()
        {
            SaveFile.Announce();

            saveIsUnreadable = false;
            warnedAboutUnreadable = false;

            switch (SaveFile.Read(FileName, out SaveData data))
            {
                case SaveRead.Loaded:
                    Unpack(data);
                    return;

                case SaveRead.Unreadable:
                    saveIsUnreadable = true;
                    return;
            }

            if (!ImportLegacyPlayerPrefs())
                Unpack(null);
        }

        public static void ForgetAll()
        {
            if (SaveFile.Delete(FileName))
            {
                saveIsUnreadable = false;
                warnedAboutUnreadable = false;
            }

            DeleteLegacyPlayerPrefs();

            Clear();
        }

        static SaveData Pack()
        {
            var data = new SaveData
            {
                version = Format,
                wisps = Wisps,
                bonusHearts = BonusHearts,
                loadout = new string[SlotCount],
            };

            for (int i = 0; i < SlotCount; i++)
                data.loadout[i] = equipped[i] ?? string.Empty;

            var keys = new List<string>(ranks.Keys);
            keys.Sort(StringComparer.Ordinal);

            data.ranks = new List<RankEntry>(keys.Count);

            foreach (string key in keys)
                data.ranks.Add(new RankEntry { key = key, rank = ranks[key] });

            data.spent = new List<string>(spent);
            data.spent.Sort(StringComparer.Ordinal);

            return data;
        }

        static void Unpack(SaveData data)
        {
            data ??= new SaveData();

            Wisps = Mathf.Max(0, data.wisps);
            BonusHearts = Mathf.Max(0, data.bonusHearts);

            ranks.Clear();

            if (data.ranks != null)
                foreach (RankEntry entry in data.ranks)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.key))
                        continue;

                    ranks[entry.key] = Mathf.Max(1, entry.rank);
                }

            for (int i = 0; i < SlotCount; i++)
                equipped[i] = data.loadout != null && i < data.loadout.Length && data.loadout[i] != null
                    ? data.loadout[i]
                    : string.Empty;

            spent.Clear();

            if (data.spent != null)
                foreach (string id in data.spent)
                    if (!string.IsNullOrEmpty(id))
                        spent.Add(id);
        }

        static bool ImportLegacyPlayerPrefs()
        {
            if (!PlayerPrefs.HasKey(LegacyWispsKey))
                return false;

            Wisps = PlayerPrefs.GetInt(LegacyWispsKey, 0);
            BonusHearts = PlayerPrefs.GetInt(LegacyHeartsKey, 0);

            ranks.Clear();

            foreach (string entry in SplitLegacy(PlayerPrefs.GetString(LegacyRanksKey, string.Empty)))
            {
                int split = entry.LastIndexOf(Pair);

                if (split <= 0 || !int.TryParse(entry.Substring(split + 1), out int rank))
                    continue;

                ranks[entry.Substring(0, split)] = Mathf.Max(1, rank);
            }

            string[] slots = PlayerPrefs.GetString(LegacyLoadoutKey, string.Empty).Split(Separator);

            for (int i = 0; i < SlotCount; i++)
                equipped[i] = i < slots.Length ? slots[i] : string.Empty;

            spent.Clear();

            foreach (string id in SplitLegacy(PlayerPrefs.GetString(LegacySpentKey, string.Empty)))
                spent.Add(id);

            if (!SaveFile.Write(FileName, Pack()))
            {
                Debug.LogWarning("Old progress was found in PlayerPrefs but could not be written to " +
                                 $"{SaveFile.PathFor(FileName)}. It has been left where it is and " +
                                 "the import will be tried again on the next launch.");
                return true;
            }

            DeleteLegacyPlayerPrefs();

            Debug.Log($"Progress saved by an older build was imported into {SaveFile.PathFor(FileName)} " +
                      "and the PlayerPrefs it came from have been deleted.");
            return true;
        }

        static void DeleteLegacyPlayerPrefs()
        {
            PlayerPrefs.DeleteKey(LegacyWispsKey);
            PlayerPrefs.DeleteKey(LegacyRanksKey);
            PlayerPrefs.DeleteKey(LegacyLoadoutKey);
            PlayerPrefs.DeleteKey(LegacySpentKey);
            PlayerPrefs.DeleteKey(LegacyHeartsKey);
            PlayerPrefs.Save();
        }

        static IEnumerable<string> SplitLegacy(string packed)
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
            Sandbox = false;
            SandboxSeeded = false;

            ClearCheckpoint();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void LoadOnPlay() => Load();

        [Serializable]
        class SaveData
        {
            public int version;
            public int wisps;
            public int bonusHearts;
            public string[] loadout;
            public List<RankEntry> ranks;
            public List<string> spent;
        }

        [Serializable]
        class RankEntry
        {
            public string key;
            public int rank;
        }
    }
}
