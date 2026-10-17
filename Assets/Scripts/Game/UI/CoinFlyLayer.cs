using System.Collections.Generic;
using UnityEngine;

namespace MergeResto.Game.UI
{
    /// <summary>Coins that arc from a point in the shop up into the coin counter.</summary>
    public class CoinFlyLayer : MonoBehaviour
    {
        [SerializeField] RectTransform coinTemplate;
        [SerializeField] RectTransform target;
        [SerializeField] ButtonJuice targetJuice;
        [SerializeField] float duration = 0.75f;

        class Flyer
        {
            public RectTransform rt;
            public Vector2 from, control, to;
            public float delay, t;
        }

        readonly List<Flyer> _live = new();
        readonly Stack<RectTransform> _pool = new();
        RectTransform _rect;
        Camera _cam;

        void Start()
        {
            _rect = (RectTransform)transform;
            _cam = Camera.main;
            coinTemplate.gameObject.SetActive(false);
        }

        public void Fly(Vector3 world, int count) => FlyFromScreen(_cam.WorldToScreenPoint(world), count);

        /// <summary>Coins burst from a screen point (e.g. a reward button) and fly to the counter.</summary>
        public void FlyFromScreen(Vector2 screen, int count)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screen, null, out var from))
                return;
            Vector2 to = _rect.InverseTransformPoint(target.position);
            for (int i = 0; i < count; i++)
            {
                var rt = _pool.Count > 0 ? _pool.Pop() : Instantiate(coinTemplate, transform);
                rt.gameObject.SetActive(true);
                var start = from + Random.insideUnitCircle * 40f;
                var control = Vector2.Lerp(start, to, 0.35f) + new Vector2(Random.Range(-160f, 160f), Random.Range(180f, 320f));
                rt.anchoredPosition = start;
                rt.localScale = Vector3.zero;
                _live.Add(new Flyer { rt = rt, from = start, control = control, to = to, delay = i * 0.06f });
            }
        }

        void Update()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var f = _live[i];
                if (f.delay > 0f)
                {
                    f.delay -= UiTime.Delta;
                    f.rt.localScale = Vector3.one * Mathf.Clamp01(1f - f.delay * 10f) * 0.6f;
                    continue;
                }
                f.t += UiTime.Delta / duration;
                float t = Mathf.Clamp01(f.t);
                float e = t * t * (3f - 2f * t);
                Vector2 a = Vector2.Lerp(f.from, f.control, e);
                Vector2 b = Vector2.Lerp(f.control, f.to, e);
                f.rt.anchoredPosition = Vector2.Lerp(a, b, e);
                float pop = t < 0.15f ? Mathf.Lerp(0.6f, 1.15f, t / 0.15f) : Mathf.Lerp(1.15f, 0.7f, (t - 0.15f) / 0.85f);
                f.rt.localScale = Vector3.one * pop;
                f.rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 12f) * 15f);
                if (t >= 1f)
                {
                    f.rt.gameObject.SetActive(false);
                    _pool.Push(f.rt);
                    _live.RemoveAt(i);
                    if (targetJuice != null)
                        targetJuice.Pop(3f);
                    AudioDirector.CoinLanded();
                }
            }
        }
    }
}
