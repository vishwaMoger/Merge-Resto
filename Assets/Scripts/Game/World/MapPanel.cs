using MergeResto.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeResto.Game.UI
{
    /// <summary>
    /// "Main Street": every shop as a card. Open shops can be visited; the next one shows what
    /// it takes to open (renovations and shop level in the shop before it, plus a coin price
    /// paid there); later ones are teased as coming soon.
    /// </summary>
    public class MapPanel : MonoBehaviour
    {
        [System.Serializable]
        public class Shop
        {
            public string id;
            public string displayName;
            [TextArea] public string blurb;
            public string sceneName;
            public Sprite picture;
            public bool comingSoon;
            [Header("Unlock (from the shop before it)")]
            public string requiresShop;
            public int renovationsNeeded;
            public int shopLevelNeeded;
            public double unlockCost;
        }

        [System.Serializable]
        public class Card
        {
            public RectTransform root;
            public Image picture;
            public TMP_Text title;
            public TMP_Text blurb;
            public TMP_Text status;
            public Button action;
            public Image actionImage;
            public TMP_Text actionText;
            public GameObject lockIcon;
            public GameObject hereBadge;
        }

        [SerializeField] Shop[] shops;
        [SerializeField] Card[] cards;
        [SerializeField] RectTransform panel;
        [SerializeField] CanvasGroup group;
        [SerializeField] CanvasGroup dim;
        [SerializeField] Button dimButton;
        [SerializeField] Button openButton;
        [SerializeField] GameObject openAlert;
        [SerializeField] Button closeButton;
        [SerializeField] Sprite goSprite;
        [SerializeField] Sprite lockedSprite;
        [SerializeField] Color lockedTint = new(0.55f, 0.5f, 0.5f);

        ShopSim _sim;
        string _here;
        bool _isOpen;

        public bool IsOpen => _isOpen;
        public RectTransform OpenButtonRect => (RectTransform)openButton.transform;

        /// <summary>The action button of the first shop that can be opened right now, or null.</summary>
        public RectTransform UnlockableActionRect
        {
            get
            {
                for (int i = 0; i < shops.Length && i < cards.Length; i++)
                {
                    var s = shops[i];
                    if (!s.comingSoon && !WorldState.IsUnlocked(s.id) && RequirementsMet(s) && _sim.State.coins >= s.unlockCost)
                        return (RectTransform)cards[i].action.transform;
                }
                return null;
            }
        }
        float _open;
        float _velocity;

        void Start()
        {
            _sim = GameController.Instance.Sim;
            _here = _sim.Def.id;
            openButton.onClick.AddListener(() => SetOpen(!_isOpen));
            closeButton.onClick.AddListener(() => SetOpen(false));
            dimButton.onClick.AddListener(() => SetOpen(false));
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                cards[i].action.onClick.AddListener(() => OnAction(index));
            }
            group.alpha = 0f;
            group.blocksRaycasts = false;
            dim.alpha = 0f;
            dim.blocksRaycasts = false;
            panel.localScale = Vector3.one * 0.8f;
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

        bool RequirementsMet(Shop s) =>
            s.requiresShop == _here
            && (_sim.Board == null || _sim.Board.State.renovationsDone.Count >= s.renovationsNeeded)
            && _sim.ShopLevel >= s.shopLevelNeeded;

        void OnAction(int index)
        {
            if (index >= shops.Length)
                return;
            var s = shops[index];
            if (s.comingSoon || s.id == _here)
                return;
            if (WorldState.IsUnlocked(s.id))
            {
                GameController.Instance.Travel(s.sceneName);
                return;
            }
            if (!RequirementsMet(s) || _sim.State.coins < s.unlockCost)
                return;
            _sim.AddCoins(-s.unlockCost);
            WorldState.Unlock(s.id);
            AudioDirector.Play(Sfx.Celebrate);
            GameController.Instance.Save();
        }

        void Update()
        {
            float dt = UiTime.Delta;
            bool allowed = !TutorialFocus.Active;
            openButton.gameObject.SetActive(allowed);
            if (!allowed && _isOpen)
                SetOpen(false);
            openAlert.SetActive(allowed && !_isOpen && AnythingToUnlock());
            if (openAlert.activeSelf)
                openAlert.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 7f) * 0.12f);

            float target = _isOpen ? 1f : 0f;
            _velocity += ((target - _open) * 260f - _velocity * 20f) * dt;
            _open += _velocity * dt;
            group.alpha = Mathf.Clamp01(_open * 2f);
            group.blocksRaycasts = group.interactable = _isOpen;
            dim.alpha = Mathf.Clamp01(_open);
            dim.blocksRaycasts = _isOpen;
            panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, _open);
            if (_open > 0.01f || _isOpen)
                Refresh();
        }

        public bool AnythingToUnlock()
        {
            foreach (var s in shops)
                if (!s.comingSoon && !WorldState.IsUnlocked(s.id) && RequirementsMet(s) && _sim.State.coins >= s.unlockCost)
                    return true;
            return false;
        }

        void Refresh()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                bool used = i < shops.Length;
                card.root.gameObject.SetActive(used);
                if (!used)
                    continue;
                var s = shops[i];
                bool here = s.id == _here;
                bool unlocked = !s.comingSoon && WorldState.IsUnlocked(s.id);
                card.title.text = s.displayName;
                card.blurb.text = s.blurb;
                card.picture.sprite = s.picture;
                card.picture.color = unlocked || here ? Color.white : lockedTint;
                card.lockIcon.SetActive(!unlocked && !here);
                card.hereBadge.SetActive(here);

                if (here)
                {
                    card.status.text = "";
                    SetAction(card, false, "Open", locked: false, show: false);
                }
                else if (s.comingSoon)
                {
                    card.status.text = "Coming soon";
                    SetAction(card, false, "Soon", locked: true, show: false);
                }
                else if (unlocked)
                {
                    card.status.text = "Open for business";
                    SetAction(card, true, "Go", locked: false, show: true);
                }
                else if (s.requiresShop != _here)
                {
                    card.status.text = $"Unlock it from {ShopName(s.requiresShop)}";
                    SetAction(card, false, "Locked", locked: true, show: true);
                }
                else
                {
                    int done = _sim.Board != null ? _sim.Board.State.renovationsDone.Count : 0;
                    // Met requirements turn green, missing ones stay red.
                    string Line(bool ok, string text) => $"<color=#{(ok ? "3E8E2E" : "B5452A")}>{text}</color>";
                    bool renoOk = done >= s.renovationsNeeded;
                    bool levelOk = _sim.ShopLevel >= s.shopLevelNeeded;
                    card.status.text =
                        Line(renoOk, $"Renovations {Mathf.Min(done, s.renovationsNeeded)}/{s.renovationsNeeded}") + "\n" +
                        Line(levelOk, $"Shop level {Mathf.Min(_sim.ShopLevel, s.shopLevelNeeded)}/{s.shopLevelNeeded}");
                    bool can = renoOk && levelOk && _sim.State.coins >= s.unlockCost;
                    SetAction(card, can, $"Open  {NumberFormat.Short(s.unlockCost)}", locked: !can, show: true);
                }
            }
        }

        string ShopName(string id)
        {
            foreach (var s in shops)
                if (s.id == id) return s.displayName;
            return "the last shop";
        }

        void SetAction(Card card, bool interactable, string label, bool locked, bool show)
        {
            card.action.gameObject.SetActive(show);
            card.action.interactable = interactable;
            card.actionImage.sprite = locked ? lockedSprite : goSprite;
            card.actionText.text = label;
            card.actionText.color = locked ? new Color(0.45f, 0.3f, 0.2f) : Color.white;
        }
    }
}
