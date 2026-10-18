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

    /// <summary>
    /// The merge board: generators spend energy to make items, two equal items merge into the
    /// next tier, and finished items are handed in to renovate the shop (raising coziness,
    /// which raises prices). Pure logic; the board view only reads it and calls its actions.
    /// </summary>
    public sealed class MergeBoard
    {
        readonly ShopSim _shop;
        readonly Random _random;

        public MergeBoardDefinition Def { get; }
        public BoardState State { get; }

        public event Action<int, int, BoardItem> ItemSpawned;    // generator cell, target cell, item
        public event Action<int, BoardItem> ItemsMerged;         // cell, new item
        public event Action<int> RenovationDone;                 // renovation index
        public event Action Changed;

        public MergeBoard(ShopSim shop, MergeBoardDefinition def, BoardState state)
        {
            _shop = shop;
            Def = def;
            State = state;
            if (State.cells == null || State.cells.Count != def.Cells)
                Reset();
            State.renovationsDone ??= new List<int>();
            _random = new Random(State.seed == 0 ? 12345 : State.seed);
        }

        void Reset()
        {
            State.cells = new List<int>(Def.Cells);
            for (int i = 0; i < Def.Cells; i++)
                State.cells.Add(BoardItem.Empty.code);
            foreach (var s in Def.start)
            {
                if (s.cell < 0 || s.cell >= Def.Cells)
                    continue;
                State.cells[s.cell] = (s.generator >= 0 ? BoardItem.Generator(s.generator) : BoardItem.Item(s.chain, s.tier)).code;
            }
            State.energy = Def.startEnergy >= 0 ? Def.startEnergy : Def.energyMax;
            State.seed = 20261017;
        }

        // ------------------------------------------------------------------ Reading

        public BoardItem this[int cell] => BoardItem.FromCode(State.cells[cell]);

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

        public int EmptyCells
        {
            get
            {
                int n = 0;
                foreach (int c in State.cells) if (c < 0) n++;
                return n;
            }
        }

        public int Count(ItemRef item)
        {
            int code = BoardItem.Item(item.chain, item.tier).code, n = 0;
            foreach (int c in State.cells) if (c == code) n++;
            return n;
        }

        // ------------------------------------------------------------------ Actions

        /// <summary>Tap a generator: spends one energy and drops an item in the nearest free cell.</summary>
        public bool Spawn(int cell, out int target)
        {
            target = -1;
            var gen = this[cell];
            if (!gen.IsGenerator || State.energy <= 0)
                return false;
            target = NearestEmpty(cell);
            if (target < 0)
                return false;
            var def = Def.generators[gen.GeneratorIndex];
            int tier = _random.NextDouble() < def.luckyChance && MaxTier(def.chain) >= 1 ? 1 : 0;
            var item = BoardItem.Item(def.chain, tier);
            State.cells[target] = item.code;
            State.energy--;
            State.seed = _random.Next(1, int.MaxValue);
            ItemSpawned?.Invoke(cell, target, item);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Drag from one cell onto another: merge equal items, else move or swap.</summary>
        public MoveResult Move(int from, int to)
        {
            if (from == to || from < 0 || to < 0 || from >= Def.Cells || to >= Def.Cells)
                return MoveResult.None;
            var a = this[from];
            var b = this[to];
            if (a.IsEmpty)
                return MoveResult.None;

            MoveResult result;
            if (b.IsEmpty)
            {
                State.cells[to] = a.code;
                State.cells[from] = BoardItem.Empty.code;
                result = MoveResult.Moved;
            }
            else if (a == b && a.IsItem && a.Tier < MaxTier(a.Chain))
            {
                var merged = BoardItem.Item(a.Chain, a.Tier + 1);
                State.cells[to] = merged.code;
                State.cells[from] = BoardItem.Empty.code;
                ItemsMerged?.Invoke(to, merged);
                result = MoveResult.Merged;
            }
            else
            {
                State.cells[to] = a.code;
                State.cells[from] = b.code;
                result = MoveResult.Swapped;
            }
            Changed?.Invoke();
            return result;
        }

        /// <summary>Sells an item for coins (frees space when the board is full).</summary>
        public bool Sell(int cell)
        {
            var item = this[cell];
            if (!item.IsItem)
                return false;
            State.cells[cell] = BoardItem.Empty.code;
            _shop.AddCoins(SellValue(item));
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ Renovations

        public bool IsRenovated(int renovation) => State.renovationsDone.Contains(renovation);

        /// <summary>The first <paramref name="count"/> renovations not done yet, in order.</summary>
        public List<int> ActiveRenovations(int count)
        {
            var list = new List<int>(count);
            for (int i = 0; i < Def.renovations.Count && list.Count < count; i++)
                if (!IsRenovated(i))
                    list.Add(i);
            return list;
        }

        /// <summary>How many of each needed item are on the board, capped at the number needed.</summary>
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

        public bool CanRenovate(int renovation) =>
            renovation >= 0 && renovation < Def.renovations.Count && !IsRenovated(renovation) && HasAll(renovation);

        /// <summary>Hands in the needed items and fixes the spot: coins plus coziness.</summary>
        public bool Renovate(int renovation)
        {
            if (!CanRenovate(renovation))
                return false;
            var def = Def.renovations[renovation];
            foreach (var n in def.needs)
            {
                int code = BoardItem.Item(n.chain, n.tier).code;
                int at = State.cells.IndexOf(code);
                State.cells[at] = BoardItem.Empty.code;
            }
            State.renovationsDone.Add(renovation);
            if (def.rewardCoins > 0)
                _shop.AddCoins(def.rewardCoins);
            RenovationDone?.Invoke(renovation);
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ Time

        public void Tick(double dt)
        {
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

        // Closest free cell to `cell` (grid distance), so items land next to their generator.
        int NearestEmpty(int cell)
        {
            int cx = cell % Def.columns, cy = cell / Def.columns;
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Def.Cells; i++)
            {
                if (State.cells[i] >= 0)
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
