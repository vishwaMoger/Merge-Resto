using TMPro;
using UnityEngine;

namespace MergeResto.Game.UI
{
    /// <summary>"Welcome back!" popup with what the bakery earned while the player was away.</summary>
    public class OfflineEarningsDialog : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] TMP_Text body;
        [SerializeField] TMP_Text amount;
        [SerializeField] UnityEngine.UI.Button collectButton;

        void Awake()
        {
            root.SetActive(false);
            collectButton.onClick.AddListener(Collect);
            // Subscribe in Awake: GameController raises this from its Start.
            GameController.Instance.OfflineEarningsReady += Show;
        }

        void OnDestroy()
        {
            if (GameController.Instance != null)
                GameController.Instance.OfflineEarningsReady -= Show;
        }

        void Show(double coins, double seconds)
        {
            body.text = $"Your bakery kept baking for {NumberFormat.Duration(seconds)} while you were away!";
            amount.text = "+" + NumberFormat.Short(coins);
            root.SetActive(true);
        }

        void Collect()
        {
            GameController.Instance.CollectOfflineEarnings();
            root.SetActive(false);
        }
    }
}
