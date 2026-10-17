using System;
using CrazyGames;
using MergeResto.Sim;
using UnityEngine;

namespace MergeResto.Game
{
    /// <summary>
    /// Owns the shop simulation for the open scene: loads and saves it, ticks it every frame,
    /// pays out offline earnings, and reports gameplay start to CrazyGames.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameController : MonoBehaviour
    {
        public static GameController Instance { get; private set; }

        [SerializeField] ShopConfig config;
        [SerializeField] float autosaveSeconds = 15f;

        public ShopSim Sim { get; private set; }

        /// <summary>Raised once after loading when offline earnings are waiting: (coins, seconds away).</summary>
        public event Action<double, double> OfflineEarningsReady;
        public event Action<int> StationSelected;
        public event Action<int> StationTapped;
        public int SelectedStation { get; private set; } = -1;

        double _pendingOffline;
        double _pendingOfflineSeconds;
        float _saveTimer;

        string SaveKey => $"shop_{config.shop.id}";

        void Awake()
        {
            Instance = this;
            Sim = new ShopSim(config.shop, LoadState());
        }

        void Start()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long last = Sim.State.lastSavedUnixSeconds;
            if (last > 0 && now > last)
            {
                _pendingOfflineSeconds = now - last;
                _pendingOffline = Sim.OfflineEarnings(_pendingOfflineSeconds);
                Sim.Board?.Tick(_pendingOfflineSeconds); // energy refills while away
            }
            if (_pendingOffline > 0)
                OfflineEarningsReady?.Invoke(_pendingOffline, _pendingOfflineSeconds);

            WorldState.SetLast(config.shop.id);
            if (CrazySDK.IsAvailable && CrazySDK.IsInitialized)
                CrazySDK.Game.GameplayStart();
        }

        void Update()
        {
            Sim.Tick(Time.deltaTime);
            _saveTimer += Time.unscaledDeltaTime;
            if (_saveTimer >= autosaveSeconds)
                Save();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Save();
        }

        void OnApplicationQuit() => Save();

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ------------------------------------------------------------------ Player actions

        /// <summary>
        /// A tap on a station. An idle station that is waiting for the player just starts working;
        /// a busy or automated one also opens its upgrade card.
        /// </summary>
        public void ClickStation(int station)
        {
            bool waiting = Sim.NeedsTap(station);
            Sim.Tap(station);
            StationTapped?.Invoke(station);
            if (!waiting)
                SelectStation(station);
        }

        public void SelectStation(int station)
        {
            SelectedStation = station;
            StationSelected?.Invoke(station);
        }

        public void ClearSelection() => SelectStation(-1);

        public void CollectOfflineEarnings(double multiplier = 1)
        {
            if (_pendingOffline <= 0)
                return;
            Sim.AddCoins(_pendingOffline * multiplier);
            _pendingOffline = 0;
            Save();
        }

        // ------------------------------------------------------------------ Saving

        ShopState LoadState()
        {
            string json = SaveStore.Load(SaveKey);
            if (string.IsNullOrEmpty(json))
                return null;
            try
            {
                var state = JsonUtility.FromJson<ShopState>(json);
                return state != null && state.version <= ShopState.CurrentVersion ? state : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Merge Resto] Save could not be read, starting fresh: {e.Message}");
                return null;
            }
        }

        public void Save()
        {
            _saveTimer = 0f;
            Sim.State.lastSavedUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SaveStore.Save(SaveKey, JsonUtility.ToJson(Sim.State));
        }

        public ShopDefinition Definition => config.shop;

        /// <summary>Saves this shop and opens another one.</summary>
        public void Travel(string sceneName)
        {
            Save();
            enabled = false;
            if (CrazySDK.IsAvailable && CrazySDK.IsInitialized)
                CrazySDK.Game.GameplayStop();
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }

        /// <summary>Deletes the save and reloads the scene with a fresh shop (debug and settings use).</summary>
        public void ResetProgress()
        {
            SaveStore.Delete(SaveKey);
            enabled = false; // no autosave before the reload
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name);
        }
    }
}
