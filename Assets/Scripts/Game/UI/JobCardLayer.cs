using System.Collections.Generic;
using MergeResto.Game.Views;
using MergeResto.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeResto.Game.UI
{
    /// <summary>
    /// The renovation jobs, out in the shop: a little pin floats over every spot the current
    /// area still needs fixed (green when its items are ready), and a job card pops up over the
    /// spot when the player taps a pin or presses Show on the board. The card says what the job
    /// is, which items it needs and what it pays, with Merge (open the board) or Fix.
    /// </summary>
    public class JobCardLayer : MonoBehaviour
    {
        [System.Serializable]
        public class Pin
        {
            public RectTransform root;
            public Button button;
            public Image background;
            public GameObject ready;
            [System.NonSerialized] public int renovation = -1;
            [System.NonSerialized] public float shown;
        }

        [SerializeField] RectTransform layer;
        [SerializeField] Pin[] pins;
        [SerializeField] Sprite pinWaiting;
        [SerializeField] Sprite pinReady;

        [Header("Card")]
        [SerializeField] RectTransform card;
        [SerializeField] CanvasGroup cardGroup;
        [SerializeField] Button blocker;
        [SerializeField] TMP_Text cardTitle;
        [SerializeField] TMP_Text cardArea;
        [SerializeField] Image[] needFrames;
        [SerializeField] Image[] needIcons;
        [SerializeField] GameObject[] needChecks;
        [SerializeField] TMP_Text reward;
        [SerializeField] Button action;
        [SerializeField] Image actionImage;
        [SerializeField] TMP_Text actionText;
        [SerializeField] Button close;
        [SerializeField] Sprite actionMerge;
        [SerializeField] Sprite actionFix;
        [SerializeField] Color readyFrame = new(0.55f, 0.85f, 0.4f);
        [SerializeField] MergeBoardView board;

        const float CardFocusSize = 3.8f;

        readonly Dictionary<int, RenovationSpot> _spots = new();
        readonly List<Vector3> _pinned = new();
        MergeBoard _board;
        Camera _cam;
        CameraDirector _director;
        int _open = -1;
        float _cardShown;
        float _cardPop;

        /// <summary>True while a job card is up (lessons and windows wait).</summary>
        public static bool IsOpen { get; private set; }

        /// <summary>The pin over a job's spot, if it is showing (for the lessons' pointer).</summary>
        public RectTransform PinFor(int renovation)
        {
            foreach (var p in pins)
                if (p.renovation == renovation && p.root.gameObject.activeInHierarchy && p.shown > 0.9f)
                    return p.root;
            return null;
        }

        public RectTransform ActionRect => _open >= 0 ? (RectTransform)action.transform : null;

        void Start()
        {
            _board = GameController.Instance.Sim.Board;
            if (_board == null)
            {
                gameObject.SetActive(false);
                return;
            }
            _cam = Camera.main;
            _director = _cam != null ? _cam.GetComponent<CameraDirector>() : null;
            foreach (var spot in FindObjectsByType<RenovationSpot>(FindObjectsSortMode.None))
                _spots[spot.renovation] = spot;
            foreach (var p in pins)
            {
                var pin = p;
                pin.button.onClick.AddListener(() =>
                {
                    if (pin.renovation >= 0)
                    {
                        AudioDirector.Play(Sfx.Click);
                        Show(pin.renovation);
                    }
                });
                pin.root.gameObject.SetActive(false);
            }
            action.onClick.AddListener(OnAction);
            close.onClick.AddListener(Hide);
            blocker.onClick.AddListener(Hide);
            cardGroup.alpha = 0f;
            cardGroup.blocksRaycasts = false;
            blocker.gameObject.SetActive(false);
        }

        void OnDestroy() => IsOpen = false;

        /// <summary>Opens the job card over the job's spot and flies the camera there.</summary>
        public void Show(int renovation)
        {
            if (!_spots.TryGetValue(renovation, out var spot) || _board.IsRenovated(renovation))
                return;
            _open = renovation;
            _cardPop = 1f;
            IsOpen = true;
            blocker.gameObject.SetActive(true);
            GameController.Instance.ClearSelection();
            if (_director != null)
                _director.Focus(spot.Focus, CardFocusSize);
            AudioDirector.Play(Sfx.Whoosh, 1.1f, 0.5f);
        }

        public void Hide()
        {
            if (_open < 0)
                return;
            _open = -1;
            IsOpen = false;
            blocker.gameObject.SetActive(false);
            if (_director != null)
                _director.Release();
        }

        void OnAction()
        {
            int r = _open;
            if (r < 0)
                return;
            bool ready = _board.CanRenovate(r);
            Hide();
            if (ready)
                board.TryFix(r);
            else
                board.SetOpen(true);
        }

        bool ToLocal(Vector3 world, out Vector2 local)
        {
            local = default;
            var screen = _cam.WorldToScreenPoint(world);
            return screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, null, out local);
        }

        void LateUpdate()
        {
            float dt = UiTime.Delta;
            var sim = GameController.Instance.Sim;
            if (_open >= 0 && (_board.IsRenovated(_open) || MergeBoardView.AnyOpen || TutorialFocus.Active))
                Hide();

            // Pins over the current area's jobs, only while nothing else is in the way.
            bool pinsAllowed = !MergeBoardView.AnyOpen && _open < 0 && !TutorialFocus.Active && !RenovationSpot.Showing
                && sim.State.tutorialStep >= ShopState.TutorialFinished && !OfflineEarningsDialog.IsOpen;
            var active = _board.ActiveRenovations(pins.Length);
            _pinned.Clear();
            for (int i = 0; i < pins.Length; i++)
            {
                var p = pins[i];
                int r = i < active.Count ? active[i] : -1;
                // Two jobs on the same spot (the stand, then its cake) share one pin: the first.
                if (r >= 0 && _spots.TryGetValue(r, out var s0))
                {
                    foreach (var other in _pinned)
                        if ((other - s0.Focus).sqrMagnitude < 1.2f)
                            r = -1;
                    if (r >= 0)
                        _pinned.Add(s0.Focus);
                }
                p.renovation = r;
                bool want = pinsAllowed && r >= 0 && _spots.ContainsKey(r);
                p.shown = Mathf.MoveTowards(p.shown, want ? 1f : 0f, dt * 5f);
                p.root.gameObject.SetActive(p.shown > 0.01f);
                if (r < 0 || !_spots.TryGetValue(r, out var spot))
                    continue;
                if (!ToLocal(spot.Focus + Vector3.up * 1.1f, out var local))
                {
                    p.root.gameObject.SetActive(false);
                    continue;
                }
                bool ready = _board.CanRenovate(r);
                float hop = Mathf.Abs(Mathf.Sin(Time.unscaledTime * (ready ? 4.5f : 2.4f) + i));
                p.root.anchoredPosition = local + new Vector2(0f, hop * (ready ? 16f : 7f));
                p.root.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, p.shown) * (ready ? 1.08f : 1f);
                p.background.sprite = ready ? pinReady : pinWaiting;
                p.ready.SetActive(ready);
            }

            // The card rides above its spot.
            _cardShown = Mathf.MoveTowards(_cardShown, _open >= 0 ? 1f : 0f, dt * 6f);
            cardGroup.alpha = _cardShown;
            cardGroup.blocksRaycasts = cardGroup.interactable = _open >= 0;
            if (_open < 0 || !_spots.TryGetValue(_open, out var openSpot))
                return;
            if (ToLocal(openSpot.Focus + Vector3.up * 0.8f, out var at))
            {
                var half = layer.rect.size * 0.5f;
                var size = card.rect.size;
                at.x = Mathf.Clamp(at.x, -half.x + size.x * 0.5f + 20f, half.x - size.x * 0.5f - 20f);
                at.y = Mathf.Clamp(at.y, -half.y + 20f, half.y - size.y - 20f);
                card.anchoredPosition = Vector2.Lerp(card.anchoredPosition, at, _cardShown < 0.5f ? 1f : 1f - Mathf.Exp(-dt * 12f));
            }
            _cardPop = Mathf.Max(0f, _cardPop - dt * 3f);
            card.localScale = Vector3.one * (Mathf.Lerp(0.85f, 1f, _cardShown) + Mathf.Sin(_cardPop * Mathf.PI) * 0.06f);
            Refresh(_open);
        }

        void Refresh(int r)
        {
            var def = _board.Def.renovations[r];
            cardTitle.text = def.displayName;
            var (done, total) = _board.AreaProgress(def.area);
            cardArea.text = $"{_board.AreaName(def.area)}  ·  {done}/{total}";
            var counted = new Dictionary<int, int>();
            for (int n = 0; n < needIcons.Length; n++)
            {
                bool used = n < def.needs.Count;
                needFrames[n].gameObject.SetActive(used);
                if (!used)
                    continue;
                var need = def.needs[n];
                var item = BoardItem.Item(need.chain, need.tier);
                counted[item.code] = counted.TryGetValue(item.code, out int c) ? c + 1 : 1;
                bool have = _board.Count(need) >= counted[item.code];
                needIcons[n].sprite = board.IconFor(item);
                needFrames[n].color = have ? readyFrame : Color.white;
                needChecks[n].SetActive(have);
            }
            reward.text = $"+{NumberFormat.Short(def.rewardCoins)} coins   +{Mathf.RoundToInt(def.coziness * 100f)}% cozy";
            bool ready = _board.CanRenovate(r);
            actionImage.sprite = ready ? actionFix : actionMerge;
            actionText.text = ready ? "Fix it!" : "Merge";
            action.transform.localScale = Vector3.one * (ready ? 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.05f : 1f);
        }
    }
}
