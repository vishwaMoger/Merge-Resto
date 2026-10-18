using MergeResto.Game.Views;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace MergeResto.Game
{
    /// <summary>
    /// Turns clicks and taps on the 3D shop into station clicks, and drags into camera panning.
    /// A press that moves more than a few millimetres is a drag, not a tap. UI presses are ignored.
    /// </summary>
    public class InputController : MonoBehaviour
    {
        [SerializeField] Camera worldCamera;
        [SerializeField] LayerMask clickMask = ~0;
        [Tooltip("Movement (fraction of screen height) before a press counts as a drag.")]
        [SerializeField] float dragThreshold = 0.025f;

        CameraDirector _director;
        bool _pressing;
        bool _dragging;
        bool _overUi;
        Vector2 _start;
        Vector2 _last;

        void Start()
        {
            var cam = worldCamera != null ? worldCamera : Camera.main;
            _director = cam.GetComponent<CameraDirector>();
        }

        void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null)
                return;
            // A second finger means a pinch: cancel the tap and let the camera zoom.
            if (Touch.activeTouches.Count >= 2)
            {
                _dragging = true;
                _last = pointer.position.ReadValue();
                return;
            }

            Vector2 pos = pointer.position.ReadValue();
            if (pointer.press.wasPressedThisFrame)
            {
                _pressing = true;
                _dragging = false;
                _start = _last = pos;
                // The UI module tracks mouse and touch pointers; no id = the pointer that just pressed.
                _overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                return;
            }
            if (!_pressing)
                return;

            if (pointer.press.isPressed)
            {
                if (!_overUi && !_dragging && (pos - _start).magnitude > dragThreshold * Screen.height)
                {
                    _dragging = true;
                    CameraDirector.SetGesturing(true);
                }
                if (_dragging && !_overUi && _director != null)
                    _director.Pan(pos - _last);
                _last = pos;
                return;
            }

            // Released.
            _pressing = false;
            bool wasDrag = _dragging;
            _dragging = false;
            CameraDirector.SetGesturing(false);
            if (wasDrag || _overUi)
                return;
            Click(pos);
        }

        void Click(Vector2 screen)
        {
            var cam = worldCamera != null ? worldCamera : Camera.main;
            var ray = cam.ScreenPointToRay(screen);
            if (Physics.Raycast(ray, out var hit, 200f, clickMask))
            {
                var station = hit.collider.GetComponentInParent<StationView>();
                if (station != null)
                {
                    GameController.Instance.ClickStation(station.stationIndex);
                    return;
                }
            }
            GameController.Instance.ClearSelection();
        }
    }
}
