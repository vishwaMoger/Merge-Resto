using MergeResto.Sim;
using TMPro;
using UnityEngine;

namespace MergeResto.Game.UI
{
    /// <summary>
    /// Bottom sheet for the selected station: its three upgrades (same layout for every room)
    /// and the manager who automates it. Slides in when a station is selected.
    /// </summary>
    public class StationPanel : MonoBehaviour
    {
        [System.Serializable]
        public class UpgradeRow
        {
            public UpgradeTrack track;
            public TMP_Text title;
            public TMP_Text detail;
            public TMP_Text cost;
            public UnityEngine.UI.Button button;
            public UnityEngine.UI.Image buttonImage;
        }

        [SerializeField] RectTransform sheet;
        [SerializeField] TMP_Text stationName;
        [SerializeField] TMP_Text stationInfo;
        [SerializeField] UpgradeRow[] rows;
        [SerializeField] TMP_Text managerTitle;
        [SerializeField] TMP_Text managerCost;
        [SerializeField] UnityEngine.UI.Button managerButton;
        [SerializeField] UnityEngine.UI.Image managerButtonImage;
        [SerializeField] UnityEngine.UI.Button closeButton;
        [SerializeField] Color affordable = new(0.45f, 0.78f, 0.45f);
        [SerializeField] Color unaffordable = new(0.75f, 0.72f, 0.7f);
        [SerializeField] Color maxed = new(0.98f, 0.8f, 0.4f);

        int _station = -1;
        float _shown;          // 0 hidden .. 1 shown
        float _hiddenOffset;

        void Start()
        {
            _hiddenOffset = -sheet.rect.height - 40f;
            sheet.anchoredPosition = new Vector2(0f, _hiddenOffset);
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

        void OnSelected(int station) => _station = station;

        void Buy(UpgradeTrack track)
        {
            if (_station >= 0 && GameController.Instance.Sim.BuyUpgrade(_station, track))
                Refresh();
        }

        void HireManager()
        {
            if (_station >= 0 && GameController.Instance.Sim.HireManager(_station))
                Refresh();
        }

        void Update()
        {
            float target = _station >= 0 ? 1f : 0f;
            _shown = Mathf.MoveTowards(_shown, target, Time.unscaledDeltaTime * 6f);
            float eased = 1f - Mathf.Pow(1f - _shown, 3f);
            sheet.anchoredPosition = new Vector2(0f, Mathf.Lerp(_hiddenOffset, 0f, eased));
            if (_station >= 0)
                Refresh();
        }

        void Refresh()
        {
            var sim = GameController.Instance.Sim;
            var def = sim.Def.stations[_station];
            var st = sim.State.stations[_station];
            bool seller = def.kind == StationKind.Seller;

            stationName.text = def.displayName;
            stationInfo.text = seller
                ? $"Lv {st.TotalLevels}  ·  {NumberFormat.Short(sim.PricePerItem)} coins per item  ·  {sim.Machines(_station)} cashier(s)"
                : $"Lv {st.TotalLevels}  ·  {sim.Batch(_station)} per batch  ·  {sim.CycleSeconds(_station):0.0}s  ·  {sim.Machines(_station)} machine(s)";

            foreach (var row in rows)
            {
                var up = def.Upgrade(row.track);
                if (up == null)
                {
                    row.button.gameObject.SetActive(false);
                    continue;
                }
                int level = st.Level(row.track);
                row.title.text = $"{up.displayName}  <size=75%>Lv {level}/{up.maxLevel}</size>";
                row.detail.text = Effect(sim, row.track, seller);
                bool isMax = sim.IsMaxed(_station, row.track);
                bool canBuy = sim.CanBuyUpgrade(_station, row.track);
                row.cost.text = isMax ? "MAX" : NumberFormat.Short(sim.UpgradeCost(_station, row.track));
                row.button.interactable = canBuy;
                row.buttonImage.color = isMax ? maxed : canBuy ? affordable : unaffordable;
            }

            if (st.hasManager)
            {
                managerTitle.text = def.startsAutomated ? "Always staffed" : "Manager hired: runs on its own";
                managerCost.text = "Hired";
                managerButton.interactable = false;
                managerButtonImage.color = maxed;
            }
            else
            {
                managerTitle.text = "Hire a manager: no more tapping";
                managerCost.text = $"{sim.ManagerCost(_station)} gems";
                managerButton.interactable = sim.CanHireManager(_station);
                managerButtonImage.color = managerButton.interactable ? affordable : unaffordable;
            }
        }

        static string Effect(ShopSim sim, UpgradeTrack track, bool seller) => track switch
        {
            UpgradeTrack.Speed => seller ? "Serve customers faster" : "Shorter cooking time",
            UpgradeTrack.Output => seller ? "+20% price per item" : "+50% goods per batch",
            _ => seller ? "Another cashier at the counter" : "Another machine working in parallel",
        };
    }
}
