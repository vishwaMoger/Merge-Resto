using MergeResto.Sim;
using UnityEngine;

namespace MergeResto.Game.Views
{
    /// <summary>
    /// The 3D side of one station: shows how many machines and goods it has, bounces when a
    /// cycle finishes, and runs little effects while working. Clicks land on its collider.
    /// </summary>
    public class StationView : MonoBehaviour
    {
        public int stationIndex;
        [Tooltip("Where couriers stand to pick up from or drop off at this station.")]
        public Transform workPoint;
        [Tooltip("Where the progress ring floats.")]
        public Transform hudAnchor;
        [Tooltip("Top of the first machine: where tap pins and the tutorial pointer touch down.")]
        public Transform tapAnchor;
        [Tooltip("One per machine slot; extra slots appear as Machines are upgraded.")]
        public GameObject[] machineVisuals;
        [Tooltip("Goods shown on the station; more appear as stock fills up.")]
        public GameObject[] stockVisuals;
        [Tooltip("Shown only while a cycle runs (oven glow, steam).")]
        public GameObject[] runningEffects;
        [Tooltip("Spins while running (mixer bowl).")]
        public Transform spinner;
        public float spinDegreesPerSecond = 360f;

        [Tooltip("Warm glow pulsed on the station while it waits for a tap.")]
        public Color glowColor = new(0.55f, 0.32f, 0.08f);

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        ShopSim _sim;
        float _bounce;
        Renderer[] _glowRenderers;
        MaterialPropertyBlock _block;
        float _glow;

        void Start()
        {
            _sim = GameController.Instance.Sim;
            _sim.CycleCompleted += OnCycleCompleted;
            // Staff are not part of the machine, so they never glow.
            var list = new System.Collections.Generic.List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r.GetComponentInParent<ChibiAnimator>() == null && !(r is ParticleSystemRenderer))
                    list.Add(r);
            _glowRenderers = list.ToArray();
            _block = new MaterialPropertyBlock();
        }

        void OnDestroy()
        {
            if (_sim != null)
                _sim.CycleCompleted -= OnCycleCompleted;
        }

        void OnCycleCompleted(int station, int machine, int amount)
        {
            if (station == stationIndex)
                _bounce = 1f;
        }

        /// <summary>Squash-and-stretch pop, e.g. after an upgrade.</summary>
        public void Bounce() => _bounce = 1f;

        public bool AnyRunning
        {
            get
            {
                foreach (var m in _sim.State.stations[stationIndex].machines)
                    if (m.running) return true;
                return false;
            }
        }

        /// <summary>Progress of the furthest-along running machine, or -1 when idle.</summary>
        public float Progress
        {
            get
            {
                float best = -1f;
                foreach (var m in _sim.State.stations[stationIndex].machines)
                    if (m.running && m.progress > best) best = m.progress;
                return best;
            }
        }

        void Update()
        {
            if (_sim == null)
                return;
            var def = _sim.Def.stations[stationIndex];
            var st = _sim.State.stations[stationIndex];

            int machines = _sim.Machines(stationIndex);
            for (int i = 0; i < machineVisuals.Length; i++)
                if (machineVisuals[i] != null)
                    machineVisuals[i].SetActive(i < machines);

            // Sellers show what is on display (input); others show finished goods (output).
            int stock = def.kind == StationKind.Seller ? st.input : st.output;
            int capacity = def.kind == StationKind.Seller ? def.inputCapacity : def.outputCapacity;
            int shown = stock <= 0 ? 0 : Mathf.CeilToInt(stockVisuals.Length * Mathf.Clamp01(stock / (float)capacity));
            for (int i = 0; i < stockVisuals.Length; i++)
                if (stockVisuals[i] != null)
                    stockVisuals[i].SetActive(i < shown);

            // Staff standing at the station (cashiers) play their work loop while their machine runs.
            var machinesState = st.machines;
            for (int i = 0; i < machineVisuals.Length && i < machinesState.Count; i++)
            {
                if (machineVisuals[i] != null && machineVisuals[i].TryGetComponent<ChibiAnimator>(out var worker))
                    worker.SetWorking(machinesState[i].running);
            }

            bool running = AnyRunning;
            foreach (var fx in runningEffects)
                if (fx != null && fx.activeSelf != running)
                    fx.SetActive(running);
            if (running && spinner != null)
                spinner.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.Self);

            UpdateGlow();

            // Squash-and-stretch pop when a batch finishes.
            if (_bounce > 0f)
            {
                _bounce = Mathf.Max(0f, _bounce - Time.deltaTime * 4f);
                float s = Mathf.Sin(_bounce * Mathf.PI) * 0.08f;
                transform.localScale = new Vector3(1f - s * 0.5f, 1f + s, 1f - s * 0.5f);
            }
        }

        // Pulses a warm glow while the station is waiting for the player (or is the tutorial's target).
        void UpdateGlow()
        {
            bool lit = TutorialFocus.Active ? TutorialFocus.Station == stationIndex : _sim.NeedsTap(stationIndex);
            float target = lit ? 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f) : 0f;
            float next = Mathf.MoveTowards(_glow, target, Time.unscaledDeltaTime * 4f);
            if (Mathf.Approximately(next, _glow))
                return;
            _glow = next;
            foreach (var r in _glowRenderers)
            {
                if (r == null)
                    continue;
                // Keep the block (renovation wear may live in it); only the glow changes.
                r.GetPropertyBlock(_block);
                _block.SetColor(EmissionId, _glow <= 0.001f ? Color.black : glowColor * _glow);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
