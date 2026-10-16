using UnityEngine;

namespace MergeResto.Game.Views
{
    /// <summary>
    /// Motion for a character whose root is moved by someone else. With an Animator it drives
    /// idle/walk blending, an arms layer for carrying, a working loop and one-shot cheers;
    /// without one it falls back to a procedural hop and squash. Always turns to face travel.
    /// </summary>
    public class ChibiAnimator : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int WorkingId = Animator.StringToHash("Working");
        static readonly int SittingId = Animator.StringToHash("Sitting");
        static readonly int CheerId = Animator.StringToHash("Cheer");
        static readonly int WaveId = Animator.StringToHash("Wave");

        [Tooltip("Rigged model's animator. Leave empty for the procedural fallback.")]
        public Animator animator;
        [Tooltip("Animator layer that overrides the arms while carrying.")]
        public int carryLayer = 1;
        [Tooltip("Where carried goods sit (in front of the chest).")]
        public Transform carryAnchor;
        [Tooltip("The visual child that hops and squashes (procedural fallback only).")]
        public Transform body;
        public float hopHeight = 0.12f;
        public float hopsPerSecond = 4f;
        public float turnSpeed = 12f;
        [Tooltip("World speed at which the walk animation plays at normal rate.")]
        public float walkAnimSpeed = 1.6f;
        [Tooltip("Direction to face when standing still; zero keeps the last heading.")]
        public Vector3 idleFacing;

        Vector3 _lastPos;
        Vector3 _baseScale = Vector3.one;
        Vector3 _basePos;
        float _time;
        float _moveBlend;
        float _carryWeight;
        bool _carrying;
        Quaternion? _faceOverride;

        void Start()
        {
            _lastPos = transform.position;
            if (body != null)
            {
                _baseScale = body.localScale;
                _basePos = body.localPosition;
            }
            if (animator != null)
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            if (idleFacing.sqrMagnitude > 0.0001f && !_faceOverride.HasValue)
                FaceWhenIdle(idleFacing);
        }

        /// <summary>Face a direction while standing still (e.g. a cashier facing customers).</summary>
        public void FaceWhenIdle(Vector3 direction) =>
            _faceOverride = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction, Vector3.up) : null;

        public void SetCarrying(bool carrying) => _carrying = carrying;

        public void SetWorking(bool working)
        {
            if (animator != null) animator.SetBool(WorkingId, working);
        }

        public void SetSitting(bool sitting)
        {
            if (animator != null) animator.SetBool(SittingId, sitting);
        }

        public void Cheer()
        {
            if (animator != null) animator.SetTrigger(CheerId);
        }

        public void Wave()
        {
            if (animator != null) animator.SetTrigger(WaveId);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector3 delta = transform.position - _lastPos;
            delta.y = 0f;
            _lastPos = transform.position;
            float speed = dt > 0f ? delta.magnitude / dt : 0f;
            bool moving = speed > 0.05f;
            _moveBlend = Mathf.MoveTowards(_moveBlend, moving ? 1f : 0f, dt * 6f);

            Quaternion? want = moving ? Quaternion.LookRotation(delta.normalized, Vector3.up) : _faceOverride;
            if (want.HasValue)
                transform.rotation = Quaternion.Slerp(transform.rotation, want.Value, turnSpeed * dt);

            if (animator != null)
            {
                animator.SetFloat(SpeedId, moving ? speed / walkAnimSpeed : 0f, 0.08f, dt);
                _carryWeight = Mathf.MoveTowards(_carryWeight, _carrying ? 1f : 0f, dt * 6f);
                if (carryLayer < animator.layerCount)
                    animator.SetLayerWeight(carryLayer, _carryWeight);
                return;
            }

            if (body == null)
                return;
            _time += dt * Mathf.Lerp(1f, hopsPerSecond, _moveBlend);
            float phase = _time * Mathf.PI * 2f;
            float hop = Mathf.Abs(Mathf.Sin(phase * 0.5f)) * hopHeight * _moveBlend;
            float squash = Mathf.Lerp(Mathf.Sin(phase) * 0.02f, Mathf.Cos(phase) * 0.06f, _moveBlend);
            body.localPosition = _basePos + Vector3.up * hop;
            body.localScale = Vector3.Scale(_baseScale, new Vector3(1f - squash * 0.5f, 1f + squash, 1f - squash * 0.5f));
        }
    }
}
