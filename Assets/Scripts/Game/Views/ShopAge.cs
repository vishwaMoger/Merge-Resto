using UnityEngine;

namespace MergeResto.Game.Views
{
    /// <summary>
    /// Grandma's shop has been closed for years: walls, floor and furniture start grey and
    /// grimy (toon shader wear) and get cleaner with every renovation, fully fresh at the end.
    /// </summary>
    public class ShopAge : MonoBehaviour
    {
        public Renderer[] renderers;
        [Range(0f, 1f)] public float startWear = 0.5f;

        static readonly int WearId = Shader.PropertyToID("_Wear");

        MaterialPropertyBlock _block;
        float _shown = -1f;

        void Start() => _block = new MaterialPropertyBlock();

        void Update()
        {
            var board = GameController.Instance != null ? GameController.Instance.Sim.Board : null;
            if (board == null)
                return;
            int total = board.Def.renovations.Count;
            float progress = total > 0 ? board.State.renovationsDone.Count / (float)total : 1f;
            float target = startWear * (1f - progress);
            // Ease after a renovation so the shop visibly brightens.
            float next = _shown < 0f ? target : Mathf.MoveTowards(_shown, target, Time.unscaledDeltaTime * 0.15f);
            if (Mathf.Approximately(next, _shown))
                return;
            _shown = next;
            foreach (var r in renderers)
            {
                if (r == null)
                    continue;
                r.GetPropertyBlock(_block);
                _block.SetFloat(WearId, _shown);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
