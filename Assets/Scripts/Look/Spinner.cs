using UnityEngine;

namespace MergeResto.Look
{
    /// <summary>Spins around local Y, e.g. a mixer bowl.</summary>
    public class Spinner : MonoBehaviour
    {
        public float degreesPerSecond = 120f;

        void Update() => transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);
    }
}
