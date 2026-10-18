using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeResto.Game.UI
{
    /// <summary>
    /// "Welcome back!": what this shop earned while the player was away, collected as is or
    /// doubled by watching a rewarded ad, plus a summary of the rest of Main Street (energy
    /// refilled here, coins waiting in the other shops).
    /// </summary>
    public class OfflineEarningsDialog : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] TMP_Text body;
        [SerializeField] TMP_Text amount;
        [SerializeField] TMP_Text summary;
        [SerializeField] Button collectButton;
        [SerializeField] Button doubleButton;
        [SerializeField] TMP_Text doubleText;
        [SerializeField] CoinFlyLayer coinFly;

        double _coins;

        public static bool IsOpen { get; private set; }

        void Awake()
        {
            root.SetActive(false);
            collectButton.onClick.AddListener(() => Collect(1));
            doubleButton.onClick.AddListener(() => AdService.ShowRewarded(() => Collect(2)));
            // Subscribe in Awake: GameController raises this from its Start.
            GameController.Instance.OfflineEarningsReady += Show;
        }

        void OnDestroy()
        {
            IsOpen = false;
            if (GameController.Instance != null)
                GameController.Instance.OfflineEarningsReady -= Show;
        }

        void Show(double coins, double seconds)
        {
            var gc = GameController.Instance;
            // No welcome-back screen in the middle of the Grand Opening tutorial.
            if (gc.Sim.State.tutorialStep < MergeResto.Sim.ShopState.TutorialFinished)
            {
                gc.CollectOfflineEarnings();
                return;
            }
            _coins = coins;
            body.text = $"{gc.Definition.displayName} kept going for {NumberFormat.Duration(seconds)} while you were away!";
            amount.text = "+" + NumberFormat.Short(coins);
            doubleText.text = "x2  +" + NumberFormat.Short(coins * 2);

            var lines = new System.Text.StringBuilder();
            if (gc.OfflineEnergyGained > 0)
                lines.Append($"Renovation energy +{gc.OfflineEnergyGained}");
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var (name, waiting) in WorldState.WaitingElsewhere(gc.Definition.id, now))
            {
                if (lines.Length > 0) lines.Append("\n");
                lines.Append($"{name}: about {NumberFormat.Short(waiting)} coins waiting");
            }
            summary.text = lines.ToString();
            root.SetActive(true);
            IsOpen = true;
            AudioDirector.Play(Sfx.Claim, 0.9f, 0.7f);
        }

        void Collect(int multiplier)
        {
            if (!IsOpen)
                return;
            GameController.Instance.CollectOfflineEarnings(multiplier);
            if (coinFly != null)
                coinFly.FlyFromScreen(RectTransformUtility.WorldToScreenPoint(null, amount.transform.position), multiplier > 1 ? 14 : 8);
            AudioDirector.Play(multiplier > 1 ? Sfx.Celebrate : Sfx.Sale);
            root.SetActive(false);
            IsOpen = false;
        }
    }
}
