using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace MergeResto.Game.Views
{
    /// <summary>
    /// Moves the shop camera: eases to a focus (the tutorial following the first customer) and
    /// back home, fits the shot to any screen shape, and lets the player zoom (mouse wheel or
    /// pinch) and pan (drag) a little around the shop. The angle never changes.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraDirector : MonoBehaviour
    {
        [SerializeField] float minZoom = 0.6f;
        [SerializeField] float maxZoom = 1.25f;
        [SerializeField] float ease = 3.5f;
        [Tooltip("How far (world units) the player may drag the view from its home.")]
        [SerializeField] float maxPan = 7f;
        [Tooltip("Screen shape the shot was framed for; narrower screens zoom out to keep the width.")]
        [SerializeField] float designAspect = 16f / 9f;

        Camera _cam;
        Vector3 _homeTarget;
        float _homeSize;
        float _zoom = 1f;
        Vector3 _pan;
        Transform _follow;
        Vector3 _focusPoint;
        float _focusSize;
        bool _focused;
        float _pinchStart = -1f;
        float _zoomAtPinch;

        const float Distance = 30f;

        Vector3 Target => transform.position + transform.forward * Distance;

        /// <summary>True while the player is dragging or pinching the view (taps are ignored).</summary>
        public static bool Gesturing { get; private set; }

        /// <summary>When set (above 0), replaces the narrow-screen zoom-out (the portrait trailer frames the shop closer).</summary>
        public static float FitOverride = -1f;

        float AspectFit => FitOverride > 0f ? FitOverride : Mathf.Max(1f, designAspect / Mathf.Max(0.1f, _cam.aspect));

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _homeTarget = Target;
            _homeSize = _cam.orthographicSize;
        }

        void OnEnable() => EnhancedTouchSupport.Enable();

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
        public void Snap() => Apply(1f);

        /// <summary>True once the camera is back (close to) its home framing.</summary>
        public bool AtHome => !_focused && (Target - _homeTarget - _pan).sqrMagnitude < 0.05f;

        /// <summary>Drag the view by a screen-space delta (pixels).</summary>
        public void Pan(Vector2 screenDelta)
        {
            if (_focused)
                return;
            float worldPerPixel = 2f * _cam.orthographicSize / Screen.height;
            var right = transform.right;
            var up = Vector3.ProjectOnPlane(transform.up, Vector3.up).normalized;
            // On the ground, a screen-vertical pixel covers more distance (the camera looks down).
            float pitch = Mathf.Max(0.2f, Mathf.Sin(transform.eulerAngles.x * Mathf.Deg2Rad));
            _pan -= right * screenDelta.x * worldPerPixel + up * screenDelta.y * worldPerPixel / pitch;
            _pan = Vector3.ClampMagnitude(_pan, maxPan);
        }

        /// <summary>Called by the input controller when a drag starts or ends.</summary>
        public static void SetGesturing(bool on) => Gesturing = on;

        void LateUpdate()
        {
            if (!_focused)
                ReadZoom();
            Apply(1f - Mathf.Exp(-Time.unscaledDeltaTime * ease));
        }

        void Apply(float k)
        {
            Vector3 wantTarget = _focused ? (_follow != null ? _follow.position + Vector3.up : _focusPoint) : _homeTarget + _pan;
            float wantSize = (_focused ? _focusSize : _homeSize * _zoom) * AspectFit;
            // Dragging follows the finger directly; everything else eases.
            float kk = Gesturing && !_focused ? 1f : k;
            Vector3 target = Vector3.Lerp(Target, wantTarget, kk);
            transform.position = target - transform.forward * Distance;
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, wantSize, k);
        }

        void ReadZoom()
        {
            // Pinch with two fingers.
            var touches = Touch.activeTouches;
            if (touches.Count >= 2)
            {
                float d = Vector2.Distance(touches[0].screenPosition, touches[1].screenPosition);
                if (_pinchStart < 0f)
                {
                    _pinchStart = Mathf.Max(1f, d);
                    _zoomAtPinch = _zoom;
                }
                _zoom = Mathf.Clamp(_zoomAtPinch * _pinchStart / Mathf.Max(1f, d), minZoom, maxZoom);
                return;
            }
            _pinchStart = -1f;

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
