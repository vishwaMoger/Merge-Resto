using TMPro;
using UnityEngine;

namespace MergeResto.Game
{
    /// <summary>
    /// The loading screen while the SDK starts and the shop scene loads: the title on its
    /// banner, Grandma bobbing in her frame, a filling progress bar and a rotating cooking tip.
    /// </summary>
    public class BootScreen : MonoBehaviour
    {
        [SerializeField] RectTransform fill;
        [SerializeField] TMP_Text tip;
        [SerializeField] TMP_Text status;
        [SerializeField] RectTransform portrait;
        [SerializeField] RectTransform banner;
        [SerializeField] RectTransform rays;
        [SerializeField] RectTransform[] treats;

        static readonly string[] Tips =
        {
            "Tip: merge two matching items to make a better one.",
            "Tip: hire a manager and the station runs by itself.",
            "Tip: every renovation makes the shop cozier, and cozy shops earn more.",
            "Tip: your shops keep earning while you're away.",
            "Tip: VIP customers pay four times as much. Don't keep them waiting!",
            "Tip: watch a short video to double your sales for 5 minutes.",
        };

        float _shown;
        float _target;
        float _tipTimer;
        int _tip;

        /// <summary>Loading progress, 0..1.</summary>
        public void SetProgress(float value) => _target = Mathf.Max(_target, Mathf.Clamp01(value));

        void Start()
        {
            _tip = Random.Range(0, Tips.Length);
            tip.text = Tips[_tip];
            SetProgress(0.08f);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            // Creep forward a little on its own so the bar never looks stuck.
            if (_target < 0.95f)
                _target = Mathf.Min(0.95f, _target + dt * 0.02f);
            _shown = Mathf.MoveTowards(_shown, _target, dt * 1.5f);
            var max = fill.anchorMax;
            max.x = Mathf.Max(0.03f, _shown);
            fill.anchorMax = max;
            status.text = $"Warming up the ovens... {Mathf.RoundToInt(_shown * 100f)}%";
            float t = Time.unscaledTime;
            portrait.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2.2f) * 4f);
            portrait.localScale = Vector3.one * (1f + Mathf.Sin(t * 4.4f) * 0.02f);
            banner.localScale = Vector3.one * (1f + Mathf.Sin(t * 1.6f) * 0.015f);
            if (rays != null)
                rays.localRotation = Quaternion.Euler(0f, 0f, -t * 8f);
            // The treat parade drifts left and loops, each one hopping a little.
            if (treats != null && treats.Length > 0)
            {
                float width = ((RectTransform)treats[0].parent).rect.width;
                float spacing = Mathf.Max(170f, width / treats.Length + 40f);
                float span = spacing * treats.Length;
                for (int i = 0; i < treats.Length; i++)
                {
                    float x = Mathf.Repeat(i * spacing - t * 70f, span) - spacing * 0.5f;
                    float hop = Mathf.Abs(Mathf.Sin(t * 3f + i * 0.9f)) * 18f;
                    treats[i].anchoredPosition = new Vector2(x, 10f + hop);
                    treats[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2f + i) * 8f);
                }
            }
            _tipTimer += dt;
            if (_tipTimer > 3.5f)
            {
                _tipTimer = 0f;
                _tip = (_tip + 1) % Tips.Length;
                tip.text = Tips[_tip];
            }
        }
    }
}
