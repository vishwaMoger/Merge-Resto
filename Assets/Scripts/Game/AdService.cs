using System;
using CrazyGames;
using UnityEngine;

namespace MergeResto.Game
{
    /// <summary>
    /// Rewarded video ads through the CrazyGames SDK. The SDK pauses the game and silences
    /// audio while an ad plays; the reward is only given when the ad finishes. Outside the SDK
    /// (opening a shop scene directly in the editor) the reward is granted straight away so the
    /// flow can be tested.
    /// </summary>
    public static class AdService
    {
        static bool _busy;

        public static bool Busy => _busy;

        public static void ShowRewarded(Action onReward, Action onFailed = null)
        {
            if (_busy)
                return;
            if (!CrazySDK.IsAvailable || !CrazySDK.IsInitialized)
            {
                if (Application.isEditor)
                {
                    Debug.Log("[Merge Resto] Rewarded ad skipped (SDK not running in this editor session); reward granted.");
                    onReward?.Invoke();
                }
                else
                    onFailed?.Invoke();
                return;
            }
            _busy = true;
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
        }
    }
}
