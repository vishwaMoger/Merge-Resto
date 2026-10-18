using System.Collections.Generic;
using System.Linq;
using System.Text;
using MergeResto.Game;
using MergeResto.Sim;
using UnityEditor;
using UnityEngine;

namespace MergeResto.EditorTools
{
    /// <summary>
    /// Plays each shop for an hour or more in fast-forward the way an engaged player would and
    /// reports when the milestones land (first upgrade, managers, renovations, expansions, the
    /// next shop), so balancing works from numbers instead of guesses.
    /// </summary>
    public static class PacingBot
    {
        const float Step = 0.1f;
        const float TapReaction = 1.2f;    // seconds before an idle manual station gets tapped
        const float ShopCheckEvery = 3f;   // the player looks at upgrades every few seconds
        const float DineInShare = 0.65f;   // share of customers who sit down and tip

        public class Milestone
        {
            public float time;
            public string text;
        }

        public class Report
        {
            public string shop;
            public readonly List<Milestone> milestones = new();
            public readonly List<float> upgradeTimes = new();
            public readonly List<(float minute, double perMinute)> income = new();
            public float nextShopReady = -1f;

            public void Add(float t, string text) => milestones.Add(new Milestone { time = t, text = text });
        }

        [MenuItem("Merge Resto/Balance/Run Pacing Report")]
        public static void RunMenu()
        {
            var text = RunAll(out _);
            string path = "Logs/PacingReport.md";
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllText(path, text);
            Debug.Log($"[Merge Resto] Pacing report written to {path}\n{text}");
        }

        public static string RunAll(out List<Report> reports)
        {
            BakerySceneBuilder.ApplyBalanceToConfigs();
            reports = new List<Report>
            {
                Run("Assets/Game/Config/GrandmasBakery.asset", 90f, nextShop: BakerySceneBuilder.CafeUnlock),
                Run("Assets/Game/Config/CornerCafe.asset", 120f, nextShop: BakerySceneBuilder.PizzeriaUnlock),
                Run("Assets/Game/Config/NonnosPizzeria.asset", 120f, nextShop: null),
            };
            var sb = new StringBuilder();
            foreach (var r in reports)
                sb.Append(Format(r));
            return sb.ToString();
        }

        static string M(float seconds) => $"{(int)(seconds / 60f)}:{(int)(seconds % 60f):00}";

        static string Format(Report r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"## {r.shop}");
            foreach (var m in r.milestones)
                sb.AppendLine($"- {M(m.time)}  {m.text}");
            sb.AppendLine(r.nextShopReady >= 0 ? $"- **Next shop ready at {M(r.nextShopReady)}**" : "- Next shop: not reached / none");
            // Upgrades per 5-minute window: the "something to buy" rhythm.
            var buckets = new int[18];
            foreach (var t in r.upgradeTimes)
                if (t < 90 * 60) buckets[(int)(t / 300f)]++;
            sb.AppendLine("- Upgrades per 5 min: " + string.Join(" ", buckets));
            if (r.upgradeTimes.Count > 1)
            {
                var gaps = r.upgradeTimes.Zip(r.upgradeTimes.Skip(1), (a, b) => b - a).ToList();
                var early = gaps.Take(Mathf.Min(15, gaps.Count)).ToList();
                sb.AppendLine($"- Gap between upgrades: first 15 avg {early.Average():0}s, overall max {gaps.Max():0}s");
            }
            sb.AppendLine("- Coins/min: " + string.Join(", ", r.income.Select(i => $"{i.minute:0}m {NumberFormatShort(i.perMinute)}")));
            sb.AppendLine();
            return sb.ToString();
        }

        static string NumberFormatShort(double v) => v >= 1e6 ? $"{v / 1e6:0.0}M" : v >= 1e3 ? $"{v / 1e3:0.0}K" : $"{v:0}";

        public static Report Run(string configPath, float minutes, (int renovations, int level, double cost)? nextShop)
        {
            var config = AssetDatabase.LoadAssetAtPath<ShopConfig>(configPath);
            var sim = new ShopSim(config.shop);
            var report = new Report { shop = config.shop.displayName };
            var def = config.shop;
            int n = def.stations.Count;
            var idleFor = new float[n];
            float shopTimer = 0f, t = 0f, incomeWindow = 0f;
            double lastLifetime = 0;
            bool managerLogged = false;
            var served = 0;
            sim.CustomerServed += (c, coins) =>
            {
                served++;
                // Expected tips from diners.
                sim.AddCoins(sim.TipFor(coins) * DineInShare);
            };
            sim.UpgradePurchased += (s, track) =>
            {
                report.upgradeTimes.Add(t);
                if (report.upgradeTimes.Count == 1)
                    report.Add(t, $"first upgrade ({def.stations[s].displayName} {track})");
            };
            sim.ManagerHired += s => report.Add(t, $"manager hired: {def.stations[s].displayName}");
            sim.Expanded += e => report.Add(t, $"EXPANSION {e}: {def.expansions[e - 1].displayName}");
            sim.GoalClaimed += g => report.Add(t, $"goal {g + 1} claimed");
            if (sim.Board != null)
                sim.Board.RenovationDone += r => report.Add(t, $"renovation {r + 1}: {def.board.renovations[r].displayName}");

            int customersLeft = 0;
            bool tutorialGift = false;
            sim.CustomerLeft += _ => customersLeft++;

            while (t < minutes * 60f)
            {
                sim.Tick(Step);
                t += Step;

                // Taps on idle manual stations after a human reaction delay.
                for (int i = 0; i < n; i++)
                {
                    if (sim.NeedsTap(i))
                    {
                        idleFor[i] += Step;
                        if (idleFor[i] >= TapReaction)
                        {
                            sim.Tap(i);
                            idleFor[i] = 0f;
                        }
                    }
                    else
                        idleFor[i] = 0f;
                }

                if (def.hasTutorial && !tutorialGift && t >= 300f)
                {
                    // Grandma's tutorial gift: enough gems for the first manager.
                    tutorialGift = true;
                    sim.State.gems = Mathf.Max(sim.State.gems, def.stations[0].managerGemCost);
                }

                shopTimer += Step;
                if (shopTimer >= ShopCheckEvery)
                {
                    shopTimer = 0f;
                    // Once the next shop only needs coins, the player saves up for it.
                    bool saving = false;
                    if (nextShop.HasValue && report.nextShopReady < 0f && sim.Board != null)
                    {
                        var ns = nextShop.Value;
                        saving = sim.Board.State.renovationsDone.Count >= ns.renovations && sim.ShopLevel >= ns.level;
                    }
                    if (!saving)
                        PlayShop(sim);
                    if (sim.Board != null)
                        PlayBoard(sim.Board);
                }

                incomeWindow += Step;
                if (incomeWindow >= 300f)
                {
                    incomeWindow = 0f;
                    report.income.Add((t / 60f, (sim.State.lifetimeCoins - lastLifetime) / 5.0));
                    lastLifetime = sim.State.lifetimeCoins;
                }

                if (nextShop.HasValue && report.nextShopReady < 0f && sim.Board != null)
                {
                    var ns = nextShop.Value;
                    if (sim.Board.State.renovationsDone.Count >= ns.renovations && sim.ShopLevel >= ns.level && sim.State.coins >= ns.cost)
                        report.nextShopReady = t;
                }
                if (!managerLogged && sim.State.stations.Take(n - 1).All(s => s.hasManager))
                {
                    managerLogged = true;
                    report.Add(t, "fully automated");
                }
            }
            report.Add(t, $"end: shop level {sim.ShopLevel}, served {served}, walked out {customersLeft}, coins {NumberFormatShort(sim.State.coins)}");
            return report;
        }

        // Claim goals, expand, hire managers, then buy the cheapest upgrade (players do).
        static void PlayShop(ShopSim sim)
        {
            for (int g = 0; g < sim.Def.goals.Count; g++)
                if (!sim.IsGoalClaimed(g) && sim.IsGoalComplete(g))
                    sim.ClaimGoal(g);
            if (sim.CanExpand)
                sim.Expand();
            for (int i = 0; i < sim.Def.stations.Count; i++)
                if (sim.CanHireManager(i))
                    sim.HireManager(i);
            for (int k = 0; k < 3; k++)
            {
                int bestS = -1;
                UpgradeTrack bestT = UpgradeTrack.Speed;
                double best = double.MaxValue;
                for (int s = 0; s < sim.Def.stations.Count; s++)
                    foreach (UpgradeTrack track in System.Enum.GetValues(typeof(UpgradeTrack)))
                        if (sim.CanBuyUpgrade(s, track) && sim.UpgradeCost(s, track) < best)
                        {
                            best = sim.UpgradeCost(s, track);
                            bestS = s;
                            bestT = track;
                        }
                if (bestS < 0)
                    break;
                // Keep enough coins for a ready expansion.
                var next = sim.NextExpansion;
                if (next != null && sim.ShopLevel >= next.shopLevelNeeded && sim.State.coins - best < next.cost && sim.State.coins > next.cost * 0.6)
                    break;
                sim.BuyUpgrade(bestS, bestT);
            }
        }

        // Spend energy on the generator the next renovation needs, merge pairs, renovate.
        static void PlayBoard(MergeBoard board)
        {
            for (int r = 0; r < board.Def.renovations.Count; r++)
                if (board.CanRenovate(r))
                    board.Renovate(r);
            var active = board.ActiveRenovations(1);
            if (active.Count == 0)
                return;
            var need = board.Def.renovations[active[0]].needs.FirstOrDefault(n => board.Count(n) < board.Def.renovations[active[0]].needs.Count(m => m.chain == n.chain && m.tier == n.tier));
            int chain = need.chain;
            int genCell = -1;
            for (int i = 0; i < board.Def.Cells; i++)
                if (board.FreeItem(i).IsGenerator && board.Def.generators[board[i].GeneratorIndex].chain == chain)
                    genCell = i;
            for (int spend = 0; spend < 3 && genCell >= 0 && board.Energy > 0; spend++)
            {
                if (board.EmptyCells == 0)
                    SellOne(board, chain);
                if (!board.Spawn(genCell, out _))
                    break;
                MergeAll(board);
            }
            MergeAll(board);
        }

        static void MergeAll(MergeBoard board)
        {
            // Items the next renovation needs, by count, are kept rather than merged further.
            var keep = new Dictionary<int, int>();
            foreach (int r in board.ActiveRenovations(1))
                foreach (var n in board.Def.renovations[r].needs)
                {
                    int code = BoardItem.Item(n.chain, n.tier).code;
                    keep[code] = keep.TryGetValue(code, out int c) ? c + 1 : 1;
                }
            bool merged = true;
            while (merged)
            {
                merged = false;
                // Free items pair up; a wrapped item is freed by merging a free match onto it.
                var seen = new Dictionary<int, int>();
                var wrapped = new Dictionary<int, int>();
                for (int i = 0; i < board.Def.Cells && !merged; i++)
                {
                    var cover = board.CoverOf(i);
                    var item = board[i];
                    if (cover == CellCover.Boxed || !item.IsItem || item.Tier >= board.MaxTier(item.Chain))
                        continue;
                    if (cover == CellCover.Wrapped)
                    {
                        if (seen.TryGetValue(item.code, out int free))
                            merged = board.Move(free, i) == MoveResult.Merged;
                        else
                            wrapped.TryAdd(item.code, i);
                        continue;
                    }
                    if (wrapped.TryGetValue(item.code, out int w))
                    {
                        merged = board.Move(i, w) == MoveResult.Merged;
                        continue;
                    }
                    if (keep.TryGetValue(item.code, out int wanted) && board.Count(new ItemRef(item.Chain, item.Tier)) <= wanted)
                        continue;
                    if (seen.TryGetValue(item.code, out int other))
                    {
                        merged = board.Move(other, i) == MoveResult.Merged;
                        continue;
                    }
                    seen[item.code] = i;
                }
            }
        }

        static void SellOne(MergeBoard board, int keepChain)
        {
            for (int i = 0; i < board.Def.Cells; i++)
                if (board.FreeItem(i).IsItem && board[i].Chain != keepChain)
                {
                    board.Sell(i);
                    return;
                }
            for (int i = 0; i < board.Def.Cells; i++)
                if (board.FreeItem(i).IsItem)
                {
                    board.Sell(i);
                    return;
                }
        }
    }
}
