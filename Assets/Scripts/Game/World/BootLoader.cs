using CrazyGames;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MergeResto.Game
{
    /// <summary>
    /// First scene: starts the CrazyGames SDK (cloud saves come through it), then opens the
    /// shop the player was last in, or Grandma's Bakery on a first visit.
    /// </summary>
    public class BootLoader : MonoBehaviour
    {
        [Tooltip("Scene name for each shop id; the first entry is the fallback.")]
        public string[] shopIds = { WorldState.FirstShop };
        public string[] sceneNames = { "Bakery" };

        void Start()
        {
            if (CrazySDK.IsAvailable)
                CrazySDK.Init(LoadShop);
            else
                LoadShop();
        }

        void LoadShop()
        {
            string last = WorldState.LastShop;
            string scene = sceneNames[0];
            for (int i = 0; i < shopIds.Length && i < sceneNames.Length; i++)
                if (shopIds[i] == last && WorldState.IsUnlocked(last))
                    scene = sceneNames[i];
            SceneManager.LoadScene(scene);
        }
    }
}
