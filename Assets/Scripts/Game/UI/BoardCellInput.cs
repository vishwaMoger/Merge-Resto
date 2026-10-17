using UnityEngine;
using UnityEngine.EventSystems;

namespace MergeResto.Game.UI
{
    /// <summary>Forwards taps and drags on one board cell to the board view.</summary>
    public class BoardCellInput : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public MergeBoardView view;
        public int cell;

        public void OnPointerClick(PointerEventData e)
        {
            if (!e.dragging)
                view.OnCellTapped(cell);
        }

        public void OnBeginDrag(PointerEventData e) => view.OnDragBegin(cell, e);
        public void OnDrag(PointerEventData e) => view.OnDragMove(e);
        public void OnEndDrag(PointerEventData e) => view.OnDragEnd(e);
    }
}
