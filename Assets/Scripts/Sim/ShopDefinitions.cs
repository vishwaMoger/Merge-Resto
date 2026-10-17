using System;
using System.Collections.Generic;
using UnityEngine;

namespace MergeResto.Sim
{
    public enum StationKind
    {
        /// <summary>Produces goods from nothing (flour delivery).</summary>
        Source,
        /// <summary>Turns input goods into output goods (mixer, oven).</summary>
        Processor,
        /// <summary>Sells goods to queued customers (counter).</summary>
        Seller,
    }

    public enum UpgradeTrack
    {
        /// <summary>Shorter cycle time.</summary>
        Speed,
        /// <summary>More goods per cycle; for a Seller, a higher price per item.</summary>
        Output,
        /// <summary>One more machine (or cashier) working in parallel.</summary>
        Machines,
    }

    [Serializable]
    public class UpgradeDefinition
    {
        public UpgradeTrack track;
        public string displayName = "Upgrade";
        public int maxLevel = 10;
        public double baseCost = 50;
        public double costGrowth = 1.7;

        public double CostForLevel(int currentLevel) => Math.Round(baseCost * Math.Pow(costGrowth, currentLevel));
    }

    [Serializable]
    public class StationDefinition
    {
        public string id = "station";
        public string displayName = "Station";
        public StationKind kind = StationKind.Processor;

        [Tooltip("Seconds per cycle at speed level 0.")]
        public float baseCycleSeconds = 3f;
        [Tooltip("Goods produced per cycle at output level 0 (Source/Processor).")]
        public int baseBatch = 5;
        [Tooltip("Goods this station can hold waiting to be picked up.")]
        public int outputCapacity = 30;
        [Tooltip("Goods this station can hold waiting to be processed or sold.")]
        public int inputCapacity = 30;

        [Tooltip("Gems to hire a manager, who starts cycles automatically.")]
        public int managerGemCost = 20;
        [Tooltip("Sellers and some stations start automated (a cashier always works).")]
        public bool startsAutomated;

        public List<UpgradeDefinition> upgrades = new()
        {
            new UpgradeDefinition { track = UpgradeTrack.Speed, displayName = "Faster Hands", baseCost = 50 },
            new UpgradeDefinition { track = UpgradeTrack.Output, displayName = "Bigger Batches", baseCost = 75 },
            new UpgradeDefinition { track = UpgradeTrack.Machines, displayName = "Add Machine", baseCost = 400, costGrowth = 3, maxLevel = 2 },
        };

        public UpgradeDefinition Upgrade(UpgradeTrack track) => upgrades.Find(u => u.track == track);
    }

    public enum GoalKind
    {
        /// <summary>A station reaches a total upgrade level.</summary>
        StationLevel,
        /// <summary>Serve this many customers in total.</summary>
        ServeCustomers,
        /// <summary>Hire the manager of a station.</summary>
        HireManager,
        /// <summary>Finish this many renovations.</summary>
        Renovations,
        /// <summary>A station has this many extra machines.</summary>
        AddMachines,
        /// <summary>Earn this many coins in total.</summary>
        LifetimeCoins,
        /// <summary>The whole shop reaches this many upgrade levels.</summary>
        ShopLevel,
    }

    /// <summary>One milestone in the shop's goal list; a few are shown at a time, in order.</summary>
    [Serializable]
    public class GoalDefinition
    {
        public GoalKind kind;
        [Tooltip("Station index, for station goals.")]
        public int station;
        public double target = 1;
        public double rewardCoins;
        public int rewardGems;
    }

    [Serializable]
    public class CourierDefinition
    {
        [Tooltip("Goods carried per trip.")]
        public int carry = 5;
        [Tooltip("Seconds to walk one way between the two stations.")]
        public float walkSeconds = 1.5f;
    }

    [Serializable]
    public class ShopDefinition
    {
        public string id = "grandmas_bakery";
        public string displayName = "Grandma's Bakery";

        [Tooltip("Production chain in order. Station 0 must be a Source, the last a Seller.")]
        public List<StationDefinition> stations = new();
        [Tooltip("One courier per link: couriers[i] carries from station i to station i+1.")]
        public List<CourierDefinition> couriers = new();

        [Header("Customers")]
        public float customerIntervalSeconds = 4f;
        [Tooltip("Each upgrade level anywhere in the shop brings customers this much faster.")]
        public float intervalPerShopLevel = 0.94f;
        public float minCustomerIntervalSeconds = 0.8f;
        public int maxQueue = 6;
        public int itemsPerOrder = 2;
        public double pricePerItem = 25;
        [Tooltip("Seconds a customer waits in line before leaving (counted once they have arrived).")]
        public float patienceSeconds = 20f;
        [Tooltip("Seconds to walk from the door to the counter; nobody is served before arriving.")]
        public float walkInSeconds = 3f;
        [Tooltip("Seconds to step up one place in line when someone ahead leaves.")]
        public float stepUpSeconds = 0.5f;

        [Header("Economy")]
        public double startCoins = 500;
        public int startGems = 20;
        [Tooltip("Price multiplier per Seller output level.")]
        public double pricePerOutputLevel = 0.2;
        [Tooltip("Gems granted every this many upgrade levels on one station.")]
        public int gemMilestoneEvery = 5;
        public int gemMilestoneReward = 10;

        [Header("Merge board")]
        public MergeBoardDefinition board = new();

        [Header("Goals")]
        [Tooltip("Milestones offered after the tutorial, a few at a time, in this order.")]
        public List<GoalDefinition> goals = new();

        [Header("Offline")]
        public float offlineCapHours = 2f;
        [Range(0f, 1f)] public float offlineEfficiency = 0.5f;
    }
}
