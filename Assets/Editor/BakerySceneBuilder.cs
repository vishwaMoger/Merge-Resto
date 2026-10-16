using System.Collections.Generic;
using System.Linq;
using MergeResto.Game;
using MergeResto.Game.UI;
using MergeResto.Game.Views;
using MergeResto.Sim;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace MergeResto.EditorTools
{
    /// <summary>
    /// Builds the playable Grandma's Bakery scene: room, production chain, staff, customers,
    /// game controller and HUD. Re-run after changing layout code; the config asset is kept.
    /// </summary>
    public static class BakerySceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Bakery.unity";
        const string ConfigPath = "Assets/Game/Config/GrandmasBakery.asset";
        const string UiFolder = "Assets/Art/UI";
        const string BakeryArt = "Assets/Art/ThirdParty/TinyTreats_BakeryInterior/Models/";
        const string GoodsArt = "Assets/Art/ThirdParty/TinyTreats_BakedGoods/Models/";
        const float CharacterScale = 1.8f;
        const float WalkSpeed = 2.4f;

        [MenuItem("Merge Resto/Game/Build Bakery Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            LookSetup.InstallRendering();
            LoadOrCreateConfig();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Opening a scene unloads unreferenced assets, so fetch the config afterwards.
            var config = AssetDatabase.LoadAssetAtPath<ShopConfig>(ConfigPath);
            var cam = LookSetup.CreateLookRig(new Vector3(0f, 1f, 0f), 8.6f);

            var world = new GameObject("Bakery").transform;
            BuildRoom(world);

            var stations = new StationView[4];
            stations[0] = BuildFlourStorage(world);
            stations[1] = BuildMixer(world);
            stations[2] = BuildOven(world);
            stations[3] = BuildCounter(world);
            for (int i = 0; i < stations.Length; i++)
                stations[i].stationIndex = i;

            UpdateCourierTimes(config, stations);
            BuildCouriers(world, stations);
            var crowd = BuildCustomers(world);
            BuildDining(world);

            var game = new GameObject("Game");
            var controller = game.AddComponent<GameController>();
            SetPrivate(controller, "config", config);
            var input = game.AddComponent<InputController>();
            SetPrivate(input, "worldCamera", cam);

            BuildHud(stations, crowd);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Merge Resto] Bakery scene saved to {ScenePath}.");
        }

        // ------------------------------------------------------------------ Config

        static ShopConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<ShopConfig>(ConfigPath);
            if (config != null)
                return config;

            LookSetup.EnsureFolder("Assets/Game/Config");
            config = ScriptableObject.CreateInstance<ShopConfig>();
            var shop = config.shop;
            shop.id = "grandmas_bakery";
            shop.displayName = "Grandma's Bakery";
            shop.customerIntervalSeconds = 4.5f;
            shop.itemsPerOrder = 2;
            shop.pricePerItem = 25;
            shop.patienceSeconds = 22f;
            shop.startCoins = 200;
            shop.startGems = 20;

            shop.stations.Add(Station("flour", "Flour Storage", StationKind.Source, 2.5f, 6, 20,
                "Quick Unpacking", "Bigger Sacks", "Extra Storage"));
            shop.stations.Add(Station("mixer", "Dough Mixer", StationKind.Processor, 3f, 6, 30,
                "Faster Mixing", "Bigger Bowl", "Add Mixer"));
            shop.stations.Add(Station("oven", "Bread Oven", StationKind.Processor, 4f, 6, 40,
                "Hotter Oven", "Bigger Trays", "Add Oven"));
            var counter = Station("counter", "Front Counter", StationKind.Seller, 1.2f, 0, 0,
                "Friendly Service", "Fancy Bread", "Add Cashier");
            counter.startsAutomated = true;
            shop.stations.Add(counter);
            for (int i = 0; i < 3; i++)
                shop.couriers.Add(new CourierDefinition { carry = 6, walkSeconds = 1.5f });

            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        static StationDefinition Station(string id, string name, StationKind kind, float cycle, int batch, int managerGems,
            string speedName, string outputName, string machinesName)
        {
            var def = new StationDefinition
            {
                id = id,
                displayName = name,
                kind = kind,
                baseCycleSeconds = cycle,
                baseBatch = batch,
                outputCapacity = 24,
                inputCapacity = 24,
                managerGemCost = managerGems,
            };
            def.Upgrade(UpgradeTrack.Speed).displayName = speedName;
            def.Upgrade(UpgradeTrack.Output).displayName = outputName;
            def.Upgrade(UpgradeTrack.Machines).displayName = machinesName;
            return def;
        }

        // Courier walk times follow the real distances in the room.
        static void UpdateCourierTimes(ShopConfig config, StationView[] stations)
        {
            for (int i = 0; i < config.shop.couriers.Count; i++)
            {
                float d = Vector3.Distance(stations[i].workPoint.position, stations[i + 1].workPoint.position);
                config.shop.couriers[i].walkSeconds = Mathf.Max(0.6f, d / WalkSpeed);
            }
            EditorUtility.SetDirty(config);
        }

        // ------------------------------------------------------------------ Room

        static void BuildRoom(Transform world)
        {
            var room = Group("Room", world);
            for (int x = -7; x <= 7; x += 2)
            for (int z = -5; z <= 5; z += 2)
                Place(z >= 2 ? "floor_wood" : "floor_tiled", room, new Vector3(x, 0f, z));

            // Back wall along z = 6 (kitchen side), windows behind the counter.
            for (int x = -7; x <= 7; x += 2)
            {
                string piece = x == 5 || x == 7 ? "wall_modular_panelled_bakery_window_large_A" : "wall_modular_panelled_bakery_straight_A";
                Place(piece, room, new Vector3(x, 0f, 6f), 180f);
            }
            // Left wall along x = -8 (dining side).
            for (int z = -5; z <= 5; z += 2)
            {
                string piece = z == -3 || z == -1 ? "wall_modular_panelled_bakery_window_large_A" : "wall_modular_panelled_bakery_straight_A";
                Place(piece, room, new Vector3(-8f, 0f, z), 90f);
            }

            Place("wall_sign_large", room, new Vector3(4f, 3.3f, 5.95f), 180f);
            Place("wall_shelf_bakery_A", room, new Vector3(-7.95f, 2.4f, 2f), 90f);
            Place("tin_A_beige", room, new Vector3(-7.6f, 2.45f, 1.5f));
            Place("tin_A_brown", room, new Vector3(-7.6f, 2.45f, 2.1f));
            Place("cookie_jar", room, new Vector3(-7.6f, 2.45f, 2.6f));
            Place("rug", room, new Vector3(-3.5f, 0f, -3f));
        }

        // ------------------------------------------------------------------ Stations

        static StationView NewStation(Transform world, string name, Vector3 pos, Vector3 workPoint, float hudHeight)
        {
            var go = new GameObject(name);
            go.transform.SetParent(world, false);
            go.transform.position = pos;
            var view = go.AddComponent<StationView>();
            view.workPoint = Marker("WorkPoint", go.transform, workPoint);
            view.hudAnchor = Marker("HudAnchor", go.transform, pos + Vector3.up * hudHeight);
            return view;
        }

        static StationView BuildFlourStorage(Transform world)
        {
            var view = NewStation(world, "FlourStorage", new Vector3(-6.4f, 0f, 4.7f), new Vector3(-6.0f, 0f, 3.0f), 2.2f);
            var t = view.transform;
            var slots = new List<GameObject>();
            for (int m = 0; m < 3; m++)
            {
                var pile = Group($"SackPile{m}", t);
                pile.localPosition = new Vector3(-0.6f + m * 1.0f, 0f, 0f);
                Place("flour_sack_closed", pile, Vector3.zero + new Vector3(-0.3f, 0f, 0f), 10f, local: true);
                Place("flour_sack_closed", pile, new Vector3(0.3f, 0f, 0.05f), -15f, local: true);
                Place("flour_sack_closed", pile, new Vector3(0f, 0.5f, 0f), 5f, local: true);
                slots.Add(pile.gameObject);
            }
            view.machineVisuals = slots.ToArray();
            view.stockVisuals = Row("flour_sack_open", t, new Vector3(-0.6f, 0f, -1.1f), new Vector3(0.55f, 0f, 0f), 4, 20f);
            AddClickCollider(view);
            return view;
        }

        static StationView BuildMixer(Transform world)
        {
            var view = NewStation(world, "Mixer", new Vector3(-3f, 0f, 5f), new Vector3(-3f, 0f, 3.2f), 2.6f);
            var t = view.transform;
            Place("countertop_straight_A_large", t, new Vector3(-0.5f, 0f, 0f), 180f, local: true);
            Place("countertop_straight_A_short", t, new Vector3(1f, 0f, 0f), 180f, local: true);
            var slots = new List<GameObject>();
            for (int m = 0; m < 3; m++)
                slots.Add(Place("stand_mixer", t, new Vector3(-1f + m * 1f, 1f, 0.1f), 180f, local: true));
            view.machineVisuals = slots.ToArray();
            view.stockVisuals = Row("dough_ball", t, new Vector3(-1.1f, 1f, -0.55f), new Vector3(0.45f, 0f, 0f), 6, 0f);
            AddClickCollider(view);
            return view;
        }

        static StationView BuildOven(Transform world)
        {
            var view = NewStation(world, "Oven", new Vector3(1f, 0f, 5f), new Vector3(1f, 0f, 3.2f), 3.4f);
            var t = view.transform;
            var slots = new List<GameObject>();
            float[] xs = { -0.2f, 2.1f, 4.4f };
            for (int m = 0; m < 3; m++)
                slots.Add(Place("bread_oven", t, new Vector3(xs[m], 0f, 0.1f), 180f, local: true));
            view.machineVisuals = slots.ToArray();

            // Cooling basket in front holds finished bread.
            Place("basket_B", t, new Vector3(1.1f, 0f, -1.2f), 0f, local: true);
            view.stockVisuals = Row("bread", GoodsArt, t, new Vector3(0.8f, 0.25f, -1.2f), new Vector3(0.3f, 0f, 0f), 3, 90f);

            var glow = new GameObject("OvenGlow");
            glow.transform.SetParent(t, false);
            glow.transform.localPosition = new Vector3(-0.2f, 1.2f, -1.2f);
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.25f);
            light.range = 3.5f;
            light.intensity = 2.2f;
            light.shadows = LightShadows.None;
            glow.SetActive(false);
            view.runningEffects = new[] { glow };
            AddClickCollider(view);
            return view;
        }

        static StationView BuildCounter(Transform world)
        {
            var view = NewStation(world, "Counter", new Vector3(4f, 0f, 0.8f), new Vector3(3f, 0f, 2.6f), 2.8f);
            var t = view.transform;
            Place("display_case_long", t, new Vector3(-1f, 0f, 0f), 0f, local: true);
            Place("countertop_counter_outer", t, new Vector3(0.75f, 0f, 0f), 0f, local: true);
            Place("countertop_straight_A_short", t, new Vector3(2f, 0f, 0f), 0f, local: true);
            Place("cash_register", t, new Vector3(0.75f, 1f, 0.1f), 180f, local: true);
            Place("pricing_card", t, new Vector3(-1.6f, 1.55f, -0.4f), 0f, local: true);

            var shelfA = Row("bread", GoodsArt, t, new Vector3(-1.65f, 0.55f, -0.1f), new Vector3(0.33f, 0f, 0f), 4, 90f);
            var shelfB = Row("croissant", GoodsArt, t, new Vector3(-1.65f, 0.95f, -0.1f), new Vector3(0.33f, 0f, 0f), 4, 0f);
            view.stockVisuals = shelfA.Concat(shelfB).ToArray();

            // Cashiers behind the counter, one per "machine".
            var cashiers = new List<GameObject>();
            float[] xs = { 0.75f, 2f, -0.4f };
            for (int m = 0; m < 3; m++)
            {
                var c = LookSetup.Chibi($"Cashier{m}", t, LookSetup.Hex("F58F7C"), LookSetup.Hex("F4C9A6"), chefHat: false);
                c.transform.localPosition = new Vector3(xs[m], 0f, 1.25f);
                c.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                c.transform.localScale = Vector3.one * CharacterScale;
                var anim = c.AddComponent<ChibiAnimator>();
                anim.body = c.transform.Find("Body");
                cashiers.Add(c);
            }
            view.machineVisuals = cashiers.ToArray();
            AddClickCollider(view);
            return view;
        }

        static void BuildCouriers(Transform world, StationView[] stations)
        {
            var staff = Group("Staff", world);
            string[] carryModels = { "flour_sack_closed", "dough_ball", "bread" };
            string[] carryFolders = { BakeryArt, BakeryArt, GoodsArt };
            Vector3[] offsets = { new(0f, 0f, -0.35f), new(0f, 0f, 0.35f), new(0f, 0f, -0.35f) };
            for (int i = 0; i < 3; i++)
            {
                var baker = LookSetup.Chibi($"Baker{i}", staff, LookSetup.Hex("FFFFFF"), LookSetup.Hex("FFE0C2"), chefHat: true);
                baker.transform.localScale = Vector3.one * CharacterScale;
                var anim = baker.AddComponent<ChibiAnimator>();
                anim.body = baker.transform.Find("Body");

                var carry = Place(carryModels[i], carryFolders[i], anim.body, new Vector3(0f, 0.5f, 0.32f), 0f, local: true);
                carry.transform.localScale = Vector3.one * (i == 0 ? 0.8f : 1.1f) / 1f;
                carry.name = "Carry";
                carry.SetActive(false);

                var courier = baker.AddComponent<CourierView>();
                courier.linkIndex = i;
                courier.from = stations[i];
                courier.to = stations[i + 1];
                courier.carryVisual = carry;
                courier.waitOffset = offsets[i];
                baker.transform.position = stations[i].workPoint.position + offsets[i];
            }
        }

        static CustomerCrowd BuildCustomers(Transform world)
        {
            var root = Group("Customers", world);
            var crowd = root.gameObject.AddComponent<CustomerCrowd>();
            crowd.entryPoint = Marker("Entry", root, new Vector3(9f, 0f, -8f));
            crowd.exitPoint = Marker("Exit", root, new Vector3(10f, 0f, -7f));

            var slots = new List<Transform>();
            Vector3[] slotPositions =
            {
                new(4.75f, 0f, -0.6f), new(4.75f, 0f, -1.9f), new(4.75f, 0f, -3.2f),
                new(4.75f, 0f, -4.5f), new(6.0f, 0f, -4.5f), new(7.2f, 0f, -4.5f),
            };
            for (int i = 0; i < slotPositions.Length; i++)
            {
                var s = Marker($"QueueSlot{i}", root, slotPositions[i]);
                var lookAt = i < 4 ? new Vector3(4.75f, 0f, 1f) : slotPositions[i - 1];
                s.rotation = Quaternion.LookRotation((lookAt - slotPositions[i]).normalized, Vector3.up);
                slots.Add(s);
            }
            crowd.queueSlots = slots.ToArray();

            var templates = new List<GameObject>();
            string[] outfits = { "7FA7E8", "F2C14E", "B48EE0", "8FD3C1" };
            string[] skins = { "E8B48E", "F4C9A6", "C68B63", "FFE0C2" };
            for (int i = 0; i < outfits.Length; i++)
            {
                var c = LookSetup.Chibi($"CustomerTemplate{i}", root, LookSetup.Hex(outfits[i]), LookSetup.Hex(skins[i]), chefHat: false);
                c.transform.localScale = Vector3.one * CharacterScale;
                var anim = c.AddComponent<ChibiAnimator>();
                anim.body = c.transform.Find("Body");
                var bag = LookSetup.Box("Bag", anim.body, new Vector3(0.28f, 0.35f, 0.12f), new Vector3(0.22f, 0.26f, 0.14f),
                    LookSetup.Mat("PaperBag", LookSetup.Hex("D9A86C")));
                bag.SetActive(false);
                var grump = LookSetup.Sphere("Grump", anim.body, new Vector3(0.24f, 1.4f, 0.1f), Vector3.one * 0.16f,
                    LookSetup.Mat("Grump", LookSetup.Hex("FF5A5A"), emission: new Color(0.6f, 0.05f, 0.05f)));
                grump.SetActive(false);
                c.SetActive(false);
                templates.Add(c);
            }
            crowd.templates = templates.ToArray();
            return crowd;
        }

        static void BuildDining(Transform world)
        {
            var dining = Group("Dining", world);
            void Table(Vector3 p)
            {
                Place("table_round_A", dining, p);
                Place("chair", dining, p + new Vector3(-1.1f, 0f, 0f), 90f);
                Place("chair", dining, p + new Vector3(1.1f, 0f, 0f), -90f);
                Place("mug_A_pink", dining, p + new Vector3(-0.25f, 1f, 0.1f));
                Place("plate", dining, p + new Vector3(0.3f, 1f, -0.1f));
                Place("cream_puff", dining, p + new Vector3(0.3f, 1.03f, -0.1f));
            }
            Table(new Vector3(-5f, 0f, -2f));
            Table(new Vector3(-2f, 0f, -4f));
            Table(new Vector3(-5.5f, 0f, -4.8f));
        }

        // ------------------------------------------------------------------ HUD

        static TMP_FontAsset _font;
        static Material _outlined;
        static Sprite _rounded, _circle;

        static void LoadUiAssets()
        {
            _font = TMP_Settings.defaultFontAsset;
            LookSetup.EnsureFolder(UiFolder);
            string matPath = $"{UiFolder}/TextOutlined.mat";
            _outlined = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (_outlined == null)
            {
                _outlined = new Material(_font.material) { name = "TextOutlined" };
                AssetDatabase.CreateAsset(_outlined, matPath);
            }
            _outlined.EnableKeyword("OUTLINE_ON");
            _outlined.SetFloat("_OutlineWidth", 0.22f);
            _outlined.SetColor("_OutlineColor", new Color(0.29f, 0.17f, 0.13f));
            _outlined.SetFloat("_FaceDilate", 0.1f);
            EditorUtility.SetDirty(_outlined);
            _rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            _circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        }

        static void BuildHud(StationView[] stations, CustomerCrowd crowd)
        {
            LoadUiAssets();

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            var canvasGo = new GameObject("HUD", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var canvasRt = (RectTransform)canvasGo.transform;

            // World-anchored markers sit behind everything else.
            var worldLayer = Stretch(Rect("WorldLayer", canvasRt));
            foreach (var s in stations)
                BuildStationHud(worldLayer, s);

            var popups = Stretch(Rect("Popups", canvasRt));
            var popupTemplate = Text("PopupTemplate", popups, "+50", 54, TextAlignmentOptions.Center);
            ((RectTransform)popupTemplate.transform).sizeDelta = new Vector2(400, 90);
            var floating = popups.gameObject.AddComponent<FloatingTextLayer>();
            SetPrivate(floating, "template", popupTemplate);
            SetPrivate(floating, "crowd", crowd);

            BuildTopBar(canvasRt);
            BuildStationPanel(canvasRt);
            BuildOfflineDialog(canvasRt);
        }

        static void BuildStationHud(RectTransform layer, StationView station)
        {
            var root = Rect($"Hud_{station.name}", layer);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(10, 10);
            var hud = root.gameObject.AddComponent<StationHud>();
            hud.station = station;

            var ring = Rect("Ring", root);
            ring.sizeDelta = new Vector2(74, 74);
            Img(ring, _circle, new Color(0.25f, 0.16f, 0.13f, 0.65f));
            var fillRt = Rect("Fill", ring);
            fillRt.sizeDelta = new Vector2(62, 62);
            var fill = Img(fillRt, _circle, new Color(1f, 0.82f, 0.35f));
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            fill.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            fill.fillClockwise = true;

            var tap = Rect("TapPrompt", root);
            tap.sizeDelta = new Vector2(150, 64);
            Img(tap, _rounded, new Color(1f, 0.55f, 0.35f));
            Stretch(Text("Label", tap, "Tap!", 40, TextAlignmentOptions.Center).rectTransform);

            var badge = Rect("ManagerBadge", root);
            badge.sizeDelta = new Vector2(110, 40);
            badge.anchoredPosition = new Vector2(0, -56);
            Img(badge, _rounded, new Color(0.45f, 0.78f, 0.45f));
            Stretch(Text("Label", badge, "AUTO", 26, TextAlignmentOptions.Center).rectTransform);

            var stock = Text("Stock", root, "", 34, TextAlignmentOptions.Center);
            stock.rectTransform.sizeDelta = new Vector2(120, 50);
            stock.rectTransform.anchoredPosition = new Vector2(0, -48);

            SetPrivate(hud, "ring", ring.gameObject);
            SetPrivate(hud, "ringFill", fill);
            SetPrivate(hud, "tapPrompt", tap.gameObject);
            SetPrivate(hud, "managerBadge", badge.gameObject);
            SetPrivate(hud, "stockText", stock);
        }

        static void BuildTopBar(RectTransform canvas)
        {
            var bar = Rect("TopBar", canvas);
            bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0f, 1f);
            bar.anchoredPosition = new Vector2(32, -28);
            bar.sizeDelta = new Vector2(620, 84);
            var view = bar.gameObject.AddComponent<TopBarView>();

            var coins = Pill(bar, "Coins", new Vector2(0, 0), 330, new Color(1f, 0.83f, 0.3f), out var coinsText);
            Pill(bar, "Gems", new Vector2(360, 0), 230, new Color(0.55f, 0.75f, 1f), out var gemsText, diamond: true);
            SetPrivate(view, "coinsText", coinsText);
            SetPrivate(view, "gemsText", gemsText);
            SetPrivate(view, "coinsPill", coins);
        }

        static RectTransform Pill(RectTransform parent, string name, Vector2 pos, float width, Color iconColor, out TMP_Text label, bool diamond = false)
        {
            var pill = Rect(name, parent);
            pill.anchorMin = pill.anchorMax = pill.pivot = new Vector2(0f, 1f);
            pill.anchoredPosition = pos;
            pill.sizeDelta = new Vector2(width, 76);
            Img(pill, _rounded, new Color(1f, 0.98f, 0.94f, 0.95f));
            var icon = Rect("Icon", pill);
            icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0f, 0.5f);
            icon.anchoredPosition = new Vector2(10, 0);
            icon.sizeDelta = new Vector2(58, 58);
            Img(icon, diamond ? _rounded : _circle, iconColor);
            if (diamond)
            {
                icon.sizeDelta = new Vector2(44, 44);
                icon.anchoredPosition = new Vector2(18, 0);
                icon.localRotation = Quaternion.Euler(0, 0, 45);
            }
            label = Text("Value", pill, "0", 44, TextAlignmentOptions.Left);
            var rt = label.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(82, 0);
            rt.offsetMax = new Vector2(-12, 0);
            return pill;
        }

        static void BuildStationPanel(RectTransform canvas)
        {
            var holder = Stretch(Rect("StationPanel", canvas));
            holder.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, 0); // layout only
            holder.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            var panel = holder.gameObject.AddComponent<StationPanel>();

            var sheet = Rect("Sheet", holder);
            sheet.anchorMin = sheet.anchorMax = sheet.pivot = new Vector2(0.5f, 0f);
            sheet.sizeDelta = new Vector2(980, 470);
            Img(sheet, _rounded, new Color(1f, 0.96f, 0.9f, 0.98f)).raycastTarget = true;

            var title = Text("Title", sheet, "Station", 50, TextAlignmentOptions.Left);
            Place(title.rectTransform, new Vector2(40, -24), new Vector2(700, 64), top: true);
            var info = Text("Info", sheet, "", 28, TextAlignmentOptions.Left, outlined: false, color: new Color(0.45f, 0.32f, 0.27f));
            Place(info.rectTransform, new Vector2(42, -86), new Vector2(880, 40), top: true);

            var close = Rect("Close", sheet);
            close.anchorMin = close.anchorMax = close.pivot = new Vector2(1f, 1f);
            close.anchoredPosition = new Vector2(-20, -20);
            close.sizeDelta = new Vector2(70, 70);
            var closeBtn = close.gameObject.AddComponent<UnityEngine.UI.Button>();
            closeBtn.targetGraphic = Img(close, _circle, new Color(0.93f, 0.55f, 0.45f));
            Stretch(Text("X", close, "X", 38, TextAlignmentOptions.Center).rectTransform);

            var rows = new List<StationPanel.UpgradeRow>();
            UpgradeTrack[] tracks = { UpgradeTrack.Speed, UpgradeTrack.Output, UpgradeTrack.Machines };
            for (int i = 0; i < tracks.Length; i++)
                rows.Add(BuildUpgradeRow(sheet, tracks[i], -140 - i * 82));

            var mgr = BuildUpgradeRow(sheet, UpgradeTrack.Speed, -140 - 3 * 82, managerRow: true);

            SetPrivate(panel, "sheet", sheet);
            SetPrivate(panel, "stationName", title);
            SetPrivate(panel, "stationInfo", info);
            SetPrivate(panel, "rows", rows.ToArray());
            SetPrivate(panel, "managerTitle", mgr.title);
            SetPrivate(panel, "managerCost", mgr.cost);
            SetPrivate(panel, "managerButton", mgr.button);
            SetPrivate(panel, "managerButtonImage", mgr.buttonImage);
            SetPrivate(panel, "closeButton", closeBtn);
        }

        static StationPanel.UpgradeRow BuildUpgradeRow(RectTransform sheet, UpgradeTrack track, float y, bool managerRow = false)
        {
            var row = Rect(managerRow ? "ManagerRow" : $"Row_{track}", sheet);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0, y);
            row.sizeDelta = new Vector2(-60, 74);
            Img(row, _rounded, managerRow ? new Color(0.88f, 0.95f, 0.86f) : new Color(1f, 1f, 1f, 0.9f)).raycastTarget = false;

            var title = Text("Title", row, "Upgrade", 32, TextAlignmentOptions.Left, outlined: false, color: new Color(0.33f, 0.21f, 0.17f));
            Place(title.rectTransform, new Vector2(24, -6), new Vector2(560, 38), top: true);
            var detail = Text("Detail", row, "", 22, TextAlignmentOptions.Left, outlined: false, color: new Color(0.5f, 0.4f, 0.35f));
            Place(detail.rectTransform, new Vector2(24, -42), new Vector2(560, 28), top: true);

            var btn = Rect("Buy", row);
            btn.anchorMin = btn.anchorMax = btn.pivot = new Vector2(1f, 0.5f);
            btn.anchoredPosition = new Vector2(-12, 0);
            btn.sizeDelta = new Vector2(220, 60);
            var image = Img(btn, _rounded, new Color(0.45f, 0.78f, 0.45f));
            var button = btn.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var cost = Text("Cost", btn, "50", 34, TextAlignmentOptions.Center);
            Stretch(cost.rectTransform);

            return new StationPanel.UpgradeRow { track = track, title = title, detail = detail, cost = cost, button = button, buttonImage = image };
        }

        static void BuildOfflineDialog(RectTransform canvas)
        {
            var holder = Stretch(Rect("OfflineDialog", canvas));
            var dialog = holder.gameObject.AddComponent<OfflineEarningsDialog>();
            var dim = Stretch(Rect("Dim", holder));
            Img(dim, null, new Color(0.15f, 0.08f, 0.1f, 0.55f)).raycastTarget = true;

            var card = Rect("Card", dim);
            card.sizeDelta = new Vector2(760, 470);
            Img(card, _rounded, new Color(1f, 0.96f, 0.9f));
            var title = Text("Title", card, "Welcome back!", 60, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0, -36), new Vector2(700, 80), top: true, centered: true);
            var body = Text("Body", card, "", 30, TextAlignmentOptions.Center, outlined: false, color: new Color(0.4f, 0.28f, 0.24f));
            Place(body.rectTransform, new Vector2(0, -130), new Vector2(640, 90), top: true, centered: true);
            var amount = Text("Amount", card, "+0", 72, TextAlignmentOptions.Center, color: new Color(1f, 0.82f, 0.3f));
            Place(amount.rectTransform, new Vector2(0, -230), new Vector2(640, 100), top: true, centered: true);

            var btn = Rect("Collect", card);
            btn.anchorMin = btn.anchorMax = btn.pivot = new Vector2(0.5f, 0f);
            btn.anchoredPosition = new Vector2(0, 36);
            btn.sizeDelta = new Vector2(340, 90);
            var button = btn.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = Img(btn, _rounded, new Color(0.45f, 0.78f, 0.45f));
            Stretch(Text("Label", btn, "Collect", 46, TextAlignmentOptions.Center).rectTransform);

            SetPrivate(dialog, "root", dim.gameObject);
            SetPrivate(dialog, "body", body);
            SetPrivate(dialog, "amount", amount);
            SetPrivate(dialog, "collectButton", button);
        }

        // ------------------------------------------------------------------ UI helpers

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size, bool top, bool centered = false)
        {
            float x = centered ? 0.5f : 0f;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(x, top ? 1f : 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static UnityEngine.UI.Image Img(RectTransform rt, Sprite sprite, Color color)
        {
            var img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = sprite == _rounded ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.pixelsPerUnitMultiplier = sprite == _rounded ? 0.35f : 1f;
            img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI Text(string name, Transform parent, string value, float size, TextAlignmentOptions align,
            bool outlined = true, Color? color = null)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = _font;
            if (outlined)
                t.fontSharedMaterial = _outlined;
            t.text = value;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.alignment = align;
            t.color = color ?? Color.white;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        // ------------------------------------------------------------------ World helpers

        static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static Transform Marker(string name, Transform parent, Vector3 worldPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            return go.transform;
        }

        static GameObject Place(string model, Transform parent, Vector3 pos, float yaw = 0f, bool local = false) =>
            Place(model, BakeryArt, parent, pos, yaw, local);

        static GameObject Place(string model, string folder, Transform parent, Vector3 pos, float yaw = 0f, bool local = false)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}{model}.fbx");
            if (prefab == null)
            {
                Debug.LogWarning($"[Merge Resto] Missing model {folder}{model}.fbx");
                return new GameObject($"MISSING_{model}");
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (local)
            {
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }
            else
            {
                go.transform.position = pos;
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
            return go;
        }

        static GameObject[] Row(string model, Transform parent, Vector3 start, Vector3 step, int count, float yaw) =>
            Row(model, BakeryArt, parent, start, step, count, yaw);

        static GameObject[] Row(string model, string folder, Transform parent, Vector3 start, Vector3 step, int count, float yaw)
        {
            var items = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                items[i] = Place(model, folder, parent, start + step * i, yaw, local: true);
                items[i].name = $"{model}_{i}";
            }
            return items;
        }

        static void AddClickCollider(StationView view)
        {
            var renderers = view.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.GetComponentInParent<ChibiAnimator>() == null).ToArray();
            if (renderers.Length == 0)
                return;
            var b = renderers[0].bounds;
            foreach (var r in renderers)
                b.Encapsulate(r.bounds);
            var box = view.gameObject.AddComponent<BoxCollider>();
            box.center = view.transform.InverseTransformPoint(b.center);
            box.size = b.size + new Vector3(0.2f, 0.2f, 0.2f);
        }

        static void SetPrivate(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[Merge Resto] {target.GetType().Name} has no serialized field '{field}'.");
                return;
            }
            switch (value)
            {
                case Object o: prop.objectReferenceValue = o; break;
                case System.Array arr when prop.isArray:
                    prop.arraySize = arr.Length;
                    for (int i = 0; i < arr.Length; i++)
                        SetRow(prop.GetArrayElementAtIndex(i), arr.GetValue(i));
                    break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetRow(SerializedProperty element, object value)
        {
            if (value is StationPanel.UpgradeRow row)
            {
                element.FindPropertyRelative("track").enumValueIndex = (int)row.track;
                element.FindPropertyRelative("title").objectReferenceValue = row.title;
                element.FindPropertyRelative("detail").objectReferenceValue = row.detail;
                element.FindPropertyRelative("cost").objectReferenceValue = row.cost;
                element.FindPropertyRelative("button").objectReferenceValue = row.button;
                element.FindPropertyRelative("buttonImage").objectReferenceValue = row.buttonImage;
            }
            else if (value is Object o)
            {
                element.objectReferenceValue = o;
            }
        }
    }
}
