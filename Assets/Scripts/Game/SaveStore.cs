using CrazyGames;
using UnityEngine;

namespace MergeResto.Game
{
    /// <summary>
    /// Key/value save storage. Uses CrazyGames cloud data when the SDK is running (on
    /// CrazyGames, localhost and in the editor), otherwise PlayerPrefs.
    /// </summary>
    public static class SaveStore
    {
        static bool UseCrazy => CrazySDK.IsAvailable && CrazySDK.IsInitialized;

        public static string Load(string key)
        {
            if (UseCrazy)
                return CrazySDK.Data.HasKey(key) ? CrazySDK.Data.GetString(key) : null;
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        }

        public static void Save(string key, string value)
        {
            if (UseCrazy)
            {
                CrazySDK.Data.SetString(key, value);
                return;
            }
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }

        public static void Delete(string key)
        {
            if (UseCrazy)
                CrazySDK.Data.DeleteKey(key);
            else
                PlayerPrefs.DeleteKey(key);
        }
    }
}
