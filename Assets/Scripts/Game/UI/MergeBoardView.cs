using System.Collections.Generic;
using MergeResto.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MergeResto.Game.UI
{
    /// <summary>
    /// The renovation board: tap generators for items, drag equal items together to merge,
    /// and hand finished items in to fix up the shop. Slides in from the right while the
    /// bakery keeps running; the HUD's Renovate button opens it.
    /// Feel: a dragged item lifts and tilts under the finger, matching items glow, a merge
    /// fuses the two with a flash, rays, sparks and a rising combo sound, a new item type is
    /// announced, and an idle board nudges a pair that can be merged.
    /// </summary>
    public class MergeBoardView : MonoBehaviour
    {
        [System.Serializable]
        public class ChainIcons
        {
            public Sprite[] tiers;
        }

        [System.Serializable]
        public class Task
        {
            public RectTransform root;
            public TMP_Text title;
            public TMP_Text bonus;
            public Image[] needFrames;
            public Image[] needIcons;
            public GameObject[] needChecks;
            public Button fix;
            public Image fixImage;
            public TMP_Text fixText;
            [System.NonSerialized] public int renovation = -1;
            [System.NonSerialized] public float slide;
        }

        [Header("Panel")]
        [SerializeField] RectTransform panel;
        [SerializeField] CanvasGroup group;
        [SerializeField] Button openButton;
        [SerializeField] GameObject openAlert;
        [SerializeField] Button closeButton;
        [SerializeField] CanvasGroup dim;
        [SerializeField] Button dimButton;
        [Tooltip("HUD pieces the open panel would overlap (the shop banner).")]
        [SerializeField] CanvasGroup[] hideWhileOpen;

        [Header("Board")]
        [SerializeField] RectTransform[] cells;
        [SerializeField] Image[] cellFrames;
        [SerializeField] Image[] icons;
        [SerializeField] Image dragGhost;
        [SerializeField] Image flyer;
        [SerializeField] RectTransform burst;
        [SerializeField] Sprite sparkSprite;
        [SerializeField] TMP_Text discoveryText;

        [Header("Info")]
        [SerializeField] TMP_Text energyText;
        [SerializeField] TMP_Text energyTimer;
        [Tooltip("Watch an ad for energy; shown when energy runs low.")]
        [SerializeField] Button refillButton;
        [SerializeField] int refillAmount = 50;
        [SerializeField] int refillBelow = 10;
        [SerializeField] TMP_Text cozinessText;
        [SerializeField] TMP_Text infoText;
        [SerializeField] Button sellButton;
        [SerializeField] TMP_Text sellText;
        [SerializeField] Task[] tasks;

        [Header("Art")]
        [SerializeField] ChainIcons[] chainIcons;
        [SerializeField] Sprite[] generatorIcons;
        [SerializeField] Sprite frameNormal;
        [SerializeField] Sprite frameSelected;
        [SerializeField] Sprite frameGenerator;
        [SerializeField] Sprite fixReady;
        [SerializeField] Sprite fixWaiting;
        [SerializeField] Color readyFrame = new(0.55f, 0.85f, 0.4f);
        [SerializeField] Color missingFrame = Color.white;
        [SerializeField] Color matchGlow = new(0.72f, 1f, 0.6f);
        [SerializeField] Color hoverGlow = new(1f, 0.93f, 0.6f);
        [SerializeField] CoinFlyLayer coinFly;

        const string Hint = "Tap a <color=#C24E18>generator</color> to make items, then drag two matching items together to <color=#C24E18>merge</color> them!";
        const string DiscoveredKey = "board_discovered";
        const float IdleHintAfter = 6f;

        class Spark
        {
            public RectTransform rt;
            public Image image;
            public Vector2 velocity;
            public float life;
            public float age;
            public float spin;
        }

        // A short animation from one screen point to another (spawns, drops, snap-backs).
        class Glide
        {
            public Image image;
            public Vector3 from, to;
            public float t, duration, arc;
            public int revealCell;
            public float startScale, endScale;
            public bool merge;
        }

        MergeBoard _board;
        float _open;
        float _velocity;
        bool _isOpen;
        bool _everOpened;
        Vector2 _shownPos;
        Vector2 _hiddenPos;
        int _selected = -1;
        int _dragFrom = -1;
        int _hover = -1;
        Vector3 _dragPos;
        bool _touchDrag;
        Vector3 _lastDragPos;
        float _dragLift;
        float _dragTilt;
        float[] _pop;
        float[] _hiddenUntilGlide;
        float _burstT = -1f;
        float _noEnergyShake;
        float _idle;
        int _hintA = -1, _hintB = -1;
        int _combo;
        float _comboTimer;
        float _discoveryT = -1f;
        HashSet<int> _discovered;
        readonly List<Spark> _sparks = new();
        readonly List<Glide> _glides = new();
        readonly Stack<Image> _flyerPool = new();

        public bool IsOpen => _isOpen;

        /// <summary>True while any shop's board is open (station pins step aside).</summary>
        public static bool AnyOpen { get; private set; }
        public RectTransform OpenButtonRect => (RectTransform)openButton.transform;
        public bool Dragging => _dragFrom >= 0;
        public RectTransform RefillRect => refillButton.gameObject.activeInHierarchy ? (RectTransform)refillButton.transform : null;
        public Sprite IconFor(BoardItem item) => SpriteFor(item);

        /// <summary>A board cell (for the lessons' pointer).</summary>
        public RectTransform Cell(int index) => cells[index];

        // Cells pivot on their top-left corner; effects and glides aim at the middle.
        Vector3 Center(int index) => cells[index].TransformPoint(cells[index].rect.center);

        /// <summary>The Fix button of the task card showing this renovation, or null.</summary>
        public RectTransform FixButtonFor(int renovation)
        {
            foreach (var t in tasks)
                if (t.renovation == renovation && t.root.gameObject.activeInHierarchy)
                    return (RectTransform)t.fix.transform;
            return null;
        }

        /// <summary>The whole task card showing this renovation, or null.</summary>
        public RectTransform TaskCardFor(int renovation)
        {
            foreach (var t in tasks)
                if (t.renovation == renovation && t.root.gameObject.activeInHierarchy)
                    return t.root;
            return null;
        }

        /// <summary>The generator cell that makes items of a chain, or -1.</summary>
        public int GeneratorCell(int chain)
        {
            for (int i = 0; i < cells.Length; i++)
                if (_board[i].IsGenerator && _board.Def.generators[_board[i].GeneratorIndex].chain == chain)
                    return i;
            return -1;
        }

        /// <summary>Two cells holding equal, still-mergeable items (lowest tier first), if any.</summary>
        public bool FindPair(out int a, out int b, int chain = -1)
        {
            a = b = -1;
            int bestTier = int.MaxValue;
            var seen = new Dictionary<int, int>();
            for (int i = 0; i < cells.Length; i++)
            {
                var item = _board[i];
                if (!item.IsItem || item.Tier >= _board.MaxTier(item.Chain) || (chain >= 0 && item.Chain != chain))
                    continue;
                if (seen.TryGetValue(item.code, out int other))
                {
                    if (item.Tier < bestTier)
                    {
                        bestTier = item.Tier;
                        a = other;
                        b = i;
                    }
                }
                else
                    seen[item.code] = i;
            }
            return a >= 0;
        }

        void Start()
        {
            _board = GameController.Instance.Sim.Board;
            if (_board == null)
            {
                openButton.gameObject.SetActive(false);
                gameObject.SetActive(false);
                return;
            }
            _shownPos = panel.anchoredPosition;
            _hiddenPos = _shownPos + new Vector2(panel.rect.width + 120f, 0f);
            panel.anchoredPosition = _hiddenPos;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            _pop = new float[cells.Length];
            _hiddenUntilGlide = new float[cells.Length];
            _everOpened = PlayerPrefs.GetInt("board_opened", 0) == 1;
            _discovered = LoadDiscovered();

            openButton.onClick.AddListener(() => SetOpen(!_isOpen));
            closeButton.onClick.AddListener(() => SetOpen(false));
            dimButton.onClick.AddListener(() => SetOpen(false));
            dim.alpha = 0f;
            dim.blocksRaycasts = false;
            sellButton.onClick.AddListener(SellSelected);
            refillButton.onClick.AddListener(() => AdService.ShowRewarded(() =>
            {
                _board.AddEnergy(refillAmount);
                AudioDirector.Play(Sfx.Claim);
            }));
            foreach (var t in tasks)
            {
                var task = t;
                task.fix.onClick.AddListener(() => Fix(task));
            }
            for (int i = 0; i < cells.Length; i++)
            {
                var input = cells[i].gameObject.AddComponent<BoardCellInput>();
                input.view = this;
                input.cell = i;
            }
            dragGhost.gameObject.SetActive(false);
            flyer.gameObject.SetActive(false);
            burst.gameObject.SetActive(false);
            if (discoveryText != null)
                discoveryText.gameObject.SetActive(false);

            _board.ItemsMerged += OnMerged;
            GameController.Instance.StationSelected += OnStationSelected;
        }

        void OnDestroy()
        {
            AnyOpen = false;
            if (_board != null)
                _board.ItemsMerged -= OnMerged;
            if (GameController.Instance != null)
                GameController.Instance.StationSelected -= OnStationSelected;
        }

        void OnStationSelected(int station)
        {
            if (station >= 0 && _isOpen)
                SetOpen(false);
        }

        public void SetOpen(bool open)
        {
            AnyOpen = open;
            if (open != _isOpen)
                AudioDirector.Play(Sfx.Whoosh, open ? 1.05f : 0.9f, 0.5f);
            _isOpen = open;
            _idle = 0f;
            if (open)
            {
                GameController.Instance.ClearSelection();
                if (!_everOpened)
                {
                    _everOpened = true;
                    PlayerPrefs.SetInt("board_opened", 1);
                }
            }
            else
            {
                _selected = -1;
                CancelDrag(snapBack: false);
            }
        }

        // ------------------------------------------------------------------ Input

        public void OnCellTapped(int cell)
        {
            _idle = 0f;
            var item = _board[cell];
            if (item.IsGenerator)
            {
                if (_board.Spawn(cell, out int target))
                {
                    _pop[cell] = 0.6f;
                    var made = _board[target];
                    StartGlide(SpriteFor(made), Center(cell), Center(target), 0.32f, 70f, target, 0.45f, 1f);
                    Discover(made, Center(target));
                }
                else
                {
                    if (_board.Energy <= 0)
                        _noEnergyShake = 1f;
                    _pop[cell] = 0.3f;
                    AudioDirector.Play(Sfx.Deny);
                }
                _selected = cell;
                return;
            }
            _selected = item.IsEmpty ? -1 : cell;
            if (!item.IsEmpty)
            {
                _pop[cell] = 0.4f;
                AudioDirector.Play(Sfx.Blip, 1.2f + item.Tier * 0.05f, 0.45f);
            }
        }

        public void OnDragBegin(int cell, PointerEventData e)
        {
            if (_board[cell].IsEmpty || _hiddenUntilGlide[cell] > 0f)
                return;
            _idle = 0f;
            _dragFrom = cell;
            _selected = cell;
            _dragLift = 0f;
            _dragPos = _lastDragPos = Center(cell);
            _touchDrag = e != null && e.pointerId >= 0; // mouse pointers are negative
            dragGhost.sprite = SpriteFor(_board[cell]);
            dragGhost.gameObject.SetActive(true);
            dragGhost.transform.SetAsLastSibling();
            AudioDirector.Play(Sfx.Blip, 1.35f, 0.4f);
            OnDragMove(e);
        }

        public void OnDragMove(PointerEventData e)
        {
            if (_dragFrom < 0)
                return;
            // Overlay canvas: world position is screen pixels, so the ghost sits right under the finger.
            _dragPos = new Vector3(e.position.x, e.position.y, 0f);
            int over = CellAt(e.position);
            if (over != _hover)
            {
                _hover = over;
                if (over >= 0 && over != _dragFrom && CanMerge(_dragFrom, over))
                    AudioDirector.Play(Sfx.Blip, 1.6f, 0.3f);
            }
        }

        public void OnDragEnd(PointerEventData e)
        {
            if (_dragFrom < 0)
                return;
            int target = CellAt(e.position);
            int from = _dragFrom;
            var sprite = dragGhost.sprite;
            var dropAt = dragGhost.transform.position;
            float ghostScale = dragGhost.transform.localScale.x;
            _dragFrom = -1;
            _hover = -1;
            dragGhost.gameObject.SetActive(false);
            if (target < 0 || target == from)
            {
                // Glide home.
                StartGlide(sprite, dropAt, Center(from), 0.18f, 0f, from, ghostScale, 1f);
                return;
            }
            bool merging = CanMerge(from, target);
            var otherSprite = _board[target].IsEmpty ? null : SpriteFor(_board[target]);
            var result = _board.Move(from, target);
            if (result == MoveResult.None)
            {
                StartGlide(sprite, dropAt, Center(from), 0.18f, 0f, from, ghostScale, 1f);
                return;
            }
            _selected = target;
            if (merging)
            {
                // The dragged item fuses into the target; OnMerged has already queued the burst.
                var g = StartGlide(sprite, dropAt, Center(target), 0.1f, 0f, target, ghostScale, 0.6f);
                g.merge = true;
                return;
            }
            StartGlide(sprite, dropAt, Center(target), 0.12f, 0f, target, ghostScale, 1f);
            if (result == MoveResult.Swapped && otherSprite != null)
                StartGlide(otherSprite, Center(target), Center(from), 0.2f, 30f, from, 1f, 1f);
            AudioDirector.Play(Sfx.Blip, 1f, 0.4f);
        }

        bool CanMerge(int a, int b)
        {
            var x = _board[a];
            var y = _board[b];
            return x.IsItem && x == y && x.Tier < _board.MaxTier(x.Chain);
        }

        void CancelDrag(bool snapBack)
        {
            if (_dragFrom >= 0 && snapBack)
                StartGlide(dragGhost.sprite, dragGhost.transform.position, Center(_dragFrom), 0.18f, 0f, _dragFrom, 1.1f, 1f);
            _dragFrom = -1;
            _hover = -1;
            dragGhost.gameObject.SetActive(false);
        }

        int CellAt(Vector2 screen)
        {
            for (int i = 0; i < cells.Length; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(cells[i], screen, null))
                    return i;
            return -1;
        }

        void SellSelected()
        {
            if (_selected < 0 || !_board[_selected].IsItem)
                return;
            var from = RectTransformUtility.WorldToScreenPoint(null, Center(_selected));
            if (_board.Sell(_selected))
            {
                AudioDirector.Play(Sfx.Sale);
                SparkBurst(Center(_selected), 8, new Color(1f, 0.85f, 0.35f));
                if (coinFly != null)
                    coinFly.FlyFromScreen(from, 3);
            }
            _selected = -1;
        }

        void Fix(Task task)
        {
            if (task.renovation < 0 || !_board.Renovate(task.renovation))
                return;
            SparkBurst(task.fix.transform.position, 18, new Color(1f, 0.85f, 0.35f));
            task.renovation = -1;
            if (_selected >= 0 && _board[_selected].IsEmpty)
                _selected = -1;
            // Close the board so the player watches the spot get fixed (RenovationSpot plays it).
            SetOpen(false);
        }

        void OnMerged(int cell, BoardItem item)
        {
            _idle = 0f;
            _hintA = _hintB = -1;
            _pop[cell] = 1f;
            _burstT = 0f;
            burst.position = Center(cell);
            burst.gameObject.SetActive(true);
            burst.SetAsLastSibling();
            // Combos: quick merges climb in pitch and sparkle more.
            _combo = _comboTimer > 0f ? _combo + 1 : 1;
            _comboTimer = 2.2f;
            AudioDirector.Play(Sfx.Merge, 1f + Mathf.Min(_combo - 1, 8) * 0.07f + item.Tier * 0.03f, 1f);
            var tint = Color.Lerp(new Color(1f, 0.92f, 0.55f), new Color(1f, 0.6f, 0.85f), Mathf.Clamp01(item.Tier / 5f));
            SparkBurst(Center(cell), 12 + item.Tier * 3 + _combo * 2, tint);
            if (_combo >= 2 && discoveryText != null && !Discover(item, Center(cell)))
                ShowBanner($"Combo x{_combo}!");
            else
                Discover(item, Center(cell));
        }

        // ------------------------------------------------------------------ Discovery

        HashSet<int> LoadDiscovered()
        {
            var set = new HashSet<int>();
            foreach (var s in PlayerPrefs.GetString(DiscoveredKey, "").Split(','))
                if (int.TryParse(s, out int code))
                    set.Add(code);
            return set;
        }

        // First time an item is made: announce it. Returns true if it was new.
        bool Discover(BoardItem item, Vector3 at)
        {
            if (!item.IsItem || _discovered.Contains(item.code))
                return false;
            _discovered.Add(item.code);
            PlayerPrefs.SetString(DiscoveredKey, string.Join(",", _discovered));
            if (item.Tier > 0)
            {
                ShowBanner($"New: {_board.NameOf(item)}!");
                SparkBurst(at, 20, new Color(1f, 0.85f, 0.35f));
                AudioDirector.Play(Sfx.Claim, 1.1f, 0.7f);
            }
            return true;
        }

        void ShowBanner(string text)
        {
            if (discoveryText == null)
                return;
            discoveryText.text = text;
            discoveryText.gameObject.SetActive(true);
            _discoveryT = 0f;
        }

        // ------------------------------------------------------------------ Effects

        Glide StartGlide(Sprite sprite, Vector3 from, Vector3 to, float duration, float arc, int revealCell, float startScale, float endScale)
        {
            var image = _flyerPool.Count > 0 ? _flyerPool.Pop() : Instantiate(flyer, flyer.transform.parent);
            image.sprite = sprite;
            image.gameObject.SetActive(true);
            image.transform.SetAsLastSibling();
            image.transform.position = from;
            if (revealCell >= 0)
                _hiddenUntilGlide[revealCell] = Mathf.Max(_hiddenUntilGlide[revealCell], duration + 0.02f);
            var g = new Glide { image = image, from = from, to = to, duration = duration, arc = arc, revealCell = revealCell, startScale = startScale, endScale = endScale };
            _glides.Add(g);
            return g;
        }

        void SparkBurst(Vector3 at, int count, Color color)
        {
            if (sparkSprite == null)
                return;
            float scale = panel.lossyScale.x;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Spark", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(panel, false);
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = Vector2.one * Random.Range(18f, 34f);
                rt.position = at;
                var img = go.GetComponent<Image>();
                img.sprite = sparkSprite;
                img.color = color;
                img.raycastTarget = false;
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float speed = Random.Range(220f, 520f) * scale;
                _sparks.Add(new Spark
                {
                    rt = rt, image = img, velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed + Vector2.up * 120f * scale,
                    life = Random.Range(0.45f, 0.8f), spin = Random.Range(-360f, 360f),
                });
            }
        }

        Sprite SpriteFor(BoardItem item)
        {
            if (item.IsGenerator)
                return item.GeneratorIndex < generatorIcons.Length ? generatorIcons[item.GeneratorIndex] : null;
            if (!item.IsItem || item.Chain >= chainIcons.Length)
                return null;
            var tiers = chainIcons[item.Chain].tiers;
            return item.Tier < tiers.Length ? tiers[item.Tier] : null;
        }

        // ------------------------------------------------------------------ Frame

        void Update()
        {
            float dt = UiTime.Delta;
            bool allowed = !TutorialFocus.Active;
            openButton.gameObject.SetActive(allowed && _open < 0.5f);
            if (!allowed && _isOpen)
                SetOpen(false);

            // Alert dot: never opened, or a renovation is ready to fix.
            bool ready = false;
            foreach (int r in _board.ActiveRenovations(tasks.Length))
                ready |= _board.CanRenovate(r);
            openAlert.SetActive(allowed && !_isOpen && (!_everOpened || ready));
            if (openAlert.activeSelf)
                openAlert.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 7f) * 0.12f);

            // Panel spring.
            float target = _isOpen ? 1f : 0f;
            _velocity += ((target - _open) * 240f - _velocity * 22f) * dt;
            _open += _velocity * dt;
            panel.anchoredPosition = Vector2.LerpUnclamped(_hiddenPos, _shownPos, _open);
            group.alpha = Mathf.Clamp01(_open * 3f);
            foreach (var g in hideWhileOpen)
                if (g != null) g.alpha = 1f - Mathf.Clamp01(_open * 2f);
            group.blocksRaycasts = group.interactable = _isOpen;
            dim.alpha = Mathf.Clamp01(_open);
            dim.blocksRaycasts = _isOpen;
            if (_open < 0.01f && !_isOpen)
                return;

            _comboTimer = Mathf.Max(0f, _comboTimer - dt);
            UpdateIdleHint(dt);
            UpdateDrag(dt);
            RefreshCells(dt);
            RefreshInfo();
            RefreshTasks(dt);
            AnimateEffects(dt);
        }

        void UpdateIdleHint(float dt)
        {
            if (_dragFrom >= 0 || !_isOpen)
            {
                _idle = 0f;
                _hintA = _hintB = -1;
                return;
            }
            _idle += dt;
            if (_idle > IdleHintAfter && _hintA < 0 && FindPair(out int a, out int b))
            {
                _hintA = a;
                _hintB = b;
            }
            if (_hintA >= 0 && (!CanMerge(_hintA, _hintB)))
                _hintA = _hintB = -1;
        }

        void UpdateDrag(float dt)
        {
            if (_dragFrom < 0)
                return;
            // Lift with a springy overshoot, tilt in the direction of travel.
            _dragLift = Mathf.MoveTowards(_dragLift, 1f, dt * 6f);
            float lift = 1f + 0.22f * Mathf.Sin(Mathf.Min(1f, _dragLift) * Mathf.PI * 0.75f) / Mathf.Sin(Mathf.PI * 0.75f);
            var delta = _dragPos - _lastDragPos;
            _lastDragPos = Vector3.Lerp(_lastDragPos, _dragPos, 1f - Mathf.Exp(-dt * 25f));
            _dragTilt = Mathf.Lerp(_dragTilt, Mathf.Clamp(-delta.x * 0.25f, -18f, 18f), 1f - Mathf.Exp(-dt * 12f));
            var t = dragGhost.transform;
            // A finger would hide the item, so on touch it rides a little above it; a mouse cursor sits right on it.
            t.position = _dragPos + (_touchDrag ? Vector3.up * 30f * panel.lossyScale.y : Vector3.zero);
            t.localScale = Vector3.one * lift * (_hover >= 0 && _hover != _dragFrom && CanMerge(_dragFrom, _hover) ? 1.1f : 1f);
            t.localRotation = Quaternion.Euler(0f, 0f, _dragTilt);
        }

        void RefreshCells(float dt)
        {
            float time = Time.unscaledTime;
            var dragItem = _dragFrom >= 0 ? _board[_dragFrom] : BoardItem.Empty;
            for (int i = 0; i < cells.Length; i++)
            {
                var item = _board[i];
                _hiddenUntilGlide[i] = Mathf.Max(0f, _hiddenUntilGlide[i] - dt);
                var sprite = item.IsEmpty ? null : SpriteFor(item);
                icons[i].enabled = sprite != null && _hiddenUntilGlide[i] <= 0f;
                icons[i].sprite = sprite;
                // The picked-up item leaves a faint copy behind.
                icons[i].color = i == _dragFrom ? new Color(1f, 1f, 1f, 0.3f) : Color.white;
                cellFrames[i].sprite = i == _selected && _dragFrom < 0 ? frameSelected : item.IsGenerator ? frameGenerator : frameNormal;

                // Glow: matching partners while dragging, the cell under the finger, the idle hint pair.
                bool partner = _dragFrom >= 0 && i != _dragFrom && CanMerge(_dragFrom, i);
                bool hovered = i == _hover && _dragFrom >= 0 && i != _dragFrom;
                bool hinted = i == _hintA || i == _hintB;
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * 8f);
                Color frame = Color.white;
                if (partner)
                    frame = Color.Lerp(Color.white, matchGlow, hovered ? 1f : 0.45f + 0.35f * pulse);
                else if (hovered)
                    frame = hoverGlow;
                else if (hinted)
                    frame = Color.Lerp(Color.white, matchGlow, 0.3f + 0.3f * pulse);
                cellFrames[i].color = frame;

                _pop[i] = Mathf.Max(0f, _pop[i] - dt * 3.2f);
                float p = _pop[i];
                // Squash-and-stretch pop: wide then tall.
                float sx = 1f + Mathf.Sin(p * Mathf.PI) * 0.28f;
                float sy = 1f + Mathf.Sin(p * Mathf.PI * 2f) * 0.12f;
                var scale = new Vector3(sx, sy, 1f);
                if (item.IsGenerator && _board.Energy > 0 && i != _selected)
                    scale *= 1f + Mathf.Sin(time * 3f + i) * 0.03f;
                if (partner)
                    scale *= 1f + 0.06f * pulse;
                var icon = icons[i].rectTransform;
                icon.localScale = scale;
                // The hint pair leans towards each other, like "put me there".
                if (hinted && _hintA >= 0)
                {
                    var other = i == _hintA ? _hintB : _hintA;
                    var dir = (Center(other) - Center(i)).normalized;
                    float nudge = Mathf.Max(0f, Mathf.Sin(time * 4f)) * 10f * panel.lossyScale.x;
                    icon.position = Center(i) + dir * nudge;
                    icon.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 8f) * 6f);
                }
                else
                {
                    icon.anchoredPosition = Vector2.zero;
                    icon.localRotation = Quaternion.identity;
                }
            }
        }

        void RefreshInfo()
        {
            int energy = _board.Energy;
            energyText.text = $"{energy}/{_board.Def.energyMax}";
            bool low = energy < refillBelow && !AdService.Busy;
            refillButton.gameObject.SetActive(low);
            energyTimer.gameObject.SetActive(!low);
            if (low)
                refillButton.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.05f);
            float next = _board.NextEnergyIn;
            energyTimer.text = energy >= _board.Def.energyMax ? "Full" : $"+1 in {Mathf.CeilToInt(next)}s";
            if (_noEnergyShake > 0f)
            {
                _noEnergyShake = Mathf.Max(0f, _noEnergyShake - UiTime.Delta * 2f);
                energyText.transform.localPosition = new Vector3(Mathf.Sin(_noEnergyShake * 40f) * 8f * _noEnergyShake, 0f, 0f);
            }
            cozinessText.text = $"Coziness +{Mathf.RoundToInt(_board.Coziness * 100f)}%";

            int shown = _dragFrom >= 0 ? _dragFrom : _selected;
            var item = shown >= 0 ? _board[shown] : BoardItem.Empty;
            if (item.IsItem)
            {
                int max = _board.MaxTier(item.Chain);
                string upgrade = item.Tier < max ? $"  ·  merge two for <b>{_board.Def.chains[item.Chain].tiers[item.Tier + 1]}</b>" : "  ·  top tier!";
                infoText.text = $"<b>{_board.NameOf(item)}</b>  Lv {item.Tier + 1}{upgrade}";
                sellButton.gameObject.SetActive(_dragFrom < 0);
                sellText.text = $"Sell +{NumberFormat.Short(_board.SellValue(item))}";
            }
            else if (item.IsGenerator)
            {
                var def = _board.Def.generators[item.GeneratorIndex];
                infoText.text = energy > 0
                    ? $"<b>{def.displayName}</b>  ·  tap to make {_board.Def.chains[def.chain].displayName.ToLowerInvariant()} (1 energy)"
                    : $"<b>{def.displayName}</b>  ·  out of energy, it refills over time";
                sellButton.gameObject.SetActive(false);
            }
            else
            {
                infoText.text = _board.EmptyCells == 0 ? "The board is full: sell or merge something to make room." : Hint;
                sellButton.gameObject.SetActive(false);
            }
        }

        void RefreshTasks(float dt)
        {
            var active = _board.ActiveRenovations(tasks.Length);
            for (int i = 0; i < tasks.Length; i++)
            {
                var task = tasks[i];
                int r = i < active.Count ? active[i] : -1;
                if (r != task.renovation)
                {
                    task.renovation = r;
                    task.slide = 1f;
                }
                task.root.gameObject.SetActive(r >= 0);
                if (r < 0)
                    continue;
                task.slide = Mathf.MoveTowards(task.slide, 0f, dt * 3f);
                task.root.localScale = Vector3.one * (1f - task.slide * task.slide * 0.6f);

                var def = _board.Def.renovations[r];
                task.title.text = def.displayName;
                task.bonus.text = $"+{Mathf.RoundToInt(def.coziness * 100f)}% cozy";
                // Group needs, so two of the same show once each with their own check.
                var counted = new Dictionary<int, int>();
                for (int n = 0; n < task.needIcons.Length; n++)
                {
                    bool used = n < def.needs.Count;
                    task.needFrames[n].gameObject.SetActive(used);
                    if (!used)
                        continue;
                    var need = def.needs[n];
                    var code = BoardItem.Item(need.chain, need.tier);
                    counted[code.code] = counted.TryGetValue(code.code, out int c) ? c + 1 : 1;
                    bool have = _board.Count(need) >= counted[code.code];
                    task.needIcons[n].sprite = SpriteFor(code);
                    task.needFrames[n].color = have ? readyFrame : missingFrame;
                    task.needChecks[n].SetActive(have);
                    // While dragging a needed item, its slot lights up.
                    bool draggingIt = _dragFrom >= 0 && _board[_dragFrom] == code;
                    task.needIcons[n].transform.localScale = Vector3.one * (draggingIt ? 1.15f + Mathf.Sin(Time.unscaledTime * 8f) * 0.05f : 1f);
                }
                bool can = _board.CanRenovate(r);
                task.fix.interactable = can;
                task.fixImage.sprite = can ? fixReady : fixWaiting;
                task.fixText.text = $"Fix  +{NumberFormat.Short(def.rewardCoins)}";
                task.fixText.color = can ? Color.white : new Color(0.45f, 0.3f, 0.2f);
                task.fix.transform.localScale = Vector3.one * (can ? 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.05f : 1f);
            }
        }

        void AnimateEffects(float dt)
        {
            for (int i = _glides.Count - 1; i >= 0; i--)
            {
                var g = _glides[i];
                g.t += dt / g.duration;
                float t = Mathf.Clamp01(g.t);
                float e = 1f - (1f - t) * (1f - t);
                var pos = Vector3.Lerp(g.from, g.to, e) + Vector3.up * Mathf.Sin(t * Mathf.PI) * g.arc * panel.lossyScale.y;
                g.image.transform.position = pos;
                g.image.transform.localScale = Vector3.one * Mathf.Lerp(g.startScale, g.endScale, e);
                g.image.transform.localRotation = Quaternion.identity;
                if (t < 1f)
                    continue;
                g.image.gameObject.SetActive(false);
                _flyerPool.Push(g.image);
                _glides.RemoveAt(i);
                if (g.revealCell >= 0)
                {
                    _hiddenUntilGlide[g.revealCell] = 0f;
                    _pop[g.revealCell] = Mathf.Max(_pop[g.revealCell], g.merge ? 1f : 0.6f);
                }
            }

            if (_burstT >= 0f)
            {
                _burstT += dt / 0.5f;
                float t = Mathf.Clamp01(_burstT);
                burst.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.8f, 1f - (1f - t) * (1f - t));
                burst.localRotation = Quaternion.Euler(0f, 0f, t * 120f);
                burst.GetComponent<Image>().color = new Color(1f, 0.92f, 0.55f, 1f - t);
                if (t >= 1f)
                {
                    _burstT = -1f;
                    burst.gameObject.SetActive(false);
                }
            }

            float gravity = 900f * panel.lossyScale.y;
            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                var s = _sparks[i];
                s.age += dt;
                s.velocity += Vector2.down * gravity * dt;
                s.velocity *= 1f - dt * 1.5f;
                s.rt.position += (Vector3)(s.velocity * dt);
                s.rt.localRotation = Quaternion.Euler(0f, 0f, s.age * s.spin);
                float k = s.age / s.life;
                s.rt.localScale = Vector3.one * (1f - k * 0.7f);
                var c = s.image.color;
                c.a = 1f - k * k;
                s.image.color = c;
                if (k >= 1f)
                {
                    Destroy(s.rt.gameObject);
                    _sparks.RemoveAt(i);
                }
            }

            if (_discoveryT >= 0f && discoveryText != null)
            {
                _discoveryT += dt;
                float inT = Mathf.Clamp01(_discoveryT / 0.25f);
                float outT = Mathf.Clamp01((_discoveryT - 1.6f) / 0.35f);
                float s = Mathf.LerpUnclamped(0.4f, 1f, 1f + Mathf.Sin(inT * Mathf.PI) * 0.25f * (1f - inT) - (1f - inT) * 0.6f);
                discoveryText.transform.localScale = Vector3.one * s;
                discoveryText.alpha = 1f - outT;
                if (_discoveryT > 2f)
                {
                    _discoveryT = -1f;
                    discoveryText.gameObject.SetActive(false);
                }
            }
        }
    }
}
