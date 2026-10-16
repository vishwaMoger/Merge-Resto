using System;

namespace MergeResto.Game.UI
{
    public static class NumberFormat
    {
        static readonly string[] Suffixes = { "", "K", "M", "B", "T", "Qa", "Qi" };

        /// <summary>1234 -> "1.23K", 950 -> "950". Idle numbers grow fast, so keep them short.</summary>
        public static string Short(double value)
        {
            if (double.IsInfinity(value) || double.IsNaN(value))
                return "-";
            double abs = Math.Abs(value);
            if (abs < 1000)
                return Math.Floor(value).ToString("0");
            int tier = Math.Min((int)(Math.Log10(abs) / 3), Suffixes.Length - 1);
            double scaled = value / Math.Pow(1000, tier);
            string format = scaled >= 100 ? "0" : scaled >= 10 ? "0.#" : "0.##";
            return scaled.ToString(format) + Suffixes[tier];
        }

        public static string Duration(double seconds)
        {
            var t = TimeSpan.FromSeconds(seconds);
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes}m";
            if (t.TotalMinutes >= 1) return $"{t.Minutes}m {t.Seconds}s";
            return $"{t.Seconds}s";
        }
    }
}
