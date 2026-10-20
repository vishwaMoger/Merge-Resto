using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MergeResto.Game.UI
{
    /// <summary>
    /// Fits the HUD to the device: bigger buttons on touch screens (fingers need more room than a
    /// mouse) and, on a phone or tablet held upright, a friendly "turn your device" screen, since the
    /// shop is landscape. A tall window on a computer just shows the shop (the camera zooms out to fit).
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public class ScreenFit : MonoBehaviour
    {
        [SerializeField] Vector2 desktopReference = new(2100, 1181);
        [SerializeField] Vector2 touchReference = new(1640, 922);
        [SerializeField] GameObject rotatePrompt;
        [SerializeField] RectTransform rotateIcon;

        CanvasScaler _scaler;
        bool _paused;

        void Awake()
        {
            _scaler = GetComponent<CanvasScaler>();
        }

        void Update()
        {
            bool touch = Touchscreen.current != null || Application.isMobilePlatform;
            _scaler.referenceResolution = touch ? touchReference : desktopReference;
            // The whole reference area always fits: a window wider or taller than it just gets more room
            // around the edges, so tall panels (the renovation board) never run off the screen.
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            bool portrait = Application.isMobilePlatform && Screen.height > Screen.width * 1.05f;
            if (rotatePrompt != null && rotatePrompt.activeSelf != portrait)
            {
                rotatePrompt.SetActive(portrait);
                rotatePrompt.transform.SetAsLastSibling();
            }
            // The shop waits while the player turns the device.
            if (portrait != _paused)
            {
                _paused = portrait;
                Time.timeScale = portrait ? 0f : 1f;
            }
            if (portrait && rotateIcon != null)
                rotateIcon.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 2.2f) * 45f - 45f);
        }

        void OnDisable()
        {
            if (_paused)
                Time.timeScale = 1f;
        }
    }
}
