using MergeResto.Game.Views;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MergeResto.Game
{
    /// <summary>Turns clicks and taps on the 3D shop into station clicks. UI clicks are ignored.</summary>
    public class InputController : MonoBehaviour
    {
        [SerializeField] Camera worldCamera;
        [SerializeField] LayerMask clickMask = ~0;

        void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame)
                return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            var cam = worldCamera != null ? worldCamera : Camera.main;
            var ray = cam.ScreenPointToRay(pointer.position.ReadValue());
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
