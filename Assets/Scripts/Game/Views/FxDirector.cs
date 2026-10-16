using MergeResto.Game.UI;
using MergeResto.Sim;
using UnityEngine;

namespace MergeResto.Game.Views
{
    /// <summary>
    /// Plays the feel-good feedback for shop events: sparkles and flying coins on sales,
    /// flour puffs when sacks are opened, bounces and "Level up!" when something improves.
    /// </summary>
    public class FxDirector : MonoBehaviour
    {
        [SerializeField] StationView[] stations;
        [SerializeField] CustomerCrowd crowd;
        [SerializeField] FloatingTextLayer popups;
        [SerializeField] CoinFlyLayer coinFly;
        [SerializeField] ParticleSystem sparkleBurst;
        [SerializeField] ParticleSystem flourPuff;
        [SerializeField] Color levelUpColor = new(0.55f, 0.92f, 0.45f);
        [SerializeField] Color managerColor = new(0.55f, 0.78f, 1f);

        ShopSim _sim;

        void Start()
        {
            _sim = GameController.Instance.Sim;
            _sim.CycleStarted += OnCycleStarted;
            _sim.UpgradePurchased += OnUpgrade;
            _sim.ManagerHired += OnManager;
            if (crowd != null)
                crowd.CustomerPaid += OnPaid;
        }

        void OnDestroy()
        {
            if (_sim != null)
            {
                _sim.CycleStarted -= OnCycleStarted;
                _sim.UpgradePurchased -= OnUpgrade;
                _sim.ManagerHired -= OnManager;
            }
            if (crowd != null)
                crowd.CustomerPaid -= OnPaid;
        }

        StationView Station(int index) => index >= 0 && index < stations.Length ? stations[index] : null;

        void OnCycleStarted(int station, int machine)
        {
            var view = Station(station);
            if (view != null && _sim.Def.stations[station].kind == StationKind.Source)
                Emit(flourPuff, view.workPoint.position + Vector3.up * 0.6f + (view.transform.position - view.workPoint.position) * 0.5f, 14);
        }

        void OnUpgrade(int station, UpgradeTrack track)
        {
            var view = Station(station);
            if (view == null)
                return;
            view.Bounce();
            Emit(sparkleBurst, view.hudAnchor.position, 22);
            popups.Show(view.hudAnchor.position + Vector3.up * 0.5f, track == UpgradeTrack.Machines ? "New machine!" : "Level up!", levelUpColor);
        }

        void OnManager(int station)
        {
            var view = Station(station);
            if (view == null)
                return;
            view.Bounce();
            Emit(sparkleBurst, view.hudAnchor.position, 30);
            popups.Show(view.hudAnchor.position + Vector3.up * 0.5f, "Manager hired!", managerColor);
        }

        void OnPaid(Vector3 position, double coins)
        {
            Emit(sparkleBurst, position - Vector3.up * 0.4f, 10);
            if (coinFly != null)
                coinFly.Fly(position, Mathf.Clamp((int)(coins / 25), 2, 6));
        }

        static void Emit(ParticleSystem ps, Vector3 position, int count)
        {
            if (ps == null)
                return;
            ps.transform.position = position;
            ps.Emit(count);
        }
    }
}
