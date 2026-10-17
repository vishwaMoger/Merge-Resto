using System;
using System.Collections.Generic;

namespace MergeResto.Sim
{
    /// <summary>Everything about a shop that is saved. Plain fields so JsonUtility can serialize it.</summary>
    [Serializable]
    public class ShopState
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string shopId;
        public double coins;
        public int gems;
        public double lifetimeCoins;
        public long lastSavedUnixSeconds;
        /// <summary>Grand Opening tutorial progress: index of the current step, or a large value once finished.</summary>
        public int tutorialStep;
        public int customersServed;
        /// <summary>Indices of goals whose reward has been collected.</summary>
        public List<int> goalsClaimed = new();
        public float customerTimer;
        public List<StationState> stations = new();
        public List<CourierState> couriers = new();
        public List<CustomerState> queue = new();
    }

    [Serializable]
    public class StationState
    {
        public int speedLevel;
        public int outputLevel;
        public int machineLevel;
        public bool hasManager;
        public int gemMilestonesClaimed;

        /// <summary>Goods waiting to be processed (Processor) or sold (Seller).</summary>
        public int input;
        /// <summary>Finished goods waiting for the courier.</summary>
        public int output;
        /// <summary>Taps waiting to start a cycle on a station without a manager.</summary>
        public int queuedStarts;
        public List<MachineState> machines = new();

        public int Level(UpgradeTrack track) => track switch
        {
            UpgradeTrack.Speed => speedLevel,
            UpgradeTrack.Output => outputLevel,
            _ => machineLevel,
        };

        public void SetLevel(UpgradeTrack track, int value)
        {
            switch (track)
            {
                case UpgradeTrack.Speed: speedLevel = value; break;
                case UpgradeTrack.Output: outputLevel = value; break;
                default: machineLevel = value; break;
            }
        }

        public int TotalLevels => speedLevel + outputLevel + machineLevel;
    }

    [Serializable]
    public class MachineState
    {
        public bool running;
        /// <summary>0..1 through the current cycle.</summary>
        public float progress;
        /// <summary>Goods the cycle will produce (fixed when it starts).</summary>
        public int batch;
    }

    public enum CourierPhase
    {
        Waiting,
        CarryingThere,
        WalkingBack,
    }

    [Serializable]
    public class CourierState
    {
        public CourierPhase phase;
        /// <summary>0..1 along the current walk.</summary>
        public float progress;
        public int carrying;
    }

    [Serializable]
    public class CustomerState
    {
        public int id;
        public int items;
        public float patienceLeft;
        /// <summary>Seconds of walking left before this customer stands at their place in line.</summary>
        public float walkLeft;
        /// <summary>Index of the cashier serving this customer, or -1 while queuing.</summary>
        public int servedBy = -1;
    }
}
