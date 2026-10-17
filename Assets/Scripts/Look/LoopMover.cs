using UnityEngine;

namespace MergeResto.Look
{
    /// <summary>Drives an object from A to B at a steady speed, then starts again at A (traffic).</summary>
    public class LoopMover : MonoBehaviour
    {
        public Vector3 from;
        public Vector3 to;
        public float speed = 4f;
        [Tooltip("0..1 starting point along the route, so several movers don't bunch up.")]
        [Range(0f, 1f)] public float startOffset;

        float _t;

        void Start()
        {
            _t = startOffset;
            Vector3 dir = to - from;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        void Update()
        {
            float length = Vector3.Distance(from, to);
            if (length < 0.01f)
                return;
            _t = Mathf.Repeat(_t + speed * Time.deltaTime / length, 1f);
            transform.position = Vector3.Lerp(from, to, _t);
        }
    }
}
