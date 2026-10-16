using UnityEngine;
using UnityEngine.EventSystems;

namespace MergeResto.Game.UI
{
    /// <summary>Squishes a button on press and springs it back with a little overshoot.</summary>
    public class ButtonJuice : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] float pressedScale = 0.9f;
        [SerializeField] float stiffness = 420f;
        [SerializeField] float damping = 16f;

        float _target = 1f;
        float _scale = 1f;
        float _velocity;
        Vector3 _baseScale;

        void Awake() => _baseScale = transform.localScale;

        public void OnPointerDown(PointerEventData e) => _target = pressedScale;
        public void OnPointerUp(PointerEventData e) => Release();
        public void OnPointerExit(PointerEventData e) { if (_target < 1f) Release(); }

        void Release()
        {
            _target = 1f;
            _velocity += 6f; // kick for the overshoot
        }

        /// <summary>A quick pop, e.g. when a value on this element changes.</summary>
        public void Pop(float strength = 5f) => _velocity += strength;

        void Update()
        {
            float dt = UiTime.Delta;
            float force = (_target - _scale) * stiffness - _velocity * damping;
            _velocity += force * dt;
            _scale += _velocity * dt;
            transform.localScale = _baseScale * _scale;
        }
    }
}
