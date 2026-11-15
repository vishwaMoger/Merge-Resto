using System;
using System.Collections.Generic;
using UnityEngine;
#if !PORTAL_BRIDGE
using CrazyGames;
#endif

namespace MergeResto.Game
{
    /// <summary>
    /// Key/value save storage. On CrazyGames it is the SDK's cloud data when the SDK is running
    /// (on CrazyGames, localhost and in the editor), otherwise PlayerPrefs.
    /// In the Playgama Bridge build every key lives in memory and the whole set is one Bridge
    /// storage entry: read once before the shop opens, written in one call when it changed (at
    /// most every <see cref="FlushSeconds"/>, and straight away on pause, an ad or travelling).
    /// </summary>
    public static class SaveStore
    {
#if PORTAL_BRIDGE
        const string PortalKey = "main_street_save";
        const float FlushSeconds = 30f;

        static readonly Dictionary<string, string> _values = new();
        static bool _dirty;
        static float _lastFlush;
        static Action _loaded;

        [Serializable]
        class Blob
        {
            public List<string> keys = new();
            public List<string> values = new();
        }

        public static string Load(string key) => _values.TryGetValue(key, out var v) ? v : null;

        public static void Save(string key, string value)
        {
            _values[key] = value;
            MarkDirty();
        }

        public static void Delete(string key)
        {
            if (_values.Remove(key))
                MarkDirty();
        }

        static void MarkDirty()
        {
            _dirty = true;
            if (Time.realtimeSinceStartup - _lastFlush >= FlushSeconds)
                Flush();
        }

        /// <summary>Writes any change to Bridge storage now.</summary>
        public static void Flush()
        {
            if (!_dirty || !Portal.Running)
                return;
            _dirty = false;
            _lastFlush = Time.realtimeSinceStartup;
            var blob = new Blob();
            foreach (var kv in _values)
            {
                blob.keys.Add(kv.Key);
                blob.values.Add(kv.Value);
            }
            Portal.WriteSave(PortalKey, JsonUtility.ToJson(blob));
        }

        internal static void LoadFromPortal(Action loaded)
        {
            _loaded = loaded;
            Portal.LoadSave(PortalKey);
        }

        internal static void OnPortalLoaded(string json)
        {
            _values.Clear();
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var blob = JsonUtility.FromJson<Blob>(json);
                    for (int i = 0; blob != null && i < blob.keys.Count && i < blob.values.Count; i++)
                        _values[blob.keys[i]] = blob.values[i];
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Merge Resto] Saved game could not be read, starting fresh: {e.Message}");
                }
            }
            _lastFlush = Time.realtimeSinceStartup;
            var loaded = _loaded;
            _loaded = null;
            loaded?.Invoke();
        }
#else
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

        /// <summary>Nothing to do: the CrazyGames SDK and PlayerPrefs write straight away.</summary>
        public static void Flush() { }

        internal static void LoadFromPortal(Action loaded) => loaded?.Invoke();
        internal static void OnPortalLoaded(string json) { }
#endif

        /// <summary>
        /// Small settings and flags (sound, music, what the board has shown). They stay in
        /// PlayerPrefs on CrazyGames, as before; in the Bridge build they are part of the save.
        /// </summary>
        public static class Prefs
        {
            public static int GetInt(string key, int fallback)
            {
#if PORTAL_BRIDGE
                return int.TryParse(Load(key), out int v) ? v : fallback;
#else
                return PlayerPrefs.GetInt(key, fallback);
#endif
            }

            public static void SetInt(string key, int value)
            {
#if PORTAL_BRIDGE
                Save(key, value.ToString());
#else
                PlayerPrefs.SetInt(key, value);
#endif
            }

            public static string GetString(string key, string fallback)
            {
#if PORTAL_BRIDGE
                return Load(key) ?? fallback;
#else
                return PlayerPrefs.GetString(key, fallback);
#endif
            }

            public static void SetString(string key, string value)
            {
#if PORTAL_BRIDGE
                Save(key, value);
#else
                PlayerPrefs.SetString(key, value);
#endif
            }

            public static void Delete(string key)
            {
#if PORTAL_BRIDGE
                SaveStore.Delete(key);
#else
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
#endif
            }
        }
    }
}
