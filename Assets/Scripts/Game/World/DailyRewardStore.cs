using System;
using MergeResto.Sim;
using UnityEngine;

namespace MergeResto.Game
{
    /// <summary>Loads and saves the daily login streak (shared by every shop).</summary>
    public static class DailyRewardStore
    {
        const string Key = "daily";
        static DailyRewardState _state;

        public static DailyRewardState State
        {
            get
            {
                if (_state != null)
                    return _state;
                string json = SaveStore.Load(Key);
                try { _state = string.IsNullOrEmpty(json) ? new DailyRewardState() : JsonUtility.FromJson<DailyRewardState>(json); }
                catch { _state = null; }
                _state ??= new DailyRewardState();
                return _state;
            }
        }

        /// <summary>Today as a whole number of local calendar days.</summary>
        public static int Today => (int)(DateTime.Now.Date - new DateTime(2000, 1, 1)).TotalDays;

        public static bool CanClaim => DailyRewards.CanClaim(State, Today);

        public static int TodayIndex => DailyRewards.TodayIndex(State, Today);

        public static int Claim()
        {
            int index = DailyRewards.Claim(State, Today);
            if (index >= 0)
                SaveStore.Save(Key, JsonUtility.ToJson(State));
            return index;
        }

        public static void Reset()
        {
            _state = new DailyRewardState();
            SaveStore.Delete(Key);
        }
    }
}
