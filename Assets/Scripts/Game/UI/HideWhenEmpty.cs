using TMPro;
using UnityEngine;

namespace MergeResto.Game.UI
{
    /// <summary>Hides this element's background images while its label has no text (e.g. empty stock).</summary>
    public class HideWhenEmpty : MonoBehaviour
    {
        public TMP_Text label;
        UnityEngine.UI.Image[] _images;
        bool _shown = true;

        void Awake() => _images = GetComponentsInChildren<UnityEngine.UI.Image>(true);

        void LateUpdate()
        {
            bool show = label != null && !string.IsNullOrEmpty(label.text);
            if (_shown == show)
                return;
            _shown = show;
            foreach (var image in _images)
                image.enabled = show;
        }
    }
}
