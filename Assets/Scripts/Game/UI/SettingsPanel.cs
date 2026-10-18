using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeResto.Game.UI
{
    /// <summary>
    /// Settings: separate music and sound switches, credits for the art, fonts and audio, and a
    /// full progress reset that has to be confirmed with a second tap.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] RectTransform panel;
        [SerializeField] CanvasGroup group;
        [SerializeField] CanvasGroup dim;
        [SerializeField] Button dimButton;
        [SerializeField] Button openButton;
        [SerializeField] Button closeButton;

        [Header("Main page")]
        [SerializeField] GameObject mainPage;
        [SerializeField] Button musicButton;
        [SerializeField] Image musicImage;
        [SerializeField] TMP_Text musicText;
        [SerializeField] Button soundButton;
        [SerializeField] Image soundImage;
        [SerializeField] TMP_Text soundText;
        [SerializeField] Button creditsButton;
        [SerializeField] Button resetButton;
        [SerializeField] Image resetImage;
        [SerializeField] TMP_Text resetText;

        [Header("Credits page")]
        [SerializeField] GameObject creditsPage;
        [SerializeField] Button backButton;

        [Header("Art")]
        [SerializeField] Sprite onSprite;
        [SerializeField] Sprite offSprite;
        [SerializeField] Sprite resetSprite;
        [SerializeField] Sprite confirmSprite;

        // Every save key the game writes; a reset clears them all.
        static readonly string[] SaveKeys = { "shop_grandmas_bakery", "shop_corner_cafe", "shop_nonnos_pizzeria" };

        bool _isOpen;
        float _open;
        float _velocity;
        float _confirmUntil = -1f;

        void Start()
        {
            openButton.onClick.AddListener(() => SetOpen(!_isOpen));
            closeButton.onClick.AddListener(() => SetOpen(false));
            dimButton.onClick.AddListener(() => SetOpen(false));
            musicButton.onClick.AddListener(() => AudioDirector.Instance?.SetMusic(!AudioDirector.Instance.MusicOn));
            soundButton.onClick.AddListener(() => AudioDirector.Instance?.SetSound(!AudioDirector.Instance.SoundOn));
            creditsButton.onClick.AddListener(() => ShowCredits(true));
            backButton.onClick.AddListener(() => ShowCredits(false));
            resetButton.onClick.AddListener(OnReset);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            dim.alpha = 0f;
            dim.blocksRaycasts = false;
            ShowCredits(false);
        }

        public void SetOpen(bool open)
        {
            if (open == _isOpen)
                return;
            _isOpen = open;
            AudioDirector.Play(Sfx.Whoosh, open ? 1.05f : 0.9f, 0.5f);
            if (open)
            {
                GameController.Instance.ClearSelection();
                ShowCredits(false);
                _confirmUntil = -1f;
            }
        }

        void ShowCredits(bool credits)
        {
            mainPage.SetActive(!credits);
            creditsPage.SetActive(credits);
        }

        void OnReset()
        {
            if (Time.unscaledTime > _confirmUntil)
            {
                _confirmUntil = Time.unscaledTime + 3f;
                AudioDirector.Play(Sfx.Deny);
                return;
            }
            foreach (var key in SaveKeys)
                SaveStore.Delete(key);
            WorldState.Reset();
            DailyRewardStore.Reset();
            PlayerPrefs.DeleteKey("board_opened");
            PlayerPrefs.Save();
            // Start over in Grandma's Bakery without saving this shop again.
            var gc = GameController.Instance;
            gc.enabled = false;
            UnityEngine.SceneManagement.SceneManager.LoadScene("Bakery");
        }

        void Update()
        {
            float dt = UiTime.Delta;
            openButton.gameObject.SetActive(!TutorialFocus.Active || _isOpen);

            float target = _isOpen ? 1f : 0f;
            _velocity += ((target - _open) * 260f - _velocity * 20f) * dt;
            _open += _velocity * dt;
            group.alpha = Mathf.Clamp01(_open * 2f);
            group.blocksRaycasts = group.interactable = _isOpen;
            dim.alpha = Mathf.Clamp01(_open);
            dim.blocksRaycasts = _isOpen;
            panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, _open);
            if (_open < 0.01f && !_isOpen)
                return;

            var audio = AudioDirector.Instance;
            bool music = audio == null || audio.MusicOn;
            bool sound = audio == null || audio.SoundOn;
            musicImage.sprite = music ? onSprite : offSprite;
            musicText.text = music ? "On" : "Off";
            soundImage.sprite = sound ? onSprite : offSprite;
            soundText.text = sound ? "On" : "Off";

            bool confirming = Time.unscaledTime <= _confirmUntil;
            resetImage.sprite = confirming ? confirmSprite : resetSprite;
            resetText.text = confirming ? "Tap again to erase everything" : "Reset progress";
        }
    }
}
