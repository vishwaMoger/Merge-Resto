using System;
using UnityEngine;
#if !PORTAL_BRIDGE
using CrazyGames;
#endif

namespace MergeResto.Game
{
    /// <summary>
    /// Rewarded video ads through the portal (CrazyGames, or Playgama Bridge in that build). The
    /// portal pauses the game and silences audio while an ad plays; the reward is only given when
    /// the ad was watched to the end. Outside a portal (opening a shop scene directly in the editor)
    /// the reward is granted straight away so the flow can be tested.
    /// </summary>
    public static class AdService
    {
        static bool _busy;
        static bool _earned;
        static Action _onReward, _onFailed;

        public static bool Busy => _busy;

        /// <summary>
        /// Rewarded ads can be offered here. When false, ad buttons hide (Bridge rule: never show
        /// what the platform does not support).
        /// </summary>
        public static bool Supported => Application.isEditor || !Portal.Running || Portal.RewardedSupported;

        public static void ShowRewarded(Action onReward, Action onFailed = null)
        {
            if (_busy)
                return;
            if (!Portal.Running)
            {
                if (Application.isEditor)
                {
                    Debug.Log("[Merge Resto] Rewarded ad skipped (no portal in this editor session); reward granted.");
                    onReward?.Invoke();
                }
                else
                    onFailed?.Invoke();
                return;
            }
            _busy = true;
#if PORTAL_BRIDGE
            _earned = false;
            _onReward = onReward;
            _onFailed = onFailed;
            Portal.ShowRewarded("reward");
#else
            CrazySDK.Ad.RequestAd(CrazyAdType.Rewarded,
                () => { },
                error =>
                {
                    _busy = false;
                    Debug.Log($"[Merge Resto] Rewarded ad unavailable: {error}");
                    onFailed?.Invoke();
                },
                () =>
                {
                    _busy = false;
                    onReward?.Invoke();
                });
#endif
        }

        /// <summary>Bridge rewarded ad states: the reward counts only after "rewarded", paid out once the ad has closed.</summary>
        internal static void OnBridgeRewardedState(string state)
        {
            switch (state)
            {
                case "rewarded":
                    _earned = true;
                    break;
                case "closed":
                case "failed":
                    if (!_busy)
                        return;
                    _busy = false;
                    var reward = _onReward;
                    var failed = _onFailed;
                    _onReward = _onFailed = null;
                    if (_earned && state == "closed")
                        reward?.Invoke();
                    else
                        failed?.Invoke();
                    _earned = false;
                    break;
            }
        }

        internal static void OnBridgeInterstitialState(string state)
        {
            // Pausing and muting come from the platform's pause and audio events; saving before
            // a full-screen ad keeps progress safe if the player closes the tab during it.
            if (state == "opened")
                SaveStore.Flush();
        }
    }
}
