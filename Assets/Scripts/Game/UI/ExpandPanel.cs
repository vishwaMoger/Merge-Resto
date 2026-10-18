using MergeResto.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeResto.Game.UI
{
    /// <summary>
    /// "Grow the shop": the building stages as a ladder (done, next, later) and a Build button
    /// for the next one. The button pulses and the HUD button shows a dot once it is affordable.
    /// </summary>
    public class ExpandPanel : MonoBehaviour
    {
        [System.Serializable]
        public class Stage
        {
            public Image frame;
            public TMP_Text title;
            public TMP_Text description;
            public GameObject check;
        }

        [SerializeField] Stage[] stages;
        [SerializeField] RectTransform panel;
        [SerializeField] CanvasGroup group;
        [SerializeField] CanvasGroup dim;
        [SerializeField] Button dimButton;
        [SerializeField] Button openButton;
        [SerializeField] GameObject openAlert;
        [SerializeField] Button closeButton;
        [SerializeField] Button buildButton;
        [SerializeField] Image buildImage;
        [SerializeField] TMP_Text buildText;
        [SerializeField] TMP_Text requirement;
        [SerializeField] Sprite readySprite;
        [SerializeField] Sprite lockedSprite;
        [SerializeField] Sprite frameDone;
        [SerializeField] Sprite frameNext;
        [SerializeField] Sprite frameLater;

        ShopSim _sim;
        bool _isOpen;

        public bool IsOpen => _isOpen;
        public RectTransform OpenButtonRect => (RectTransform)openButton.transform;
        public RectTransform BuildButtonRect => (RectTransform)buildButton.transform;
        float _open;
        float _velocity;

        void Start()
        {
            _sim = GameController.Instance.Sim;
            openButton.onClick.AddListener(() => SetOpen(!_isOpen));
            closeButton.onClick.AddListener(() => SetOpen(false));
            dimButton.onClick.AddListener(() => SetOpen(false));
            buildButton.onClick.AddListener(Build);
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

        void Build()
        {
            if (!_sim.Expand())
                return;
            GameController.Instance.Save();
            SetOpen(false);
        }

        void Update()
        {
            float dt = UiTime.Delta;
            bool allowed = !TutorialFocus.Active && _sim.Def.expansions.Count > 0;
            openButton.gameObject.SetActive(allowed && _sim.NextExpansion != null);
            openAlert.SetActive(allowed && _sim.CanExpand && !_isOpen);
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

        void Refresh()
        {
            int done = _sim.State.expansion;
            for (int i = 0; i < stages.Length; i++)
            {
                var row = stages[i];
                // Row 0 is the starting shop; rows 1.. are the expansions.
                bool used = i == 0 || i - 1 < _sim.Def.expansions.Count;
                row.frame.gameObject.SetActive(used);
                if (!used)
                    continue;
                if (i == 0)
                {
                    row.title.text = "Cozy little shop";
                    row.description.text = "Where it all started.";
                }
                else
                {
                    var e = _sim.Def.expansions[i - 1];
                    row.title.text = e.displayName;
                    row.description.text = $"{e.description}  <color=#3E8E2E>+{Mathf.RoundToInt(e.priceBonus * 100f)}% prices, {Mathf.RoundToInt(e.customerBonus * 100f)}% more customers</color>";
                }
                bool isDone = i <= done;
                row.check.SetActive(isDone);
                row.frame.sprite = isDone ? frameDone : i == done + 1 ? frameNext : frameLater;
            }

            var next = _sim.NextExpansion;
            if (next == null)
            {
                buildButton.gameObject.SetActive(false);
                requirement.text = "The shop is fully built. Beautiful!";
                return;
            }
            buildButton.gameObject.SetActive(true);
            bool levelOk = _sim.ShopLevel >= next.shopLevelNeeded;
            bool can = _sim.CanExpand;
            buildButton.interactable = can;
            buildImage.sprite = can ? readySprite : lockedSprite;
            buildText.text = $"Build  {NumberFormat.Short(next.cost)}";
            buildText.color = can ? Color.white : new Color(0.45f, 0.3f, 0.2f);
            buildButton.transform.localScale = Vector3.one * (can ? 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.05f : 1f);
            requirement.text = levelOk
                ? (can ? "Everything is ready. Let's build!" : $"Save up {NumberFormat.Short(next.cost)} coins to build.")
                : $"<color=#B5452A>Reach shop level {next.shopLevelNeeded} first ({_sim.ShopLevel}/{next.shopLevelNeeded}). Upgrade your stations!</color>";
        }
    }
}
