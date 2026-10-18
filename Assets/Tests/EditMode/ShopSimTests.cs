using NUnit.Framework;
using UnityEngine;

namespace MergeResto.Sim.Tests
{
    public class ShopSimTests
    {
        const int Flour = 0, Mixer = 1, Oven = 2, Counter = 3;

        static ShopDefinition Bakery()
        {
            var def = new ShopDefinition
            {
                customerIntervalSeconds = 4f,
                itemsPerOrder = 2,
                pricePerItem = 25,
                patienceSeconds = 20f,
                startCoins = 500,
                startGems = 20,
            };
            def.stations.Add(new StationDefinition { id = "flour", kind = StationKind.Source, baseCycleSeconds = 2f, baseBatch = 10, outputCapacity = 40 });
            def.stations.Add(new StationDefinition { id = "mixer", kind = StationKind.Processor, baseCycleSeconds = 3f, baseBatch = 5 });
            def.stations.Add(new StationDefinition { id = "oven", kind = StationKind.Processor, baseCycleSeconds = 4f, baseBatch = 5 });
            def.stations.Add(new StationDefinition { id = "counter", kind = StationKind.Seller, baseCycleSeconds = 1.5f, startsAutomated = true, inputCapacity = 40 });
            for (int i = 0; i < 3; i++)
                def.couriers.Add(new CourierDefinition { carry = 5, walkSeconds = 1f });
            return def;
        }

        static void AutomateAll(ShopSim sim)
        {
            for (int i = 0; i < sim.Def.stations.Count; i++)
                sim.State.stations[i].hasManager = true;
        }

        [Test]
        public void NewShop_StartsWithConfiguredCurrency()
        {
            var sim = new ShopSim(Bakery());
            Assert.AreEqual(500, sim.State.coins);
            Assert.AreEqual(20, sim.State.gems);
            Assert.IsTrue(sim.IsAutomated(Counter), "the cashier always works");
            Assert.IsFalse(sim.IsAutomated(Flour));
        }

        [Test]
        public void ManualStation_OnlyRunsWhenTapped()
        {
            var sim = new ShopSim(Bakery());
            Assert.IsTrue(sim.NeedsTap(Flour));
            sim.Tick(5f);
            Assert.AreEqual(0, sim.State.stations[Flour].output, "nothing happens without a tap");

            Assert.IsTrue(sim.Tap(Flour));
            sim.Tick(2.05f);
            Assert.AreEqual(10, sim.State.stations[Flour].output + sim.State.couriers[0].carrying);
        }

        [Test]
        public void Tap_CannotQueueMoreStartsThanMachines()
        {
            var sim = new ShopSim(Bakery());
            Assert.IsTrue(sim.Tap(Flour));   // starts immediately
            Assert.IsTrue(sim.Tap(Flour));   // queued behind the running cycle
            Assert.IsFalse(sim.Tap(Flour));  // one machine: only one queued start
        }

        [Test]
        public void Manager_RunsStationAutomatically()
        {
            var sim = new ShopSim(Bakery());
            Assert.IsTrue(sim.HireManager(Flour));
            Assert.AreEqual(0, sim.State.gems);
            sim.Tick(4.1f);
            var flour = sim.State.stations[Flour];
            Assert.GreaterOrEqual(flour.output + sim.State.couriers[0].carrying + sim.State.stations[Mixer].input, 20);
        }

        [Test]
        public void HireManager_FailsWithoutGems()
        {
            var sim = new ShopSim(Bakery());
            sim.State.gems = 5;
            Assert.IsFalse(sim.HireManager(Flour));
            Assert.IsFalse(sim.IsAutomated(Flour));
        }

        [Test]
        public void Courier_CarriesGoodsToNextStation()
        {
            var sim = new ShopSim(Bakery());
            sim.State.stations[Flour].output = 10;
            sim.Tick(0.05f);
            Assert.AreEqual(5, sim.State.couriers[0].carrying);
            sim.Tick(1.05f);
            Assert.AreEqual(5, sim.State.stations[Mixer].input);
            Assert.AreEqual(CourierPhase.WalkingBack, sim.State.couriers[0].phase);
        }

        [Test]
        public void FullyAutomatedChain_SellsToCustomers()
        {
            var sim = new ShopSim(Bakery());
            AutomateAll(sim);
            double served = 0;
            sim.CustomerServed += (_, coins) => served += coins;
            sim.Tick(60f);
            Assert.Greater(served, 0);
            Assert.AreEqual(500 + served, sim.State.coins, 0.001);
            Assert.AreEqual(0, served % 50, 0.001, "each order is 2 items at 25 coins");
        }

        [Test]
        public void Customer_IsNotServedBeforeReachingTheCounter()
        {
            var def = Bakery();
            def.walkInSeconds = 3f;
            var sim = new ShopSim(def);
            sim.State.stations[Counter].input = 20; // bread already on display
            bool served = false;
            sim.CustomerServed += (_, _) => served = true;

            sim.Tick(4.05f + 2.9f); // arrives at 4s, still walking until 7s
            Assert.IsFalse(served, "served while still walking to the counter");
            Assert.AreEqual(1, sim.State.queue.Count);

            sim.Tick(0.3f + 1.5f); // reaches the counter, then 1.5s of service
            Assert.IsTrue(served);
        }

        [Test]
        public void Queue_StepsUpBeforeNextCustomerIsServed()
        {
            var def = Bakery();
            def.walkInSeconds = 0f;
            def.stepUpSeconds = 0.5f;
            var sim = new ShopSim(def);
            sim.State.stations[Counter].input = 20;
            sim.State.queue.Add(new CustomerState { id = 100, items = 2, patienceLeft = 20f });
            sim.State.queue.Add(new CustomerState { id = 101, items = 2, patienceLeft = 20f });
            int servedId = -1;
            sim.CustomerServed += (c, _) => servedId = c.id;

            sim.Tick(1.75f); // first customer served (1.5s service)
            Assert.AreEqual(100, servedId);
            Assert.Greater(sim.State.queue[0].walkLeft, 0f, "second customer still stepping up");
            Assert.AreEqual(-1, sim.State.queue[0].servedBy);

            sim.Tick(0.6f);
            Assert.AreEqual(0, sim.State.queue[0].servedBy, "served once at the counter");
        }

        [Test]
        public void Customer_LeavesWhenPatienceRunsOut()
        {
            var sim = new ShopSim(Bakery());
            int left = 0;
            sim.CustomerLeft += _ => left++;
            sim.Tick(30f); // nothing produced, so nobody gets served
            Assert.Greater(left, 0);
        }

        [Test]
        public void Customer_StaysWhilePatienceIsPaused()
        {
            var sim = new ShopSim(Bakery()) { PatienceScale = 0f };
            int left = 0;
            sim.CustomerLeft += _ => left++;
            sim.Tick(60f);
            Assert.AreEqual(0, left);
            Assert.Greater(sim.State.queue.Count, 0);
        }

        [Test]
        public void Queue_NeverExceedsMax()
        {
            var def = Bakery();
            def.patienceSeconds = 1000f;
            var sim = new ShopSim(def);
            sim.Tick(200f);
            Assert.AreEqual(def.maxQueue, sim.State.queue.Count);
        }

        [Test]
        public void Upgrade_SpendsCoinsAndSpeedsUpStation()
        {
            var sim = new ShopSim(Bakery());
            float before = sim.CycleSeconds(Oven);
            double cost = sim.UpgradeCost(Oven, UpgradeTrack.Speed);
            Assert.IsTrue(sim.BuyUpgrade(Oven, UpgradeTrack.Speed));
            Assert.AreEqual(500 - cost, sim.State.coins, 0.001);
            Assert.Less(sim.CycleSeconds(Oven), before);
            Assert.Greater(sim.UpgradeCost(Oven, UpgradeTrack.Speed), cost, "next level costs more");
        }

        [Test]
        public void Upgrade_FailsWhenTooExpensive()
        {
            var sim = new ShopSim(Bakery());
            sim.State.coins = 10;
            Assert.IsFalse(sim.BuyUpgrade(Oven, UpgradeTrack.Speed));
            Assert.AreEqual(0, sim.State.stations[Oven].speedLevel);
        }

        [Test]
        public void MachineUpgrade_AddsParallelMachine()
        {
            var sim = new ShopSim(Bakery());
            sim.State.coins = 10000;
            Assert.IsTrue(sim.BuyUpgrade(Mixer, UpgradeTrack.Machines));
            Assert.AreEqual(2, sim.Machines(Mixer));
            Assert.AreEqual(2, sim.State.stations[Mixer].machines.Count);
        }

        [Test]
        public void GemMilestone_GrantsGemsEveryFiveLevels()
        {
            var sim = new ShopSim(Bakery());
            sim.State.coins = 1e9;
            for (int i = 0; i < 5; i++)
                Assert.IsTrue(sim.BuyUpgrade(Flour, UpgradeTrack.Speed));
            Assert.AreEqual(30, sim.State.gems);
        }

        [Test]
        public void SellerOutputUpgrade_RaisesPrice()
        {
            var sim = new ShopSim(Bakery());
            double before = sim.PricePerItem;
            Assert.IsTrue(sim.BuyUpgrade(Counter, UpgradeTrack.Output));
            Assert.Greater(sim.PricePerItem, before);
        }

        [Test]
        public void Offline_EarnsNothingUnlessFullyAutomated()
        {
            var sim = new ShopSim(Bakery());
            Assert.AreEqual(0, sim.OfflineEarnings(3600));
        }

        [Test]
        public void Offline_UsesSlowestLinkAndCap()
        {
            var sim = new ShopSim(Bakery());
            AutomateAll(sim);
            // Demand is 2 items / 4 s = 0.5 items/s, the slowest link: 12.5 coins/s.
            Assert.AreEqual(12.5, sim.IdleCoinsPerSecond(), 0.001);
            Assert.AreEqual(Mathf.Floor(12.5f * 600 * 0.5f), sim.OfflineEarnings(600), 0.001);
            Assert.AreEqual(sim.OfflineEarnings(2 * 3600), sim.OfflineEarnings(10 * 3600), 0.001, "capped at 2 hours");
        }

        [Test]
        public void State_SurvivesJsonRoundTrip()
        {
            var sim = new ShopSim(Bakery());
            AutomateAll(sim);
            sim.State.coins = 1e6;
            sim.BuyUpgrade(Oven, UpgradeTrack.Machines);
            sim.Tick(37f);

            string json = JsonUtility.ToJson(sim.State);
            var restored = new ShopSim(Bakery(), JsonUtility.FromJson<ShopState>(json));

            Assert.AreEqual(sim.State.coins, restored.State.coins, 0.001);
            Assert.AreEqual(2, restored.Machines(Oven));
            Assert.AreEqual(sim.State.queue.Count, restored.State.queue.Count);
            Assert.AreEqual(JsonUtility.ToJson(sim.State), JsonUtility.ToJson(restored.State));
        }

        static ShopDefinition WithGoals()
        {
            var def = Bakery();
            def.goals.Add(new GoalDefinition { kind = GoalKind.StationLevel, station = 2, target = 2, rewardGems = 15 });
            def.goals.Add(new GoalDefinition { kind = GoalKind.ServeCustomers, target = 3, rewardCoins = 100 });
            def.goals.Add(new GoalDefinition { kind = GoalKind.HireManager, station = 1, rewardCoins = 50 });
            def.goals.Add(new GoalDefinition { kind = GoalKind.LifetimeCoins, target = 1000, rewardGems = 5 });
            return def;
        }

        [Test]
        public void Goals_ShowFirstUnclaimedInOrder()
        {
            var sim = new ShopSim(WithGoals());
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, sim.ActiveGoals(3));
        }

        [Test]
        public void Goal_CannotBeClaimedBeforeItIsDone()
        {
            var sim = new ShopSim(WithGoals());
            Assert.IsFalse(sim.ClaimGoal(0));
            sim.BuyUpgrade(2, UpgradeTrack.Speed);
            Assert.AreEqual(1, sim.GoalProgress(0));
            Assert.IsFalse(sim.IsGoalComplete(0));
        }

        [Test]
        public void Goal_ClaimPaysRewardOnceAndRevealsTheNext()
        {
            var sim = new ShopSim(WithGoals());
            sim.BuyUpgrade(2, UpgradeTrack.Speed);
            sim.BuyUpgrade(2, UpgradeTrack.Output);
            int gems = sim.State.gems;
            Assert.IsTrue(sim.ClaimGoal(0));
            Assert.AreEqual(gems + 15, sim.State.gems);
            Assert.IsFalse(sim.ClaimGoal(0));
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, sim.ActiveGoals(3));
        }

        [Test]
        public void Goal_CountsServedCustomersAndManagers()
        {
            var sim = new ShopSim(WithGoals());
            AutomateAll(sim);
            for (int i = 0; i < 300; i++)
                sim.Tick(0.1f);
            Assert.Greater(sim.State.customersServed, 0);
            Assert.IsTrue(sim.IsGoalComplete(2)); // the mixer is automated, so its manager goal is met
            Assert.AreEqual(sim.State.customersServed >= 3, sim.IsGoalComplete(1));
        }

        // ------------------------------------------------------------------ Merge board

        static ShopDefinition WithBoard()
        {
            var def = Bakery();
            var b = def.board;
            b.columns = 4;
            b.rows = 3;
            b.energyMax = 10;
            b.energyRegenSeconds = 5f;
            b.chains.Add(new MergeChainDefinition { id = "sweets", tiers = new() { "Cookie", "Macaron", "Cupcake", "Cake" } });
            b.chains.Add(new MergeChainDefinition { id = "plants", tiers = new() { "Sprout", "Succulent", "Monstera" } });
            b.generators.Add(new GeneratorDefinition { id = "jar", chain = 0, luckyChance = 0f });
            b.generators.Add(new GeneratorDefinition { id = "can", chain = 1, luckyChance = 0f });
            b.start.Add(new BoardStartItem { cell = 0, generator = 0 });
            b.start.Add(new BoardStartItem { cell = 11, generator = 1 });
            b.renovations.Add(new RenovationDefinition { id = "stand", needs = new() { new ItemRef(0, 2) }, rewardCoins = 100, coziness = 0.1f });
            b.renovations.Add(new RenovationDefinition { id = "plants", needs = new() { new ItemRef(1, 1), new ItemRef(1, 1) }, coziness = 0.2f });
            return def;
        }

        [Test]
        public void Board_StartsWithGeneratorsAndFullEnergy()
        {
            var sim = new ShopSim(WithBoard());
            Assert.IsTrue(sim.Board[0].IsGenerator);
            Assert.IsTrue(sim.Board[11].IsGenerator);
            Assert.AreEqual(10, sim.Board.Energy);
            Assert.AreEqual(10, sim.Board.EmptyCells);
        }

        [Test]
        public void Generator_SpendsEnergyAndDropsItemNextToItself()
        {
            var sim = new ShopSim(WithBoard());
            Assert.IsTrue(sim.Board.Spawn(0, out int cell));
            Assert.AreEqual(1, cell); // right neighbour, first in reading order
            Assert.AreEqual(BoardItem.Item(0, 0), sim.Board[cell]);
            Assert.AreEqual(9, sim.Board.Energy);
        }

        [Test]
        public void Generator_StopsWithoutEnergyOrSpace()
        {
            var sim = new ShopSim(WithBoard());
            for (int i = 0; i < 10; i++)
                Assert.IsTrue(sim.Board.Spawn(0, out _));
            Assert.IsFalse(sim.Board.Spawn(0, out _), "no energy");
            sim.Tick(1000f);
            Assert.AreEqual(10, sim.Board.Energy);
            Assert.IsFalse(sim.Board.Spawn(0, out _), "board full");
        }

        [Test]
        public void EqualItems_MergeIntoNextTier()
        {
            var sim = new ShopSim(WithBoard());
            sim.Board.Spawn(0, out int a);
            sim.Board.Spawn(0, out int b);
            Assert.AreEqual(MoveResult.Merged, sim.Board.Move(a, b));
            Assert.AreEqual(BoardItem.Item(0, 1), sim.Board[b]);
            Assert.IsTrue(sim.Board[a].IsEmpty);
        }

        [Test]
        public void DifferentItems_Swap_AndEmptyCellsTakeMoves()
        {
            var sim = new ShopSim(WithBoard());
            sim.Board.Spawn(0, out int cookie);
            sim.Board.Spawn(11, out int sprout);
            Assert.AreEqual(MoveResult.Swapped, sim.Board.Move(cookie, sprout));
            Assert.AreEqual(BoardItem.Item(0, 0), sim.Board[sprout]);
            Assert.AreEqual(MoveResult.Moved, sim.Board.Move(sprout, 5));
            Assert.AreEqual(BoardItem.Item(0, 0), sim.Board[5]);
        }

        [Test]
        public void MaxTierItems_DoNotMerge()
        {
            var def = WithBoard();
            def.board.start.Add(new BoardStartItem { cell = 4, generator = -1, chain = 1, tier = 2 });
            def.board.start.Add(new BoardStartItem { cell = 5, generator = -1, chain = 1, tier = 2 });
            var sim = new ShopSim(def);
            Assert.AreEqual(MoveResult.Swapped, sim.Board.Move(4, 5));
        }

        [Test]
        public void Renovation_ConsumesItems_PaysCoins_AndRaisesPrices()
        {
            var def = WithBoard();
            def.board.start.Add(new BoardStartItem { cell = 4, generator = -1, chain = 0, tier = 2 });
            var sim = new ShopSim(def);
            double price = sim.PricePerItem;
            double coins = sim.State.coins;
            Assert.IsFalse(sim.Board.CanRenovate(1));
            Assert.IsTrue(sim.Board.Renovate(0));
            Assert.IsTrue(sim.Board[4].IsEmpty);
            Assert.AreEqual(coins + 100, sim.State.coins);
            Assert.AreEqual(price * 1.1, sim.PricePerItem, 1e-6);
            Assert.IsFalse(sim.Board.Renovate(0), "only once");
            CollectionAssert.AreEqual(new[] { 1 }, sim.Board.ActiveRenovations(3));
        }

        [Test]
        public void Renovation_NeedsEveryCopy()
        {
            var def = WithBoard();
            def.board.start.Add(new BoardStartItem { cell = 4, generator = -1, chain = 1, tier = 1 });
            var sim = new ShopSim(def);
            Assert.IsFalse(sim.Board.CanRenovate(1));
            sim.State.board.cells[5] = BoardItem.Item(1, 1).code;
            Assert.IsTrue(sim.Board.Renovate(1));
            Assert.IsTrue(sim.Board[4].IsEmpty && sim.Board[5].IsEmpty);
        }

        [Test]
        public void Board_SurvivesSaveAndLoad()
        {
            var def = WithBoard();
            var sim = new ShopSim(def);
            sim.Board.Spawn(0, out _);
            sim.Board.Spawn(11, out _);
            var json = JsonUtility.ToJson(sim.State);
            var restored = new ShopSim(def, JsonUtility.FromJson<ShopState>(json));
            CollectionAssert.AreEqual(sim.State.board.cells, restored.State.board.cells);
            Assert.AreEqual(sim.Board.Energy, restored.Board.Energy);
        }

        [Test]
        public void Selling_FreesCellForCoins()
        {
            var sim = new ShopSim(WithBoard());
            sim.Board.Spawn(0, out int cell);
            double coins = sim.State.coins;
            Assert.IsTrue(sim.Board.Sell(cell));
            Assert.Greater(sim.State.coins, coins);
            Assert.IsFalse(sim.Board.Sell(0), "generators cannot be sold");
        }

        // ------------------------------------------------------------------ Daily rewards

        [Test]
        public void Daily_FirstClaimIsDayOne_OncePerDay()
        {
            var state = new DailyRewardState();
            Assert.IsTrue(DailyRewards.CanClaim(state, 100));
            Assert.AreEqual(0, DailyRewards.Claim(state, 100));
            Assert.IsFalse(DailyRewards.CanClaim(state, 100));
            Assert.AreEqual(-1, DailyRewards.Claim(state, 100));
        }

        [Test]
        public void Daily_NextDayAdvances_MissedDayRestarts_WeekRepeats()
        {
            var state = new DailyRewardState();
            for (int d = 0; d < 7; d++)
                Assert.AreEqual(d, DailyRewards.Claim(state, 100 + d));
            Assert.AreEqual(0, DailyRewards.Claim(state, 107), "after day 7 the week repeats");
            Assert.AreEqual(1, DailyRewards.Claim(state, 108));
            Assert.AreEqual(0, DailyRewards.Claim(state, 110), "a missed day starts again");
        }

        [Test]
        public void Board_AddEnergyCanExceedMaximum()
        {
            var sim = new ShopSim(WithBoard());
            sim.Board.AddEnergy(25);
            Assert.AreEqual(35, sim.Board.Energy);
            sim.Tick(100f);
            Assert.AreEqual(35, sim.Board.Energy, "no regeneration above the maximum");
        }
    }
}
