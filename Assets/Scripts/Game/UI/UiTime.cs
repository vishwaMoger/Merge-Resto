using UnityEngine;

namespace MergeResto.Game.UI
{
    /// <summary>
    /// Frame time for UI animation: unscaled (keeps animating if the game is ever paused), but
    /// falls back to the game delta when the unscaled clock reports zero (e.g. editor ticks
    /// while unfocused), and is capped so a long hitch never makes springs explode.
    /// </summary>
    public static class UiTime
    {
        public static float Delta
        {
            get
            {
                float dt = Time.unscaledDeltaTime;
                if (dt <= 0f) dt = Time.deltaTime;
                return Mathf.Min(dt, 0.05f);
            }
        }
    }
}
