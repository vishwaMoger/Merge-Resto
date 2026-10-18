using System;
using System.Collections.Generic;
using UnityEngine;

namespace MergeResto.Sim
{
    /// <summary>A line of items that merge into each other: two of tier n make one of tier n+1.</summary>
    [Serializable]
    public class MergeChainDefinition
    {
        public string id = "chain";
        public string displayName = "Chain";
        [Tooltip("Item names from the lowest tier up; the count is the chain's length.")]
        public List<string> tiers = new();
        [Tooltip("Model shown for each tier (art only).")]
        public List<string> models = new();

        public int MaxTier => tiers.Count - 1;
    }

    /// <summary>
    /// A board item that makes tier-0 items of one chain when tapped, for one energy and one of
    /// its own charges. Charges come back one at a time.
    /// </summary>
    [Serializable]
    public class GeneratorDefinition
    {
        public string id = "generator";
        public string displayName = "Generator";
        public int chain;
        [Range(0f, 1f), Tooltip("Chance that a tap makes a tier-1 item instead.")]
        public float luckyChance = 0.12f;
        [Tooltip("Items it can make before it needs to recharge (0: no limit, energy only).")]
        public int capacity;
        [Tooltip("Seconds for one charge to come back.")]
        public float rechargeSeconds = 20f;
        [Tooltip("Model shown on the board (art only).")]
        public string model;
    }

    [Serializable]
    public struct ItemRef
    {
        public int chain;
        public int tier;

        public ItemRef(int chain, int tier)
        {
            this.chain = chain;
            this.tier = tier;
        }
    }

    /// <summary>A part of the shop (the counter, the dining room...) whose jobs are done in order.</summary>
    [Serializable]
    public class RenovationArea
    {
        public string id = "area";
        public string displayName = "Area";
        [Tooltip("Gems for finishing every job in the area.")]
        public int rewardGems = 10;
    }

    /// <summary>One job in an area: a run-down spot in the shop, fixed by handing in merged items.</summary>
    [Serializable]
    public class RenovationDefinition
    {
        public string id = "renovation";
        public string displayName = "Renovation";
        [Tooltip("Index into the board's areas.")]
        public int area;
        public List<ItemRef> needs = new();
        public double rewardCoins = 100;
        [Tooltip("Added to the shop's coziness; every 1.0 doubles item prices.")]
        public float coziness = 0.05f;
    }

    /// <summary>How a cell starts: open, its item wrapped up, or hidden in a box.</summary>
    public enum CellCover
    {
        Open = 0,
        Wrapped = 1,
        Boxed = 2,
    }

    [Serializable]
    public struct BoardStartItem
    {
        public int cell;
        [Tooltip("Generator index, or -1 for a plain item.")]
        public int generator;
        public int chain;
        public int tier;
        [Tooltip("Boxed items show up wrapped when the box opens; wrapped ones are freed by merging a match onto them.")]
        public CellCover cover;
    }

    [Serializable]
    public class MergeBoardDefinition
    {
        public int columns = 7;
        public int rows = 5;
        public int energyMax = 100;
        [Tooltip("Energy on a brand-new board (-1: full).")]
        public int startEnergy = -1;
        [Tooltip("Seconds per energy point regained.")]
        public float energyRegenSeconds = 6f;
        [Tooltip("Coins for selling a tier-0 item; each tier doubles it.")]
        public double sellBase = 5;
        [Tooltip("Cells open from the start. Every other cell without a start item is a box. Empty: all open.")]
        public List<int> openCells = new();
        [Tooltip("Gems to unwrap a wrapped item without a match (plus one per tier).")]
        public int unwrapGems = 2;

        public List<RenovationArea> areas = new();
        public List<MergeChainDefinition> chains = new();
        public List<GeneratorDefinition> generators = new();
        public List<RenovationDefinition> renovations = new();
        public List<BoardStartItem> start = new();

        public int Cells => columns * rows;
    }
}
