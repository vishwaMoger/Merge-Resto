using MergeResto.Game.Views;
using MergeResto.Sim;
using TMPro;
using UnityEngine;

namespace MergeResto.Game.UI
{
    /// <summary>
    /// Screen-space marker that follows one station: a radial progress ring while it works,
    /// a bouncing "Tap!" pin that touches down on the machine when it is waiting for the
    /// player, an upgrade badge when something here is affordable, and its stock count.
    /// </summary>
    public class StationHud : MonoBehaviour
    {
        public StationView station;
        [SerializeField] UnityEngine.UI.Image ringFill;
        [SerializeField] GameObject ring;
        [SerializeField] RectTransform tapPin;
        [SerializeField] RectTransform upgradeBadge;
        [SerializeField] GameObject managerBadge;
        [SerializeField] TMP_Text stockText;

        RectTransform _rect;
        RectTransform _canvasRect;
        Camera _cam;
        float _pinShown;
        float _badgeShown;

        /// <summary>The upgrade badge, when it is showing (the tutorial points at it).</summary>
        public RectTransform UpgradeBadge => _badgeShown > 0.5f ? upgradeBadge : null;

        void Start()
        {
            _rect = (RectTransform)transform;
            _canvasRect = (RectTransform)GetComponentInParent<Canvas>().transform;
            _cam = Camera.main;
            var button = upgradeBadge.GetComponent<UnityEngine.UI.Button>();
            if (button != null)
                button.onClick.AddListener(() => GameController.Instance.SelectStation(station.stationIndex));
        }

        bool ToLocal(Vector3 world, out Vector2 local)
        {
            Vector3 screen = _cam.WorldToScreenPoint(world);
            local = default;
            return screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out local);
        }

        void LateUpdate()
        {
            if (station == null || GameController.Instance == null)
                return;
            var sim = GameController.Instance.Sim;
            int i = station.stationIndex;
            float dt = UiTime.Delta;

            bool visible = ToLocal(station.hudAnchor.position, out var local);
            if (visible)
                _rect.anchoredPosition = local;

            float progress = station.Progress;
            ring.SetActive(visible && progress >= 0f);
            if (progress >= 0f)
                ringFill.fillAmount = progress;

            // Tap pin: its tip rests on the machine and it hops, so it is obvious what to click.
            bool wantsTap = visible && sim.NeedsTap(i) && !TutorialFocus.Active && !GuideOverlay.Showing && !MergeBoardView.AnyOpen;
            _pinShown = Mathf.MoveTowards(_pinShown, wantsTap ? 1f : 0f, dt * 6f);
            tapPin.gameObject.SetActive(_pinShown > 0.01f);
            bool hasTip = ToLocal(station.tapAnchor != null ? station.tapAnchor.position : station.hudAnchor.position, out var tip);
            if (_pinShown > 0.01f && hasTip)
            {
                float t = Time.unscaledTime * 3.2f + i * 0.7f;
                float hop = Mathf.Abs(Mathf.Sin(t));
                float squash = hop < 0.15f ? (0.15f - hop) / 0.15f : 0f;
                tapPin.anchoredPosition = tip - local + new Vector2(0f, hop * 20f);
                float pop = Mathf.SmoothStep(0f, 1f, _pinShown);
                tapPin.localScale = new Vector3(pop * (1f + squash * 0.12f), pop * (1f - squash * 0.12f), 1f);
            }

            // Upgrade badge on the machine's shoulder: something here can be bought right now.
            // The tap pin has priority, so the two never stack.
            bool canUpgrade = visible && hasTip && !wantsTap && GameController.Instance.SelectedStation != i && AnythingAffordable(sim, i)
                && (!TutorialFocus.Active || (TutorialFocus.ShowBadge && TutorialFocus.Station == i));
            _badgeShown = Mathf.MoveTowards(_badgeShown, canUpgrade ? 1f : 0f, dt * 6f);
            upgradeBadge.gameObject.SetActive(_badgeShown > 0.01f);
            if (_badgeShown > 0.01f)
            {
                upgradeBadge.anchoredPosition = tip - local + new Vector2(50f, 16f);
                float wobble = 1f + Mathf.Sin(Time.unscaledTime * 6f + i) * 0.06f;
                upgradeBadge.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, _badgeShown) * wobble;
            }

            managerBadge.SetActive(visible && sim.IsAutomated(i) && sim.Def.stations[i].kind != StationKind.Seller);

            var def = sim.Def.stations[i];
            var st = sim.State.stations[i];
            int stock = def.kind == StationKind.Seller ? st.input : st.output;
            stockText.text = stock > 0 ? stock.ToString() : "";
        }

        static bool AnythingAffordable(ShopSim sim, int station)
        {
            if (sim.CanHireManager(station))
                return true;
            foreach (UpgradeTrack track in System.Enum.GetValues(typeof(UpgradeTrack)))
                if (sim.CanBuyUpgrade(station, track))
                    return true;
            return false;
        }
    }
}
