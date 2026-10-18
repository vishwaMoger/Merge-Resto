using MergeResto.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeResto.Game.UI
{
    /// <summary>
    /// "x2 income": watch a rewarded ad for five minutes of doubled sales, stackable up to an
    /// hour. Counts down only while the shop is open; glows while active.
    /// </summary>
    public class BoostButton : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image background;
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text timer;
        [SerializeField] Sprite idleSprite;
        [SerializeField] Sprite activeSprite;
        [SerializeField] float secondsPerAd = 300f;
        [SerializeField] ButtonJuice juice;

        ShopSim _sim;
        CanvasGroup _group;

        void Start()
        {
            _sim = GameController.Instance.Sim;
            // Hidden with a canvas group: deactivating this object would stop Update for good.
            if (!TryGetComponent(out _group))
                _group = gameObject.AddComponent<CanvasGroup>();
            button.onClick.AddListener(() => AdService.ShowRewarded(() =>
            {
                if (_sim.AddBoost(secondsPerAd))
                {
                    AudioDirector.Play(Sfx.Celebrate);
                    if (juice != null) juice.Pop(10f);
                    GameController.Instance.Save();
                }
            }));
        }

        void Update()
        {
            bool tutorial = _sim.State.tutorialStep < ShopState.TutorialFinished;
            bool show = !tutorial && !TutorialFocus.Active;
            _group.alpha = Mathf.MoveTowards(_group.alpha, show ? 1f : 0f, UiTime.Delta * 4f);
            _group.blocksRaycasts = _group.interactable = show;
            bool active = _sim.BoostActive;
            bool full = _sim.State.boostSeconds >= ShopSim.MaxBoostSeconds - 1f;
            background.sprite = active ? activeSprite : idleSprite;
            background.color = active ? Color.white : new Color(0.45f, 0.78f, 0.36f);
            int s = Mathf.CeilToInt(_sim.State.boostSeconds);
            timer.text = active ? (full ? $"{s / 60}:{s % 60:00} (max)" : $"{s / 60}:{s % 60:00}  +5m") : "Watch ad: 5 min";
            float pulse = active ? 1f + Mathf.Sin(Time.unscaledTime * 4f) * 0.03f : 1f + Mathf.Sin(Time.unscaledTime * 2.5f) * 0.05f;
            transform.localScale = Vector3.one * pulse;
            button.interactable = !full && !AdService.Busy;
        }
    }
}
