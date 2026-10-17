namespace MergeResto.Game
{
    /// <summary>
    /// What the tutorial is pointing at, so the regular hints step aside: while it runs, stations
    /// only glow when they are the tutorial's target and the usual tap pins stay hidden.
    /// </summary>
    public static class TutorialFocus
    {
        public static bool Active;
        public static int Station = -1;
        /// <summary>Show the focused station's upgrade badge (upgrade and manager lessons).</summary>
        public static bool ShowBadge;
    }
}
