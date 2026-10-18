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
            public List<Idle> idle = new();
        }

        /// <summary>What a shop earns per hour while closed, so other shops can show it.</summary>
        [Serializable]
        public class Idle
        {
            public string id;
            public string name;
            public double perHour;
            public float capHours;
            public long savedAt;
        }

        public static void RecordIdle(string id, string name, double perHour, float capHours, long savedAt)
        {
            var list = Current.idle ??= new List<Idle>();
            var entry = list.Find(e => e.id == id);
            if (entry == null)
                list.Add(entry = new Idle { id = id });
            entry.name = name;
            entry.perHour = perHour;
            entry.capHours = capHours;
            entry.savedAt = savedAt;
            Save();
        }

        /// <summary>Estimated coins waiting in each other open shop right now.</summary>
        public static List<(string name, double coins)> WaitingElsewhere(string hereId, long now)
        {
            var result = new List<(string, double)>();
            foreach (var e in Current.idle ?? new List<Idle>())
            {
                if (e.id == hereId || !IsUnlocked(e.id) || e.savedAt <= 0 || now <= e.savedAt)
                    continue;
                double hours = Math.Min((now - e.savedAt) / 3600.0, e.capHours);
                double coins = e.perHour * hours;
                if (coins >= 1)
                    result.Add((e.name, coins));
            }
            return result;
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
