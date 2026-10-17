using UnityEngine;

namespace MergeResto.Game.Views
{
    /// <summary>
    /// A run-down corner of the shop tied to one renovation. Until it is fixed its props are
    /// greyed and grimy (toon shader wear) or replaced by a shabby stand-in; when the renovation
    /// is done the fixed props pop in with a bounce and sparkles.
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

        static readonly int WearId = Shader.PropertyToID("_Wear");

        MaterialPropertyBlock _block;
        bool _fixed;
        float _pop = -1f;
        Vector3[] _scales;

        /// <summary>Where the tutorial or a camera hint can point.</summary>
        public Vector3 Focus => transform.position + Vector3.up;

        void Start()
        {
            _block = new MaterialPropertyBlock();
            _scales = new Vector3[fixedObjects.Length];
            for (int i = 0; i < fixedObjects.Length; i++)
                _scales[i] = fixedObjects[i] != null ? fixedObjects[i].transform.localScale : Vector3.one;
            var board = GameController.Instance.Sim.Board;
            Apply(board != null && board.IsRenovated(renovation), instant: true);
            if (board != null)
                board.RenovationDone += OnRenovated;
        }

        void OnDestroy()
        {
            var board = GameController.Instance != null ? GameController.Instance.Sim.Board : null;
            if (board != null)
                board.RenovationDone -= OnRenovated;
        }

        void OnRenovated(int index)
        {
            if (index == renovation)
                Apply(true, instant: false);
        }

        void Apply(bool isFixed, bool instant)
        {
            _fixed = isFixed;
            foreach (var go in broken)
                if (go != null) go.SetActive(!isFixed);
            foreach (var go in fixedObjects)
                if (go != null) go.SetActive(isFixed);
            foreach (var l in lights)
                if (l != null) l.enabled = isFixed;
            SetWear(isFixed ? 0f : wear);
            if (isFixed && !instant)
            {
                _pop = 0f;
                if (sparkle != null)
                {
                    sparkle.transform.position = Focus;
                    sparkle.Emit(40);
                }
            }
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

        void Update()
        {
            if (_pop < 0f)
                return;
            // Fixed props grow in with a springy overshoot.
            _pop += Time.unscaledDeltaTime * 2.2f;
            float t = Mathf.Clamp01(_pop);
            float s = 1f + Mathf.Sin(t * Mathf.PI * 2.5f) * (1f - t) * 0.35f;
            s *= Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 3f));
            for (int i = 0; i < fixedObjects.Length; i++)
                if (fixedObjects[i] != null)
                    fixedObjects[i].transform.localScale = _scales[i] * s;
            if (t >= 1f)
            {
                for (int i = 0; i < fixedObjects.Length; i++)
                    if (fixedObjects[i] != null)
                        fixedObjects[i].transform.localScale = _scales[i];
                _pop = -1f;
            }
        }
    }
}
