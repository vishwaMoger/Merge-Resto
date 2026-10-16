using MergeResto.Sim;
using TMPro;
using UnityEngine;

namespace MergeResto.Game.UI
{
    /// <summary>
    /// Card for the selected station: its three upgrades (same layout for every room, showing
    /// the before → after value) and the manager who automates it. Springs in from the side.
    /// </summary>
    public class StationPanel : MonoBehaviour
    {
        [System.Serializable]
        public class UpgradeRow
        {
            public UpgradeTrack track;
            public TMP_Text title;
            public TMP_Text level;
            public TMP_Text detail;
            public TMP_Text cost;
            public UnityEngine.UI.Button button;
            public UnityEngine.UI.Image buttonImage;
            public GameObject costIcon;
        }

        [SerializeField] RectTransform sheet;
        [SerializeField] TMP_Text stationName;
        [SerializeField] TMP_Text stationInfo;
        [SerializeField] UpgradeRow[] rows;
        [SerializeField] TMP_Text managerTitle;
        [SerializeField] TMP_Text managerDetail;
        [SerializeField] TMP_Text managerCost;
        [SerializeField] UnityEngine.UI.Button managerButton;
        [SerializeField] UnityEngine.UI.Image managerButtonImage;
        [SerializeField] GameObject managerCostIcon;
        [SerializeField] UnityEngine.UI.Button closeButton;
        [SerializeField] Sprite affordableSprite;
        [SerializeField] Sprite unaffordableSprite;
        [SerializeField] Sprite maxedSprite;
        [SerializeField] Sprite managerSprite;
        [SerializeField] Color goodValue = new(0.27f, 0.62f, 0.22f);

        int _station = -1;
        int _shownStation = -1;
        float _shown;
        float _velocity;
        Vector2 _shownPos;
        Vector2 _hiddenPos;

        void Start()
        {
            _shownPos = sheet.anchoredPosition;
            _hiddenPos = _shownPos + new Vector2(-sheet.rect.width - 80f, 0f);
            sheet.anchoredPosition = _hiddenPos;
            GameController.Instance.StationSelected += OnSelected;
            foreach (var row in rows)
            {
                var r = row;
                r.button.onClick.AddListener(() => Buy(r.track));
            }
            managerButton.onClick.AddListener(HireManager);
            closeButton.onClick.AddListener(() => GameController.Instance.ClearSelection());
        }

        void OnDestroy()
        {
            if (GameController.Instance != null)
                GameController.Instance.StationSelected -= OnSelected;
        }

        void OnSelected(int station)
        {
            _station = station;
            if (station >= 0)
                _shownStation = station;
        }

        void Buy(UpgradeTrack track)
        {
            if (_shownStation >= 0)
                GameController.Instance.Sim.BuyUpgrade(_shownStation, track);
        }

        void HireManager()
        {
            if (_shownStation >= 0)
                GameController.Instance.Sim.HireManager(_shownStation);
        }

        void Update()
        {
            // Critically-damped-ish spring with a touch of overshoot.
            float target = _station >= 0 ? 1f : 0f;
            float dt = UiTime.Delta;
            _velocity += ((target - _shown) * 260f - _velocity * 22f) * dt;
            _shown += _velocity * dt;
            sheet.anchoredPosition = Vector2.LerpUnclamped(_hiddenPos, _shownPos, _shown);
            if (_shownStation >= 0)
                Refresh();
        }

        void Refresh()
        {
            var sim = GameController.Instance.Sim;
            int s = _shownStation;
            var def = sim.Def.stations[s];
            var st = sim.State.stations[s];
            bool seller = def.kind == StationKind.Seller;

            stationName.text = def.displayName;
            stationInfo.text = seller
                ? $"Level {st.TotalLevels}   ·   {NumberFormat.Short(sim.PricePerItem)} coins each   ·   {sim.Machines(s)} cashier{(sim.Machines(s) > 1 ? "s" : "")}"
                : $"Level {st.TotalLevels}   ·   {sim.Batch(s)} per batch   ·   {sim.CycleSeconds(s):0.0}s each";

            foreach (var row in rows)
            {
                var up = def.Upgrade(row.track);
                bool show = up != null;
                row.button.transform.parent.gameObject.SetActive(show);
                if (!show)
                    continue;
                int level = st.Level(row.track);
                bool isMax = sim.IsMaxed(s, row.track);
                bool canBuy = sim.CanBuyUpgrade(s, row.track);
                row.title.text = up.displayName;
                row.level.text = isMax ? "MAX" : $"Lv {level}";
                row.detail.text = Effect(sim, s, row.track, level, isMax, seller);
                row.cost.text = isMax ? "MAX" : NumberFormat.Short(sim.UpgradeCost(s, row.track));
                row.costIcon.SetActive(!isMax);
                row.button.interactable = canBuy;
                row.buttonImage.sprite = isMax ? maxedSprite : canBuy ? affordableSprite : unaffordableSprite;
            }

            if (st.hasManager)
            {
                managerTitle.text = def.startsAutomated ? "Always staffed" : "Manager on duty";
                managerDetail.text = "This room runs on its own";
                managerCost.text = "Hired";
                managerCostIcon.SetActive(false);
                managerButton.interactable = false;
                managerButtonImage.sprite = maxedSprite;
            }
            else
            {
                managerTitle.text = "Hire a Manager";
                managerDetail.text = "Runs this room for you";
                managerCost.text = sim.ManagerCost(s).ToString();
                managerCostIcon.SetActive(true);
                managerButton.interactable = sim.CanHireManager(s);
                managerButtonImage.sprite = managerButton.interactable ? managerSprite : unaffordableSprite;
            }
        }

        string Effect(ShopSim sim, int s, UpgradeTrack track, int level, bool isMax, bool seller)
        {
            string hex = ColorUtility.ToHtmlStringRGB(goodValue);
            string Arrow(string now, string next) => isMax ? now : $"{now}  <color=#{hex}>» {next}</color>";
            switch (track)
            {
                case UpgradeTrack.Speed:
                    return Arrow($"{sim.CycleSecondsAtLevel(s, level):0.0}s", $"{sim.CycleSecondsAtLevel(s, level + 1):0.0}s");
                case UpgradeTrack.Output:
                    return seller
                        ? Arrow($"{NumberFormat.Short(sim.PriceAtLevel(level))} coins", $"{NumberFormat.Short(sim.PriceAtLevel(level + 1))} coins")
                        : Arrow($"{sim.BatchAtLevel(s, level)} per batch", $"{sim.BatchAtLevel(s, level + 1)}");
                default:
                    return Arrow($"{1 + level} {(seller ? "cashier" : "machine")}{(level > 0 ? "s" : "")}", $"{2 + level}");
            }
        }
    }
}
