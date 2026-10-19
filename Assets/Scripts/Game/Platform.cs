using CrazyGames;

namespace MergeResto.Game
{
    /// <summary>Small CrazyGames hooks that are safe to call anywhere (nothing happens outside the SDK).</summary>
    public static class Platform
    {
        /// <summary>A moment to celebrate (a new shop, a finished area, a bigger shop).</summary>
        public static void HappyTime()
        {
            if (CrazySDK.IsAvailable && CrazySDK.IsInitialized)
                CrazySDK.Game.HappyTime();
        }
    }
}
