using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MergeResto.Game.UI
{
    /// <summary>
    /// Fits the HUD to the device: bigger buttons on touch screens (fingers need more room than a
    /// mouse) and, in portrait, a friendly "turn your device" screen, since the shop is landscape.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public class ScreenFit : MonoBehaviour
    {
        [SerializeField] Vector2 desktopReference = new(1920, 1080);
        [SerializeField] Vector2 touchReference = new(1640, 922);
        [SerializeField] GameObject rotatePrompt;
        [SerializeField] RectTransform rotateIcon;

        CanvasScaler _scaler;
        bool _paused;

        void Awake() => _scaler = GetComponent<CanvasScaler>();

        void Update()
        {
            bool touch = Touchscreen.current != null || Application.isMobilePlatform;
            _scaler.referenceResolution = touch ? touchReference : desktopReference;

            bool portrait = Screen.height > Screen.width * 1.05f;
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
