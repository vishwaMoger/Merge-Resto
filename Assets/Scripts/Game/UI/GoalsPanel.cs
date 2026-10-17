using MergeResto.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeResto.Game.UI
{
    /// <summary>
    /// The shop's next few goals, top left: what to do, how far along it is, and the reward.
    /// A finished goal's reward turns into a glowing Claim button; claiming slides the next
    /// goal in. Hidden during the tutorial and while an upgrade card is open.
    /// </summary>
    public class GoalsPanel : MonoBehaviour
    {
        [System.Serializable]
        public class Card
        {
            public RectTransform root;
            public Image icon;
            public TMP_Text title;
            public TMP_Text count;
            public RectTransform fill;
            public Button claim;
            public Image claimImage;
            public Image rewardIcon;
            public TMP_Text rewardText;
            public ButtonJuice juice;
            [System.NonSerialized] public int goal = -1;
            [System.NonSerialized] public float slide;
        }

        [SerializeField] CanvasGroup group;
        [SerializeField] Card[] cards;
        [SerializeField] CoinFlyLayer coinFly;

        [Header("Art")]
        [SerializeField] Sprite claimReady;
        [SerializeField] Sprite claimWaiting;
        [SerializeField] Sprite coinIcon;
        [SerializeField] Sprite gemIcon;
        [SerializeField] Sprite levelIcon;
        [SerializeField] Sprite serveIcon;
        [SerializeField] Sprite managerIcon;
        [SerializeField] Sprite machineIcon;
        [SerializeField] Sprite shopLevelIcon;
        [SerializeField] Sprite renovateIcon;
        [SerializeField] Color waitingText = new(0.4f, 0.25f, 0.16f);
        [Tooltip("Reward text material while the goal is unfinished (same font, no outline).")]
        [SerializeField] Material waitingMaterial;

        ShopSim _sim;
        Material _outlined;
        Material _plain;

        void Start()
        {
            _sim = GameController.Instance.Sim;
            foreach (var card in cards)
            {
                var c = card;
                c.claim.onClick.AddListener(() => Claim(c));
            }
            _outlined = cards[0].rewardText.fontSharedMaterial;
            _plain = waitingMaterial != null ? waitingMaterial : _outlined;
            group.alpha = 0f;
        }

        void Claim(Card card)
        {
            if (card.goal < 0 || !_sim.ClaimGoal(card.goal))
                return;
            if (coinFly != null && _sim.Def.goals[card.goal].rewardCoins > 0)
                coinFly.FlyFromScreen(RectTransformUtility.WorldToScreenPoint(null, card.claim.transform.position), 8);
            card.goal = -1; // refilled (and slid in) next frame
        }

        void Update()
        {
            float dt = UiTime.Delta;
            bool show = _sim.State.tutorialStep >= GrandOpeningTutorial.Finished && !TutorialFocus.Active
                && GameController.Instance.SelectedStation < 0 && _sim.Def.goals.Count > 0;
            group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, dt * 5f);
            group.blocksRaycasts = group.interactable = group.alpha > 0.5f;
            if (group.alpha <= 0f)
                return;

            var active = _sim.ActiveGoals(cards.Length);
            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                int goal = i < active.Count ? active[i] : -1;
                if (goal != card.goal)
                {
                    card.goal = goal;
                    card.slide = 1f;
                }
                card.root.gameObject.SetActive(goal >= 0);
                if (goal < 0)
                    continue;

                card.slide = Mathf.MoveTowards(card.slide, 0f, dt * 3.5f);
                float ease = card.slide * card.slide;
                card.root.anchoredPosition = new Vector2(-460f * ease, card.root.anchoredPosition.y);
                Fill(card, goal);
            }
        }

        void Fill(Card card, int goal)
        {
            var g = _sim.Def.goals[goal];
            double target = _sim.GoalTarget(goal);
            double progress = _sim.GoalProgress(goal);
            bool done = progress >= target;

            card.icon.sprite = g.kind switch
            {
                GoalKind.StationLevel => levelIcon,
                GoalKind.ServeCustomers => serveIcon,
                GoalKind.HireManager => managerIcon,
                GoalKind.Renovations => renovateIcon,
                GoalKind.AddMachines => machineIcon,
                GoalKind.LifetimeCoins => coinIcon,
                _ => shopLevelIcon,
            };
            card.title.text = Describe(g);
            card.count.text = g.kind == GoalKind.HireManager
                ? (done ? "Done!" : "")
                : $"{NumberFormat.Short(progress)}/{NumberFormat.Short(target)}";
            float t = target > 0 ? Mathf.Clamp01((float)(progress / target)) : 1f;
            var fillMax = card.fill.anchorMax;
            fillMax.x = Mathf.MoveTowards(fillMax.x, Mathf.Max(0.02f, t), UiTime.Delta * 2f);
            card.fill.anchorMax = fillMax;

            bool gems = g.rewardGems > 0;
            card.rewardIcon.sprite = gems ? gemIcon : coinIcon;
            card.rewardText.text = gems ? $"+{g.rewardGems}" : $"+{NumberFormat.Short(g.rewardCoins)}";
            card.claim.interactable = done;
            card.claimImage.sprite = done ? claimReady : claimWaiting;
            card.rewardText.fontSharedMaterial = done ? _outlined : _plain;
            card.rewardText.color = done ? Color.white : waitingText;
            float pulse = done ? 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.06f : 1f;
            card.claim.transform.localScale = Vector3.one * pulse;
        }

        static string Noun(int station) => station switch
        {
            0 => "flour pile",
            1 => "mixer",
            2 => "oven",
            _ => "cashier",
        };

        string Describe(GoalDefinition g)
        {
            int s = Mathf.Clamp(g.station, 0, _sim.Def.stations.Count - 1);
            string station = _sim.Def.stations[s].displayName;
            return g.kind switch
            {
                GoalKind.StationLevel => $"Upgrade the {station} to Lv {g.target:0}",
                GoalKind.ServeCustomers => $"Serve {NumberFormat.Short(g.target)} customers",
                GoalKind.HireManager => $"Hire a manager for the {station}",
                GoalKind.Renovations => g.target <= 1 ? "Finish your first renovation" : $"Finish {g.target:0} renovations",
                GoalKind.AddMachines => $"Get {g.target + 1:0} {Noun(s)}s",
                GoalKind.LifetimeCoins => $"Earn {NumberFormat.Short(g.target)} coins",
                _ => $"Reach shop level {g.target:0}",
            };
        }
    }
}
