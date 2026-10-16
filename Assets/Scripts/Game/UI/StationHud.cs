using MergeResto.Game.Views;
using MergeResto.Sim;
using TMPro;
using UnityEngine;

namespace MergeResto.Game.UI
{
    /// <summary>
    /// Screen-space marker that follows one station: a radial progress ring while it works,
    /// a bobbing "Tap!" bubble when it is waiting for the player, and its stock count.
    /// </summary>
    public class StationHud : MonoBehaviour
    {
        public StationView station;
        [SerializeField] UnityEngine.UI.Image ringFill;
        [SerializeField] GameObject ring;
        [SerializeField] GameObject tapPrompt;
        [SerializeField] GameObject managerBadge;
        [SerializeField] TMP_Text stockText;

        RectTransform _rect;
        RectTransform _canvasRect;
        Camera _cam;

        void Start()
        {
            _rect = (RectTransform)transform;
            _canvasRect = (RectTransform)GetComponentInParent<Canvas>().transform;
            _cam = Camera.main;
        }

        void LateUpdate()
        {
            if (station == null || GameController.Instance == null)
                return;
            var sim = GameController.Instance.Sim;
            int i = station.stationIndex;

            Vector3 screen = _cam.WorldToScreenPoint(station.hudAnchor.position);
            bool visible = screen.z > 0f;
            if (visible && RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out var local))
                _rect.anchoredPosition = local;

            float progress = station.Progress;
            ring.SetActive(visible && progress >= 0f);
            if (progress >= 0f)
                ringFill.fillAmount = progress;

            bool needsTap = sim.NeedsTap(i);
            tapPrompt.SetActive(visible && needsTap);
            if (needsTap)
                tapPrompt.transform.localPosition = new Vector3(0f, 40f + Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 14f, 0f);

            managerBadge.SetActive(visible && sim.IsAutomated(i) && sim.Def.stations[i].kind != StationKind.Seller);

            var def = sim.Def.stations[i];
            var st = sim.State.stations[i];
            int stock = def.kind == StationKind.Seller ? st.input : st.output;
            stockText.text = stock > 0 ? stock.ToString() : "";
        }
    }
}
