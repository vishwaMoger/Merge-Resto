using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MergeResto.Game.Views
{
    /// <summary>
    /// Moves the shop camera: eases to a focus (the tutorial following the first customer) and
    /// back home, and lets the player zoom with the mouse wheel. The angle never changes.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraDirector : MonoBehaviour
    {
        [SerializeField] float minZoom = 0.7f;
        [SerializeField] float maxZoom = 1.25f;
        [SerializeField] float ease = 3.5f;

        Camera _cam;
        Vector3 _homeTarget;
        float _homeSize;
        float _zoom = 1f;
        Transform _follow;
        Vector3 _focusPoint;
        float _focusSize;
        bool _focused;

        const float Distance = 30f;

        Vector3 Target => transform.position + transform.forward * Distance;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _homeTarget = Target;
            _homeSize = _cam.orthographicSize;
        }

        /// <summary>Changes the resting view (a bigger shop needs a wider shot).</summary>
        public void SetHome(Vector3 target, float size, bool snap)
        {
            if (_cam == null)
                Awake();
            _homeTarget = target;
            _homeSize = size;
            if (snap && !_focused)
                Apply(1f);
        }

        /// <summary>Follow a moving object at the given zoom (orthographic size).</summary>
        public void Follow(Transform target, float size)
        {
            _follow = target;
            _focusSize = size;
            _focused = true;
        }

        /// <summary>Hold on a point at the given zoom.</summary>
        public void Focus(Vector3 point, float size)
        {
            _follow = null;
            _focusPoint = point;
            _focusSize = size;
            _focused = true;
        }

        public void Release()
        {
            _focused = false;
            _follow = null;
        }

        /// <summary>Jump straight to the current focus (no easing), e.g. for an opening shot.</summary>
        public void Snap()
        {
            Apply(1f);
        }

        /// <summary>True once the camera is back (close to) its home framing.</summary>
        public bool AtHome => !_focused && (Target - _homeTarget).sqrMagnitude < 0.05f && Mathf.Abs(_cam.orthographicSize - _homeSize * _zoom) < 0.05f;

        void LateUpdate()
        {
            if (!_focused)
                ReadZoom();
            Apply(1f - Mathf.Exp(-Time.unscaledDeltaTime * ease));
        }

        void Apply(float k)
        {
            Vector3 wantTarget = _focused ? (_follow != null ? _follow.position + Vector3.up : _focusPoint) : _homeTarget;
            float wantSize = _focused ? _focusSize : _homeSize * _zoom;
            Vector3 target = Vector3.Lerp(Target, wantTarget, k);
            transform.position = target - transform.forward * Distance;
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, wantSize, k);
        }

        void ReadZoom()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                _zoom = Mathf.Clamp(_zoom * (scroll > 0f ? 0.92f : 1.08f), minZoom, maxZoom);
        }
    }
}
