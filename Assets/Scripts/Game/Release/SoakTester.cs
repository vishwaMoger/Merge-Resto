#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MergeResto.Game.UI;
using MergeResto.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace MergeResto.Game.Release
{
    /// <summary>
    /// Editor-only soak test: plays every shop like an eager player (taps, upgrades, managers,
    /// goals, merging and fixing on the board, expanding, every window), fast, for minutes on end,
    /// travelling between shops. It reports every error and warning, the frame times of a busy
    /// shop at normal speed, memory, and the object count (to catch leaks), to Logs/SoakReport.md.
    /// </summary>
    public class SoakTester : MonoBehaviour
    {
        public static bool Running { get; private set; }

        static readonly string[] Shops = { "Bakery", "Cafe", "Pizzeria", "Patisserie", "DriveThru" };
        readonly List<string> _problems = new();
        readonly StringBuilder _report = new();
        float _realSecondsPerShop;

        public static void Run(float realSecondsPerShop)
        {
            var go = new GameObject("SoakTester");
            DontDestroyOnLoad(go);
            var t = go.AddComponent<SoakTester>();
            t._realSecondsPerShop = realSecondsPerShop;
            Running = true;
            t.StartCoroutine(t.Soak());
        }

        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;

        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Log)
                return;
            // Known editor noise, not the game.
            if (message.Contains("Rewarded ad skipped") || message.Contains("AnalyzeTab") || message.Contains("/api/exec"))
                return;
            string first = stack.Split('\n').FirstOrDefault(l => l.Contains("MergeResto")) ?? stack.Split('\n').FirstOrDefault() ?? "";
            string line = $"[{SceneManager.GetActiveScene().name}] {type}: {message.Split('\n')[0]}  ({first.Trim()})";
            if (_problems.Count < 200 && !_problems.Contains(line))
                _problems.Add(line);
        }

        static int Objects() => FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        IEnumerator Soak()
        {
            // A clean start in every shop, everything unlocked, no lessons in the way.
            SaveStore.Save("lessons", "renovate,goal,expand,shops,vip,boost,boxes");
            DailyRewardStore.Claim();
            foreach (var id in new[] { "grandmas_bakery", "corner_cafe", "nonnos_pizzeria", "belles_patisserie", "drive_thru" })
            {
                SaveStore.Delete($"shop_{id}");
                WorldState.Unlock(id);
            }
            _report.AppendLine($"# Soak report ({System.DateTime.Now:yyyy-MM-dd HH:mm})");
            _report.AppendLine();
            _report.AppendLine($"Each shop played for {_realSecondsPerShop:0} s real time at 6x (about {_realSecondsPerShop * 6f / 60f:0} minutes of game time), then 6 s at normal speed for frame times.");
            _report.AppendLine();
            _report.AppendLine("| Shop | Frame avg | Frame worst | Over 33 ms | Objects start/end | Managed MB start/end | Customers |");
            _report.AppendLine("|---|---|---|---|---|---|---|");

            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var scene in Shops)
                {
                    var gc = GameController.Instance;
                    if (gc != null && SceneManager.GetActiveScene().name != scene)
                        gc.Travel(scene);
                    else if (gc == null || pass == 0)
                        SceneManager.LoadScene(scene);
                    yield return null;
                    yield return null;
                    yield return PlayShop(scene, pass);
                }
            }

            Time.timeScale = 1f;
            _report.AppendLine();
            _report.AppendLine(_problems.Count == 0 ? "**No errors or warnings.**" : $"**{_problems.Count} problems:**");
            foreach (var p in _problems)
                _report.AppendLine($"- {p}");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/SoakReport.md", _report.ToString());
            Debug.Log($"[Merge Resto] Soak finished: {_problems.Count} problems. Logs/SoakReport.md");
            Running = false;
            Destroy(gameObject);
        }

        IEnumerator PlayShop(string scene, int pass)
        {
            var gc = GameController.Instance;
            var sim = gc.Sim;
            sim.State.tutorialStep = ShopState.TutorialFinished;
            int objectsStart = Objects();
            long memStart = Profiler.GetMonoUsedSizeLong();
            var board = FindAnyObjectByType<MergeBoardView>();
            var map = FindAnyObjectByType<MapPanel>();
            var expand = FindAnyObjectByType<ExpandPanel>();
            var settings = FindAnyObjectByType<SettingsPanel>();
            var daily = FindAnyObjectByType<DailyRewardsPanel>();

            Time.timeScale = 6f;
            float end = Time.realtimeSinceStartup + _realSecondsPerShop;
            float nextBoard = 0f, nextWindow = 0f;
            int window = 0;
            while (Time.realtimeSinceStartup < end)
            {
                float now = Time.realtimeSinceStartup;
                // Taps, goals, managers, upgrades and expansions, as an eager player.
                for (int i = 0; i < sim.Def.stations.Count; i++)
                    if (sim.NeedsTap(i))
                        gc.ClickStation(i);
                for (int g = 0; g < sim.Def.goals.Count; g++)
                    if (!sim.IsGoalClaimed(g) && sim.IsGoalComplete(g))
                        sim.ClaimGoal(g);
                for (int i = 0; i < sim.Def.stations.Count; i++)
                    if (sim.CanHireManager(i))
                        sim.HireManager(i);
                if (sim.CanExpand)
                    sim.Expand();
                int bestS = -1;
                var bestT = UpgradeTrack.Speed;
                double best = double.MaxValue;
                for (int s = 0; s < sim.Def.stations.Count; s++)
                    foreach (UpgradeTrack track in System.Enum.GetValues(typeof(UpgradeTrack)))
                        if (sim.CanBuyUpgrade(s, track) && sim.UpgradeCost(s, track) < best)
                        {
                            best = sim.UpgradeCost(s, track);
                            bestS = s;
                            bestT = track;
                        }
                if (bestS >= 0)
                    sim.BuyUpgrade(bestS, bestT);

                // The board: open it, make items, merge everything that matches, fix what is ready.
                if (board != null && now > nextBoard)
                {
                    nextBoard = now + 1.5f;
                    yield return PlayBoard(board, sim);
                }
                // Every window, opened and closed again.
                if (now > nextWindow)
                {
                    nextWindow = now + 2.5f;
                    switch (window++ % 4)
                    {
                        case 0: map?.SetOpen(true); break;
                        case 1: expand?.SetOpen(true); break;
                        case 2: settings?.SetOpen(true); break;
                        case 3: daily?.SetOpen(true); break;
                    }
                    yield return new WaitForSecondsRealtime(0.4f);
                    map?.SetOpen(false);
                    expand?.SetOpen(false);
                    settings?.SetOpen(false);
                    daily?.SetOpen(false);
                }
                yield return null;
            }

            // Frame times of the busy shop at normal speed.
            Time.timeScale = 1f;
            yield return new WaitForSecondsRealtime(1f);
            var frames = new List<float>();
            float stop = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < stop)
            {
                yield return null;
                frames.Add(Time.unscaledDeltaTime * 1000f);
            }
            frames.Sort();
            float avg = frames.Average();
            float worst = frames[^1];
            int slow = frames.Count(f => f > 33.4f);
            int objectsEnd = Objects();
            long memEnd = Profiler.GetMonoUsedSizeLong();
            int customers = sim.State.queue.Count;
            _report.AppendLine($"| {scene}{(pass > 0 ? " (2nd visit)" : "")} | {avg:0.0} ms | {worst:0} ms | {slow}/{frames.Count} | {objectsStart} / {objectsEnd} | {memStart / 1048576f:0.0} / {memEnd / 1048576f:0.0} | {customers} in line, {sim.State.customersServed} served |");
            gc.Save();
        }

        static IEnumerator PlayBoard(MergeBoardView board, ShopSim sim)
        {
            var b = sim.Board;
            board.SetOpen(true);
            yield return null;
            // A few items from each generator.
            for (int i = 0; i < b.Def.Cells; i++)
                if (b.FreeItem(i).IsGenerator && b.CheckSpawn(i) == SpawnFail.None)
                    board.OnCellTapped(i);
            yield return null;
            // Merge pairs (free ones, or onto wrapped matches) with real drags.
            for (int k = 0; k < 4 && board.FindPair(out int a, out int c); k++)
            {
                Vector2 P(int cell)
                {
                    var rt = board.Cell(cell);
                    return rt.TransformPoint(rt.rect.center);
                }
                var e = new PointerEventData(EventSystem.current) { position = P(a), pointerId = -1 };
                board.OnDragBegin(a, e);
                e.position = P(c);
                board.OnDragMove(e);
                board.OnDragEnd(e);
                yield return null;
            }
            // Keep space free: sell the lowest items when the board fills up.
            if (b.EmptyCells < 3)
                for (int i = 0; i < b.Def.Cells && b.EmptyCells < 5; i++)
                    if (b.FreeItem(i).IsItem && b.FreeItem(i).Tier == 0)
                        b.Sell(i);
            foreach (int r in b.ActiveRenovations(3))
                if (b.CanRenovate(r))
                {
                    board.TryFix(r);
                    break;
                }
            board.SetOpen(false);
        }
    }
}
#endif
