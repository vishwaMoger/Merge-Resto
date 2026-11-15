namespace MergeResto.Game
{
    /// <summary>Small portal hooks that are safe to call anywhere (nothing happens outside a portal).</summary>
    public static class Platform
    {
        /// <summary>A moment to celebrate (a new shop, a finished area, a bigger shop).</summary>
        public static void HappyTime() => Portal.HappyTime();
    }
}
