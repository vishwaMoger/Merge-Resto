using MergeResto.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeResto.Game.UI
{
    /// <summary>
    /// Grandma's daily treats: a 7-day calendar. Opens by itself once a day (after the
    /// welcome-back summary), and from the Daily button whose dot shows a waiting reward.
    /// Today's reward can be doubled with a rewarded ad.
    /// </summary>
    public class DailyRewardsPanel : MonoBehaviour
    {
        [System.Serializable]
        public class Day
        {
            public Image frame;
            public TMP_Text label;
            public Image icon;
            public TMP_Text amount;
            public GameObject check;
        }

        [SerializeField] Day[] days;
        [SerializeField] RectTransform panel;
        [SerializeField] CanvasGroup group;
        [SerializeField] CanvasGroup dim;
        [SerializeField] Button openButton;
        [SerializeField] GameObject openAlert;
        [SerializeField] Button closeButton;
        [SerializeField] Button claimButton;
        [SerializeField] Button doubleButton;
        [SerializeField] TMP_Text doubleText;
        [SerializeField] TMP_Text footer;
        [SerializeField] CoinFlyLayer coinFly;

        [Header("Art")]
        [SerializeField] Sprite coinIcon;
        [SerializeField] Sprite gemIcon;
        [SerializeField] Sprite energyIcon;
        [SerializeField] Sprite giftIcon;
        [SerializeField] Sprite frameToday;
        [SerializeField] Sprite frameOther;
        [SerializeField] Sprite frameGift;

        bool _isOpen;
        float _open;
        float _velocity;
        float _autoOpenDelay = 1.2f;
        bool _autoOpened;

        void Start()
        {
            openButton.onClick.AddListener(() => SetOpen(!_isOpen));
            closeButton.onClick.AddListener(() => SetOpen(false));
            claimButton.onClick.AddListener(() => Claim(1));
            doubleButton.onClick.AddListener(() => AdService.ShowRewarded(() => Claim(2)));
            group.alpha = 0f;
            group.blocksRaycasts = false;
            dim.alpha = 0f;
            dim.blocksRaycasts = false;
        }

        public void SetOpen(bool open)
        {
            if (open == _isOpen)
                return;
            _isOpen = open;
            AudioDirector.Play(Sfx.Whoosh, open ? 1.05f : 0.9f, 0.5f);
            if (open)
                GameController.Instance.ClearSelection();
        }

        void Claim(int multiplier)
        {
            int index = DailyRewardStore.Claim();
            if (index < 0)
                return;
            var reward = DailyRewards.Week[index];
            GameController.Instance.GrantDaily(reward, multiplier);
            if (coinFly != null && reward.coins > 0)
                coinFly.FlyFromScreen(RectTransformUtility.WorldToScreenPoint(null, days[index].icon.transform.position), multiplier > 1 ? 14 : 8);
            AudioDirector.Play(index == days.Length - 1 || multiplier > 1 ? Sfx.Celebrate : Sfx.Claim);
            _closeAfter = 0.9f;
        }

        float _closeAfter = -1f;

        void Update()
        {
            float dt = UiTime.Delta;
            var sim = GameController.Instance.Sim;
            bool allowed = !TutorialFocus.Active && sim.State.tutorialStep >= ShopState.TutorialFinished;
            bool canClaim = DailyRewardStore.CanClaim;
            openButton.gameObject.SetActive(allowed);
            openAlert.SetActive(allowed && canClaim && !_isOpen);
            if (openAlert.activeSelf)
                openAlert.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 7f) * 0.12f);

            // Once per visit: open by itself when today's treat is waiting.
            if (allowed && canClaim && !_autoOpened && !OfflineEarningsDialog.IsOpen)
            {
                _autoOpenDelay -= dt;
                if (_autoOpenDelay <= 0f)
                {
                    _autoOpened = true;
                    SetOpen(true);
                }
            }
            if (_closeAfter > 0f)
            {
                _closeAfter -= dt;
                if (_closeAfter <= 0f)
                    SetOpen(false);
            }

            float target = _isOpen ? 1f : 0f;
            _velocity += ((target - _open) * 260f - _velocity * 20f) * dt;
            _open += _velocity * dt;
            group.alpha = Mathf.Clamp01(_open * 2f);
            group.blocksRaycasts = group.interactable = _isOpen;
            dim.alpha = Mathf.Clamp01(_open);
            dim.blocksRaycasts = _isOpen;
            panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, _open);
            if (_open > 0.01f || _isOpen)
                Refresh(canClaim);
        }

        void Refresh(bool canClaim)
        {
            var state = DailyRewardStore.State;
            int today = DailyRewardStore.TodayIndex;
            double scale = GameController.Instance.CoinScale;
            for (int i = 0; i < days.Length; i++)
            {
                var r = DailyRewards.Week[i];
                var d = days[i];
                d.label.text = $"Day {i + 1}";
                d.icon.sprite = r.kind switch
                {
                    DailyRewardKind.Gems => gemIcon,
                    DailyRewardKind.Energy => energyIcon,
                    DailyRewardKind.Gift => giftIcon,
                    _ => coinIcon,
                };
                d.amount.text = r.kind switch
                {
                    DailyRewardKind.Gems => $"{r.gems}",
                    DailyRewardKind.Energy => $"+{r.energy}",
                    DailyRewardKind.Gift => $"{NumberFormat.Short(r.coins * scale)} + {r.gems}",
                    _ => NumberFormat.Short(r.coins * scale),
                };
                // Claimed this week: every day up to the last claim of the current streak.
                bool claimed = canClaim ? i < today : i <= state.lastIndex;
                bool isToday = i == today && canClaim;
                d.check.SetActive(claimed);
                d.frame.sprite = isToday ? frameToday : i == days.Length - 1 ? frameGift : frameOther;
                d.frame.transform.localScale = Vector3.one * (isToday ? 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.04f : 1f);
                d.icon.color = claimed ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
            }
            claimButton.gameObject.SetActive(canClaim);
            doubleButton.gameObject.SetActive(canClaim);
            footer.gameObject.SetActive(!canClaim);
            footer.text = "Come back tomorrow for the next treat!";
            if (canClaim)
                doubleText.text = "Claim x2";
        }
    }
}
