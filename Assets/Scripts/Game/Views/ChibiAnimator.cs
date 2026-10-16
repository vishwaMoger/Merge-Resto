using UnityEngine;

namespace MergeResto.Game.Views
{
    /// <summary>
    /// Cute motion for a character whose root is moved by someone else: hops and squashes
    /// while moving, breathes while idle, and turns to face where it is going.
    /// </summary>
    public class ChibiAnimator : MonoBehaviour
    {
        [Tooltip("The visual child that hops and squashes.")]
        public Transform body;
        public float hopHeight = 0.12f;
        public float hopsPerSecond = 4f;
        public float turnSpeed = 12f;

        Vector3 _lastPos;
        Vector3 _baseScale = Vector3.one;
        Vector3 _basePos;
        float _time;
        float _moveBlend;
        Quaternion? _faceOverride;

        void Start()
        {
            _lastPos = transform.position;
            if (body != null)
            {
                _baseScale = body.localScale;
                _basePos = body.localPosition;
            }
        }

        /// <summary>Face a direction while standing still (e.g. a cashier facing customers).</summary>
        public void FaceWhenIdle(Vector3 direction) =>
            _faceOverride = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction, Vector3.up) : null;

        void LateUpdate()
        {
            Vector3 delta = transform.position - _lastPos;
            delta.y = 0f;
            _lastPos = transform.position;
            float speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
            bool moving = speed > 0.05f;
            _moveBlend = Mathf.MoveTowards(_moveBlend, moving ? 1f : 0f, Time.deltaTime * 6f);

            Quaternion? want = moving ? Quaternion.LookRotation(delta.normalized, Vector3.up) : _faceOverride;
            if (want.HasValue)
                transform.rotation = Quaternion.Slerp(transform.rotation, want.Value, turnSpeed * Time.deltaTime);

            if (body == null)
                return;
            _time += Time.deltaTime * Mathf.Lerp(1f, hopsPerSecond, _moveBlend);
            float phase = _time * Mathf.PI * 2f;
            float hop = Mathf.Abs(Mathf.Sin(phase * 0.5f)) * hopHeight * _moveBlend;
            float squash = Mathf.Lerp(Mathf.Sin(phase) * 0.02f, Mathf.Cos(phase) * 0.06f, _moveBlend);
            body.localPosition = _basePos + Vector3.up * hop;
            body.localScale = Vector3.Scale(_baseScale, new Vector3(1f - squash * 0.5f, 1f + squash, 1f - squash * 0.5f));
        }
    }
}
