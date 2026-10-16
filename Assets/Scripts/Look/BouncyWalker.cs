using UnityEngine;

namespace MergeResto.Look
{
    /// <summary>
    /// Walks a character between waypoints with a cute hop and squash.
    /// Placeholder motion for the style test; real workers will use the production sim.
    /// </summary>
    public class BouncyWalker : MonoBehaviour
    {
        public Vector3[] waypoints;
        public float speed = 1.2f;
        public float pauseSeconds = 1.2f;
        public float hopHeight = 0.08f;
        public float hopsPerSecond = 4f;

        [Tooltip("The visual child that hops and squashes; the root only moves.")]
        public Transform body;

        int _target;
        float _pauseLeft;
        float _hopTime;
        Vector3 _bodyBaseScale = Vector3.one;
        Vector3 _bodyBasePos;

        void Start()
        {
            if (body != null)
            {
                _bodyBaseScale = body.localScale;
                _bodyBasePos = body.localPosition;
            }
            if (waypoints != null && waypoints.Length > 0)
                transform.position = waypoints[0];
        }

        void Update()
        {
            bool moving = false;
            if (waypoints == null || waypoints.Length < 2)
            {
                // Idle character: only breathe.
            }
            else if (_pauseLeft > 0f)
            {
                _pauseLeft -= Time.deltaTime;
            }
            else
            {
                Vector3 goal = waypoints[_target];
                Vector3 to = goal - transform.position;
                to.y = 0f;
                float step = speed * Time.deltaTime;
                if (to.magnitude <= step)
                {
                    transform.position = new Vector3(goal.x, transform.position.y, goal.z);
                    _target = (_target + 1) % waypoints.Length;
                    _pauseLeft = pauseSeconds;
                }
                else
                {
                    moving = true;
                    transform.position += to.normalized * step;
                    var look = Quaternion.LookRotation(to.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, look, 10f * Time.deltaTime);
                }
            }

            if (body == null)
                return;

            // Hop while walking, gentle breathing while idle.
            _hopTime += Time.deltaTime * (moving ? hopsPerSecond : 1f);
            float phase = _hopTime * Mathf.PI * 2f;
            float hop = moving ? Mathf.Abs(Mathf.Sin(phase * 0.5f)) * hopHeight : 0f;
            float squash = moving ? Mathf.Cos(phase) * 0.06f : Mathf.Sin(phase) * 0.02f;
            body.localPosition = _bodyBasePos + Vector3.up * hop;
            body.localScale = Vector3.Scale(_bodyBaseScale, new Vector3(1f - squash * 0.5f, 1f + squash, 1f - squash * 0.5f));
        }
    }
}
