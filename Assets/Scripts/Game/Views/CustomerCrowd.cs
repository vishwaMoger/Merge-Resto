using System;
using System.Collections.Generic;
using MergeResto.Sim;
using UnityEngine;

namespace MergeResto.Game.Views
{
    /// <summary>
    /// Spawns a customer character for each customer in the sim's queue, walks them to their
    /// queue slot, and sends them out happy (with their order) or grumpy (out of patience).
    /// </summary>
    public class CustomerCrowd : MonoBehaviour
    {
        [Tooltip("Inactive character templates; one is picked at random per customer.")]
        public GameObject[] templates;
        public Transform entryPoint;
        public Transform exitPoint;
        [Tooltip("Queue positions, front (at the register) first.")]
        public Transform[] queueSlots;
        public float walkSpeed = 2.6f;

        /// <summary>World position where a customer paid, and how much.</summary>
        public event Action<Vector3, double> CustomerPaid;
        /// <summary>World position where a customer gave up.</summary>
        public event Action<Vector3> CustomerGaveUp;

        class Walker
        {
            public GameObject go;
            public ChibiAnimator anim;
            public Transform bag;
            public Transform grump;
            public bool leaving;
        }

        readonly Dictionary<int, Walker> _active = new();
        readonly List<Walker> _leaving = new();
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

        void OnServed(CustomerState c, double coins)
        {
            if (!_active.Remove(c.id, out var w))
                return;
            if (w.bag != null) w.bag.gameObject.SetActive(true);
            StartLeaving(w);
            CustomerPaid?.Invoke(w.go.transform.position + Vector3.up * 2.2f, coins);
        }

        void OnLeft(CustomerState c)
        {
            if (!_active.Remove(c.id, out var w))
                return;
            if (w.grump != null) w.grump.gameObject.SetActive(true);
            StartLeaving(w);
            CustomerGaveUp?.Invoke(w.go.transform.position + Vector3.up * 2.2f);
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
                bag = go.transform.Find("Body/Bag"),
                grump = go.transform.Find("Body/Grump"),
            };
            if (w.bag != null) w.bag.gameObject.SetActive(false);
            if (w.grump != null) w.grump.gameObject.SetActive(false);
            int slot = Mathf.Min(_sim.State.queue.FindIndex(c => c.id == id), queueSlots.Length - 1);
            go.transform.position = atQueue && slot >= 0 ? queueSlots[slot].position : entryPoint.position;
            _active[id] = w;
        }

        void StartLeaving(Walker w)
        {
            w.leaving = true;
            _leaving.Add(w);
        }

        void Update()
        {
            if (_sim == null)
                return;
            var queue = _sim.State.queue;
            for (int i = 0; i < queue.Count; i++)
            {
                if (!_active.TryGetValue(queue[i].id, out var w))
                    continue;
                var slot = queueSlots[Mathf.Min(i, queueSlots.Length - 1)];
                Step(w, slot.position);
                if (w.anim != null)
                    w.anim.FaceWhenIdle(slot.forward);
            }

            for (int i = _leaving.Count - 1; i >= 0; i--)
            {
                var w = _leaving[i];
                if (Step(w, exitPoint.position))
                {
                    Destroy(w.go);
                    _leaving.RemoveAt(i);
                }
            }
        }

        bool Step(Walker w, Vector3 target)
        {
            var t = w.go.transform;
            t.position = Vector3.MoveTowards(t.position, target, walkSpeed * Time.deltaTime);
            return (t.position - target).sqrMagnitude < 0.0025f;
        }
    }
}
