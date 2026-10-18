using System.Collections;
using MergeResto.Game.UI;
using UnityEngine;

namespace MergeResto.Game.Views
{
    /// <summary>
    /// A run-down corner of the shop tied to one renovation. Until it is fixed its props are
    /// greyed and grimy (toon shader wear) or replaced by a shabby stand-in. When the player
    /// presses Fix, the camera flies over and the repair plays out where it happens: the old
    /// props shake and puff away, the grime fades, the new props pop in with sparkles, a
    /// caption says what was done, and the camera returns to the shop.
    /// </summary>
    public class RenovationSpot : MonoBehaviour
    {
        public int renovation;
        [Tooltip("Shown only while run-down (dusty cover, empty pots).")]
        public GameObject[] broken;
        [Tooltip("Shown only once fixed (fresh plants, plates, treats).")]
        public GameObject[] fixedObjects;
        [Tooltip("Props that stay but look worn until fixed.")]
        public Renderer[] worn;
        [Tooltip("Lights that switch on once fixed (sign glow).")]
        public Light[] lights;
        [Range(0f, 1f)] public float wear = 0.85f;
        public ParticleSystem sparkle;
        public ParticleSystem dust;
        [TextArea] public string doneCaption;

        static readonly int WearId = Shader.PropertyToID("_Wear");

        MaterialPropertyBlock _block;
        Vector3[] _scales;
        Vector3 _focus;
        float _focusSize = 3.6f;

        /// <summary>The middle of everything this renovation changes (camera and pointer target).</summary>
        public Vector3 Focus => _focus;

        /// <summary>True while the fix is playing out (the lessons wait for it).</summary>
        public static bool Showing { get; private set; }

        void Start()
        {
            _block = new MaterialPropertyBlock();
            _scales = new Vector3[fixedObjects.Length];
            for (int i = 0; i < fixedObjects.Length; i++)
                _scales[i] = fixedObjects[i] != null ? fixedObjects[i].transform.localScale : Vector3.one;
            ComputeFocus();
            var board = GameController.Instance.Sim.Board;
            Apply(board != null && board.IsRenovated(renovation));
            if (board != null)
                board.RenovationDone += OnRenovated;
        }

        void OnDestroy()
        {
            Showing = false;
            var board = GameController.Instance != null ? GameController.Instance.Sim.Board : null;
            if (board != null)
                board.RenovationDone -= OnRenovated;
        }

        void ComputeFocus()
        {
            var b = new Bounds(transform.position, Vector3.zero);
            bool any = false;
            void Add(Renderer r)
            {
                if (r == null) return;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            foreach (var go in broken) if (go != null) foreach (var r in go.GetComponentsInChildren<Renderer>(true)) Add(r);
            foreach (var go in fixedObjects) if (go != null) foreach (var r in go.GetComponentsInChildren<Renderer>(true)) Add(r);
            foreach (var r in worn) Add(r);
            _focus = any ? b.center : transform.position + Vector3.up;
            // Frame the whole spot: bigger spots (all the dining tables) need a wider shot.
            _focusSize = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 0.55f + 1.6f, 2.8f, 5.5f);
        }

        void OnRenovated(int index)
        {
            if (index == renovation)
                StartCoroutine(PlayFix());
        }

        void Apply(bool isFixed)
        {
            foreach (var go in broken)
                if (go != null) go.SetActive(!isFixed);
            foreach (var go in fixedObjects)
                if (go != null) go.SetActive(isFixed);
            foreach (var l in lights)
                if (l != null) l.enabled = isFixed;
            SetWear(isFixed ? 0f : wear);
        }

        void SetWear(float value)
        {
            foreach (var r in worn)
            {
                if (r == null)
                    continue;
                r.GetPropertyBlock(_block);
                _block.SetFloat(WearId, value);
                r.SetPropertyBlock(_block);
            }
        }

        IEnumerator PlayFix()
        {
            Showing = true;
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraDirector>() : null;
            if (cam != null)
                cam.Focus(_focus, _focusSize);
            yield return Wait(0.75f);

            // Old props shake and vanish in a puff of dust.
            float t = 0f;
            var basePos = new Vector3[broken.Length];
            for (int i = 0; i < broken.Length; i++)
                if (broken[i] != null) basePos[i] = broken[i].transform.position;
            while (t < 0.45f)
            {
                t += Time.unscaledDeltaTime;
                for (int i = 0; i < broken.Length; i++)
                    if (broken[i] != null)
                        broken[i].transform.position = basePos[i] + new Vector3(Mathf.Sin(t * 70f), 0f, Mathf.Cos(t * 55f)) * 0.06f;
                yield return null;
            }
            for (int i = 0; i < broken.Length; i++)
            {
                if (broken[i] == null) continue;
                broken[i].transform.position = basePos[i];
                Emit(dust, broken[i].transform.position + Vector3.up * 0.5f, 18);
                broken[i].SetActive(false);
            }
            if (broken.Length == 0)
                Emit(dust, _focus, 24);
            AudioDirector.Play(Sfx.Whoosh, 1.2f, 0.6f);

            // The grime washes off.
            t = 0f;
            while (t < 0.6f)
            {
                t += Time.unscaledDeltaTime;
                SetWear(Mathf.Lerp(wear, 0f, t / 0.6f));
                yield return null;
            }
            SetWear(0f);
            foreach (var l in lights)
                if (l != null) l.enabled = true;

            // New props grow in one after another with a springy overshoot.
            for (int i = 0; i < fixedObjects.Length; i++)
                if (fixedObjects[i] != null)
                {
                    fixedObjects[i].SetActive(true);
                    fixedObjects[i].transform.localScale = Vector3.zero;
                }
            float stagger = Mathf.Min(0.12f, 0.9f / Mathf.Max(1, fixedObjects.Length));
            t = 0f;
            float total = 0.55f + stagger * fixedObjects.Length;
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                for (int i = 0; i < fixedObjects.Length; i++)
                {
                    if (fixedObjects[i] == null) continue;
                    float k = Mathf.Clamp01((t - i * stagger) / 0.55f);
                    float s = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, k * 2.2f)) * (1f + Mathf.Sin(k * Mathf.PI * 2.5f) * (1f - k) * 0.35f);
                    fixedObjects[i].transform.localScale = _scales[i] * s;
                }
                yield return null;
            }
            for (int i = 0; i < fixedObjects.Length; i++)
                if (fixedObjects[i] != null) fixedObjects[i].transform.localScale = _scales[i];

            Emit(sparkle, _focus + Vector3.up * 0.4f, 60);
            AudioDirector.Play(Sfx.Celebrate);
            var board = GameController.Instance.Sim.Board;
            var def = board.Def.renovations[renovation];
            var popups = FindAnyObjectByType<FloatingTextLayer>();
            if (popups != null)
            {
                string caption = string.IsNullOrEmpty(doneCaption) ? def.displayName + ": done!" : doneCaption;
                popups.Show(_focus + Vector3.up * 1.6f, caption, new Color(1f, 0.85f, 0.35f));
                popups.Show(_focus + Vector3.up * 0.9f, $"+{Mathf.RoundToInt(def.coziness * 100f)}% cozy", new Color(0.6f, 0.95f, 0.5f));
            }
            var coins = FindAnyObjectByType<CoinFlyLayer>();
            if (coins != null)
                coins.Fly(_focus, 10);
            yield return Wait(1.8f);

            if (cam != null)
                cam.Release();
            yield return Wait(0.6f);
            Showing = false;
        }

        static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        static void Emit(ParticleSystem ps, Vector3 at, int count)
        {
            if (ps == null)
                return;
            ps.transform.position = at;
            ps.Emit(count);
        }
    }
}
