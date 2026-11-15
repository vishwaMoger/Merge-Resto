using System;
using System.Runtime.InteropServices;
using UnityEngine;
#if !PORTAL_BRIDGE
using CrazyGames;
#endif

namespace MergeResto.Game
{
    /// <summary>
    /// The web portal the game runs on. The normal build talks to CrazyGames; a build made with
    /// the PORTAL_BRIDGE define (Merge Resto/Release/Bridge Build) talks to Playgama Bridge, which
    /// serves Playgama and GameDistribution. Gameplay code only uses this class, AdService and
    /// SaveStore, never a portal SDK directly.
    /// </summary>
    public static class Portal
    {
#if PORTAL_BRIDGE && UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int PB_IsAvailable();
        [DllImport("__Internal")] static extern string PB_PlatformId();
        [DllImport("__Internal")] static extern string PB_Language();
        [DllImport("__Internal")] static extern int PB_IsAudioEnabled();
        [DllImport("__Internal")] static extern int PB_IsRewardedSupported();
        [DllImport("__Internal")] static extern void PB_Listen();
        [DllImport("__Internal")] static extern void PB_SendMessage(string message);
        [DllImport("__Internal")] static extern void PB_ShowInterstitial(string placement);
        [DllImport("__Internal")] static extern void PB_ShowRewarded(string placement);
        [DllImport("__Internal")] static extern void PB_StorageLoad(string key);
        [DllImport("__Internal")] static extern void PB_StorageSave(string key, string value);
        const bool Live = true;
#else
        static int PB_IsAvailable() => 0;
        static string PB_PlatformId() => "";
        static string PB_Language() => "en";
        static int PB_IsAudioEnabled() => 1;
        static int PB_IsRewardedSupported() => 0;
        static void PB_Listen() { }
        static void PB_SendMessage(string message) { }
        static void PB_ShowInterstitial(string placement) { }
        static void PB_ShowRewarded(string placement) { }
        static void PB_StorageLoad(string key) { }
        static void PB_StorageSave(string key, string value) { }
        const bool Live = false;
#endif

        /// <summary>True in the Playgama Bridge build.</summary>
        public const bool IsBridge =
#if PORTAL_BRIDGE
            true;
#else
            false;
#endif

        static bool _bridgeUp;
        static bool _gameReadySent;

        /// <summary>True while the portal is running and answering (Bridge up, or the CrazyGames SDK ready).</summary>
        public static bool Running
        {
            get
            {
#if PORTAL_BRIDGE
                return _bridgeUp;
#else
                return CrazySDK.IsAvailable && CrazySDK.IsInitialized;
#endif
            }
        }

        /// <summary>The portal muted the game (its own mute button, or an ad is playing).</summary>
        public static bool Muted { get; private set; }

        /// <summary>The portal paused the game (tab hidden, an ad, the platform asked).</summary>
        public static bool Paused { get; private set; }

        public static event Action MuteChanged;

        /// <summary>
        /// Starts the portal, then calls <paramref name="ready"/>: on Bridge once the save has been
        /// read back (gameplay must not start before), on CrazyGames once its SDK is up.
        /// </summary>
        public static void Init(Action ready)
        {
#if PORTAL_BRIDGE
            _bridgeUp = Live && PB_IsAvailable() == 1;
            if (!_bridgeUp)
            {
                // Editor, or a page without Bridge: play on, saving in memory only.
                ready?.Invoke();
                return;
            }
            Receiver.Ensure();
            PB_Listen();
            Muted = PB_IsAudioEnabled() == 0;
            AudioListener.volume = Muted ? 0f : 1f;  // silent from the first frame when the platform says so
            Debug.Log($"[Merge Resto] Playgama Bridge on {PB_PlatformId()}, language {PB_Language()} (the game is in English).");
            SaveStore.LoadFromPortal(ready);
#else
            if (CrazySDK.IsAvailable)
                CrazySDK.Init(() =>
                {
                    Muted = CrazySDK.Game.Settings.muteAudio;
                    CrazySDK.Game.AddSettingsChangeListener(s =>
                    {
                        Muted = s.muteAudio;
                        MuteChanged?.Invoke();
                    });
                    ready?.Invoke();
                });
            else
                ready?.Invoke();
#endif
        }

        /// <summary>The shop is on screen and playable.</summary>
        public static void GameplayStart()
        {
#if PORTAL_BRIDGE
            if (_bridgeUp && !_gameReadySent)
            {
                _gameReadySent = true;
                PB_SendMessage("game_ready");
            }
#else
            if (Running)
                CrazySDK.Game.GameplayStart();
#endif
        }

        /// <summary>The shop is being left (travelling to another one).</summary>
        public static void GameplayStop()
        {
#if !PORTAL_BRIDGE
            if (Running)
                CrazySDK.Game.GameplayStop();
#endif
        }

        /// <summary>A moment to celebrate (a new shop, a finished area, a bigger shop).</summary>
        public static void HappyTime()
        {
#if PORTAL_BRIDGE
            if (_bridgeUp)
                PB_SendMessage("player_got_achievement");
#else
            if (Running)
                CrazySDK.Game.HappyTime();
#endif
        }

        /// <summary>
        /// A natural break (leaving the renovation board, travelling, after the welcome back):
        /// Bridge decides whether an interstitial actually shows. Nothing on CrazyGames.
        /// </summary>
        public static void Break(string placement)
        {
#if PORTAL_BRIDGE
            if (_bridgeUp && !AdService.Busy)
                PB_ShowInterstitial(placement);
#endif
        }

        internal static bool RewardedSupported
        {
            get
            {
#if PORTAL_BRIDGE
                return _bridgeUp && PB_IsRewardedSupported() == 1;
#else
                return true;
#endif
            }
        }

        internal static void ShowRewarded(string placement)
        {
            Receiver.Ensure();
            PB_ShowRewarded(placement);
        }

        internal static void LoadSave(string key)
        {
            Receiver.Ensure();
            PB_StorageLoad(key);
        }

        internal static void WriteSave(string key, string value) => PB_StorageSave(key, value);

        static void SetPaused(bool paused)
        {
            if (paused == Paused)
                return;
            Paused = paused;
            AudioListener.pause = paused;
            Time.timeScale = paused ? 0f : 1f;
            if (paused)
                SaveStore.Flush();
        }

        static void SetMuted(bool muted)
        {
            Muted = muted;
            AudioListener.volume = muted ? 0f : 1f;
            MuteChanged?.Invoke();
        }

        /// <summary>Receives the page's answers and events (SendMessage needs a GameObject).</summary>
        class Receiver : MonoBehaviour
        {
            static Receiver _instance;

            public static void Ensure()
            {
                if (_instance != null)
                    return;
                var go = new GameObject("PortalBridge");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<Receiver>();
            }

            void OnPauseState(string value) => SetPaused(value == "1");
            void OnAudioState(string value) => SetMuted(value == "0");
            void OnRewardedState(string state) => AdService.OnBridgeRewardedState(state);
            void OnInterstitialState(string state) => AdService.OnBridgeInterstitialState(state);
            void OnStorageLoaded(string json) => SaveStore.OnPortalLoaded(json);
            void OnPageHidden(string value) => SaveStore.Flush();

            void OnApplicationPause(bool paused)
            {
                if (paused)
                    SaveStore.Flush();
            }

            void OnApplicationFocus(bool focused)
            {
                if (!focused)
                    SaveStore.Flush();
            }
        }
    }
}
