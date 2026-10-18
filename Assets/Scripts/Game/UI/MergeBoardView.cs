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
        [SerializeField] CoinFlyLayer coinFly;

        const string Hint = "Tap a <color=#C24E18>generator</color> to make items, then drag two matching items together to <color=#C24E18>merge</color> them!";

        MergeBoard _board;
        float _open;
        float _velocity;
        bool _isOpen;
        bool _everOpened;
        Vector2 _shownPos;
        Vector2 _hiddenPos;
        int _selected = -1;
        int _dragFrom = -1;
        float[] _pop;
        float _flyT = -1f;
        Vector2 _flyFrom, _flyTo;
        int _flyCell = -1;
        float _burstT = -1f;
        float _noEnergyShake;

        public bool IsOpen => _isOpen;

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
            _everOpened = PlayerPrefs.GetInt("board_opened", 0) == 1;

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

            _board.ItemsMerged += OnMerged;
            GameController.Instance.StationSelected += OnStationSelected;
        }

        void OnDestroy()
        {
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
            if (open != _isOpen)
                AudioDirector.Play(Sfx.Whoosh, open ? 1.05f : 0.9f, 0.5f);
            _isOpen = open;
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
                CancelDrag();
            }
        }

        // ------------------------------------------------------------------ Input

        public void OnCellTapped(int cell)
        {
            var item = _board[cell];
            if (item.IsGenerator)
            {
                if (_board.Spawn(cell, out int target))
                {
                    _pop[cell] = 0.6f;
                    StartFly(cell, target);
                }
                else
                {
                    if (_board.Energy <= 0)
                        _noEnergyShake = 1f;
                    AudioDirector.Play(Sfx.Deny);
                }
                _selected = cell;
                return;
            }
            _selected = item.IsEmpty ? -1 : cell;
            if (!item.IsEmpty)
                _pop[cell] = 0.4f;
        }

        public void OnDragBegin(int cell, PointerEventData e)
        {
            if (_board[cell].IsEmpty)
                return;
            _dragFrom = cell;
            _selected = cell;
            dragGhost.sprite = SpriteFor(_board[cell]);
            dragGhost.gameObject.SetActive(true);
            dragGhost.transform.SetAsLastSibling();
            OnDragMove(e);
        }

        public void OnDragMove(PointerEventData e)
        {
            if (_dragFrom < 0)
                return;
            var parent = (RectTransform)dragGhost.transform.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, e.position, null, out var local))
                dragGhost.rectTransform.anchoredPosition = local + new Vector2(0f, 14f);
        }

        public void OnDragEnd(PointerEventData e)
        {
            if (_dragFrom < 0)
                return;
            int target = CellAt(e.position);
            int from = _dragFrom;
            CancelDrag();
            if (target < 0 || target == from)
                return;
            var result = _board.Move(from, target);
            if (result != MoveResult.None)
            {
                _selected = target;
                _pop[target] = Mathf.Max(_pop[target], 0.5f);
                if (result == MoveResult.Swapped)
                    _pop[from] = 0.5f;
            }
        }

        void CancelDrag()
        {
            _dragFrom = -1;
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
            var from = RectTransformUtility.WorldToScreenPoint(null, cells[_selected].position);
            if (_board.Sell(_selected))
            {
                AudioDirector.Play(Sfx.Sale);
                if (coinFly != null)
                    coinFly.FlyFromScreen(from, 3);
            }
            _selected = -1;
        }

        void Fix(Task task)
        {
            if (task.renovation < 0 || !_board.Renovate(task.renovation))
                return;
            if (coinFly != null)
                coinFly.FlyFromScreen(RectTransformUtility.WorldToScreenPoint(null, task.fix.transform.position), 10);
            task.renovation = -1;
            if (_selected >= 0 && _board[_selected].IsEmpty)
                _selected = -1;
        }

        void OnMerged(int cell, BoardItem item)
        {
            _pop[cell] = 1f;
            _burstT = 0f;
            burst.position = cells[cell].position;
            burst.gameObject.SetActive(true);
        }

        void StartFly(int fromCell, int toCell)
        {
            var parent = (RectTransform)flyer.transform.parent;
            _flyFrom = parent.InverseTransformPoint(cells[fromCell].position);
            _flyTo = parent.InverseTransformPoint(cells[toCell].position);
            _flyCell = toCell;
            _flyT = 0f;
            flyer.sprite = SpriteFor(_board[toCell]);
            flyer.gameObject.SetActive(true);
            flyer.transform.SetAsLastSibling();
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

            RefreshCells(dt);
            RefreshInfo();
            RefreshTasks(dt);
            AnimateEffects(dt);
        }

        void RefreshCells(float dt)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                var item = _board[i];
                bool hidden = i == _dragFrom || (i == _flyCell && _flyT >= 0f);
                var sprite = item.IsEmpty ? null : SpriteFor(item);
                icons[i].enabled = sprite != null && !hidden;
                icons[i].sprite = sprite;
                cellFrames[i].sprite = i == _selected ? frameSelected : item.IsGenerator ? frameGenerator : frameNormal;

                _pop[i] = Mathf.Max(0f, _pop[i] - dt * 3.5f);
                float p = _pop[i];
                float scale = 1f + Mathf.Sin(p * Mathf.PI) * 0.22f;
                if (item.IsGenerator && _board.Energy > 0 && i != _selected)
                    scale *= 1f + Mathf.Sin(Time.unscaledTime * 3f + i) * 0.03f;
                icons[i].rectTransform.localScale = Vector3.one * scale;
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

            var item = _selected >= 0 ? _board[_selected] : BoardItem.Empty;
            if (item.IsItem)
            {
                int max = _board.MaxTier(item.Chain);
                string upgrade = item.Tier < max ? $"  ·  merge two for <b>{_board.Def.chains[item.Chain].tiers[item.Tier + 1]}</b>" : "  ·  top tier!";
                infoText.text = $"<b>{_board.NameOf(item)}</b>  Lv {item.Tier + 1}{upgrade}";
                sellButton.gameObject.SetActive(true);
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
            if (_flyT >= 0f)
            {
                _flyT += dt / 0.28f;
                float t = Mathf.Clamp01(_flyT);
                var pos = Vector2.Lerp(_flyFrom, _flyTo, 1f - (1f - t) * (1f - t));
                pos.y += Mathf.Sin(t * Mathf.PI) * 60f;
                flyer.rectTransform.anchoredPosition = pos;
                flyer.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, t);
                if (t >= 1f)
                {
                    _flyT = -1f;
                    flyer.gameObject.SetActive(false);
                    if (_flyCell >= 0)
                        _pop[_flyCell] = 0.7f;
                    _flyCell = -1;
                }
            }
            if (_burstT >= 0f)
            {
                _burstT += dt / 0.45f;
                float t = Mathf.Clamp01(_burstT);
                burst.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.6f, t);
                burst.localRotation = Quaternion.Euler(0f, 0f, t * 90f);
                burst.GetComponent<Image>().color = new Color(1f, 0.9f, 0.5f, 1f - t);
                if (t >= 1f)
                {
                    _burstT = -1f;
                    burst.gameObject.SetActive(false);
                }
            }
        }
    }
}
