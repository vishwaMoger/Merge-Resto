using System;
using System.Collections.Generic;
using UnityEngine;

namespace MergeResto.Game
{
    /// <summary>
    /// Progress across shops: which shops are open and which one the player was last in.
    /// Each shop keeps its own save (coins, stations, board); this only links them.
    /// </summary>
    public static class WorldState
    {
        const string Key = "world";
        public const string FirstShop = "grandmas_bakery";

        [Serializable]
        class Data
        {
            public List<string> unlocked = new() { FirstShop };
            public string last = FirstShop;
        }

        static Data _data;

        static Data Current
        {
            get
            {
                if (_data != null)
                    return _data;
                string json = SaveStore.Load(Key);
                try { _data = string.IsNullOrEmpty(json) ? new Data() : JsonUtility.FromJson<Data>(json) ?? new Data(); }
                catch { _data = new Data(); }
                if (!_data.unlocked.Contains(FirstShop))
                    _data.unlocked.Add(FirstShop);
                return _data;
            }
        }

        public static bool IsUnlocked(string shopId) => Current.unlocked.Contains(shopId);

        public static string LastShop => string.IsNullOrEmpty(Current.last) ? FirstShop : Current.last;

        public static void Unlock(string shopId)
        {
            if (!Current.unlocked.Contains(shopId))
                Current.unlocked.Add(shopId);
            Save();
        }

        public static void SetLast(string shopId)
        {
            if (Current.last == shopId)
                return;
            Current.last = shopId;
            Save();
        }

        /// <summary>Forget everything (debug / reset all progress).</summary>
        public static void Reset()
        {
            _data = new Data();
            SaveStore.Delete(Key);
        }

        static void Save() => SaveStore.Save(Key, JsonUtility.ToJson(Current));
    }
}
