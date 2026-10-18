using System;
using System.Collections.Generic;
using MergeResto.Sim;
using UnityEngine;

namespace MergeResto.Game.Views
{
    /// <summary>
    /// Spawns a customer character for each customer in the sim's queue. Customers walk in along
    /// the street through the entrance, wait at their queue slot, and leave through the separate
    /// exit, happy with their order or grumpy when they ran out of patience. In the drive-thru the
    /// customers are cars: they follow the lane through every queue spot, turn to face where they
    /// drive, and pull away after the pickup window.
    /// </summary>
    public class CustomerCrowd : MonoBehaviour
    {
        [Tooltip("Inactive character templates; one is picked at random per customer.")]
        public GameObject[] templates;
        [Tooltip("Arrival route: spawn on the street, then through the entrance. The queue slot follows.")]
        public Transform[] entryPath;
        [Tooltip("Leaving route: from the counter through the exit door and away down the street.")]
        public Transform[] exitPath;
        [Tooltip("Queue positions, front (at the register) first.")]
        public Transform[] queueSlots;
        [Tooltip("Places to sit and eat (facing the table). Inactive ones (an unbuilt annex) are skipped.")]
        public Transform[] seats;
        [Range(0f, 1f)] public float dineInChance = 0.65f;
        public Vector2 eatSeconds = new(7f, 12f);
        public float walkSpeed = 2.6f;
        [Tooltip("Customers are cars: they drive the lane through the queue spots and turn to face their way.")]
        public bool vehicles;
        public float turnSpeed = 7f;

        /// <summary>World position where a customer paid, and how much.</summary>
        public event Action<Vector3, double> CustomerPaid;
        /// <summary>World position where a customer gave up.</summary>
        public event Action<Vector3> CustomerGaveUp;
        /// <summary>A VIP walked in (world position above them).</summary>
        public event Action<Vector3> VipArrived;
        /// <summary>A diner finished eating and left a tip (already added to the coins).</summary>
        public event Action<Vector3, double> CustomerTipped;

        class Walker
        {
            public GameObject go;
            public ChibiAnimator anim;
            public Transform bag;
            public Transform grump;
            public bool leaving;
            public float leaveDelay;
            public int seat = -1;
            public float eatLeft;
            public bool seated;
            public double order;
            public bool vip;
            public readonly Queue<Vector3> route = new();
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        readonly Dictionary<int, Walker> _active = new();
        readonly List<Walker> _leaving = new();
        readonly List<Walker> _diners = new();
        bool[] _seatTaken;
        ShopSim _sim;

        void Start()
        {
            _sim = GameController.Instance.Sim;
            _sim.CustomerArrived += OnArrived;
            _sim.CustomerServed += OnServed;
            _sim.CustomerLeft += OnLeft;
            // Customers already queued in a loaded save appear at their slots.
            foreach (var c in _sim.State.queue)
                Spawn(c.id, atQueue: true);
        }

        void OnDestroy()
        {
            if (_sim == null)
                return;
            _sim.CustomerArrived -= OnArrived;
            _sim.CustomerServed -= OnServed;
            _sim.CustomerLeft -= OnLeft;
        }

        void OnArrived(CustomerState c) => Spawn(c.id, atQueue: false);

        /// <summary>The character for a waiting customer, or null.</summary>
        public Transform CharacterFor(int customerId) =>
            _active.TryGetValue(customerId, out var w) && w.go != null ? w.go.transform : null;

        void OnServed(CustomerState c, double coins)
        {
            if (!_active.Remove(c.id, out var w))
                return;
            if (w.anim != null) w.anim.Cheer();
            w.leaveDelay = 0.4f; // a quick cheer before walking off
            CustomerPaid?.Invoke(w.go.transform.position + Vector3.up * 2.6f, coins);
            int seat = !w.vip && UnityEngine.Random.value < dineInChance ? FreeSeat(w.go.transform.position) : -1;
            if (seat >= 0)
            {
                // Eat in: walk to the table, sit, enjoy, tip, then leave.
                _seatTaken[seat] = true;
                w.seat = seat;
                w.order = coins;
                w.eatLeft = UnityEngine.Random.Range(eatSeconds.x, eatSeconds.y);
                w.route.Clear();
                var s = seats[seat];
                w.route.Enqueue(s.position - s.forward * 0.55f);
                w.route.Enqueue(s.position);
                _diners.Add(w);
                return;
            }
            if (w.bag != null) w.bag.gameObject.SetActive(true);
            StartLeaving(w);
        }

        void OnLeft(CustomerState c)
        {
            if (!_active.Remove(c.id, out var w))
                return;
            if (w.grump != null) w.grump.gameObject.SetActive(true);
            StartLeaving(w);
            CustomerGaveUp?.Invoke(w.go.transform.position + Vector3.up * 2.6f);
        }

        void Spawn(int id, bool atQueue)
        {
            if (templates == null || templates.Length == 0)
                return;
            var template = templates[UnityEngine.Random.Range(0, templates.Length)];
            var go = Instantiate(template, transform);
            go.name = $"Customer_{id}";
            go.SetActive(true);
            var w = new Walker
            {
                go = go,
                anim = go.GetComponent<ChibiAnimator>(),
                bag = FindDeep(go.transform, "Bag"),
                grump = FindDeep(go.transform, "Grump"),
            };
            if (w.bag != null) w.bag.gameObject.SetActive(false);
            if (w.grump != null) w.grump.gameObject.SetActive(false);
            var state = _sim.State.queue.Find(c => c.id == id);
            w.vip = state != null && state.vip;
            var crown = FindDeep(go.transform, "Crown");
            if (crown != null)
                crown.gameObject.SetActive(w.vip);
            int slot = Mathf.Min(_sim.State.queue.FindIndex(c => c.id == id), queueSlots.Length - 1);
            if (atQueue && slot >= 0)
            {
                go.transform.position = queueSlots[slot].position;
                if (vehicles)
                    go.transform.rotation = queueSlots[slot].rotation;
            }
            else
            {
                go.transform.position = entryPath[0].position;
                for (int i = 1; i < entryPath.Length; i++)
                    w.route.Enqueue(entryPath[i].position);
                // Cars drive the whole lane up to their spot instead of cutting across.
                if (vehicles)
                {
                    if (entryPath.Length > 1)
                        go.transform.rotation = Quaternion.LookRotation(entryPath[1].position - entryPath[0].position);
                    for (int i = queueSlots.Length - 1; i > Mathf.Max(slot, 0); i--)
                        w.route.Enqueue(queueSlots[i].position);
                }
            }
            _active[id] = w;
            if (w.vip && !atQueue)
                VipArrived?.Invoke(go.transform.position + Vector3.up * 2.6f);
        }

        /// <summary>The VIP's character while they wait, or null.</summary>
        public Transform VipCharacter
        {
            get
            {
                foreach (var kv in _active)
                    if (kv.Value.vip && kv.Value.go != null)
                        return kv.Value.go.transform;
                return null;
            }
        }

        void StartLeaving(Walker w)
        {
            w.leaving = true;
            w.route.Clear();
            foreach (var p in exitPath)
                w.route.Enqueue(p.position);
            _leaving.Add(w);
        }

        // A random free seat that is in use (its room is built), or -1.
        int FreeSeat(Vector3 from)
        {
            if (seats == null || seats.Length == 0)
                return -1;
            _seatTaken ??= new bool[seats.Length];
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < seats.Length; i++)
            {
                if (_seatTaken[i] || seats[i] == null || !seats[i].gameObject.activeInHierarchy)
                    continue;
                // Any free seat, so diners spread over every table in the shop.
                float d = UnityEngine.Random.value;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }
            return best;
        }

        void UpdateDiners()
        {
            for (int i = _diners.Count - 1; i >= 0; i--)
            {
                var w = _diners[i];
                if (w.leaveDelay > 0f)
                {
                    w.leaveDelay -= Time.deltaTime;
                    continue;
                }
                var seat = seats[w.seat];
                if (!w.seated)
                {
                    if (!FollowRoute(w))
                        continue;
                    w.seated = true;
                    w.go.transform.SetPositionAndRotation(seat.position, seat.rotation);
                    if (w.anim != null)
                    {
                        w.anim.FaceWhenIdle(seat.forward);
                        w.anim.SetSitting(true);
                    }
                    continue;
                }
                w.eatLeft -= Time.deltaTime;
                if (w.eatLeft > 0f)
                    continue;
                // Done: stand up, leave a tip, and head out.
                if (w.anim != null)
                    w.anim.SetSitting(false);
                double tip = _sim.TipFor(w.order);
                if (tip > 0)
                {
                    _sim.AddCoins(tip);
                    CustomerTipped?.Invoke(seat.position + Vector3.up * 2.2f, tip);
                }
                _seatTaken[w.seat] = false;
                _diners.RemoveAt(i);
                w.go.transform.position = seat.position - seat.forward * 0.55f;
                StartLeaving(w);
            }
        }

        void Update()
        {
            if (_sim == null)
                return;
            UpdateDiners();
            var queue = _sim.State.queue;
            for (int i = 0; i < queue.Count; i++)
            {
                if (!_active.TryGetValue(queue[i].id, out var w))
                    continue;
                var slot = queueSlots[Mathf.Min(i, queueSlots.Length - 1)];
                if (!FollowRoute(w))
                    continue; // still walking in through the door
                Step(w, slot.position);
                if (w.anim != null)
                    w.anim.FaceWhenIdle(slot.forward);
            }

            for (int i = _leaving.Count - 1; i >= 0; i--)
            {
                var w = _leaving[i];
                if (w.leaveDelay > 0f)
                {
                    w.leaveDelay -= Time.deltaTime;
                    continue;
                }
                if (FollowRoute(w))
                {
                    Destroy(w.go);
                    _leaving.RemoveAt(i);
                }
            }
        }

        // Walks towards the next waypoint; true once the route is finished.
        bool FollowRoute(Walker w)
        {
            while (w.route.Count > 0)
            {
                if (!Step(w, w.route.Peek()))
                    return false;
                w.route.Dequeue();
            }
            return true;
        }

        bool Step(Walker w, Vector3 target)
        {
            var t = w.go.transform;
            if (vehicles)
            {
                var dir = target - t.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
            }
            t.position = Vector3.MoveTowards(t.position, target, walkSpeed * Time.deltaTime);
            return (t.position - target).sqrMagnitude < 0.0025f;
        }
    }
}
