using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeResto.Game.UI
{
    /// <summary>What a lesson points at: nothing, a UI element, a spot in the shop, or a drag.</summary>
    public struct GuideTarget
    {
        public enum Kind { None, Rect, World, Drag }

        public Kind kind;
        public RectTransform rect;
        public Vector3 world;
        public RectTransform from, to;
        public Sprite dragSprite;
        public bool fromBelow;

        public static GuideTarget None => new() { kind = Kind.None };
        public static GuideTarget At(RectTransform rt) => rt != null ? new GuideTarget { kind = Kind.Rect, rect = rt } : None;
        /// <summary>Points up at the element from underneath, for buttons with text above them.</summary>
        public static GuideTarget Below(RectTransform rt) => rt != null ? new GuideTarget { kind = Kind.Rect, rect = rt, fromBelow = true } : None;
        public static GuideTarget AtWorld(Vector3 p) => new() { kind = Kind.World, world = p };
        public static GuideTarget DragFrom(RectTransform a, RectTransform b, Sprite sprite) =>
            new() { kind = Kind.Drag, from = a, to = b, dragSprite = sprite };
    }

    /// <summary>
    /// Grandma's hint layer for the feature lessons: a soft spotlight on the target, a bouncing
    /// pin (or a finger showing a drag), and her speech box. The dim never blocks the game
    /// unless the lesson waits for a tap, so the player can do what she points at.
    /// </summary>
    public class GuideOverlay : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform layer;
        [SerializeField] Image dim;
        [SerializeField] Button dimButton;
        [SerializeField] RectTransform pointer;
        [SerializeField] RectTransform ping;
        [SerializeField] RectTransform speech;
        [SerializeField] Button speechButton;
        [SerializeField] TMP_Text speechText;
        [SerializeField] GameObject continueHint;
        [SerializeField] RectTransform portrait;
        [SerializeField] Image dragIcon;

        const float DimSize = 6000f;
        static readonly int HoleId = Shader.PropertyToID("_Hole");

        Material _dimMaterial;
        Camera _cam;
        TMP_Text _pointerLabel;
        bool _active;
        string _text;
        GuideTarget _target;
        bool _waitTap;
        bool _showSpeech;
        bool _tapped;
        float _typed;
        float _speechPop;
        float _dimAlpha;
        float _pointerShown;
        float _show;
        Vector2 _holeCenter;
        Vector2 _holeRadius;
        float _dragT;

        /// <summary>True for one frame after the player tapped to continue.</summary>
        public bool ConsumeTap()
        {
            bool t = _tapped;
            _tapped = false;
            return t;
        }

        public bool Visible => _active;

        /// <summary>True while a lesson is on screen, so other hints (station pins) step aside.</summary>
        public static bool Showing { get; private set; }

        void OnDestroy() => Showing = false;

        void Start()
        {
            _cam = Camera.main;
            _dimMaterial = new Material(dim.material);
            dim.material = _dimMaterial;
            dim.rectTransform.sizeDelta = new Vector2(DimSize, DimSize);
            dimButton.onClick.AddListener(OnTap);
            speechButton.onClick.AddListener(OnTap);
            _pointerLabel = pointer.GetComponentInChildren<TMP_Text>(true);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            dragIcon.gameObject.SetActive(false);
        }

        void OnTap()
        {
            if (!_active)
                return;
            if (_typed < Length())
            {
                _typed = 9999f;
                return;
            }
            if (_waitTap)
                _tapped = true;
        }

        /// <summary>Called every frame by the lesson that is running.</summary>
        public void Present(string text, GuideTarget target, bool waitTap, bool showSpeech = true)
        {
            if (text != _text)
            {
                _text = text;
                speechText.text = text ?? "";
                _typed = 0f;
                _speechPop = 1f;
                if (!string.IsNullOrEmpty(text) && showSpeech)
                    AudioDirector.Play(Sfx.Blip, 1.1f, 0.6f);
            }
            if (target.kind == GuideTarget.Kind.Drag && (_target.kind != GuideTarget.Kind.Drag || _target.from != target.from))
                _dragT = 0f;
            _target = target;
            _waitTap = waitTap;
            _showSpeech = showSpeech && !string.IsNullOrEmpty(text);
            _active = true;
        }

        public void Clear()
        {
            _active = false;
            _text = null;
            _tapped = false;
        }

        int Length() => speechText.textInfo != null ? speechText.textInfo.characterCount : 0;

        void LateUpdate()
        {
            float dt = UiTime.Delta;
            _show = Mathf.MoveTowards(_show, _active ? 1f : 0f, dt * 5f);
            group.alpha = _show;
            Showing = _active;
            group.blocksRaycasts = _active && (_waitTap || _showSpeech);
            if (_show <= 0f)
            {
                dragIcon.gameObject.SetActive(false);
                return;
            }

            // Speech.
            speech.gameObject.SetActive(_showSpeech);
            _typed += dt * 60f;
            speechText.maxVisibleCharacters = Mathf.Min((int)_typed, 99999);
            bool talking = _typed < Length();
            continueHint.SetActive(_waitTap && !talking);
            _speechPop = Mathf.Max(0f, _speechPop - dt * 4f);
            speech.localScale = Vector3.one * (1f + Mathf.Sin(_speechPop * Mathf.PI) * 0.05f);
            portrait.localRotation = Quaternion.Euler(0f, 0f, talking ? Mathf.Sin(Time.unscaledTime * 14f) * 4f : 0f);

            // Spotlight.
            bool has = Resolve(out var tip, out var center, out var radius);
            dim.raycastTarget = _waitTap;
            float alpha = !_active ? 0f : _waitTap ? 0.5f : has ? 0.3f : 0.0f;
            _dimAlpha = Mathf.MoveTowards(_dimAlpha, alpha, dt * 1.5f);
            var c = _dimMaterial.color;
            c.a = _dimAlpha;
            _dimMaterial.color = c;
            float k = 1f - Mathf.Exp(-dt * 10f);
            _holeCenter = Vector2.Lerp(_holeCenter, has ? center : _holeCenter, k);
            _holeRadius = Vector2.Lerp(_holeRadius, has ? radius : Vector2.zero, k);
            dim.rectTransform.anchoredPosition = _holeCenter;
            _dimMaterial.SetVector(HoleId, new Vector4(0.5f, 0.5f, _holeRadius.x / DimSize, _holeRadius.y / DimSize));

            // Pointer: bounces on a target, or carries an item along a drag.
            // While she waits for a tap the spotlight alone shows what she talks about.
            _pointerShown = Mathf.MoveTowards(_pointerShown, has && _active && !_waitTap ? 1f : 0f, dt * 5f);
            pointer.gameObject.SetActive(_pointerShown > 0.01f);
            ping.gameObject.SetActive(_pointerShown > 0.01f && _target.kind != GuideTarget.Kind.Drag);
            bool drag = _target.kind == GuideTarget.Kind.Drag && has;
            dragIcon.gameObject.SetActive(drag && _pointerShown > 0.5f);
            if (_pointerLabel != null)
                _pointerLabel.text = drag ? "DRAG" : "TAP!";
            if (drag)
            {
                _dragT += dt / 1.6f;
                float t = Mathf.Repeat(_dragT, 1f);
                // Hold, move, drop, pause.
                float move = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.15f) / 0.55f));
                var a = LocalCenter(_target.from);
                var b = LocalCenter(_target.to);
                var p = Vector2.Lerp(a, b, move);
                pointer.anchoredPosition = p + new Vector2(0f, 30f);
                dragIcon.sprite = _target.dragSprite;
                ((RectTransform)dragIcon.transform).anchoredPosition = p;
                float fade = t > 0.85f ? 1f - (t - 0.85f) / 0.15f : Mathf.Clamp01(t / 0.1f);
                dragIcon.color = new Color(1f, 1f, 1f, 0.85f * fade);
                dragIcon.transform.localScale = Vector3.one * (1.1f + 0.05f * Mathf.Sin(t * Mathf.PI * 6f));
            }
            else if (has)
            {
                float hop = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.4f));
                float dir = _target.fromBelow ? -1f : 1f;
                pointer.anchoredPosition = tip + new Vector2(0f, hop * 24f * dir);
                ping.anchoredPosition = tip;
            }
            bool flip = !drag && _target.fromBelow;
            pointer.localRotation = Quaternion.Euler(0f, 0f, flip ? 180f : 0f);
            if (_pointerLabel != null)
                _pointerLabel.transform.localRotation = Quaternion.Euler(0f, 0f, flip ? 180f : 0f);
            float pop = Mathf.SmoothStep(0f, 1f, _pointerShown);
            pointer.localScale = Vector3.one * pop;
            float phase = Mathf.Repeat(Time.unscaledTime * 0.9f, 1f);
            ping.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.5f, phase) * pop;
            var pingImage = ping.GetComponent<Graphic>();
            if (pingImage != null)
                pingImage.color = new Color(1f, 0.95f, 0.8f, (1f - phase) * 0.9f);
        }

        Vector2 LocalCenter(RectTransform rt) => layer.InverseTransformPoint(rt.TransformPoint(rt.rect.center));

        bool Resolve(out Vector2 tip, out Vector2 center, out Vector2 radius)
        {
            tip = center = radius = Vector2.zero;
            switch (_target.kind)
            {
                case GuideTarget.Kind.Rect:
                {
                    if (_target.rect == null || !_target.rect.gameObject.activeInHierarchy)
                        return false;
                    var corners = new Vector3[4];
                    _target.rect.GetWorldCorners(corners);
                    Vector2 min = layer.InverseTransformPoint(corners[0]);
                    Vector2 max = layer.InverseTransformPoint(corners[2]);
                    center = (min + max) * 0.5f;
                    radius = (max - min) * 0.5f * 1.25f + new Vector2(18f, 18f);
                    tip = new Vector2(center.x, _target.fromBelow ? min.y + 6f : max.y - 6f);
                    return true;
                }
                case GuideTarget.Kind.World:
                {
                    var screen = _cam.WorldToScreenPoint(_target.world);
                    if (screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, null, out var local))
                        return false;
                    tip = center = local;
                    radius = new Vector2(170f, 150f);
                    return true;
                }
                case GuideTarget.Kind.Drag:
                {
                    if (_target.from == null || _target.to == null)
                        return false;
                    var a = LocalCenter(_target.from);
                    var b = LocalCenter(_target.to);
                    center = (a + b) * 0.5f;
                    radius = new Vector2(Mathf.Abs(a.x - b.x) * 0.5f + 90f, Mathf.Abs(a.y - b.y) * 0.5f + 90f);
                    tip = a;
                    return true;
                }
                default:
                    return false;
            }
        }
    }
}
