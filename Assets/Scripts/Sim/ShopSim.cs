using System;
using System.Collections.Generic;

namespace MergeResto.Sim
{
    /// <summary>
    /// The shop's production chain as pure game logic: stations run cycles, couriers carry goods
    /// along the chain, customers queue and pay at the Seller. Visuals only read this and
    /// listen to its events, so frame rate never changes earnings.
    /// </summary>
    public sealed class ShopSim
    {
        const float MaxStep = 0.1f;

        public ShopDefinition Def { get; }
        public ShopState State { get; }
        /// <summary>The merge board, or null when the shop has none.</summary>
        public MergeBoard Board { get; }

        public event Action<int, int> CycleStarted;          // station, machine
        public event Action<int, int, int> CycleCompleted;   // station, machine, amount
        public event Action<int, int> CourierPickedUp;       // link, amount
        public event Action<int, int> CourierDelivered;      // link, amount
        public event Action<CustomerState> CustomerArrived;
        public event Action<CustomerState, double> CustomerServed;
        public event Action<CustomerState> CustomerLeft;
        public event Action<int, int> GemsEarned;            // station, amount
        public event Action<int, UpgradeTrack> UpgradePurchased;
        public event Action<int> ManagerHired;
        public event Action CoinsChanged;
        public event Action<int> GoalClaimed;                // goal index

        int _nextCustomerId = 1;

        /// <summary>How fast waiting customers lose patience (0 pauses it, e.g. during the tutorial).</summary>
        public float PatienceScale { get; set; } = 1f;

        public ShopSim(ShopDefinition def, ShopState state = null)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            if (def.stations.Count < 2 || def.stations[0].kind != StationKind.Source || def.stations[^1].kind != StationKind.Seller)
                throw new ArgumentException("A shop needs a Source first and a Seller last.");
            if (def.couriers.Count != def.stations.Count - 1)
                throw new ArgumentException("A shop needs exactly one courier per link between stations.");

            State = state ?? NewState(def);
            Normalize();
            foreach (var c in State.queue)
                _nextCustomerId = Math.Max(_nextCustomerId, c.id + 1);
            if (def.board != null && def.board.chains.Count > 0)
            {
                State.board ??= new BoardState();
                Board = new MergeBoard(this, def.board, State.board);
            }
        }

        public static ShopState NewState(ShopDefinition def)
        {
            var s = new ShopState { shopId = def.id, coins = def.startCoins, gems = def.startGems };
            foreach (var st in def.stations)
                s.stations.Add(new StationState { hasManager = st.startsAutomated });
            for (int i = 0; i < def.couriers.Count; i++)
                s.couriers.Add(new CourierState());
            return s;
        }

        // Keeps saved state consistent with the current definition (e.g. after a content update).
        void Normalize()
        {
            State.shopId = Def.id;
            State.goalsClaimed ??= new List<int>();
            while (State.stations.Count < Def.stations.Count)
                State.stations.Add(new StationState { hasManager = Def.stations[State.stations.Count].startsAutomated });
            while (State.couriers.Count < Def.couriers.Count)
                State.couriers.Add(new CourierState());
            for (int i = 0; i < Def.stations.Count; i++)
            {
                var st = State.stations[i];
                if (Def.stations[i].startsAutomated)
                    st.hasManager = true;
                int machines = Machines(i);
                while (st.machines.Count < machines)
                    st.machines.Add(new MachineState());
            }
        }

        // ------------------------------------------------------------------ Derived values

        public int ShopLevel
        {
            get
            {
                int total = 0;
                foreach (var st in State.stations) total += st.TotalLevels;
                return total;
            }
        }

        public bool IsAutomated(int station) => State.stations[station].hasManager;

        public float CycleSeconds(int station) =>
            Def.stations[station].baseCycleSeconds * (float)Math.Pow(0.85, State.stations[station].speedLevel);

        public int Batch(int station)
        {
            var def = Def.stations[station];
            if (def.kind == StationKind.Seller)
                return Def.itemsPerOrder;
            return (int)Math.Round(def.baseBatch * (1 + 0.5 * State.stations[station].outputLevel));
        }

        public int Machines(int station) => 1 + State.stations[station].machineLevel;

        // Previews of the value after one more level, for upgrade panels.
        public float CycleSecondsAtLevel(int station, int speedLevel) =>
            Def.stations[station].baseCycleSeconds * (float)Math.Pow(0.85, speedLevel);

        public int BatchAtLevel(int station, int outputLevel) =>
            Def.stations[station].kind == StationKind.Seller
                ? Def.itemsPerOrder
                : (int)Math.Round(Def.stations[station].baseBatch * (1 + 0.5 * outputLevel));

        public double PriceAtLevel(int outputLevel) =>
            Def.pricePerItem * (1 + Def.pricePerOutputLevel * outputLevel) * CozinessMultiplier;

        public double PricePerItem => PriceAtLevel(State.stations[^1].outputLevel);

        /// <summary>Price multiplier from renovations (1 = no bonus).</summary>
        public double CozinessMultiplier => 1 + (Board?.Coziness ?? 0f);

        public float CustomerInterval =>
            Math.Max(Def.minCustomerIntervalSeconds, Def.customerIntervalSeconds * (float)Math.Pow(Def.intervalPerShopLevel, ShopLevel));

        public double UpgradeCost(int station, UpgradeTrack track)
        {
            var up = Def.stations[station].Upgrade(track);
            return up == null ? double.PositiveInfinity : up.CostForLevel(State.stations[station].Level(track));
        }

        public bool IsMaxed(int station, UpgradeTrack track)
        {
            var up = Def.stations[station].Upgrade(track);
            return up == null || State.stations[station].Level(track) >= up.maxLevel;
        }

        public bool CanBuyUpgrade(int station, UpgradeTrack track) =>
            !IsMaxed(station, track) && State.coins >= UpgradeCost(station, track);

        /// <summary>True when a manual station is idle but could start a cycle if tapped.</summary>
        public bool NeedsTap(int station)
        {
            var st = State.stations[station];
            if (st.hasManager || st.queuedStarts > 0)
                return false;
            return FreeMachine(station) >= 0 && CanStartCycle(station);
        }

        // ------------------------------------------------------------------ Player actions

        /// <summary>Tap a station: queues a cycle start on a manual station. Returns false if nothing happens.</summary>
        public bool Tap(int station)
        {
            var st = State.stations[station];
            if (st.hasManager || Def.stations[station].kind == StationKind.Seller)
                return false;
            if (st.queuedStarts >= Machines(station))
                return false;
            st.queuedStarts++;
            TryStartCycles(station);
            return true;
        }

        public bool BuyUpgrade(int station, UpgradeTrack track)
        {
            if (!CanBuyUpgrade(station, track))
                return false;
            var st = State.stations[station];
            State.coins -= UpgradeCost(station, track);
            st.SetLevel(track, st.Level(track) + 1);
            if (track == UpgradeTrack.Machines)
                st.machines.Add(new MachineState());
            CheckGemMilestones(station);
            UpgradePurchased?.Invoke(station, track);
            CoinsChanged?.Invoke();
            return true;
        }

        public int ManagerCost(int station) => Def.stations[station].managerGemCost;

        public bool CanHireManager(int station) =>
            !State.stations[station].hasManager && State.gems >= ManagerCost(station);

        public bool HireManager(int station)
        {
            if (!CanHireManager(station))
                return false;
            State.gems -= ManagerCost(station);
            var st = State.stations[station];
            st.hasManager = true;
            st.queuedStarts = 0;
            ManagerHired?.Invoke(station);
            CoinsChanged?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ Goals

        /// <summary>How far along a goal is, clamped to its target.</summary>
        public double GoalProgress(int goal)
        {
            var g = Def.goals[goal];
            int s = Math.Clamp(g.station, 0, State.stations.Count - 1);
            double value = g.kind switch
            {
                GoalKind.StationLevel => State.stations[s].TotalLevels,
                GoalKind.ServeCustomers => State.customersServed,
                GoalKind.HireManager => State.stations[s].hasManager ? 1 : 0,
                GoalKind.Renovations => State.board?.renovationsDone?.Count ?? 0,
                GoalKind.AddMachines => State.stations[s].machineLevel,
                GoalKind.LifetimeCoins => State.lifetimeCoins,
                _ => ShopLevel,
            };
            return Math.Min(value, GoalTarget(goal));
        }

        public double GoalTarget(int goal) => Def.goals[goal].kind == GoalKind.HireManager ? 1 : Def.goals[goal].target;

        public bool IsGoalComplete(int goal) => GoalProgress(goal) >= GoalTarget(goal);

        public bool IsGoalClaimed(int goal) => State.goalsClaimed.Contains(goal);

        /// <summary>The first <paramref name="count"/> goals not yet claimed, in list order.</summary>
        public List<int> ActiveGoals(int count)
        {
            var list = new List<int>(count);
            for (int i = 0; i < Def.goals.Count && list.Count < count; i++)
                if (!IsGoalClaimed(i))
                    list.Add(i);
            return list;
        }

        /// <summary>Collects a finished goal's reward. Returns false if it is not finished or already claimed.</summary>
        public bool ClaimGoal(int goal)
        {
            if (goal < 0 || goal >= Def.goals.Count || IsGoalClaimed(goal) || !IsGoalComplete(goal))
                return false;
            var g = Def.goals[goal];
            State.goalsClaimed.Add(goal);
            State.gems += g.rewardGems;
            if (g.rewardCoins > 0)
                AddCoins(g.rewardCoins);
            else
                CoinsChanged?.Invoke();
            GoalClaimed?.Invoke(goal);
            return true;
        }

        public void AddCoins(double amount)
        {
            State.coins += amount;
            State.lifetimeCoins += amount;
            CoinsChanged?.Invoke();
        }

        // ------------------------------------------------------------------ Simulation

        public void Tick(float dt)
        {
            Board?.Tick(dt);
            while (dt > 0f)
            {
                float step = Math.Min(dt, MaxStep);
                Step(step);
                dt -= step;
            }
        }

        void Step(float dt)
        {
            for (int i = 0; i < Def.stations.Count; i++)
                StepStation(i, dt);
            for (int i = 0; i < Def.couriers.Count; i++)
                StepCourier(i, dt);
            StepCustomers(dt);
        }

        void StepStation(int index, float dt)
        {
            var st = State.stations[index];
            float cycle = CycleSeconds(index);
            for (int m = 0; m < st.machines.Count; m++)
            {
                var machine = st.machines[m];
                if (!machine.running)
                    continue;
                machine.progress += dt / cycle;
                if (machine.progress >= 1f)
                    CompleteCycle(index, m);
            }
            TryStartCycles(index);
        }

        void TryStartCycles(int index)
        {
            var st = State.stations[index];
            var kind = Def.stations[index].kind;
            int m;
            while ((m = FreeMachine(index)) >= 0 && CanStartCycle(index))
            {
                if (kind != StationKind.Seller && !st.hasManager)
                {
                    if (st.queuedStarts <= 0)
                        break;
                    st.queuedStarts--;
                }
                StartCycle(index, m);
            }
        }

        int FreeMachine(int index)
        {
            var machines = State.stations[index].machines;
            for (int m = 0; m < machines.Count; m++)
                if (!machines[m].running)
                    return m;
            return -1;
        }

        int InProgress(int index)
        {
            int total = 0;
            foreach (var machine in State.stations[index].machines)
                if (machine.running) total += machine.batch;
            return total;
        }

        bool CanStartCycle(int index)
        {
            var def = Def.stations[index];
            var st = State.stations[index];
            switch (def.kind)
            {
                case StationKind.Source:
                    return st.output + InProgress(index) + Batch(index) <= def.outputCapacity;
                case StationKind.Processor:
                {
                    int amount = Math.Min(st.input, Batch(index));
                    return amount > 0 && st.output + InProgress(index) + amount <= def.outputCapacity;
                }
                default:
                    return st.input >= Def.itemsPerOrder && NextWaitingCustomer() != null;
            }
        }

        void StartCycle(int index, int m)
        {
            var def = Def.stations[index];
            var st = State.stations[index];
            var machine = st.machines[m];
            switch (def.kind)
            {
                case StationKind.Source:
                    machine.batch = Batch(index);
                    break;
                case StationKind.Processor:
                    machine.batch = Math.Min(st.input, Batch(index));
                    st.input -= machine.batch;
                    break;
                default:
                    var customer = NextWaitingCustomer();
                    customer.servedBy = m;
                    machine.batch = customer.items;
                    st.input -= customer.items;
                    break;
            }
            machine.running = true;
            machine.progress = 0f;
            CycleStarted?.Invoke(index, m);
        }

        void CompleteCycle(int index, int m)
        {
            var def = Def.stations[index];
            var st = State.stations[index];
            var machine = st.machines[m];
            int amount = machine.batch;
            machine.running = false;
            machine.progress = 0f;
            machine.batch = 0;

            if (def.kind == StationKind.Seller)
            {
                int qi = State.queue.FindIndex(c => c.servedBy == m);
                if (qi >= 0)
                {
                    var customer = State.queue[qi];
                    RemoveCustomerAt(qi);
                    double coins = customer.items * PricePerItem;
                    State.customersServed++;
                    AddCoins(coins);
                    CustomerServed?.Invoke(customer, coins);
                }
            }
            else
            {
                st.output += amount;
            }
            CycleCompleted?.Invoke(index, m, amount);
        }

        void StepCourier(int link, float dt)
        {
            var courier = State.couriers[link];
            var def = Def.couriers[link];
            var from = State.stations[link];
            var to = State.stations[link + 1];
            var toDef = Def.stations[link + 1];

            switch (courier.phase)
            {
                case CourierPhase.Waiting:
                {
                    int room = toDef.inputCapacity - to.input;
                    int amount = Math.Min(def.carry, Math.Min(from.output, room));
                    if (amount <= 0)
                        return;
                    from.output -= amount;
                    courier.carrying = amount;
                    courier.phase = CourierPhase.CarryingThere;
                    courier.progress = 0f;
                    CourierPickedUp?.Invoke(link, amount);
                    break;
                }
                case CourierPhase.CarryingThere:
                    courier.progress += dt / def.walkSeconds;
                    if (courier.progress >= 1f)
                    {
                        int delivered = courier.carrying;
                        to.input += delivered;
                        courier.carrying = 0;
                        courier.phase = CourierPhase.WalkingBack;
                        courier.progress = 0f;
                        CourierDelivered?.Invoke(link, delivered);
                    }
                    break;
                case CourierPhase.WalkingBack:
                    courier.progress += dt / def.walkSeconds;
                    if (courier.progress >= 1f)
                    {
                        courier.phase = CourierPhase.Waiting;
                        courier.progress = 0f;
                    }
                    break;
            }
        }

        void StepCustomers(float dt)
        {
            float interval = CustomerInterval;
            State.customerTimer += dt;
            if (State.customerTimer >= interval)
            {
                if (State.queue.Count < Def.maxQueue)
                {
                    State.customerTimer -= interval;
                    var customer = new CustomerState
                    {
                        id = _nextCustomerId++,
                        items = Def.itemsPerOrder,
                        patienceLeft = Def.patienceSeconds,
                        walkLeft = Def.walkInSeconds,
                    };
                    State.queue.Add(customer);
                    CustomerArrived?.Invoke(customer);
                }
                else
                {
                    State.customerTimer = interval;
                }
            }

            for (int i = State.queue.Count - 1; i >= 0; i--)
            {
                var customer = State.queue[i];
                if (customer.servedBy >= 0)
                    continue;
                // Still walking to their place in line: not servable, and not impatient yet.
                if (customer.walkLeft > 0f)
                {
                    customer.walkLeft = Math.Max(0f, customer.walkLeft - dt);
                    continue;
                }
                customer.patienceLeft -= dt * PatienceScale;
                if (customer.patienceLeft <= 0f)
                {
                    RemoveCustomerAt(i);
                    CustomerLeft?.Invoke(customer);
                }
            }
        }

        // The first customer in line who is not being served yet, once they reach the counter.
        // Nobody behind them can skip ahead.
        CustomerState NextWaitingCustomer()
        {
            foreach (var c in State.queue)
                if (c.servedBy < 0)
                    return c.walkLeft <= 0f ? c : null;
            return null;
        }

        // Everyone behind a leaving customer has to step up one place before being servable.
        void RemoveCustomerAt(int index)
        {
            State.queue.RemoveAt(index);
            for (int i = index; i < State.queue.Count; i++)
            {
                var c = State.queue[i];
                if (c.servedBy < 0)
                    c.walkLeft = Math.Max(c.walkLeft, Def.stepUpSeconds);
            }
        }

        void CheckGemMilestones(int station)
        {
            if (Def.gemMilestoneEvery <= 0)
                return;
            var st = State.stations[station];
            int reached = st.TotalLevels / Def.gemMilestoneEvery;
            while (st.gemMilestonesClaimed < reached)
            {
                st.gemMilestonesClaimed++;
                State.gems += Def.gemMilestoneReward;
                GemsEarned?.Invoke(station, Def.gemMilestoneReward);
            }
        }

        // ------------------------------------------------------------------ Offline

        /// <summary>
        /// Coins per second the shop makes when nobody is tapping: zero unless every station
        /// is automated, otherwise the slowest link of the chain (or customer demand).
        /// </summary>
        public double IdleCoinsPerSecond()
        {
            double itemsPerSecond = double.PositiveInfinity;
            for (int i = 0; i < Def.stations.Count; i++)
            {
                if (!IsAutomated(i))
                    return 0;
                double perCycle = Def.stations[i].kind == StationKind.Seller ? Def.itemsPerOrder : Batch(i);
                itemsPerSecond = Math.Min(itemsPerSecond, Machines(i) * perCycle / CycleSeconds(i));
            }
            foreach (var c in Def.couriers)
                itemsPerSecond = Math.Min(itemsPerSecond, c.carry / (2.0 * c.walkSeconds));
            itemsPerSecond = Math.Min(itemsPerSecond, Def.itemsPerOrder / CustomerInterval);
            return itemsPerSecond * PricePerItem;
        }

        /// <summary>Coins earned while away for the given time, after the cap and efficiency.</summary>
        public double OfflineEarnings(double secondsAway)
        {
            if (secondsAway <= 0)
                return 0;
            double capped = Math.Min(secondsAway, Def.offlineCapHours * 3600.0);
            return Math.Floor(IdleCoinsPerSecond() * capped * Def.offlineEfficiency);
        }
    }
}
