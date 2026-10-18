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
            _sim.VipServed += OnVipServed;
            if (crowd != null)
            {
                crowd.CustomerPaid += OnPaid;
                crowd.CustomerTipped += OnTipped;
                crowd.VipArrived += OnVipArrived;
            }
        }

        void OnDestroy()
        {
            if (_sim != null)
            {
                _sim.CycleStarted -= OnCycleStarted;
                _sim.UpgradePurchased -= OnUpgrade;
                _sim.ManagerHired -= OnManager;
                _sim.VipServed -= OnVipServed;
            }
            if (crowd != null)
            {
                crowd.CustomerPaid -= OnPaid;
                crowd.CustomerTipped -= OnTipped;
                crowd.VipArrived -= OnVipArrived;
            }
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
            string text = track == UpgradeTrack.Machines ? "New machine!" : "Level up!";
            // A new dish on the menu gets its own announcement.
            if (track == UpgradeTrack.Output && _sim.Def.stations[station].kind == StationKind.Seller)
            {
                int level = _sim.State.stations[station].outputLevel;
                if (_sim.RecipeAtLevel(level) != _sim.RecipeAtLevel(level - 1))
                {
                    text = $"New recipe: {_sim.CurrentRecipe}!";
                    Celebrate(view.hudAnchor.position);
                }
            }
            popups.Show(view.hudAnchor.position + Vector3.up * 0.5f, text, levelUpColor);
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

        void OnVipArrived(Vector3 position)
        {
            popups.Show(position, "VIP!", new Color(1f, 0.8f, 0.2f));
            Emit(sparkleBurst, position - Vector3.up * 0.5f, 20);
            AudioDirector.Play(Sfx.Claim, 1.15f);
        }

        void OnVipServed(CustomerState customer, int gems)
        {
            var who = crowd != null ? crowd.VipCharacter : null;
            var at = who != null ? who.position + Vector3.up * 2.4f : stations[stations.Length - 1].hudAnchor.position;
            Celebrate(at);
            popups.Show(at + Vector3.up * 0.6f, $"VIP served!  +{gems} gems", new Color(0.55f, 0.85f, 1f));
            if (coinFly != null)
                coinFly.Fly(at, 12);
            AudioDirector.Play(Sfx.Celebrate);
        }

        void OnTipped(Vector3 position, double coins)
        {
            popups.Show(position, "+" + NumberFormat.Short(coins) + " tip", new Color(1f, 0.85f, 0.35f));
            Emit(sparkleBurst, position - Vector3.up * 0.6f, 8);
            if (coinFly != null)
                coinFly.Fly(position, 2);
        }

        void OnPaid(Vector3 position, double coins)
        {
            Emit(sparkleBurst, position - Vector3.up * 0.4f, 10);
            if (coinFly != null)
                coinFly.Fly(position, Mathf.Clamp((int)(coins / 25), 2, 6));
        }

        /// <summary>A big sparkle burst for milestones (tutorial finished, new shop).</summary>
        public void Celebrate(Vector3 position)
        {
            Emit(sparkleBurst, position, 60);
            Emit(sparkleBurst, position + Vector3.up * 0.8f, 40);
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
