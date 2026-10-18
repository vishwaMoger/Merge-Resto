using UnityEngine;

namespace MergeResto.Game.Views
{
    /// <summary>Shown only for some building stages (the empty lot before, the annex after, ...).</summary>
    public class StageGroup : MonoBehaviour
    {
        public int minStage;
        [Tooltip("-1: stays from minStage on.")]
        public int maxStage = -1;

        public bool VisibleAt(int stage) => stage >= minStage && (maxStage < 0 || stage <= maxStage);
    }
}
