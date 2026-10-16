using MergeResto.Sim;
using UnityEngine;

namespace MergeResto.Game
{
    /// <summary>Tunable data for one shop. All balancing numbers live here, not in code.</summary>
    [CreateAssetMenu(menuName = "Merge Resto/Shop Config", fileName = "ShopConfig")]
    public class ShopConfig : ScriptableObject
    {
        public ShopDefinition shop = new();
    }
}
