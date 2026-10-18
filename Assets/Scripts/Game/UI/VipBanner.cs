using MergeResto.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeResto.Game.UI
{
    /// <summary>
    /// Top-centre banner while a VIP waits: what they pay and how long they will stay, with a
    /// draining patience bar that turns red near the end. Slides in and out.
    /// </summary>
    public class VipBanner : MonoBehaviour
    {
        [SerializeField] RectTransform panel;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text detail;
        [SerializeField] RectTransform fill;
        [SerializeField] Image fillImage;
        [SerializeField] Color fillColor = new(1f, 0.76f, 0.2f);
        [SerializeField] Color urgentColor = new(0.9f, 0.3f, 0.25f);

        ShopSim _sim;
        MergeBoardView _board;
        MapPanel _map;
        ExpandPanel _expand;

        public RectTransform Panel => panel;
        float _shown;
        Vector2 _home;

        void Start()
        {
            _sim = GameController.Instance.Sim;
            _home = panel.anchoredPosition;
            _board = FindAnyObjectByType<MergeBoardView>(FindObjectsInactive.Include);
            _map = FindAnyObjectByType<MapPanel>(FindObjectsInactive.Include);
            _expand = FindAnyObjectByType<ExpandPanel>(FindObjectsInactive.Include);
        }

        // Big windows reach the top of the screen; the banner steps aside until they close.
        bool Covered =>
            (_board != null && _board.IsOpen) || (_map != null && _map.IsOpen) || (_expand != null && _expand.IsOpen);

        void Update()
        {
            var vip = _sim.State.queue.Find(c => c.vip && c.servedBy < 0);
            _shown = Mathf.MoveTowards(_shown, vip != null && !Covered ? 1f : 0f, UiTime.Delta * 4f);
            panel.anchoredPosition = _home + new Vector2(0f, (1f - Mathf.SmoothStep(0f, 1f, _shown)) * 200f);
            panel.gameObject.SetActive(_shown > 0.01f);
            if (vip == null)
                return;
            double pay = vip.items * _sim.PricePerItem * _sim.Def.vipPriceMultiplier;
            title.text = "A VIP is here!";
            detail.text = vip.walkLeft > 0f
                ? $"Walking in... pays {NumberFormat.Short(pay)} + {_sim.Def.vipGems} gems"
                : $"Wants {vip.items} items: {NumberFormat.Short(pay)} + {_sim.Def.vipGems} gems";
            float t = Mathf.Clamp01(vip.patienceLeft / Mathf.Max(1f, _sim.Def.vipPatienceSeconds));
            var max = fill.anchorMax;
            max.x = Mathf.Max(0.02f, t);
            fill.anchorMax = max;
            bool urgent = t < 0.3f;
            fillImage.color = urgent ? urgentColor : fillColor;
            panel.localScale = Vector3.one * (urgent ? 1f + Mathf.Sin(Time.unscaledTime * 10f) * 0.03f : 1f);
        }
    }
}
