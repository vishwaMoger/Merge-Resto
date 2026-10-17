using UnityEngine;
using UnityEngine.UI;

namespace MergeResto.Game.UI
{
    /// <summary>One tap mutes or unmutes all game audio (music and effects).</summary>
    public class SoundToggle : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image icon;
        [SerializeField] Sprite onSprite;
        [SerializeField] Sprite offSprite;

        void Start() => button.onClick.AddListener(Toggle);

        void Toggle()
        {
            var audio = AudioDirector.Instance;
            if (audio == null)
                return;
            bool on = !(audio.SoundOn || audio.MusicOn);
            audio.SetSound(on);
            audio.SetMusic(on);
            if (on)
                AudioDirector.Play(Sfx.Click);
        }

        void Update()
        {
            var audio = AudioDirector.Instance;
            bool on = audio == null || audio.SoundOn || audio.MusicOn;
            icon.sprite = on ? onSprite : offSprite;
        }
    }
}
