using TMPro;
using UnityEngine;

namespace MergeResto.Game.UI
{
    /// <summary>Coins and gems along the top of the screen, with a little pop when they change.</summary>
    public class TopBarView : MonoBehaviour
    {
        [SerializeField] TMP_Text coinsText;
        [SerializeField] TMP_Text gemsText;
        [SerializeField] RectTransform coinsPill;

        double _shownCoins = -1;
        int _shownGems = -1;
        float _pop;

        void Update()
        {
            var sim = GameController.Instance.Sim;
            double coins = System.Math.Floor(sim.State.coins);
            if (coins != _shownCoins)
            {
                if (_shownCoins >= 0 && coins > _shownCoins) _pop = 1f;
                _shownCoins = coins;
                coinsText.text = NumberFormat.Short(sim.State.coins);
            }
            if (sim.State.gems != _shownGems)
            {
                _shownGems = sim.State.gems;
                gemsText.text = _shownGems.ToString();
            }
            if (_pop > 0f && coinsPill != null)
            {
                _pop = Mathf.Max(0f, _pop - Time.deltaTime * 5f);
                coinsPill.localScale = Vector3.one * (1f + Mathf.Sin(_pop * Mathf.PI) * 0.08f);
            }
        }
    }
}
