using TMPro;
using UnityEngine;

namespace MergeResto.Game.UI
{
    /// <summary>Coins and gems along the top. Coins count up smoothly instead of jumping.</summary>
    public class TopBarView : MonoBehaviour
    {
        [SerializeField] TMP_Text coinsText;
        [SerializeField] TMP_Text gemsText;
        [SerializeField] ButtonJuice coinsJuice;
        [SerializeField] ButtonJuice gemsJuice;
        [SerializeField] float countUpSeconds = 0.5f;

        double _displayed = -1;
        int _shownGems = -1;

        void Update()
        {
            var sim = GameController.Instance.Sim;
            double actual = System.Math.Floor(sim.State.coins);
            if (_displayed < 0)
                _displayed = actual;
            else if (_displayed != actual)
            {
                // Ease towards the real value; spending snaps down quickly.
                double step = System.Math.Max(1, System.Math.Abs(actual - _displayed) * UiTime.Delta / countUpSeconds * 3);
                _displayed = actual > _displayed ? System.Math.Min(actual, _displayed + step) : System.Math.Max(actual, _displayed - step * 2);
            }
            coinsText.text = NumberFormat.Short(_displayed);

            if (sim.State.gems != _shownGems)
            {
                if (_shownGems >= 0 && gemsJuice != null) gemsJuice.Pop(6f);
                _shownGems = sim.State.gems;
                gemsText.text = _shownGems.ToString();
            }
        }
    }
}
