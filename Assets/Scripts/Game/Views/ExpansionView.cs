using System.Collections.Generic;
using UnityEngine;

namespace MergeResto.Game.Views
{
    /// <summary>
    /// Shows the shop at its current building stage and plays the build when the player expands:
    /// the camera swings over, dust puffs, the new rooms and furniture pop in, and the view widens
    /// to fit the bigger shop.
    /// </summary>
    public class ExpansionView : MonoBehaviour
    {
        [SerializeField] StageGroup[] groups;
        [SerializeField] CameraDirector cameraDirector;
        [SerializeField] Vector3 buildFocus;
        [Tooltip("Home camera (target, size) per stage; the last entry repeats.")]
        [SerializeField] Vector3[] homeTargets;
        [SerializeField] float[] homeSizes;
        [SerializeField] ParticleSystem dust;
        [SerializeField] ParticleSystem sparkle;

        class Pop
        {
            public Transform t;
            public Vector3 scale;
            public float delay;
            public float time;
        }

        readonly List<Pop> _pops = new();
        float _build = -1f;

        void Start()
        {
            var sim = GameController.Instance.Sim;
            Apply(sim.State.expansion, animate: false);
            SetHome(sim.State.expansion, snap: true);
            sim.Expanded += OnExpanded;
        }

        void OnDestroy()
        {
            if (GameController.Instance != null)
                GameController.Instance.Sim.Expanded -= OnExpanded;
        }

        void OnExpanded(int stage)
        {
            cameraDirector.Focus(buildFocus, 7.2f);
            _build = 0f;
            AudioDirector.Play(Sfx.Renovate);
            Apply(stage, animate: true);
        }

        void SetHome(int stage, bool snap)
        {
            if (homeTargets == null || homeTargets.Length == 0)
                return;
            int i = Mathf.Min(stage, homeTargets.Length - 1);
            cameraDirector.SetHome(homeTargets[i], homeSizes[Mathf.Min(stage, homeSizes.Length - 1)], snap);
        }

        void Apply(int stage, bool animate)
        {
            float delay = 0.6f;
            foreach (var g in groups)
            {
                if (g == null)
                    continue;
                bool show = g.VisibleAt(stage);
                bool wasShown = g.gameObject.activeSelf;
                g.gameObject.SetActive(show);
                if (!animate || !show || wasShown)
                    continue;
                // Each piece of the new stage grows in, one after another.
                foreach (Transform child in g.transform)
                {
                    _pops.Add(new Pop { t = child, scale = child.localScale, delay = delay });
                    child.localScale = Vector3.zero;
                    delay += 0.035f;
                }
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                var p = _pops[i];
                if (p.t == null)
                {
                    _pops.RemoveAt(i);
                    continue;
                }
                if (p.delay > 0f)
                {
                    p.delay -= dt;
                    if (p.delay <= 0f && dust != null)
                    {
                        dust.transform.position = p.t.position + Vector3.up * 0.4f;
                        dust.Emit(6);
                    }
                    continue;
                }
                p.time += dt * 2.6f;
                float t = Mathf.Clamp01(p.time);
                float s = 1f + Mathf.Sin(t * Mathf.PI * 2.2f) * (1f - t) * 0.3f;
                p.t.localScale = p.scale * (Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 2.5f)) * s);
                if (t >= 1f)
                {
                    p.t.localScale = p.scale;
                    _pops.RemoveAt(i);
                }
            }

            if (_build >= 0f)
            {
                _build += dt;
                if (_build > 0.6f && _build - dt <= 0.6f && sparkle != null)
                {
                    sparkle.transform.position = buildFocus + Vector3.up;
                    sparkle.Emit(60);
                }
                if (_pops.Count == 0 && _build > 2.2f)
                {
                    _build = -1f;
                    cameraDirector.Release();
                    SetHome(GameController.Instance.Sim.State.expansion, snap: false);
                }
            }
        }
    }
}
