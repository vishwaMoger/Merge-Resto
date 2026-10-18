using System;

namespace MergeResto.Sim
{
    public enum DailyRewardKind
    {
        Coins,
        Gems,
        Energy,
        /// <summary>Day 7: coins and gems together.</summary>
        Gift,
    }

    [Serializable]
    public struct DailyReward
    {
        public DailyRewardKind kind;
        /// <summary>Coins are multiplied by the shop's coin scale; gems and energy are not.</summary>
        public double coins;
        public int gems;
        public int energy;
    }

    /// <summary>Saved progress of the 7-day login streak (days are local calendar days).</summary>
    [Serializable]
    public class DailyRewardState
    {
        public int lastClaimDay = -1;
        /// <summary>Index (0..6) of the day claimed last, or -1 before the first claim.</summary>
        public int lastIndex = -1;
    }

    /// <summary>
    /// A 7-day login calendar: one reward per calendar day; coming back the next day moves one
    /// step along, missing a day starts again at day 1, and after day 7 the week repeats.
    /// </summary>
    public static class DailyRewards
    {
        public static readonly DailyReward[] Week =
        {
            new() { kind = DailyRewardKind.Coins, coins = 200 },
            new() { kind = DailyRewardKind.Gems, gems = 10 },
            new() { kind = DailyRewardKind.Coins, coins = 450 },
            new() { kind = DailyRewardKind.Energy, energy = 50 },
            new() { kind = DailyRewardKind.Coins, coins = 900 },
            new() { kind = DailyRewardKind.Gems, gems = 20 },
            new() { kind = DailyRewardKind.Gift, coins = 2000, gems = 30 },
        };

        public static bool CanClaim(DailyRewardState state, int today) => today > state.lastClaimDay;

        /// <summary>The day (0..6) the player gets if they claim today.</summary>
        public static int TodayIndex(DailyRewardState state, int today)
        {
            if (!CanClaim(state, today))
                return Math.Max(0, state.lastIndex);
            bool streak = state.lastClaimDay >= 0 && today - state.lastClaimDay == 1;
            return streak ? (state.lastIndex + 1) % Week.Length : 0;
        }

        /// <summary>Marks today's reward as taken and returns its index, or -1 if already claimed.</summary>
        public static int Claim(DailyRewardState state, int today)
        {
            if (!CanClaim(state, today))
                return -1;
            int index = TodayIndex(state, today);
            state.lastIndex = index;
            state.lastClaimDay = today;
            return index;
        }
    }
}
