using MergeResto.Game.Views;
using MergeResto.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeResto.Game.UI
{
    /// <summary>
    /// The "Grand Opening": Grandma walks a new player through the bread line in six small
    /// lessons (flour, mixer, oven, first sale, first upgrade, first manager), with a spotlight
    /// on what to touch, a bouncing pointer, and a progress bar. If the chain stalls she points
    /// at whatever needs a tap instead, so the player is never stuck. Progress is saved.
    /// </summary>
    public class GrandOpeningTutorial : MonoBehaviour
    {
        /// <summary>Saved step value once the tutorial is over.</summary>
        public const int Finished = 1000;

        enum Kind { Talk, Tap, Serve, Upgrade, Manager, Reward }

        readonly struct Step
        {
            public readonly Kind kind;
            public readonly int station;
            public readonly string text;
            public Step(Kind kind, int station, string text) { this.kind = kind; this.station = station; this.text = text; }
            public bool Counts => kind != Kind.Talk && kind != Kind.Reward;
        }

        static string Key(string s) => $"<color=#C24E18>{s}</color>";

        static readonly Step[] Steps =
        {
            new(Kind.Talk, -1, "Welcome home, sweetie! Grandma's Bakery has been closed for years. Shall we open it again, together?"),
            new(Kind.Tap, 0, $"First things first: flour! Tap the {Key("flour sacks")} to unpack a delivery."),
            new(Kind.Tap, 1, $"Our baker carries the flour over all by himself. Now tap the {Key("mixer")} to knead some dough!"),
            new(Kind.Tap, 2, $"Lovely, soft dough! Tap the {Key("oven")} to bake our very first bread."),
            new(Kind.Serve, 3, $"Fresh bread goes to the {Key("counter")}, and I'll serve our first customer. Keep that bread coming!"),
            new(Kind.Upgrade, 2, $"Look at those coins! Let's make the oven faster: open the {Key("oven's upgrades")} and buy {Key("Hotter Oven")}."),
            new(Kind.Manager, 0, $"Tapping all day is tiring, dear. Hire a {Key("manager")} for the flour sacks. They'll work all on their own!"),
            new(Kind.Reward, -1, "Our Grand Opening is a success! Here's a little something from my savings. The place is still a bit dusty though: tap Renovate and let's fix it up!"),
        };

        static int CountedSteps
        {
            get
            {
                int n = 0;
                foreach (var s in Steps) if (s.Counts) n++;
                return n;
            }
        }

        [Header("Scene")]
        [SerializeField] StationView[] stations;
        [SerializeField] StationHud[] markers;
        [SerializeField] StationPanel panel;
        [SerializeField] CoinFlyLayer coinFly;
        [SerializeField] FxDirector fx;
        [SerializeField] ChibiAnimator grandma;
        [SerializeField] int rewardCoins = 250;
        [SerializeField] CustomerCrowd crowd;
        [SerializeField] CameraDirector cameraDirector;
        [Tooltip("Zoom (orthographic size) while following the first customer in.")]
        [SerializeField] float introZoom = 4.2f;

        [Header("UI")]
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
        [SerializeField] RectTransform progressRoot;
        [SerializeField] RectTransform progressFill;
        [SerializeField] TMP_Text progressText;
        [SerializeField] ButtonJuice progressJuice;

        const float DimSize = 6000f;
        static readonly int HoleId = Shader.PropertyToID("_Hole");

        ShopSim _sim;
        Material _dimMaterial;
        Camera _cam;
        int _entered = -1;
        string _shownText;
        float _typed;
        float _speechPop;
        float _fill;
        float _dimAlpha;
        Vector2 _holeCenter;
        Vector2 _holeRadius;
        float _pointerShown;
        float _closing = -1f;

        // Opening shot: the first customer walks in from the street before Grandma speaks.
        enum Intro { None, Waiting, Following, Returning, Done }
        Intro _intro = Intro.None;
        float _introTime;
        int _introCustomer = -1;

        struct Target
        {
            public bool has;
            public Vector2 tip;        // where the pointer touches down
            public Vector2 center;     // spotlight centre
            public Vector2 radius;     // spotlight radii
            public int station;        // station to glow, or -1
            public bool badge;         // show that station's upgrade badge
            public string helper;      // replaces the step text while redirecting
        }

        bool Done => _sim.State.tutorialStep >= Steps.Length;
        Step Current => Steps[_sim.State.tutorialStep];

        void Start()
        {
            _sim = GameController.Instance.Sim;
            _cam = Camera.main;
            if (Done)
            {
                gameObject.SetActive(false);
                return;
            }
            _dimMaterial = new Material(dim.material);
            dim.material = _dimMaterial;
            dim.rectTransform.sizeDelta = new Vector2(DimSize, DimSize);
            dimButton.onClick.AddListener(OnContinue);
            speechButton.onClick.AddListener(OnContinue);
            _sim.CycleStarted += OnCycleStarted;
            _sim.CustomerServed += OnServed;
            _sim.UpgradePurchased += OnUpgrade;
            _sim.ManagerHired += OnManager;
            _fill = CompletedCount() / (float)CountedSteps;
            TutorialFocus.Active = true;
            // Nobody walks out while a new player is still learning, and they come one at a time.
            _sim.PatienceScale = 0f;
            _sim.QueueLimit = 1;
            if (_sim.State.tutorialStep == 0 && _sim.State.customersServed == 0 && _sim.State.queue.Count == 0 && cameraDirector != null && crowd != null)
            {
                _intro = Intro.Waiting;
                _sim.CustomerArrived += OnIntroCustomer;
                _sim.State.customerTimer = _sim.CustomerInterval; // the first customer arrives right away
                group.alpha = 0f;
                cameraDirector.Focus(crowd.entryPath[0].position, introZoom);
                cameraDirector.Snap();
            }
            else
                _intro = Intro.Done;
        }

        void OnDestroy()
        {
            if (_sim != null)
            {
                _sim.PatienceScale = 1f;
                _sim.QueueLimit = -1;
                _sim.CustomerArrived -= OnIntroCustomer;
            }
            TutorialFocus.Active = false;
            TutorialFocus.Station = -1;
            if (_sim == null)
                return;
            _sim.CycleStarted -= OnCycleStarted;
            _sim.CustomerServed -= OnServed;
            _sim.UpgradePurchased -= OnUpgrade;
            _sim.ManagerHired -= OnManager;
        }

        // ------------------------------------------------------------------ Progress

        int CompletedCount()
        {
            int n = 0;
            for (int i = 0; i < Steps.Length && i < _sim.State.tutorialStep; i++)
                if (Steps[i].Counts) n++;
            return n;
        }

        void Advance()
        {
            if (Done)
                return;
            bool counted = Current.Counts;
            _sim.State.tutorialStep++;
            if (_sim.State.tutorialStep >= Steps.Length)
                _sim.State.tutorialStep = Finished;
            if (counted && progressJuice != null)
                progressJuice.Pop(8f);
            GameController.Instance.Save();
        }

        void OnCycleStarted(int station, int machine)
        {
            if (!Done && Current.kind == Kind.Tap && Current.station == station)
                Advance();
        }

        void OnServed(CustomerState customer, double coins)
        {
            if (!Done && Current.kind == Kind.Serve)
                Advance();
        }

        void OnUpgrade(int station, UpgradeTrack track)
        {
            if (!Done && Current.kind == Kind.Upgrade)
            {
                GameController.Instance.ClearSelection();
                Advance();
            }
        }

        void OnManager(int station)
        {
            if (!Done && Current.kind == Kind.Manager)
            {
                GameController.Instance.ClearSelection();
                Advance();
            }
        }

        void OnContinue()
        {
            if (Done || _closing >= 0f)
                return;
            if (_typed < SpeechLength())
            {
                _typed = 9999f; // finish the line first
                return;
            }
            var kind = Current.kind;
            if (kind == Kind.Reward)
                GiveReward();
            if (kind == Kind.Talk || kind == Kind.Reward)
                Advance();
        }

        void GiveReward()
        {
            AudioDirector.Play(Sfx.Celebrate);
            _sim.AddCoins(rewardCoins);
            var counter = stations[stations.Length - 1];
            if (coinFly != null)
                coinFly.Fly(counter.hudAnchor.position, 10);
            if (fx != null)
                fx.Celebrate(counter.hudAnchor.position);
            if (grandma != null)
                grandma.Cheer();
        }

        // ------------------------------------------------------------------ Intro

        void OnIntroCustomer(CustomerState customer)
        {
            if (_intro == Intro.Waiting)
                _introCustomer = customer.id;
        }

        // Returns true while the opening shot is still playing (the lesson UI stays hidden).
        bool PlayIntro(float dt)
        {
            if (_intro == Intro.Done)
                return false;
            _introTime += dt;
            group.alpha = Mathf.MoveTowards(group.alpha, 0f, dt * 4f);
            switch (_intro)
            {
                case Intro.Waiting:
                    var who = _introCustomer >= 0 ? crowd.CharacterFor(_introCustomer) : null;
                    if (who != null)
                    {
                        cameraDirector.Follow(who, introZoom);
                        _intro = Intro.Following;
                        _introTime = 0f;
                    }
                    else if (_introTime > 3f)
                        _intro = Intro.Returning;
                    break;
                case Intro.Following:
                    var c = _sim.State.queue.Find(q => q.id == _introCustomer);
                    // Hold on them until they reach the counter (or a few seconds pass).
                    if (c == null || c.walkLeft <= 0.3f || _introTime > 7f)
                    {
                        if (grandma != null)
                            cameraDirector.Focus(grandma.transform.position + Vector3.up * 0.5f, introZoom);
                        _intro = Intro.Returning;
                        _introTime = 0f;
                    }
                    break;
                case Intro.Returning:
                    // A beat on Grandma, then back to the whole shop.
                    if (_introTime > 1.1f)
                        cameraDirector.Release();
                    if (_introTime > 2.2f)
                    {
                        _intro = Intro.Done;
                        _sim.CustomerArrived -= OnIntroCustomer;
                        return false;
                    }
                    break;
            }
            return true;
        }

        // ------------------------------------------------------------------ Frame

        void Update()
        {
            float dt = UiTime.Delta;
            if (Done)
            {
                Close(dt);
                return;
            }
            if (PlayIntro(dt))
                return;
            group.alpha = Mathf.MoveTowards(group.alpha, 1f, dt * 3f);

            int index = _sim.State.tutorialStep;
            if (index != _entered)
                Enter(index);

            var step = Current;
            var target = Resolve(step);
            TutorialFocus.Station = target.station;
            TutorialFocus.ShowBadge = target.badge;

            // Speech: the step's line, or a helper line while redirecting.
            string text = target.helper ?? step.text;
            if (text != _shownText)
            {
                bool fresh = _shownText == null || target.helper == null;
                _shownText = text;
                speechText.text = text;
                _typed = fresh ? 0f : 9999f;
                _speechPop = 1f;
                AudioDirector.Play(Sfx.Blip, 1.1f, 0.6f);
            }
            _typed += dt * 60f;
            speechText.maxVisibleCharacters = Mathf.Min((int)_typed, 99999);
            bool talking = _typed < SpeechLength();
            bool waitsForTap = step.kind == Kind.Talk || step.kind == Kind.Reward;
            continueHint.SetActive(waitsForTap && !talking);
            _speechPop = Mathf.Max(0f, _speechPop - dt * 4f);
            speech.localScale = Vector3.one * (1f + Mathf.Sin(_speechPop * Mathf.PI) * 0.05f);
            // Bottom left, or bottom right while the upgrade card (bottom left) is out.
            bool cardOut = GameController.Instance.SelectedStation >= 0;
            float rightX = layer.rect.width - speech.rect.width - 36f;
            float wantX = cardOut ? rightX : 36f;
            var sp = speech.anchoredPosition;
            sp.x = Mathf.Lerp(sp.x, wantX, 1f - Mathf.Exp(-dt * 9f));
            speech.anchoredPosition = sp;
            portrait.localRotation = Quaternion.Euler(0f, 0f, talking ? Mathf.Sin(Time.unscaledTime * 14f) * 4f : 0f);

            // Dim and spotlight.
            dim.raycastTarget = waitsForTap;
            float alpha = waitsForTap ? 0.55f : target.has ? 0.5f : 0.15f;
            _dimAlpha = Mathf.MoveTowards(_dimAlpha, alpha, dt * 1.5f);
            var c = _dimMaterial.color;
            c.a = _dimAlpha;
            _dimMaterial.color = c;
            Vector2 wantCenter = target.has ? target.center : _holeCenter;
            Vector2 wantRadius = target.has ? target.radius : Vector2.zero;
            float k = 1f - Mathf.Exp(-dt * 10f);
            _holeCenter = Vector2.Lerp(_holeCenter, wantCenter, k);
            _holeRadius = Vector2.Lerp(_holeRadius, wantRadius, k);
            dim.rectTransform.anchoredPosition = _holeCenter;
            _dimMaterial.SetVector(HoleId, new Vector4(0.5f, 0.5f, _holeRadius.x / DimSize, _holeRadius.y / DimSize));

            // Pointer bouncing onto the target, with a ping ring.
            _pointerShown = Mathf.MoveTowards(_pointerShown, target.has ? 1f : 0f, dt * 5f);
            pointer.gameObject.SetActive(_pointerShown > 0.01f);
            ping.gameObject.SetActive(_pointerShown > 0.01f);
            if (target.has)
            {
                float hop = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.4f));
                pointer.anchoredPosition = target.tip + new Vector2(0f, hop * 24f);
                ping.anchoredPosition = target.tip;
            }
            float pop = Mathf.SmoothStep(0f, 1f, _pointerShown);
            pointer.localScale = Vector3.one * pop;
            float phase = Mathf.Repeat(Time.unscaledTime * 0.9f, 1f);
            ping.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.5f, phase) * pop;
            var pingImage = ping.GetComponent<Graphic>();
            if (pingImage != null)
                pingImage.color = new Color(1f, 0.95f, 0.8f, (1f - phase) * 0.9f);

            // Progress bar.
            float want = CompletedCount() / (float)CountedSteps;
            _fill = Mathf.MoveTowards(_fill, want, dt * 0.8f);
            progressFill.anchorMax = new Vector2(Mathf.Max(0.001f, _fill), 1f);
            progressText.text = $"Grand Opening  {CompletedCount()}/{CountedSteps}";
        }

        void Enter(int index)
        {
            _entered = index;
            _shownText = null;
            if (grandma != null && (Steps[index].kind == Kind.Talk || Steps[index].kind == Kind.Reward))
                grandma.Wave();
        }

        int SpeechLength() => speechText.textInfo != null ? speechText.textInfo.characterCount : 0;

        void Close(float dt)
        {
            if (_closing < 0f)
            {
                _closing = 0f;
                _sim.PatienceScale = 1f;
                _sim.QueueLimit = -1;
                // Customers who waited through the lesson start fresh, one after another.
                for (int i = 0; i < _sim.State.queue.Count; i++)
                    _sim.State.queue[i].patienceLeft = _sim.Def.patienceSeconds + i * 4f;
                TutorialFocus.Active = false;
                TutorialFocus.Station = -1;
                TutorialFocus.ShowBadge = false;
            }
            _closing += dt;
            group.alpha = Mathf.Clamp01(1f - _closing / 0.5f);
            group.blocksRaycasts = false;
            if (_closing > 0.5f)
                gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ Targets

        Target Resolve(Step step)
        {
            switch (step.kind)
            {
                case Kind.Tap:
                {
                    int s = step.station;
                    if (_sim.NeedsTap(s))
                        return AtStation(s);
                    if (Busy(s))
                        return new Target { station = s, helper = "That's it! Just a moment..." };
                    int feed = Feeder(s);
                    if (feed >= 0)
                        return AtStation(feed, $"We need more {Goods(feed)} first. Tap the {Key(Name(feed))}!");
                    return new Target { station = s, helper = "Our baker is on the way with the goods..." };
                }
                case Kind.Serve:
                {
                    int feed = Feeder(stations.Length - 1);
                    if (feed >= 0)
                        return AtStation(feed, $"Keep that bread coming: tap the {Key(Name(feed))}!");
                    var t = AtStation(step.station);
                    return t;
                }
                case Kind.Upgrade:
                    return UpgradeTarget(step.station, false);
                case Kind.Manager:
                    return UpgradeTarget(ManagerStation(), true);
                default:
                    return default;
            }
        }

        Target UpgradeTarget(int s, bool manager)
        {
            if (s < 0)
            {
                Advance(); // every station already has a manager
                return default;
            }
            if (manager && !_sim.CanHireManager(s) && !_sim.State.stations[s].hasManager)
                _sim.State.gems = Mathf.Max(_sim.State.gems, _sim.ManagerCost(s)); // Grandma's gift, never stuck

            if (!manager && !_sim.CanBuyUpgrade(s, UpgradeTrack.Speed))
            {
                int feed = Feeder(stations.Length);
                string cost = NumberFormat.Short(_sim.UpgradeCost(s, UpgradeTrack.Speed));
                return feed >= 0
                    ? AtStation(feed, $"Let's earn {cost} coins first. Tap the {Key(Name(feed))}!")
                    : new Target { station = -1, helper = $"Let's earn {cost} coins first. Customers are on their way!" };
            }

            int open = panel.OpenStation;
            if (open == s)
            {
                var button = manager ? panel.ManagerButton : panel.ButtonFor(UpgradeTrack.Speed);
                var t = AtRect(button);
                t.station = s;
                return t;
            }
            if (GameController.Instance.SelectedStation >= 0 && GameController.Instance.SelectedStation != s)
                GameController.Instance.ClearSelection();

            var badge = markers[s].UpgradeBadge;
            if (badge != null)
            {
                var t = AtRect(badge);
                t.station = s;
                t.badge = true;
                return t;
            }
            return new Target { station = s, badge = true };
        }

        int ManagerStation()
        {
            int preferred = Current.station;
            if (preferred >= 0 && !_sim.State.stations[preferred].hasManager)
                return preferred;
            for (int i = 0; i < stations.Length; i++)
                if (!_sim.State.stations[i].hasManager)
                    return i;
            return -1;
        }

        bool Busy(int s)
        {
            var st = _sim.State.stations[s];
            if (st.queuedStarts > 0)
                return true;
            foreach (var m in st.machines)
                if (m.running) return true;
            return false;
        }

        // The closest station before `s` in the chain that is waiting for a tap.
        int Feeder(int s)
        {
            for (int j = Mathf.Min(s, stations.Length) - 1; j >= 0; j--)
                if (_sim.NeedsTap(j))
                    return j;
            return -1;
        }

        string Name(int s) => s switch
        {
            0 => "flour sacks",
            1 => "mixer",
            2 => "oven",
            _ => _sim.Def.stations[s].displayName.ToLowerInvariant(),
        };

        string Goods(int s) => s switch
        {
            0 => "flour",
            1 => "dough",
            _ => "bread",
        };

        Target AtStation(int s, string helper = null)
        {
            var view = stations[s];
            var t = new Target { has = true, station = s, helper = helper };
            var anchor = view.tapAnchor != null ? view.tapAnchor.position : view.hudAnchor.position;
            if (!ToLayer(anchor, out t.tip))
                return new Target { station = s, helper = helper };

            // Spotlight around the first machine (or the whole station when it is staffed).
            if (FocusBounds(view, out var b))
            {
                Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    if (ToLayer(corner, out var p))
                    {
                        min = Vector2.Min(min, p);
                        max = Vector2.Max(max, p);
                    }
                }
                t.center = (min + max) * 0.5f;
                t.radius = (max - min) * 0.5f * 0.75f + new Vector2(55f, 55f);
            }
            else
            {
                t.center = t.tip;
                t.radius = new Vector2(150f, 150f);
            }
            return t;
        }

        static bool FocusBounds(StationView view, out Bounds bounds)
        {
            bounds = default;
            var first = view.machineVisuals != null && view.machineVisuals.Length > 0 ? view.machineVisuals[0] : null;
            if (first != null && first.GetComponentInChildren<ChibiAnimator>(true) == null)
            {
                bool any = false;
                foreach (var r in first.GetComponentsInChildren<Renderer>())
                {
                    if (!any) { bounds = r.bounds; any = true; }
                    else bounds.Encapsulate(r.bounds);
                }
                if (any)
                    return true;
            }
            if (view.TryGetComponent<BoxCollider>(out var box))
            {
                bounds = box.bounds;
                return true;
            }
            return false;
        }

        Target AtRect(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector2 min = layer.InverseTransformPoint(corners[0]);
            Vector2 max = layer.InverseTransformPoint(corners[2]);
            var t = new Target { has = true, station = -1 };
            t.center = (min + max) * 0.5f;
            t.radius = (max - min) * 0.5f * 1.25f + new Vector2(18f, 18f);
            t.tip = new Vector2(t.center.x, max.y - 6f);
            return t;
        }

        bool ToLayer(Vector3 world, out Vector2 local)
        {
            Vector3 screen = _cam.WorldToScreenPoint(world);
            local = default;
            return screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, null, out local);
        }
    }
}
