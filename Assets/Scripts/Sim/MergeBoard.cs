using System;
using System.Collections.Generic;

namespace MergeResto.Sim
{
    /// <summary>What sits in one board cell. Empty, an item (chain + tier), or a generator.</summary>
    public readonly struct BoardItem : IEquatable<BoardItem>
    {
        // Saved as one int: -1 empty, 0..9999 item (chain * 100 + tier), 10000+ generator.
        const int GeneratorBase = 10000;
        public readonly int code;

        BoardItem(int code) => this.code = code;

        public static readonly BoardItem Empty = new(-1);
        public static BoardItem Item(int chain, int tier) => new(chain * 100 + tier);
        public static BoardItem Generator(int index) => new(GeneratorBase + index);
        public static BoardItem FromCode(int code) => new(code);

        public bool IsEmpty => code < 0;
        public bool IsGenerator => code >= GeneratorBase;
        public bool IsItem => code >= 0 && code < GeneratorBase;
        public int Chain => IsItem ? code / 100 : -1;
        public int Tier => IsItem ? code % 100 : -1;
        public int GeneratorIndex => IsGenerator ? code - GeneratorBase : -1;

        public bool Equals(BoardItem other) => code == other.code;
        public override bool Equals(object obj) => obj is BoardItem other && Equals(other);
        public override int GetHashCode() => code;
        public static bool operator ==(BoardItem a, BoardItem b) => a.code == b.code;
        public static bool operator !=(BoardItem a, BoardItem b) => a.code != b.code;
    }

    public enum MoveResult
    {
        None,
        Moved,
        Swapped,
        Merged,
    }

    public enum SpawnFail
    {
        None,
        NotAGenerator,
        NoEnergy,
        Recharging,
        BoardFull,
    }

    /// <summary>
    /// The merge board: generators spend energy and their own charges to make items, two equal
    /// items merge into the next tier, and finished items are handed in for renovation jobs,
    /// grouped by area (raising coziness, which raises prices). Most of the board starts boxed
    /// up: a merge next to a box opens it, showing a wrapped item that a matching item frees.
    /// Pure logic; the board view only reads it and calls its actions.
    /// </summary>
    public sealed class MergeBoard
    {
        readonly ShopSim _shop;
        readonly Random _random;

        public MergeBoardDefinition Def { get; }
        public BoardState State { get; }

        public event Action<int, int, BoardItem> ItemSpawned;    // generator cell, target cell, item
        public event Action<int, BoardItem> ItemsMerged;         // cell, new item
        public event Action<int, CellCover> CellOpened;          // cell, what it is now (Open or Wrapped)
        public event Action<int> RenovationDone;                 // renovation index
        public event Action<int> AreaDone;                       // area index
        public event Action Changed;

        public MergeBoard(ShopSim shop, MergeBoardDefinition def, BoardState state)
        {
            _shop = shop;
            Def = def;
            State = state;
            if (State.cells == null || State.cells.Count != def.Cells)
                Reset();
            if (State.covers == null || State.covers.Count != def.Cells)
            {
                State.covers = new List<int>(def.Cells);
                for (int i = 0; i < def.Cells; i++)
                    State.covers.Add(0);
            }
            State.renovationsDone ??= new List<int>();
            State.areasDone ??= new List<int>();
            State.genCharges ??= new List<int>();
            State.genTimers ??= new List<float>();
            while (State.genCharges.Count < def.generators.Count)
                State.genCharges.Add(def.generators[State.genCharges.Count].capacity);
            while (State.genTimers.Count < def.generators.Count)
                State.genTimers.Add(0f);
            _random = new Random(State.seed == 0 ? 12345 : State.seed);
        }

        void Reset()
        {
            State.cells = new List<int>(Def.Cells);
            State.covers = new List<int>(Def.Cells);
            bool boxRest = Def.openCells != null && Def.openCells.Count > 0;
            for (int i = 0; i < Def.Cells; i++)
            {
                State.cells.Add(BoardItem.Empty.code);
                State.covers.Add(boxRest && !Def.openCells.Contains(i) ? (int)CellCover.Boxed : (int)CellCover.Open);
            }
            foreach (var s in Def.start)
            {
                if (s.cell < 0 || s.cell >= Def.Cells)
                    continue;
                State.cells[s.cell] = (s.generator >= 0 ? BoardItem.Generator(s.generator) : BoardItem.Item(s.chain, s.tier)).code;
                State.covers[s.cell] = (int)s.cover;
            }
            State.energy = Def.startEnergy >= 0 ? Def.startEnergy : Def.energyMax;
            State.genCharges = new List<int>();
            State.genTimers = new List<float>();
            State.seed = 20261017;
        }

        // ------------------------------------------------------------------ Reading

        public BoardItem this[int cell] => BoardItem.FromCode(State.cells[cell]);

        public CellCover CoverOf(int cell) => (CellCover)State.covers[cell];
        public bool IsOpen(int cell) => State.covers[cell] == (int)CellCover.Open;

        /// <summary>The item in an open cell, or Empty for empty, boxed or wrapped cells.</summary>
        public BoardItem FreeItem(int cell) => IsOpen(cell) ? this[cell] : BoardItem.Empty;

        public int Energy => State.energy;

        public float Coziness
        {
            get
            {
                float total = 0f;
                foreach (int r in State.renovationsDone)
                    if (r >= 0 && r < Def.renovations.Count)
                        total += Def.renovations[r].coziness;
                return total;
            }
        }

        public int MaxTier(int chain) => Def.chains[chain].MaxTier;

        public string NameOf(BoardItem item) =>
            item.IsGenerator ? Def.generators[item.GeneratorIndex].displayName
            : item.IsItem ? Def.chains[item.Chain].tiers[item.Tier]
            : "";

        public double SellValue(BoardItem item) => item.IsItem ? Def.sellBase * Math.Pow(2, item.Tier) : 0;

        public int UnwrapCost(int cell) => Def.unwrapGems + Math.Max(0, this[cell].Tier);

        /// <summary>Open cells with nothing in them.</summary>
        public int EmptyCells
        {
            get
            {
                int n = 0;
                for (int i = 0; i < State.cells.Count; i++)
                    if (State.cells[i] < 0 && State.covers[i] == 0) n++;
                return n;
            }
        }

        public int BoxedCells
        {
            get
            {
                int n = 0;
                foreach (int c in State.covers) if (c == (int)CellCover.Boxed) n++;
                return n;
            }
        }

        /// <summary>How many of an item are free to use (wrapped and boxed ones don't count).</summary>
        public int Count(ItemRef item)
        {
            int code = BoardItem.Item(item.chain, item.tier).code, n = 0;
            for (int i = 0; i < State.cells.Count; i++)
                if (State.cells[i] == code && State.covers[i] == 0) n++;
            return n;
        }

        /// <summary>True when dropping the item in <paramref name="from"/> on <paramref name="to"/> merges them.</summary>
        public bool CanMerge(int from, int to)
        {
            if (from == to || from < 0 || to < 0 || from >= Def.Cells || to >= Def.Cells || !IsOpen(from))
                return false;
            if (CoverOf(to) == CellCover.Boxed)
                return false;
            var a = this[from];
            var b = this[to];
            return a.IsItem && a == b && a.Tier < MaxTier(a.Chain);
        }

        // ------------------------------------------------------------------ Generators

        public int Capacity(int generator) => Def.generators[generator].capacity;

        /// <summary>Charges left (int.MaxValue for generators without a limit).</summary>
        public int Charges(int generator) => Capacity(generator) <= 0 ? int.MaxValue : State.genCharges[generator];

        /// <summary>Seconds until the next charge comes back, or 0 when full.</summary>
        public float NextChargeIn(int generator)
        {
            var g = Def.generators[generator];
            return g.capacity <= 0 || State.genCharges[generator] >= g.capacity ? 0f : g.rechargeSeconds - State.genTimers[generator];
        }

        /// <summary>Why tapping this cell would not make an item right now (None: it would).</summary>
        public SpawnFail CheckSpawn(int cell)
        {
            var gen = this[cell];
            if (!gen.IsGenerator || !IsOpen(cell))
                return SpawnFail.NotAGenerator;
            if (State.energy <= 0)
                return SpawnFail.NoEnergy;
            if (Charges(gen.GeneratorIndex) <= 0)
                return SpawnFail.Recharging;
            if (NearestEmpty(cell) < 0)
                return SpawnFail.BoardFull;
            return SpawnFail.None;
        }

        // ------------------------------------------------------------------ Actions

        /// <summary>Tap a generator: spends one energy and one charge, and drops an item in the nearest free cell.</summary>
        public bool Spawn(int cell, out int target)
        {
            target = -1;
            if (CheckSpawn(cell) != SpawnFail.None)
                return false;
            int g = this[cell].GeneratorIndex;
            var def = Def.generators[g];
            target = NearestEmpty(cell);
            int tier = _random.NextDouble() < def.luckyChance && MaxTier(def.chain) >= 1 ? 1 : 0;
            var item = BoardItem.Item(def.chain, tier);
            State.cells[target] = item.code;
            State.energy--;
            if (def.capacity > 0)
                State.genCharges[g]--;
            State.seed = _random.Next(1, int.MaxValue);
            ItemSpawned?.Invoke(cell, target, item);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Drag from one cell onto another: merge equal items (a wrapped target is freed by the
        /// merge), else move into an empty cell or swap. Boxed and wrapped cells don't move.
        /// </summary>
        public MoveResult Move(int from, int to)
        {
            if (from == to || from < 0 || to < 0 || from >= Def.Cells || to >= Def.Cells)
                return MoveResult.None;
            if (!IsOpen(from) || this[from].IsEmpty)
                return MoveResult.None;
            var a = this[from];
            var b = this[to];

            if (CanMerge(from, to))
            {
                var merged = BoardItem.Item(a.Chain, a.Tier + 1);
                State.cells[to] = merged.code;
                State.covers[to] = (int)CellCover.Open;
                State.cells[from] = BoardItem.Empty.code;
                ItemsMerged?.Invoke(to, merged);
                OpenAround(to);
                Changed?.Invoke();
                return MoveResult.Merged;
            }
            if (!IsOpen(to))
                return MoveResult.None;
            if (b.IsEmpty)
            {
                State.cells[to] = a.code;
                State.cells[from] = BoardItem.Empty.code;
                Changed?.Invoke();
                return MoveResult.Moved;
            }
            State.cells[to] = a.code;
            State.cells[from] = b.code;
            Changed?.Invoke();
            return MoveResult.Swapped;
        }

        // A merge pops the boxes next to it: empty ones open, ones holding an item show it wrapped
        // (generators come out ready to use).
        void OpenAround(int cell)
        {
            int cx = cell % Def.columns, cy = cell / Def.columns;
            void Try(int x, int y)
            {
                if (x < 0 || y < 0 || x >= Def.columns || y >= Def.rows)
                    return;
                int i = y * Def.columns + x;
                if (State.covers[i] != (int)CellCover.Boxed)
                    return;
                var item = this[i];
                var now = item.IsItem ? CellCover.Wrapped : CellCover.Open;
                State.covers[i] = (int)now;
                CellOpened?.Invoke(i, now);
            }
            Try(cx - 1, cy);
            Try(cx + 1, cy);
            Try(cx, cy - 1);
            Try(cx, cy + 1);
        }

        /// <summary>Frees a wrapped item for gems, without a match.</summary>
        public bool Unwrap(int cell)
        {
            if (CoverOf(cell) != CellCover.Wrapped)
                return false;
            int cost = UnwrapCost(cell);
            if (_shop.State.gems < cost)
                return false;
            _shop.State.gems -= cost;
            State.covers[cell] = (int)CellCover.Open;
            CellOpened?.Invoke(cell, CellCover.Open);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Sells a free item for coins (frees space when the board is full).</summary>
        public bool Sell(int cell)
        {
            var item = FreeItem(cell);
            if (!item.IsItem)
                return false;
            State.cells[cell] = BoardItem.Empty.code;
            _shop.AddCoins(SellValue(item));
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ Renovation jobs

        public bool IsRenovated(int renovation) => State.renovationsDone.Contains(renovation);

        public int AreaOf(int renovation) => Def.renovations[renovation].area;

        /// <summary>The area being worked on: the one holding the first unfinished job (-1: all done).</summary>
        public int CurrentArea
        {
            get
            {
                for (int i = 0; i < Def.renovations.Count; i++)
                    if (!IsRenovated(i))
                        return AreaOf(i);
                return -1;
            }
        }

        public string AreaName(int area) =>
            area >= 0 && area < Def.areas.Count ? Def.areas[area].displayName : "";

        /// <summary>Jobs done and in total for an area.</summary>
        public (int done, int total) AreaProgress(int area)
        {
            int done = 0, total = 0;
            for (int i = 0; i < Def.renovations.Count; i++)
                if (AreaOf(i) == area)
                {
                    total++;
                    if (IsRenovated(i)) done++;
                }
            return (done, total);
        }

        public bool IsAreaDone(int area)
        {
            var (done, total) = AreaProgress(area);
            return total > 0 && done >= total;
        }

        /// <summary>The next unfinished jobs of the current area, in order (at most <paramref name="count"/>).</summary>
        public List<int> ActiveRenovations(int count)
        {
            var list = new List<int>(count);
            int area = CurrentArea;
            for (int i = 0; i < Def.renovations.Count && list.Count < count; i++)
                if (!IsRenovated(i) && AreaOf(i) == area)
                    list.Add(i);
            return list;
        }

        /// <summary>Whether every item the job needs is free on the board.</summary>
        public bool HasAll(int renovation)
        {
            var needed = new Dictionary<int, int>();
            foreach (var n in Def.renovations[renovation].needs)
            {
                int code = BoardItem.Item(n.chain, n.tier).code;
                needed[code] = needed.TryGetValue(code, out int c) ? c + 1 : 1;
            }
            foreach (var kv in needed)
                if (Count(new ItemRef(kv.Key / 100, kv.Key % 100)) < kv.Value)
                    return false;
            return true;
        }

        /// <summary>Whether the job can be done now: it belongs to the current area and every item is there.</summary>
        public bool CanRenovate(int renovation) =>
            renovation >= 0 && renovation < Def.renovations.Count && !IsRenovated(renovation)
            && AreaOf(renovation) == CurrentArea && HasAll(renovation);

        /// <summary>Hands in the needed items and does the job: coins plus coziness (and the area's gems when it was the last one).</summary>
        public bool Renovate(int renovation)
        {
            if (!CanRenovate(renovation))
                return false;
            var def = Def.renovations[renovation];
            foreach (var n in def.needs)
            {
                int code = BoardItem.Item(n.chain, n.tier).code;
                for (int i = 0; i < State.cells.Count; i++)
                    if (State.cells[i] == code && State.covers[i] == 0)
                    {
                        State.cells[i] = BoardItem.Empty.code;
                        break;
                    }
            }
            State.renovationsDone.Add(renovation);
            if (def.rewardCoins > 0)
                _shop.AddCoins(def.rewardCoins);
            RenovationDone?.Invoke(renovation);
            int area = def.area;
            if (IsAreaDone(area) && !State.areasDone.Contains(area))
            {
                State.areasDone.Add(area);
                if (area < Def.areas.Count)
                    _shop.State.gems += Def.areas[area].rewardGems;
                AreaDone?.Invoke(area);
            }
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ Time

        public void Tick(double dt)
        {
            for (int g = 0; g < Def.generators.Count; g++)
            {
                var def = Def.generators[g];
                if (def.capacity <= 0 || State.genCharges[g] >= def.capacity)
                {
                    State.genTimers[g] = 0f;
                    continue;
                }
                State.genTimers[g] += (float)dt;
                float step = Math.Max(0.01f, def.rechargeSeconds);
                while (State.genTimers[g] >= step && State.genCharges[g] < def.capacity)
                {
                    State.genTimers[g] -= step;
                    State.genCharges[g]++;
                }
                if (State.genCharges[g] >= def.capacity)
                    State.genTimers[g] = 0f;
            }

            if (State.energy >= Def.energyMax)
            {
                State.energyTimer = 0f;
                return;
            }
            State.energyTimer += (float)dt;
            while (State.energyTimer >= Def.energyRegenSeconds && State.energy < Def.energyMax)
            {
                State.energyTimer -= Def.energyRegenSeconds;
                State.energy++;
            }
            if (State.energy >= Def.energyMax)
                State.energyTimer = 0f;
        }

        /// <summary>Adds energy (rewards); it may go above the maximum, regeneration then waits.</summary>
        public void AddEnergy(int amount)
        {
            if (amount <= 0)
                return;
            State.energy += amount;
            Changed?.Invoke();
        }

        /// <summary>Seconds until the next energy point, or 0 when full.</summary>
        public float NextEnergyIn => State.energy >= Def.energyMax ? 0f : Def.energyRegenSeconds - State.energyTimer;

        // Closest open, empty cell to `cell` (grid distance), so items land next to their generator.
        int NearestEmpty(int cell)
        {
            int cx = cell % Def.columns, cy = cell / Def.columns;
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Def.Cells; i++)
            {
                if (State.cells[i] >= 0 || State.covers[i] != 0)
                    continue;
                int dx = i % Def.columns - cx, dy = i / Def.columns - cy;
                float d = dx * dx + dy * dy + i * 0.0001f; // stable tie-break in reading order
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }
            return best;
        }
    }
}
