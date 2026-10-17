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
        const float CharacterScale = 1f;
        const string RestaurantArt = "Assets/Art/ThirdParty/KayKit_RestaurantBits/Models/";
        const string CityArt = "Assets/Art/ThirdParty/KayKit_CityBuilder/Models/";
        const string ForestArt = "Assets/Art/ThirdParty/KayKit_Forest/Models/";
        const float LowWallHeight = 1.2f;
        const float EntranceX = 6.2f;
        const float ExitZ = -2.2f;
        // KayKit City Builder is a miniature set; this brings it to character scale.
        const float CityScale = 3f;
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
            EnsureGoals(config);
            EnsureBoard(config);
            var cam = LookSetup.CreateLookRig(new Vector3(0.3f, 1f, 0.6f), 7.9f);
            var cameraDirector = cam.gameObject.AddComponent<CameraDirector>();

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
            UpdateCustomerTimes(config, crowd);
            BuildDining(world);

            var game = new GameObject("Game");
            var controller = game.AddComponent<GameController>();
            SetPrivate(controller, "config", config);
            var input = game.AddComponent<InputController>();
            SetPrivate(input, "worldCamera", cam);

            HudBuilder.Build(stations, crowd, out var popups, out var coinFly);
            var fx = BuildFx(world, stations, crowd, popups, coinFly);
            BuildDecor(world);
            BuildInterior(world);
            BuildStreet(world);

            // Grandma guides the Grand Opening; her portrait is rendered from the 3D model.
            var portrait = PortraitRenderer.Render(CharacterLibrary.GrandmaLook, LookSetup.Hex("FFD9A8"));
            var grandma = stations[3].GetComponentsInChildren<ChibiAnimator>(true).First(a => a.name == "Grandma");
            HudBuilder.BuildTutorial(stations, fx, coinFly, grandma, portrait, crowd, cameraDirector);

            // The renovation board and the run-down spots it fixes.
            var boardIcons = RenderBoardIcons(config.shop.board);
            var openIcon = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconRenderer.Folder}/renovate.png")
                ?? IconRenderer.Render($"{CityArt}box_A.fbx", "renovate", yaw: 30f);
            HudBuilder.BuildBoard(config.shop.board, boardIcons.chains, boardIcons.generators, openIcon, coinFly);
            BuildRenovationSpots(world, stations);
            BuildShopAge(world);
            BuildAudio(crowd);
            HudBuilder.BuildSoundToggle();
            HudBuilder.EnableButtonTargets();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Merge Resto] Bakery scene saved to {ScenePath}.");
        }

        // ------------------------------------------------------------------ Config

        // The bakery's milestone list, shown three at a time after the tutorial. Gems from the
        // early ones pay for the next managers, so automation keeps growing.
        static void EnsureGoals(ShopConfig config)
        {
            var goals = config.shop.goals;
            goals.Clear();
            GoalDefinition G(GoalKind kind, double target, int station = 0, double coins = 0, int gems = 0) =>
                new() { kind = kind, target = target, station = station, rewardCoins = coins, rewardGems = gems };
            goals.Add(G(GoalKind.Renovations, 1, coins: 200));
            goals.Add(G(GoalKind.StationLevel, 2, station: 2, gems: 15));
            goals.Add(G(GoalKind.ServeCustomers, 15, coins: 150));
            goals.Add(G(GoalKind.Renovations, 3, gems: 20));
            goals.Add(G(GoalKind.StationLevel, 2, station: 1, gems: 15));
            goals.Add(G(GoalKind.HireManager, 1, station: 1, coins: 300));
            goals.Add(G(GoalKind.AddMachines, 1, station: 2, gems: 20));
            goals.Add(G(GoalKind.LifetimeCoins, 3000, gems: 25));
            goals.Add(G(GoalKind.HireManager, 1, station: 2, coins: 600));
            goals.Add(G(GoalKind.StationLevel, 3, station: 3, gems: 20));
            goals.Add(G(GoalKind.ServeCustomers, 100, gems: 30));
            goals.Add(G(GoalKind.ShopLevel, 20, coins: 1500));
            goals.Add(G(GoalKind.Renovations, 6, gems: 50));
            goals.Add(G(GoalKind.AddMachines, 1, station: 1, gems: 30));
            goals.Add(G(GoalKind.LifetimeCoins, 25000, gems: 50));
            goals.Add(G(GoalKind.ServeCustomers, 300, coins: 4000));
            goals.Add(G(GoalKind.ShopLevel, 40, gems: 75));
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        // The renovation board: three chains of bakery props (sweets, kitchenware, plants),
        // a generator for each, and six run-down spots in the shop to fix with them.
        static void EnsureBoard(ShopConfig config)
        {
            var b = config.shop.board = new MergeBoardDefinition
            {
                columns = 7,
                rows = 5,
                energyMax = 100,
                energyRegenSeconds = 6f,
                sellBase = 5,
            };
            MergeChainDefinition Chain(string id, string name, params (string tier, string model)[] tiers)
            {
                var c = new MergeChainDefinition { id = id, displayName = name };
                foreach (var t in tiers)
                {
                    c.tiers.Add(t.tier);
                    c.models.Add(t.model);
                }
                return c;
            }
            b.chains.Add(Chain("sweets", "Sweets",
                ("Cookie", "TinyTreats_BakedGoods/cookie"), ("Macaron", "TinyTreats_BakeryInterior/macaron_pink"),
                ("Cupcake", "TinyTreats_BakedGoods/cupcake"), ("Donut", "TinyTreats_BakedGoods/donut_pink"),
                ("Cinnamon Roll", "TinyTreats_BakedGoods/cinnamon_roll"), ("Strawberry Cake", "TinyTreats_BakedGoods/cake_strawberry"),
                ("Birthday Cake", "TinyTreats_BakedGoods/cake_birthday")));
            b.chains.Add(Chain("kitchen", "Kitchenware",
                ("Whisk", "TinyTreats_BakeryInterior/whisk"), ("Mug", "TinyTreats_BakeryInterior/mug_A_pink"),
                ("Plates", "TinyTreats_BakeryInterior/plate_stacked"), ("Mixing Bowl", "TinyTreats_BakeryInterior/mixing_bowl"),
                ("Kitchen Scale", "TinyTreats_BakeryInterior/scale"), ("Stand Mixer", "TinyTreats_BakeryInterior/stand_mixer")));
            b.chains.Add(Chain("plants", "Plants",
                ("Succulent", "TinyTreats_HousePlants/succulent_plant_pot_small"), ("Little Cactus", "TinyTreats_HousePlants/cacti_plant_pot_small"),
                ("Pothos", "TinyTreats_HousePlants/pothos_plant_small_potted"), ("ZZ Plant", "TinyTreats_HousePlants/zzplant_plant_medium_potted"),
                ("Snake Plant", "TinyTreats_HousePlants/sansevieria_plant_large_potted"), ("Monstera", "TinyTreats_HousePlants/monstera_plant_large_potted")));
            b.generators.Add(new GeneratorDefinition { id = "cookie_jar", displayName = "Cookie Jar", chain = 0, model = "TinyTreats_BakeryInterior/cookie_jar" });
            b.generators.Add(new GeneratorDefinition { id = "dish_rack", displayName = "Dish Rack", chain = 1, model = "KayKit_RestaurantBits/dishrack_plates" });
            b.generators.Add(new GeneratorDefinition { id = "watering_can", displayName = "Watering Can", chain = 2, model = "TinyTreats_HousePlants/watering_can_A" });

            b.start.Add(new BoardStartItem { cell = 0, generator = 0 });
            b.start.Add(new BoardStartItem { cell = 14, generator = 1 });
            b.start.Add(new BoardStartItem { cell = 28, generator = 2 });
            b.start.Add(new BoardStartItem { cell = 9, generator = -1, chain = 0, tier = 0 });
            b.start.Add(new BoardStartItem { cell = 10, generator = -1, chain = 0, tier = 0 });

            RenovationDefinition R(string id, string name, double coins, float cozy, params (int chain, int tier)[] needs)
            {
                var r = new RenovationDefinition { id = id, displayName = name, rewardCoins = coins, coziness = cozy };
                foreach (var n in needs)
                    r.needs.Add(new ItemRef(n.chain, n.tier));
                return r;
            }
            b.renovations.Add(R("pastry_stand", "Restock the pastry stand", 150, 0.05f, (0, 2)));
            b.renovations.Add(R("window_plants", "Revive the window plants", 200, 0.06f, (2, 2), (2, 1)));
            b.renovations.Add(R("dining", "Set the dining tables", 300, 0.08f, (1, 2), (1, 2), (1, 1)));
            b.renovations.Add(R("display_case", "Polish the display case", 500, 0.08f, (1, 3), (0, 3)));
            b.renovations.Add(R("corner", "Green up the cozy corner", 800, 0.08f, (2, 4)));
            b.renovations.Add(R("shop_sign", "Light up the shop sign", 2000, 0.15f, (0, 5), (1, 4), (2, 4)));
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        // Board icons rendered from the item models (only missing ones, unless forced).
        [MenuItem("Merge Resto/Art/Render Board Icons")]
        static void RenderBoardIconsForced() => RenderBoardIcons(AssetDatabase.LoadAssetAtPath<ShopConfig>(ConfigPath).shop.board, force: true);

        static (Sprite[][] chains, Sprite[] generators) RenderBoardIcons(MergeBoardDefinition b, bool force = false)
        {
            Sprite Icon(string model, string name)
            {
                string path = $"{IconRenderer.Folder}/{name}.png";
                var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (existing != null && !force)
                    return existing;
                // "Pack/model" lives at Assets/Art/ThirdParty/Pack/Models/model.fbx.
                int slash = model.IndexOf('/');
                return IconRenderer.Render($"Assets/Art/ThirdParty/{model[..slash]}/Models/{model[(slash + 1)..]}.fbx", name);
            }
            var chains = new Sprite[b.chains.Count][];
            for (int c = 0; c < b.chains.Count; c++)
            {
                chains[c] = new Sprite[b.chains[c].models.Count];
                for (int t = 0; t < chains[c].Length; t++)
                    chains[c][t] = Icon(b.chains[c].models[t], $"{b.chains[c].id}_{t}");
            }
            var gens = new Sprite[b.generators.Count];
            for (int g = 0; g < gens.Length; g++)
                gens[g] = Icon(b.generators[g].model, $"gen_{b.generators[g].id}");
            return (chains, gens);
        }

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

        // Customers are only served once they really stand at the counter, so the sim needs
        // the walking times of the actual path in this room.
        static void UpdateCustomerTimes(ShopConfig config, CustomerCrowd crowd)
        {
            float speed = crowd.walkSpeed;
            float length = 0f;
            for (int i = 1; i < crowd.entryPath.Length; i++)
                length += Vector3.Distance(crowd.entryPath[i - 1].position, crowd.entryPath[i].position);
            length += Vector3.Distance(crowd.entryPath[^1].position, crowd.queueSlots[0].position);
            float walkIn = length / speed;
            float step = Vector3.Distance(crowd.queueSlots[0].position, crowd.queueSlots[1].position) / speed;
            config.shop.walkInSeconds = walkIn + 0.15f;
            config.shop.stepUpSeconds = step + 0.15f;
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

            Place("wall_shelf_bakery_A", room, new Vector3(-7.95f, 2.4f, 2f), 90f);
            Place("tin_A_beige", room, new Vector3(-7.6f, 2.45f, 1.5f));
            Place("tin_A_brown", room, new Vector3(-7.6f, 2.45f, 2.1f));
            Place("cookie_jar", room, new Vector3(-7.6f, 2.45f, 2.6f));
            Place("rug", room, new Vector3(-3.5f, 0f, -3f));

            // Cut-away front and right walls: knee height so the shop stays visible, with a
            // gap for the entrance (front) and a separate exit (right side).
            var low = Group("LowWalls", room);
            // The same panelled bakery wall as the back, cut down to knee height. Pieces are
            // 2 wide with their back face on the pivot; models keep their import scale.
            void Segment(Vector3 from, Vector3 to, float yaw)
            {
                var mid = (from + to) * 0.5f;
                float len = Vector3.Distance(from, to);
                var w = Place("wall_modular_panelled_bakery_straight_A", low, mid, yaw);
                w.transform.localScale = Vector3.Scale(w.transform.localScale, new Vector3(len / 2f, LowWallHeight / 4f, 1f));
            }
            void Post(Vector3 at)
            {
                var post = Place("pillar_A", RestaurantArt, low, at);
                post.transform.localScale = Vector3.Scale(post.transform.localScale, new Vector3(0.75f, 1.75f / 4.1f, 0.75f));
            }
            // Wall meshes are single-sided, so the front and right walls face outwards: the
            // camera looks at their outside. They sit just outside the floor edge.
            Segment(new Vector3(-8f, 0f, -6f), new Vector3(EntranceX - 1.1f, 0f, -6f), 180f);
            Segment(new Vector3(EntranceX + 1.1f, 0f, -6f), new Vector3(8f, 0f, -6f), 180f);
            Segment(new Vector3(8f, 0f, -6f), new Vector3(8f, 0f, ExitZ - 1.1f), 90f);
            Segment(new Vector3(8f, 0f, ExitZ + 1.1f), new Vector3(8f, 0f, 6f), 90f);
            Post(new Vector3(EntranceX - 1.1f, 0f, -6.15f));
            Post(new Vector3(EntranceX + 1.1f, 0f, -6.15f));
            Post(new Vector3(8.15f, 0f, ExitZ - 1.1f));
            Post(new Vector3(8.15f, 0f, ExitZ + 1.1f));
            Post(new Vector3(8.15f, 0f, -6.15f));
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
            var view = NewStation(world, "FlourStorage", new Vector3(-6.1f, 0f, 4.45f), new Vector3(-6.0f, 0f, 3.0f), 1.5f);
            var t = view.transform;
            var slots = new List<GameObject>();
            for (int m = 0; m < 3; m++)
            {
                // A delivery: a stack of big flour sacks on a wooden crate, easy to spot and tap.
                var pile = Group($"SackPile{m}", t);
                pile.localPosition = new Vector3(-0.75f + m * 1.25f, 0f, 0f);
                var crate = Place("crate", RestaurantArt, pile, Vector3.zero, 0f, local: true);
                crate.transform.localScale = Vector3.Scale(crate.transform.localScale, new Vector3(1.15f, 0.55f, 1.0f));
                float top = 0.32f;
                var a = Place("flour_sack_closed", pile, new Vector3(-0.28f, top, 0f), 10f, local: true);
                var b = Place("flour_sack_closed", pile, new Vector3(0.3f, top, 0.05f), -15f, local: true);
                var c = Place("flour_sack_closed", pile, new Vector3(0f, top + 0.55f, 0f), 5f, local: true);
                foreach (var sack in new[] { a, b, c })
                    sack.transform.localScale *= 1.25f;
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
            var view = NewStation(world, "Oven", new Vector3(1f, 0f, 5f), new Vector3(1f, 0f, 3.2f), 3.2f);
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
            var steam = SteamEmitter(t, new Vector3(-0.2f, 3.1f, 0.2f));
            view.runningEffects = new[] { glow, steam };
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
                // Grandma herself runs the first till.
                var c = Character(m == 0 ? CharacterLibrary.GrandmaLook : CharacterLibrary.CashierLook, t, m == 0 ? "Grandma" : $"Cashier{m}");
                c.transform.localPosition = new Vector3(xs[m], 0f, 1.25f);
                c.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                c.GetComponent<ChibiAnimator>().idleFacing = Vector3.back;
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
                var baker = Character(CharacterLibrary.BakerLook, staff, $"Baker{i}");
                var anim = baker.GetComponent<ChibiAnimator>();

                var carry = Place(carryModels[i], carryFolders[i], anim.carryAnchor, Vector3.zero, 0f, local: true);
                carry.transform.localRotation = Quaternion.identity;
                carry.transform.localScale = Vector3.one * (i == 0 ? 1.1f : 1.5f);
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
            // In from the street along the front sidewalk, through the entrance.
            crowd.entryPath = new[]
            {
                Marker("Entry_Street", root, new Vector3(22f, 0f, -8.2f)),
                Marker("Entry_Outside", root, new Vector3(EntranceX, 0f, -8.2f)),
                Marker("Entry_Door", root, new Vector3(EntranceX, 0f, -6.25f)),
                Marker("Entry_Inside", root, new Vector3(EntranceX, 0f, -5.0f)),
            };
            // Out through the side exit and away down the right-hand sidewalk.
            crowd.exitPath = new[]
            {
                Marker("Exit_Inside", root, new Vector3(7.0f, 0f, ExitZ)),
                Marker("Exit_Door", root, new Vector3(8.25f, 0f, ExitZ)),
                Marker("Exit_Outside", root, new Vector3(9.8f, 0f, ExitZ)),
                Marker("Exit_Street", root, new Vector3(9.8f, 0f, 20f)),
            };

            // The line runs from the register towards the entrance, then snakes left.
            var slots = new List<Transform>();
            Vector3[] slotPositions =
            {
                new(4.75f, 0f, -0.6f), new(4.75f, 0f, -1.9f), new(4.75f, 0f, -3.2f),
                new(4.75f, 0f, -4.5f), new(3.5f, 0f, -4.5f), new(2.25f, 0f, -4.5f),
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
            foreach (var look in CharacterLibrary.CustomerLooks)
            {
                var c = Character(look, root, $"Template_{look}");
                c.SetActive(false);
                templates.Add(c);
            }
            crowd.templates = templates.ToArray();
            return crowd;
        }

        // ------------------------------------------------------------------ Decor

        const string PlantArt = "Assets/Art/ThirdParty/TinyTreats_HousePlants/Models/";

        static void BuildDecor(Transform world)
        {
            var decor = Group("Decor", world);
            void Plant(string model, Vector3 pos, float yaw = 0f, float scale = 1f)
            {
                var p = Place(model, PlantArt, decor, pos, yaw);
                p.transform.localScale = Vector3.one * scale;
                ApplySway(p);
            }
            Plant("monstera_plant_large_potted", new Vector3(-7.2f, 0f, -5.2f), 30f);
            Plant("yucca_plant_large_potted", new Vector3(7.3f, 0f, 4.9f), -20f);
            Plant("sansevieria_plant_medium_potted", new Vector3(-7.3f, 0f, -0.2f), 10f);
            Plant("pothos_plant_small_potted", new Vector3(-7.55f, 2.45f, 1.0f), 90f);
            Plant("succulent_plant_pot_small", new Vector3(-7.55f, 2.45f, 3.0f), 0f);
            Plant("zzplant_plant_medium_potted", new Vector3(7.4f, 0f, -5.4f), -40f);
            Plant("monstera_plant_medium_potted", new Vector3(2.2f, 0f, 5.3f), 0f);

            // Mats at the entrance and the exit.
            var inMat = Place("rug", decor, new Vector3(EntranceX, 0.01f, -5.3f), 0f);
            inMat.transform.localScale = new Vector3(0.75f, 1f, 0.75f);
            var outMat = Place("rug", decor, new Vector3(7.3f, 0.01f, ExitZ), 90f);
            outMat.transform.localScale = new Vector3(0.75f, 1f, 0.75f);
        }

        // ------------------------------------------------------------------ Interior dressing

        // Makes the room feel lived-in: shelves, curtains, signs, pastries on display and warm
        // pendant lamps that actually light the counter and the tables.
        static void BuildInterior(Transform world)
        {
            var dress = Group("Dressing", world);

            // Back wall: shelf of jars behind the mixer, sign above the oven area.
            Place("wall_shelf_bakery_B", dress, new Vector3(-3f, 2.75f, 5.7f), 180f);
            Place("cookie_jar", dress, new Vector3(-3.6f, 2.78f, 5.3f));
            Place("tin_A_grey", dress, new Vector3(-2.9f, 2.78f, 5.3f));
            Place("tin_B_beige", dress, new Vector3(-2.4f, 2.78f, 5.3f));
            Place("wall_sign_large", dress, new Vector3(4.2f, 3.2f, 5.7f), 180f);

            // Curtains on every window.
            Place("curtains", dress, new Vector3(5f, 3.55f, 5.69f), 180f);
            Place("curtains", dress, new Vector3(7f, 3.55f, 5.69f), 180f);
            Place("curtains", dress, new Vector3(-7.69f, 3.55f, -3f), 90f);
            Place("curtains", dress, new Vector3(-7.69f, 3.55f, -1f), 90f);

            // Hanging vines under the shelves.
            var vine = Place("pothos_vine_medium_A", PlantArt, dress, new Vector3(-7.55f, 2.0f, 1.4f), 90f);
            ApplySway(vine);
            var vine2 = Place("pothos_vine_medium_A", PlantArt, dress, new Vector3(-2.2f, 2.25f, 5.45f), 180f);
            ApplySway(vine2);

            // Treats on display at the counter.
            Place("pastry_stand_B_decorated", dress, new Vector3(6.05f, 1.0f, 0.75f), 0f);
            Place("cake_birthday", GoodsArt, dress, new Vector3(5.75f, 1.0f, 1.25f), 30f).transform.localScale *= 0.55f;
            Place("cookie_jar", dress, new Vector3(3.95f, 1.0f, 1.15f));

            // Chalkboard menu by the door.
            var menu = Place("menu", RestaurantArt, dress, new Vector3(4.9f, 0f, -5.25f), 200f);
            menu.transform.localScale *= 1.6f;

            // Warm light pools over the counter and the dining tables. The room has no ceiling,
            // so these are bare lights rather than hanging lamps floating in the air.
            foreach (var p in new[] { new Vector3(4.2f, 0f, 1.4f), new Vector3(-4.2f, 0f, -2.6f), new Vector3(-1.6f, 0f, -3.6f) })
                WarmLight(dress, p);
        }

        // ------------------------------------------------------------------ Renovations

        // Each renovation's run-down look and its fixed props. Indices match EnsureBoard.
        static void BuildRenovationSpots(Transform world, StationView[] stations)
        {
            var root = Group("Renovations", world);
            var sparkle = GameObject.Find("Bakery/FX/SparkleBurst").GetComponent<ParticleSystem>();
            RenovationSpot Spot(int index, string name, Vector3 pos)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                go.transform.position = pos;
                var spot = go.AddComponent<RenovationSpot>();
                spot.renovation = index;
                spot.sparkle = sparkle;
                spot.broken = new GameObject[0];
                spot.fixedObjects = new GameObject[0];
                spot.worn = new Renderer[0];
                spot.lights = new Light[0];
                return spot;
            }
            GameObject Find(string path) => GameObject.Find(path);
            Renderer[] RenderersOf(params GameObject[] gos) =>
                gos.Where(g => g != null).SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).ToArray();
            GameObject Prop(string model, string folder, Transform parent, Vector3 pos, float yaw = 0f, float scale = 1f, bool sway = false)
            {
                var p = Place(model, folder, parent, pos, yaw);
                p.transform.localScale *= scale;
                if (sway) ApplySway(p);
                return p;
            }

            // 0: the pastry stand on the counter is dust-sheeted.
            {
                var spot = Spot(0, "PastryStand", new Vector3(6.05f, 1f, 0.75f));
                var stand = Find("Bakery/Dressing/pastry_stand_B_decorated");
                var cake = Find("Bakery/Dressing/cake_birthday");
                var covered = Prop("pastry_stand_A_covered", BakeryArt, spot.transform, new Vector3(6.05f, 1f, 0.75f));
                spot.broken = new[] { covered };
                spot.fixedObjects = new[] { stand, cake }.Where(g => g != null).ToArray();
                spot.worn = RenderersOf(covered);
            }
            // 1: empty pots under the left windows.
            {
                var spot = Spot(1, "WindowPlants", new Vector3(-7.3f, 0f, -1.6f));
                var snake = Find("Bakery/Decor/sansevieria_plant_medium_potted");
                var pothos = Prop("pothos_plant_medium_potted", PlantArt, spot.transform, new Vector3(-7.3f, 0f, -2.9f), 40f, sway: true);
                var potA = Prop("pot_B_medium", PlantArt, spot.transform, new Vector3(-7.3f, 0f, -0.2f));
                var potB = Prop("pot_B_medium", PlantArt, spot.transform, new Vector3(-7.3f, 0f, -2.9f), 30f);
                spot.broken = new[] { potA, potB };
                spot.fixedObjects = new[] { snake, pothos }.Where(g => g != null).ToArray();
                spot.worn = RenderersOf(potA, potB);
            }
            // 2: dining tables bare and grimy; plates, mugs and treats appear once set.
            {
                var spot = Spot(2, "Dining", new Vector3(-4f, 0f, -3.5f));
                var dining = Find("Bakery/Dining");
                var tableware = new List<GameObject>();
                var furniture = new List<GameObject>();
                foreach (Transform child in dining.transform)
                {
                    if (child.name.StartsWith("mug") || child.name.StartsWith("plate") || child.name.StartsWith("cream_puff"))
                        tableware.Add(child.gameObject);
                    else
                        furniture.Add(child.gameObject);
                }
                spot.fixedObjects = tableware.ToArray();
                spot.worn = RenderersOf(furniture.ToArray());
            }
            // 3: the display case at the counter is grimy.
            {
                var spot = Spot(3, "DisplayCase", stations[3].transform.position + Vector3.left);
                var display = stations[3].transform.Find("display_case_long");
                spot.worn = RenderersOf(display != null ? display.gameObject : null);
            }
            // 4: the front-left corner has a dead, empty pot.
            {
                var spot = Spot(4, "CozyCorner", new Vector3(-7.2f, 0f, -5.2f));
                var monstera = Find("Bakery/Decor/monstera_plant_large_potted");
                var pot = Prop("pot_C_large", PlantArt, spot.transform, new Vector3(-7.2f, 0f, -5.2f), 10f);
                var stick = Prop("yucca_branch_small_A", PlantArt, spot.transform, new Vector3(-7.2f, 0.45f, -5.2f), 60f);
                spot.broken = new[] { pot, stick };
                spot.fixedObjects = monstera != null ? new[] { monstera } : new GameObject[0];
                spot.worn = RenderersOf(pot, stick);
            }
            // 5: the shop sign over the oven is dull until it is lit up.
            {
                var sign = Find("Bakery/Dressing/wall_sign_large");
                var spot = Spot(5, "ShopSign", sign != null ? sign.transform.position : new Vector3(4.2f, 3.2f, 5.7f));
                spot.worn = RenderersOf(sign);
                var lightGo = new GameObject("SignGlow");
                lightGo.transform.SetParent(spot.transform, false);
                lightGo.transform.position = spot.transform.position + new Vector3(0f, 0f, -0.8f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.75f, 0.45f);
                light.range = 4f;
                light.intensity = 2.5f;
                light.shadows = LightShadows.None;
                spot.lights = new[] { light };
            }
        }

        // Music and effects; clip choices and mix levels live here.
        static void BuildAudio(CustomerCrowd crowd)
        {
            var go = new GameObject("Audio");
            var audio = go.AddComponent<AudioDirector>();
            AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/SFX/{name}.wav");
            (Sfx sfx, string clip, float volume, float interval)[] mix =
            {
                (Sfx.Click, "click", 0.55f, 0.04f), (Sfx.Tap, "tap", 0.7f, 0.05f), (Sfx.Coin, "coin", 0.45f, 0.04f),
                (Sfx.Sale, "sale", 0.6f, 0.08f), (Sfx.Upgrade, "upgrade", 0.8f, 0.05f), (Sfx.Manager, "manager", 0.85f, 0.2f),
                (Sfx.Merge, "merge", 0.8f, 0.03f), (Sfx.Spawn, "spawn", 0.55f, 0.03f), (Sfx.Renovate, "renovate", 0.9f, 0.3f),
                (Sfx.Celebrate, "celebrate", 0.9f, 0.5f), (Sfx.Claim, "claim", 0.8f, 0.1f), (Sfx.Deny, "deny", 0.6f, 0.15f),
                (Sfx.Grumpy, "grumpy", 0.5f, 0.3f), (Sfx.Whoosh, "whoosh", 0.5f, 0.1f), (Sfx.Blip, "blip", 0.5f, 0.06f),
            };
            var so = new SerializedObject(audio);
            var entries = so.FindProperty("entries");
            entries.arraySize = mix.Length;
            for (int i = 0; i < mix.Length; i++)
            {
                var e = entries.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("sfx").enumValueIndex = (int)mix[i].sfx;
                e.FindPropertyRelative("clip").objectReferenceValue = Clip(mix[i].clip);
                e.FindPropertyRelative("volume").floatValue = mix[i].volume;
                e.FindPropertyRelative("pitchSpread").floatValue = mix[i].sfx == Sfx.Blip ? 0.12f : 0.05f;
                e.FindPropertyRelative("minInterval").floatValue = mix[i].interval;
            }
            var playlist = so.FindProperty("playlist");
            string[] tracks = { "Assets/Audio/Music/etirwer.ogg", "Assets/Audio/Music/bossa_shop.mp3" };
            playlist.arraySize = tracks.Length;
            for (int i = 0; i < tracks.Length; i++)
                playlist.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(tracks[i]);
            so.FindProperty("crowd").objectReferenceValue = crowd;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // The whole shop starts old and grimy; renovations clean it up step by step.
        static void BuildShopAge(Transform world)
        {
            var spotRenderers = new HashSet<Renderer>(world.GetComponentsInChildren<RenovationSpot>(true).SelectMany(s => s.worn));
            var renderers = new List<Renderer>();
            foreach (string group in new[] { "Room", "Dressing", "Decor", "FlourStorage", "Mixer", "Oven", "Counter" })
            {
                var t = world.Find(group);
                if (t == null)
                    continue;
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer || r.GetComponentInParent<ChibiAnimator>() != null || spotRenderers.Contains(r))
                        continue;
                    var mat = r.sharedMaterial;
                    if (mat == null || mat.shader == null || mat.shader.name != "MergeResto/Toon" || mat.GetColor("_EmissionColor").maxColorComponent > 0.01f)
                        continue;
                    renderers.Add(r);
                }
            }
            var age = world.gameObject.AddComponent<ShopAge>();
            age.renderers = renderers.ToArray();
        }

        static void WarmLight(Transform parent, Vector3 floorPos)
        {
            var lightGo = new GameObject("WarmLight");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.position = new Vector3(floorPos.x, 2.8f, floorPos.z);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.52f);
            light.range = 4.5f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------------ Street

        // The neighbourhood around the shop: grass, sidewalks, a road with traffic, street
        // furniture in front (kept low so it never hides the shop) and houses and trees behind.
        static void BuildStreet(Transform world)
        {
            var street = Group("Street", world);

            var grass = LookSetup.Box("Grass", street, new Vector3(0f, -0.12f, 4f), new Vector3(90f, 0.2f, 70f),
                LookSetup.Mat("Grass", LookSetup.Hex("A9CC8A"), rim: 0f));
            grass.name = "Grass";

            GameObject City(string model, Vector3 pos, float yaw = 0f, float scale = CityScale)
            {
                var go = Place(model, CityArt, street, pos, yaw);
                go.transform.localScale *= scale;
                return go;
            }
            GameObject Nature(string model, Vector3 pos, float yaw = 0f, float scale = 1f, bool sway = true)
            {
                var go = Place(model, ForestArt, street, pos, yaw);
                go.transform.localScale *= scale;
                if (sway) ApplySway(go);
                return go;
            }

            // Sidewalks: front (along x) and right side (along z), 3-unit pavement tiles.
            float tile = 2f * 1.5f;
            for (float x = -22.5f; x <= 25f; x += tile)
                City("base", new Vector3(x, -0.13f, -8.0f), 0f, 1.5f);
            for (float z = -5f; z <= 22f; z += tile)
                City("base", new Vector3(9.95f, -0.13f, z), 0f, 1.5f);

            // Road in front of the shop: asphalt, curbs, dashed centre line, zebra crossing.
            var asphalt = LookSetup.Mat("Asphalt", LookSetup.Hex("67626B"), rim: 0f);
            var curb = LookSetup.Mat("Curb", LookSetup.Hex("D9D2C8"), rim: 0f);
            var paint = LookSetup.Mat("RoadPaint", LookSetup.Hex("FFF6E2"), rim: 0f);
            LookSetup.Box("Road", street, new Vector3(2f, -0.06f, -12.6f), new Vector3(90f, 0.1f, 5.6f), asphalt);
            LookSetup.Box("CurbNear", street, new Vector3(2f, -0.02f, -9.62f), new Vector3(90f, 0.18f, 0.35f), curb);
            LookSetup.Box("CurbFar", street, new Vector3(2f, -0.02f, -15.58f), new Vector3(90f, 0.18f, 0.35f), curb);
            for (float x = -40f; x <= 44f; x += 3f)
                if (Mathf.Abs(x - EntranceX) > 2.5f)
                    LookSetup.Box("Dash", street, new Vector3(x, 0.0f, -12.6f), new Vector3(1.5f, 0.02f, 0.18f), paint);
            for (int i = 0; i < 5; i++)
                LookSetup.Box("Zebra", street, new Vector3(EntranceX - 1.7f + i * 0.85f, 0.0f, -12.6f), new Vector3(0.45f, 0.02f, 5.0f), paint);

            // Flower beds along the shop front.
            var soil = LookSetup.Mat("Soil", LookSetup.Hex("8A5A3C"), rim: 0f);
            string[] petals = { "FF8FB1", "FFD35C", "FFFFFF", "C9A0FF", "FF8C66" };
            void FlowerBed(Vector3 center, float length, bool alongX)
            {
                var size = alongX ? new Vector3(length, 0.25f, 0.7f) : new Vector3(0.7f, 0.25f, length);
                LookSetup.Box("FlowerBed", street, center + Vector3.up * 0.1f, size, soil);
                int count = Mathf.RoundToInt(length * 2.2f);
                for (int i = 0; i < count; i++)
                {
                    float t = (i + 0.5f) / count - 0.5f;
                    var offset = alongX ? new Vector3(t * length, 0f, ((i * 37) % 3 - 1) * 0.18f) : new Vector3(((i * 37) % 3 - 1) * 0.18f, 0f, t * length);
                    var stemPos = center + offset + Vector3.up * 0.3f;
                    LookSetup.Sphere("Leaf", street, stemPos, new Vector3(0.28f, 0.2f, 0.28f), LookSetup.Mat("Leaves", LookSetup.Hex("86C46E"), sway: true));
                    LookSetup.Sphere("Petal", street, stemPos + Vector3.up * 0.16f, Vector3.one * 0.17f,
                        LookSetup.Mat($"Petal{i % petals.Length}", LookSetup.Hex(petals[i % petals.Length]), rim: 0.3f, sway: true));
                }
            }
            FlowerBed(new Vector3(-4.5f, 0f, -6.75f), 6.5f, true);
            FlowerBed(new Vector3(1.8f, 0f, -6.75f), 4f, true);
            FlowerBed(new Vector3(8.75f, 0f, 2.5f), 5f, false);

            // Street furniture along the front sidewalk (low, so the view stays clear).
            foreach (float x in new[] { -14f, -4f, 13f })
                City("streetlight", new Vector3(x, 0f, -9.2f), 180f);
            City("bench", new Vector3(-9f, 0f, -8.9f), 180f);
            City("bench", new Vector3(1f, 0f, -8.9f), 180f);
            City("firehydrant", new Vector3(11.4f, 0f, -9.2f));
            City("trash_A", new Vector3(3.8f, 0f, -9.2f));
            City("streetlight", new Vector3(11.4f, 0f, 3f), 90f);
            City("streetlight", new Vector3(11.4f, 0f, 14f), 90f);

            // Parked and passing cars. KayKit cars have their origin at the wheel centres, so
            // they are lifted until the tyres rest on the asphalt.
            const float CarLift = 0.175f;
            City("car_hatchback", new Vector3(-12f, CarLift, -10.6f), 90f);
            City("car_taxi", new Vector3(18f, CarLift, -10.6f), 90f);
            var mover = City("car_sedan", new Vector3(-20f, CarLift, -14.1f), 90f);
            var loop = mover.AddComponent<MergeResto.Look.LoopMover>();
            loop.from = new Vector3(-30f, CarLift, -14.1f);
            loop.to = new Vector3(34f, CarLift, -14.1f);
            loop.speed = 5f;
            var mover2 = City("car_stationwagon", new Vector3(20f, CarLift, -12.1f), -90f);
            var loop2 = mover2.AddComponent<MergeResto.Look.LoopMover>();
            loop2.from = new Vector3(34f, CarLift, -12.1f);
            loop2.to = new Vector3(-30f, CarLift, -12.1f);
            loop2.speed = 4f;
            loop2.startOffset = 0.45f;

            // Hedges and flowers right outside the low walls.
            foreach (float x in new[] { -8.4f, -0.7f, 4.4f })
                Nature("Bush_1_" + "ABCDEFG"[(int)Mathf.Abs(x * 3) % 7] + "_Color1", new Vector3(x, 0f, -6.75f), x * 40f, 1.0f);
            foreach (float z in new[] { -0.4f, 5.6f })
                Nature("Bush_2_" + "ABCDEF"[(int)Mathf.Abs(z * 3) % 6] + "_Color1", new Vector3(8.75f, 0f, z), z * 50f, 0.9f);

            // Behind the shop: houses peeking over the walls, trees and greenery.
            City("building_C", new Vector3(-4f, 0f, 12.5f), 180f);
            City("building_A", new Vector3(4.5f, 0f, 12.5f), 180f);
            City("building_E", new Vector3(13f, 0f, 12.5f), 180f);
            City("building_B", new Vector3(-15.5f, 0f, 2f), 90f);
            City("building_D", new Vector3(-15.5f, 0f, -6f), 90f);
            Nature("Tree_1_A_Color1", new Vector3(-10.5f, 0f, 8.5f), 20f, 1.3f);
            Nature("Tree_2_B_Color1", new Vector3(-0.5f, 0f, 9f), 80f, 1.25f);
            Nature("Tree_3_A_Color1", new Vector3(9f, 0f, 9f), 140f, 1.3f);
            Nature("Tree_4_A_Color1", new Vector3(-11f, 0f, -2f), 200f, 1.2f);
            Nature("Tree_1_C_Color1", new Vector3(-11.5f, 0f, -9.5f), 60f, 1.1f);
            Nature("Tree_2_A_Color1", new Vector3(15f, 0f, 4f), 300f, 1.2f);
            foreach (var (x, z) in new[] { (-9.5f, 4f), (-9.8f, -4.5f), (2f, 7.6f), (6.5f, 7.4f), (12.5f, -4f), (13.5f, 8f) })
                Nature("Bush_1_" + "ABCDEFG"[(int)Mathf.Abs(x + z) % 7] + "_Color1", new Vector3(x, 0f, z), x * 30f, 1.1f);
            // Grass tufts in little clumps rather than lone blades.
            foreach (var (x, z) in new[] { (-10f, 1f), (-9.2f, 7f), (-6f, 8f), (4f, 8.4f), (-13f, -7f) })
                for (int i = 0; i < 4; i++)
                    Nature("Grass_1_" + "ABCD"[i] + "_Color1", new Vector3(x + (i % 2) * 0.5f - 0.25f, 0f, z + (i / 2) * 0.5f - 0.25f), x * 70f + i * 90f, 1.6f);

            // A little park across the side street: path, bench, lamp, flowers and trees.
            var path = LookSetup.Mat("ParkPath", LookSetup.Hex("E6D3B3"), rim: 0f);
            LookSetup.Box("ParkPath", street, new Vector3(17f, -0.015f, -5f), new Vector3(1.6f, 0.02f, 9f), path);
            LookSetup.Box("ParkPath", street, new Vector3(17f, -0.015f, -5f), new Vector3(9f, 0.02f, 1.6f), path);
            City("bench", new Vector3(15.6f, 0f, -2.5f), 90f);
            City("streetlight", new Vector3(18.4f, 0f, -7.4f), -90f);
            FlowerBed(new Vector3(14.2f, 0f, -7.6f), 3.2f, true);
            FlowerBed(new Vector3(19.6f, 0f, -2.6f), 3.2f, true);
            Nature("Tree_1_B_Color1", new Vector3(23.5f, 0f, -5.5f), 40f, 1.2f);
            Nature("Tree_3_B_Color1", new Vector3(21.5f, 0f, 2.5f), 160f, 1.1f);
            Nature("Bush_2_A_Color1", new Vector3(19.8f, 0f, 0.6f), 30f, 1.1f);
            Nature("Bush_1_C_Color1", new Vector3(13.6f, 0f, -9.6f), 75f, 1.0f);
            Nature("Rock_1_A_Color1", new Vector3(-12.5f, 0f, 5f), 30f, 1.2f, sway: false);

            // Across the road: a low hedge and flowers, kept short so they never hide the shop.
            for (float x = -20f; x <= 26f; x += 2.3f)
                if (Mathf.Abs(x - EntranceX) > 2f)
                    Nature("Bush_1_" + "ABCDEFG"[(int)Mathf.Abs(x) % 7] + "_Color1", new Vector3(x, 0f, -16.6f), x * 35f, 0.75f);
        }

        // Leafy parts of plants get the gentle vertex sway.
        static void ApplySway(GameObject plant)
        {
            foreach (var r in plant.GetComponentsInChildren<Renderer>())
            {
                var src = r.sharedMaterial;
                if (src == null)
                    continue;
                string path = $"Assets/Art/Materials/{src.name}_Sway.mat";
                var sway = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (sway == null)
                {
                    sway = new Material(src);
                    sway.SetFloat("_Sway", 1f);
                    sway.EnableKeyword("_SWAY");
                    sway.SetFloat("_SwayStrength", 0.035f);
                    sway.SetFloat("_SwaySpeed", 1.2f);
                    AssetDatabase.CreateAsset(sway, path);
                }
                r.sharedMaterial = sway;
            }
        }

        // ------------------------------------------------------------------ FX

        const string FxFolder = "Assets/Art/FX";

        static FxDirector BuildFx(Transform world, StationView[] stations, CustomerCrowd crowd, FloatingTextLayer popups, CoinFlyLayer coinFly)
        {
            var fx = Group("FX", world);
            var sparkle = Burst(fx, "SparkleBurst", "fx_sparkle", new Color(1f, 0.85f, 0.35f), size: (0.25f, 0.55f), speed: (2.5f, 5f), life: (0.45f, 0.8f), gravity: 0.6f);
            var flour = Burst(fx, "FlourPuff", "fx_puff", new Color(1f, 0.98f, 0.94f, 0.85f), size: (0.5f, 1.0f), speed: (0.6f, 1.6f), life: (0.6f, 1.1f), gravity: -0.15f, grow: true);

            var director = fx.gameObject.AddComponent<FxDirector>();
            var so = new SerializedObject(director);
            var arr = so.FindProperty("stations");
            arr.arraySize = stations.Length;
            for (int i = 0; i < stations.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = stations[i];
            so.FindProperty("crowd").objectReferenceValue = crowd;
            so.FindProperty("popups").objectReferenceValue = popups;
            so.FindProperty("coinFly").objectReferenceValue = coinFly;
            so.FindProperty("sparkleBurst").objectReferenceValue = sparkle;
            so.FindProperty("flourPuff").objectReferenceValue = flour;
            so.ApplyModifiedPropertiesWithoutUndo();
            return director;
        }

        static ParticleSystem Burst(Transform parent, string name, string sprite, Color color,
            (float min, float max) size, (float min, float max) speed, (float min, float max) life, float gravity, bool grow = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.min, life.max);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.min, speed.max);
            main.startSize = new ParticleSystem.MinMaxCurve(size.min, size.max);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;
            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, grow ? AnimationCurve.EaseInOut(0, 0.6f, 1, 1.4f) : AnimationCurve.EaseInOut(0, 1f, 1, 0f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial(sprite);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = -10f;
            return ps;
        }

        static GameObject SteamEmitter(Transform parent, Vector3 localPos)
        {
            var go = new GameObject("Steam");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
            main.startColor = new Color(1f, 1f, 1f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.05f;
            var emission = ps.emission;
            emission.rateOverTime = 5f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.15f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.5f, 1, 1.6f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMaterial("fx_puff");
            go.SetActive(false);
            return go;
        }

        static Material ParticleMaterial(string sprite)
        {
            LookSetup.EnsureFolder(FxFolder);
            string path = $"{FxFolder}/{sprite}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            var tex = UiArtGenerator.Load(sprite).texture;
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            // Transparent, alpha blended, no depth write.
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(mat);
            return mat;
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

        // ------------------------------------------------------------------ World helpers

        static GameObject Character(string look, Transform parent, string name)
        {
            var prefab = CharacterLibrary.LoadPrefab(look);
            if (prefab == null)
            {
                CharacterLibrary.BuildAll();
                prefab = CharacterLibrary.LoadPrefab(look);
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.localScale = Vector3.one * CharacterScale;
            return go;
        }

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

            // Tap pins touch down on top of the first machine; the progress ring hovers just
            // above it. Staffed stations (the counter) use the top of the whole counter.
            var first = view.machineVisuals != null && view.machineVisuals.Length > 0 ? view.machineVisuals[0] : null;
            Vector3 top;
            if (first != null && first.GetComponentInChildren<ChibiAnimator>(true) == null)
            {
                var fb = new Bounds(first.transform.position, Vector3.zero);
                bool any = false;
                foreach (var r in first.GetComponentsInChildren<Renderer>(true))
                {
                    if (!any) { fb = r.bounds; any = true; }
                    else fb.Encapsulate(r.bounds);
                }
                top = new Vector3(fb.center.x, fb.max.y + 0.1f, fb.center.z);
            }
            else
                top = new Vector3(b.center.x, b.max.y + 0.1f, b.center.z);
            view.tapAnchor = Marker("TapAnchor", view.transform, top);
            view.hudAnchor.position = top + Vector3.up * 0.75f;
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
