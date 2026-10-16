using MergeResto.Sim;
using UnityEngine;

namespace MergeResto.Game.Views
{
    /// <summary>Places a staff character along its link according to the courier's state in the sim.</summary>
    public class CourierView : MonoBehaviour
    {
        public int linkIndex;
        public StationView from;
        public StationView to;
        [Tooltip("Shown in the character's hands while carrying goods.")]
        public GameObject carryVisual;
        [Tooltip("Sideways offset so couriers sharing a station don't overlap.")]
        public Vector3 waitOffset;

        ShopSim _sim;
        ChibiAnimator _anim;

        void Start()
        {
            _sim = GameController.Instance.Sim;
            _anim = GetComponent<ChibiAnimator>();
            // Waiting staff turn towards the player so their faces read from the iso camera.
            if (_anim != null)
                _anim.FaceWhenIdle(new Vector3(0.5f, 0f, -1f));
            transform.position = from.workPoint.position + waitOffset;
        }

        void Update()
        {
            if (_sim == null)
                return;
            var courier = _sim.State.couriers[linkIndex];
            Vector3 a = from.workPoint.position + waitOffset;
            Vector3 b = to.workPoint.position + waitOffset;
            float t = Mathf.SmoothStep(0f, 1f, courier.progress);
            transform.position = courier.phase switch
            {
                CourierPhase.CarryingThere => Vector3.Lerp(a, b, t),
                CourierPhase.WalkingBack => Vector3.Lerp(b, a, t),
                _ => a,
            };
            bool carrying = courier.carrying > 0;
            if (carryVisual != null)
                carryVisual.SetActive(carrying);
            if (_anim != null)
                _anim.SetCarrying(carrying);
        }
    }
}
