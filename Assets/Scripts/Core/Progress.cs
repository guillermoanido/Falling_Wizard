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

        // Stamped into every file written. Nothing reads it yet; it is there so that the day the
        // shape of the save changes, an old file can be recognised and converted instead of being
        // thrown away as unreadable.
        const int Format = 1;

        // The old PlayerPrefs save, kept only so a machine that last played the previous build can
        // be read once and moved into the file. Nothing writes these any more.
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

        // Set when the save file is there and would not open. While it is up, Save() refuses to
        // write - see Load() for why that is the only safe answer.
        static bool saveIsUnreadable;
        static bool warnedAboutUnreadable;

        public static bool Sandbox { get; private set; }

        // Sandbox is a property of the play SESSION; seeding it is a one-time act. Re-seeding on
        // every scene load is what made a restart forget the loadout - Clear() empties `equipped`
        // and `ranks`, and Playtest ran it at execution order -100, a frame ahead of the spellbook
        // reading them.
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

        // Put `key` on `slot`. Coming off another button the two TRADE PLACES; coming off the
        // bench, whatever was on that button goes back to the bench. Equip cannot express this -
        // it clears the key from wherever it was and overwrites the target, so dropping one spell
        // onto another loses the second one with no sign that it happened.
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

        // Raising a rank, never learning one: rank 1 is what Buy and Grant hand out. Refusing
        // to learn here means no bug in a screen can hand out rank 2 to a spell nobody bought.
        // The cap arrives as an argument because Progress does not know what a spell is.
        public static bool Upgrade(string key, int cost, int cap)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            int rank = Rank(key);

            if (rank < 1 || rank >= Mathf.Max(1, cap) || Wisps < cost)
                return false;

            Wisps -= cost;
            ranks[key] = rank + 1;
            Save();
            return true;
        }

        // For Playtest and nothing else. Save() is asleep while sandboxed, so this cannot reach
        // the real save from where it is called.
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

            // Put them back where they were found, which is what the death screen promises and
            // what `carrying` is FOR: these are the pickups that only stay taken once banked.
            // Dropping them out of `carrying` alone left them in `found`, and Pickup.Awake
            // destroys anything in `found` the moment the level reloads - so every wisp already
            // collected simply was not there any more, invisible and uncollectable, for the rest
            // of the run. A pickup that stays taken FOR GOOD is in `spent` as well and is
            // untouched by this.
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

        // NOTHING calls this today - there is no Continue button, and MainMenuController sends
        // Play straight to the skill screen. It is kept for the menu that will want it, and it
        // asks the disk rather than a cached flag because that is the only answer that cannot go
        // stale. The legacy half matters in exactly one case: an import that found old keys but
        // could not write the file, which leaves those keys where they were.
        public static bool HasSave => SaveFile.Exists(FileName) || PlayerPrefs.HasKey(LegacyWispsKey);

        public static void BeginSandbox(bool reseed = false)
        {
            if (Sandbox && SandboxSeeded && !reseed)
                return;

            // Clear() ends by setting Sandbox false, so the order of these two matters.
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
                    // Leave the statics exactly as Clear() left them - a blank purse - and clamp
                    // the save shut for the session. A blank session is recoverable the moment the
                    // file opens again; a blank session that saves itself over the real file is not.
                    saveIsUnreadable = true;
                    return;
            }

            // Missing: either a brand new save, or a machine whose progress is still in the old
            // PlayerPrefs. The importer fills in the statics itself when it finds something.
            if (!ImportLegacyPlayerPrefs())
                Unpack(null);
        }

        public static void ForgetAll()
        {
            // The guard only comes off if the file is ACTUALLY gone. Delete returns false when it
            // could not remove it - typically the very same lock that made it unreadable in the
            // first place - and letting the guard off while the file is still sitting there is
            // how the next Save() writes a blank purse over a save nobody could read.
            if (SaveFile.Delete(FileName))
            {
                saveIsUnreadable = false;
                warnedAboutUnreadable = false;
            }

            // The old keys as well. Someone who erases their progress before ever launching a
            // build with the file save would otherwise have the whole lot imported back on the
            // next launch, which reads as "Erase Progress did nothing".
            DeleteLegacyPlayerPrefs();

            Clear();
        }

        // JsonUtility cannot serialise a Dictionary or a HashSet - it silently writes nothing at
        // all for either - so the two are flattened into lists of things it can write, and rebuilt
        // in Unpack. `loadout` needs no such treatment and must NOT be sorted: its meaning is
        // which slot a spell is in.
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

            // Both of these are SORTED, and that is the whole reason this file is worth tracking.
            // A Dictionary and a HashSet hand their contents back in bucket order, which is not
            // the order things went in and is not the same order twice. Dumped as they come, every
            // single save rewrites the identical contents shuffled, and `git diff` shows a wall of
            // moved lines with no way to see what actually changed.
            var keys = new List<string>(ranks.Keys);
            keys.Sort(StringComparer.Ordinal);

            data.ranks = new List<RankEntry>(keys.Count);

            foreach (string key in keys)
                data.ranks.Add(new RankEntry { key = key, rank = ranks[key] });

            data.spent = new List<string>(spent);
            data.spent.Sort(StringComparer.Ordinal);

            return data;
        }

        // Everything here is defended against nonsense, because this file is meant to be opened
        // and edited by hand - that is the point of it - and a hand-edited file is a file with a
        // missing bracket, a negative purse or a rank of 0 in it sooner or later. JsonUtility also
        // leaves any field the file does not mention at its default, which for a list is null.
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

                    // Rank 0 means "does not own it", and an entry that is present says the
                    // opposite. Clamping up rather than dropping the line keeps a typo from
                    // quietly unlearning a spell.
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

        // A one-off rescue for anyone who played the build before the save became a file. It only
        // runs when there is no file, reads the five old keys with the old parsing exactly as it
        // was, and then deletes them so there is only ever one truth about what has been earned.
        // Returns whether it found anything, so Load knows the statics have been filled in.
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

            // Forget the old keys ONLY once the new file is actually on disk. Deleting first and
            // then failing to write - a read-only install folder, a full drive - is how a
            // migration eats a save it was written to protect.
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

        // The save file itself, laid out the way it appears on disk. Public fields and a
        // [Serializable] attribute are what JsonUtility can see; anything private, any property
        // and any Dictionary or HashSet is skipped without a word of complaint.
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

        // One learned spell and how far it has been taken. A list of these is the stand-in for the
        // Dictionary<string, int> that JsonUtility cannot write.
        [Serializable]
        class RankEntry
        {
            public string key;
            public int rank;
        }
    }
}
