using System.Collections.Generic;
using MergeResto.Game.Views;
using TMPro;
using UnityEngine;

namespace MergeResto.Game.UI
{
    /// <summary>"+50" coins, grumpy faces and gem rewards that pop up over the shop and float away.</summary>
    public class FloatingTextLayer : MonoBehaviour
    {
        [SerializeField] TMP_Text template;
        [SerializeField] CustomerCrowd crowd;
        [SerializeField] float lifetime = 1.1f;
        [SerializeField] float rise = 90f;
        [SerializeField] Color coinColor = new(1f, 0.82f, 0.25f);
        [SerializeField] Color grumpyColor = new(0.9f, 0.35f, 0.35f);
        [SerializeField] Color gemColor = new(0.55f, 0.75f, 1f);

        class Popup
        {
            public TMP_Text text;
            public Vector3 world;
            public float age;
        }

        readonly List<Popup> _live = new();
        readonly Stack<TMP_Text> _pool = new();
        RectTransform _rect;
        Camera _cam;

        void Start()
        {
            _rect = (RectTransform)transform;
            _cam = Camera.main;
            template.gameObject.SetActive(false);
            if (crowd != null)
            {
                crowd.CustomerPaid += (pos, coins) => Show(pos, "+" + NumberFormat.Short(coins), coinColor);
                crowd.CustomerGaveUp += pos => Show(pos, "Too slow!", grumpyColor);
            }
            GameController.Instance.Sim.GemsEarned += (station, gems) =>
                Show(_cam.transform.position + _cam.transform.forward * 10f, $"+{gems} gems!", gemColor);
        }

        public void Show(Vector3 world, string message, Color color)
        {
            var text = _pool.Count > 0 ? _pool.Pop() : Instantiate(template, transform);
            text.gameObject.SetActive(true);
            text.text = message;
            text.color = color;
            _live.Add(new Popup { text = text, world = world, age = 0f });
        }

        void LateUpdate()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                p.age += Time.deltaTime;
                float t = p.age / lifetime;
                if (t >= 1f)
                {
                    p.text.gameObject.SetActive(false);
                    _pool.Push(p.text);
                    _live.RemoveAt(i);
                    continue;
                }
                Vector3 screen = _cam.WorldToScreenPoint(p.world);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screen, null, out var local);
                var rt = (RectTransform)p.text.transform;
                rt.anchoredPosition = local + Vector2.up * (rise * (1f - (1f - t) * (1f - t)));
                float pop = t < 0.15f ? Mathf.Lerp(0.6f, 1.15f, t / 0.15f) : Mathf.Lerp(1.15f, 1f, (t - 0.15f) / 0.2f);
                rt.localScale = Vector3.one * pop;
                var c = p.text.color;
                c.a = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                p.text.color = c;
            }
        }
    }
}
