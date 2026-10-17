using UnityEngine;

namespace MergeResto.Game.UI
{
    /// <summary>Slowly turns a UI element (shine rays behind rewards). Runs while the game is paused.</summary>
    public class UiSpin : MonoBehaviour
    {
        [SerializeField] float degreesPerSecond = 20f;

        void Update() => transform.Rotate(0f, 0f, -degreesPerSecond * UiTime.Delta);
    }
}
