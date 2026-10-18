using System.Collections.Generic;
using MergeResto.Game.Views;
using MergeResto.Sim;
using UnityEngine;

namespace MergeResto.Game.UI
{
    /// <summary>
    /// Grandma's short lessons for every feature after the Grand Opening, each shown once (for
    /// the whole game, not per shop) at the moment the feature first matters: the renovation
    /// board (tap a generator, drag to merge, Fix), goal rewards, growing the shop, opening the
    /// next shop, VIPs and the x2 boost. One lesson at a time, never over another window.
    /// </summary>
    public class FeatureLessons : MonoBehaviour
    {
        [SerializeField] GuideOverlay guide;
        [SerializeField] MergeBoardView board;
        [SerializeField] GoalsPanel goals;
        [SerializeField] ExpandPanel expand;
        [SerializeField] MapPanel map;
        [SerializeField] DailyRewardsPanel daily;
        [SerializeField] SettingsPanel settings;
        [SerializeField] VipBanner vip;
        [SerializeField] BoostButton boost;
        [Tooltip("Seconds of play after the tutorial before the boost tip.")]
        [SerializeField] float boostTipAfter = 240f;

        const string Key = "lessons";

        ShopSim _sim;
        HashSet<string> _done;
        string _current;
        float _played;
        int _startExpansion;
        int _renovationsAtStart;
        int _goal = -1;
        bool _boardIntroDone;
        bool _merged;

        void Start()
        {
            _sim = GameController.Instance.Sim;
            _done = new HashSet<string>();
            var saved = SaveStore.Load(Key);
            if (!string.IsNullOrEmpty(saved))
                foreach (var id in saved.Split(','))
                    _done.Add(id);
            if (_sim.Board != null)
                _sim.Board.ItemsMerged += (c, i) => _merged = true;
        }

        void Finish()
        {
            if (_current != null)
            {
                _done.Add(_current);
                SaveStore.Save(Key, string.Join(",", _done));
            }
            _current = null;
            guide.Clear();
        }

        bool Busy =>
            TutorialFocus.Active || _sim.State.tutorialStep < ShopState.TutorialFinished || OfflineEarningsDialog.IsOpen
            || (daily != null && daily.IsOpen) || (settings != null && settings.IsOpen) || RenovationSpot.Showing;

        bool AnyWindowOpen =>
            (board != null && board.IsOpen) || (expand != null && expand.IsOpen) || (map != null && map.IsOpen)
            || GameController.Instance.SelectedStation >= 0;

        void Update()
        {
            if (_sim.State.tutorialStep >= ShopState.TutorialFinished)
                _played += Time.unscaledDeltaTime;

            if (_current == null)
            {
                if (Busy)
                {
                    guide.Clear();
                    return;
                }
                _current = PickLesson();
                if (_current == null)
                {
                    guide.Clear();
                    return;
                }
                _startExpansion = _sim.State.expansion;
                _renovationsAtStart = _sim.Board != null ? _sim.Board.State.renovationsDone.Count : 0;
                _merged = false;
            }

            // A fix playing out in the shop: stay quiet until it is done.
            if (RenovationSpot.Showing || OfflineEarningsDialog.IsOpen || (daily != null && daily.IsOpen))
            {
                guide.Clear();
                return;
            }

            switch (_current)
            {
                case "renovate": Renovate(); break;
                case "goal": Goal(); break;
                case "expand": Expand(); break;
                case "shops": Shops(); break;
                case "vip": Vip(); break;
                case "boost": Boost(); break;
                default: Finish(); break;
            }
        }

        string PickLesson()
        {
            bool Need(string id) => !_done.Contains(id);
            if (Need("renovate") && _sim.Board != null && board != null && _sim.Board.ActiveRenovations(1).Count > 0)
                return "renovate";
            if (AnyWindowOpen)
                return null;
            if (Need("goal") && goals != null)
            {
                foreach (int g in _sim.ActiveGoals(3))
                    if (_sim.IsGoalComplete(g) && goals.ClaimRectFor(g) != null)
                    {
                        _goal = g;
                        return "goal";
                    }
            }
            if (Need("expand") && expand != null && _sim.CanExpand)
                return "expand";
            if (Need("shops") && map != null && map.AnythingToUnlock())
                return "shops";
            if (Need("vip") && vip != null && _sim.State.queue.Exists(c => c.vip))
                return "vip";
            if (Need("boost") && boost != null && _played > boostTipAfter && !_sim.BoostActive)
                return "boost";
            return null;
        }

        // ------------------------------------------------------------------ Renovation board

        void Renovate()
        {
            var b = _sim.Board;
            // Done: the first fix was made and has played out in the shop.
            if (b.State.renovationsDone.Count > _renovationsAtStart)
            {
                guide.Present("Look how lovely it is now! Every fix makes the shop cozier, and cozy shops earn more. More jobs are waiting on the board.",
                    GuideTarget.None, waitTap: true);
                if (guide.ConsumeTap())
                    Finish();
                return;
            }
            if (!board.IsOpen)
            {
                guide.Present("The shop is still a little dusty, dear. Tap Renovate and let's fix it up together!",
                    GuideTarget.At(board.OpenButtonRect), waitTap: false);
                return;
            }
            var active = b.ActiveRenovations(1);
            if (active.Count == 0)
            {
                Finish();
                return;
            }
            int r = active[0];
            var job = b.Def.renovations[r];
            var need = NextNeed(b, r);

            if (!_boardIntroDone)
            {
                string needName = b.NameOf(BoardItem.Item(need.chain, need.tier));
                guide.Present($"This is the renovation board. Each job up here needs items you make below. To \"{job.displayName.ToLowerInvariant()}\" we need a {needName}.",
                    GuideTarget.At(board.TaskCardFor(r)), waitTap: true);
                if (guide.ConsumeTap())
                    _boardIntroDone = true;
                return;
            }

            if (b.CanRenovate(r))
            {
                guide.Present("Wonderful, you have everything! Tap Fix and watch the shop.",
                    GuideTarget.Below(board.FixButtonFor(r)), waitTap: false);
                return;
            }
            if (board.Dragging)
            {
                guide.Present("Drop it on the matching item!", GuideTarget.None, waitTap: false, showSpeech: true);
                return;
            }
            // A pair below the tier we need: show the drag.
            if (board.FindPair(out int a, out int c, need.chain) && b[a].Tier < need.tier)
            {
                var item = b[a];
                string from = b.NameOf(item), to = b.NameOf(BoardItem.Item(item.Chain, item.Tier + 1));
                string line = _merged
                    ? $"Lovely! Keep merging: two {Plural(from)} make a {to}."
                    : $"Drag one {from} onto the other {from} to merge them into a {to}!";
                guide.Present(line, GuideTarget.DragFrom(board.Cell(a), board.Cell(c), board.IconFor(item)), waitTap: false);
                return;
            }
            int gen = board.GeneratorCell(need.chain);
            if (gen >= 0 && b.Energy > 0)
            {
                var g = b[gen];
                string genName = b.NameOf(g);
                string first = b.Def.chains[need.chain].tiers[0];
                guide.Present($"Tap the {genName} to make a {first}. Then match two of the same!",
                    GuideTarget.At(board.Cell(gen)), waitTap: false);
                return;
            }
            guide.Present("Out of energy! It refills by itself, or watch a short video for +50 right away.",
                GuideTarget.At(board.RefillRect), waitTap: false);
        }

        // The first item of the job that is not on the board yet (or the last one).
        static ItemRef NextNeed(MergeBoard b, int renovation)
        {
            var needs = b.Def.renovations[renovation].needs;
            var counted = new Dictionary<int, int>();
            foreach (var n in needs)
            {
                int code = BoardItem.Item(n.chain, n.tier).code;
                counted[code] = counted.TryGetValue(code, out int c) ? c + 1 : 1;
                if (b.Count(n) < counted[code])
                    return n;
            }
            return needs[needs.Count - 1];
        }

        static string Plural(string name) => name.EndsWith("s") ? name : name + "s";

        // ------------------------------------------------------------------ Others

        void Goal()
        {
            if (_goal < 0 || _sim.IsGoalClaimed(_goal))
            {
                Finish();
                return;
            }
            var rect = goals.ClaimRectFor(_goal);
            if (rect == null)
            {
                guide.Clear();
                return;
            }
            guide.Present("You finished a goal! Tap the reward to collect it. New goals appear right away.",
                GuideTarget.At(rect), waitTap: false);
        }

        void Expand()
        {
            if (_sim.State.expansion > _startExpansion || _sim.NextExpansion == null)
            {
                Finish();
                return;
            }
            if (!expand.IsOpen)
            {
                if (!_sim.CanExpand)
                {
                    Finish();
                    return;
                }
                guide.Present("The shop is doing so well, it can grow! Tap Expand to build a whole new dining room.",
                    GuideTarget.At(expand.OpenButtonRect), waitTap: false);
                return;
            }
            guide.Present(null, GuideTarget.Below(expand.BuildButtonRect), waitTap: false, showSpeech: false);
        }

        void Shops()
        {
            if (!map.AnythingToUnlock())
            {
                Finish();
                return;
            }
            if (!map.IsOpen)
            {
                guide.Present("A new shop can open on Main Street! Tap Shops to have a look.",
                    GuideTarget.At(map.OpenButtonRect), waitTap: false);
                return;
            }
            guide.Present(null, GuideTarget.Below(map.UnlockableActionRect), waitTap: false, showSpeech: false);
        }

        void Vip()
        {
            guide.Present("A VIP is here! They order a lot and pay four times as much, plus gems. Keep the line moving so they don't leave!",
                GuideTarget.At(vip.Panel), waitTap: true);
            if (guide.ConsumeTap() || !_sim.State.queue.Exists(c => c.vip))
                Finish();
        }

        void Boost()
        {
            guide.Present("A little tip: watch a short video to double all your sales for 5 minutes. You can stack it up to an hour!",
                GuideTarget.At((RectTransform)boost.transform), waitTap: true);
            if (guide.ConsumeTap())
                Finish();
        }
    }
}
