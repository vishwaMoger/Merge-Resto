using TMPro;
using UnityEngine;

namespace MergeResto.Game.UI
{
    /// <summary>Hides this element's background while its label has no text (e.g. empty stock).</summary>
    [RequireComponent(typeof(UnityEngine.UI.Image))]
    public class HideWhenEmpty : MonoBehaviour
    {
        public TMP_Text label;
        UnityEngine.UI.Image _image;

        void Awake() => _image = GetComponent<UnityEngine.UI.Image>();

        void LateUpdate()
        {
            bool show = label != null && !string.IsNullOrEmpty(label.text);
            if (_image.enabled != show)
                _image.enabled = show;
        }
    }
}
