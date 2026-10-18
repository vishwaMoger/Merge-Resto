using System.Collections.Generic;
using System.Linq;
using MergeResto.Game;
using MergeResto.Game.Views;
using MergeResto.Sim;
using UnityEditor;
using UnityEngine;

namespace MergeResto.EditorTools
{
    // Belle's Patisserie, the fourth shop: Grandma's granddaughter's cake shop. Its own layout:
    // a glass pastry display in the middle of the room, an open pastry kitchen behind it (pantry
    // fridges and mixers on the back wall, a decorating island on the right), tea tables on both
    // sides, in through the front-right door and out through the front-left one. Blush walls,
    // mint tiles, lilac curtains and soft morning light.
    public static partial class BakerySceneBuilder
    {
        const float PatisserieEntranceX = 6.6f;
        const float PatisserieExitX = -1.8f;
        static readonly Vector3 PatisserieCounter = new(0.3f, 0f, 1.0f);

        static readonly Color PatisserieWall = new(1f, 0.84f, 0.88f);    // blush pink
        static readonly Color PatisserieTiles = new(0.74f, 1f, 0.9f);    // mint
        static readonly Color PatisserieWood = new(1f, 0.9f, 0.86f);     // pale rose wood
        static readonly Color PatisseriePink = new(1f, 0.72f, 0.8f);
        static readonly Color PatisserieLilac = new(0.85f, 0.76f, 1f);

        static void BuildPatisserieStations(Transform world, StationView[] stations)
        {
            stations[0] = MoveStation(BuildPantryFridge(world), new Vector3(-5.6f, 0f, 5.15f));
            stations[1] = MoveStation(BuildBatterMixer(world), new Vector3(-2.2f, 0f, 5.0f));
            stations[2] = MoveStation(BuildDecoratingIsland(world), new Vector3(4.4f, 0f, 3.3f));
        }

        static Material PastelMat(string name, Color color, float rim = 0.2f) => LookSetup.Mat(name, color, rim: rim);

        // Three fridges of eggs, milk and cream along the back wall, with full cartons in front.
        static StationView BuildPantryFridge(Transform world)
        {
            var view = NewStation(world, "PantryFridge", Vector3.zero, new Vector3(0f, 0f, -1.65f), 2.9f);
            var t = view.transform;
            var slots = new List<GameObject>();
            for (int m = 0; m < 3; m++)
            {
                var fridge = Place(m == 1 ? "fridge_A_decorated" : "fridge_A", RestaurantArt, t, new Vector3(-1.15f + m * 1.15f, 0f, 0.1f), 180f, local: true);
                Tint(fridge, new Color(1f, 0.93f, 0.95f), "Blush");
                slots.Add(fridge);
            }
            view.machineVisuals = slots.ToArray();
            // A low cart in front: the eggs and milk waiting to be mixed.
            LookSetup.Box("Cart", t, new Vector3(0f, 0.3f, -0.85f), new Vector3(2.9f, 0.6f, 0.5f), PastelMat("PantryCart", LookSetup.Hex("E7B7C0")));
            LookSetup.Box("CartTop", t, new Vector3(0f, 0.62f, -0.85f), new Vector3(2.98f, 0.05f, 0.56f), PastelMat("MarbleTop", LookSetup.Hex("FFF7F2")));
            var stock = new List<GameObject>();
            for (int i = 0; i < 6; i++)
            {
                var item = i % 2 == 0
                    ? Place("milk", t, new Vector3(-1.25f + i * 0.5f, 0.65f, -0.85f), 0f, local: true)
                    : Place("egg_A", t, new Vector3(-1.25f + i * 0.5f, 0.65f, -0.85f), 0f, local: true);
                if (i % 2 == 1)
                    item.transform.localScale *= 2.2f;
                stock.Add(item);
            }
            view.stockVisuals = stock.ToArray();
            AddClickCollider(view);
            return view;
        }

        // Pink stand mixers on a marble worktop; bowls of batter wait in front.
        static StationView BuildBatterMixer(Transform world)
        {
            var view = NewStation(world, "BatterMixer", Vector3.zero, new Vector3(0f, 0f, -1.7f), 2.6f);
            var t = view.transform;
            Place("countertop_straight_A_large", t, new Vector3(-0.5f, 0f, 0f), 180f, local: true);
            Place("countertop_straight_A_short", t, new Vector3(1f, 0f, 0f), 180f, local: true);
            var slots = new List<GameObject>();
            for (int m = 0; m < 3; m++)
            {
                var mixer = Place("stand_mixer", t, new Vector3(-1f + m * 1f, 1f, 0.1f), 180f, local: true);
                Tint(mixer, PatisseriePink, "Pink");
                slots.Add(mixer);
            }
            view.machineVisuals = slots.ToArray();
            view.stockVisuals = Row("mixing_bowl", t, new Vector3(-1.1f, 1f, -0.5f), new Vector3(0.45f, 0f, 0f), 6, 0f);
            Place("whisk", t, new Vector3(1.35f, 1f, 0.05f), 60f, local: true);
            AddClickCollider(view);
            return view;
        }

        // The decorating island: three cakes on stands being finished, cupcakes lined up in front.
        // The pastry chef works from behind it, facing the room.
        static StationView BuildDecoratingIsland(Transform world)
        {
            var view = NewStation(world, "DecoratingIsland", Vector3.zero, new Vector3(0f, 0f, 1.4f), 2.6f);
            var t = view.transform;
            LookSetup.Box("Island", t, new Vector3(0f, 0.45f, 0f), new Vector3(3.2f, 0.9f, 1.05f), PastelMat("IslandBody", LookSetup.Hex("F2A9B8")));
            LookSetup.Box("IslandTop", t, new Vector3(0f, 0.93f, 0f), new Vector3(3.32f, 0.07f, 1.15f), PastelMat("MarbleTop", LookSetup.Hex("FFF7F2")));
            LookSetup.Box("IslandTrim", t, new Vector3(0f, 0.08f, 0f), new Vector3(3.26f, 0.16f, 1.1f), PastelMat("IslandTrim", LookSetup.Hex("FFFFFF")));
            string[] cakes = { "cake_strawberry", "cake_chocolate", "cake_birthday" };
            var slots = new List<GameObject>();
            for (int m = 0; m < 3; m++)
            {
                var spot = Group($"CakeSpot{m}", t);
                spot.localPosition = new Vector3(-1.05f + m * 1.05f, 0.97f, 0.15f);
                Place("pastry_stand_A", spot, Vector3.zero, 0f, local: true);
                var cake = Place(cakes[m], GoodsArt, spot, new Vector3(0f, 0.12f, 0f), m * 40f, local: true);
                cake.transform.localScale *= 0.6f;
                slots.Add(spot.gameObject);
            }
            view.machineVisuals = slots.ToArray();
            view.stockVisuals = Row("cupcake", GoodsArt, t, new Vector3(-1.25f, 0.97f, -0.38f), new Vector3(0.5f, 0f, 0f), 6, 0f);
            AddClickCollider(view);
            return view;
        }

        static ShopConfig CreatePatisserieConfig()
        {
            LookSetup.EnsureFolder("Assets/Game/Config");
            var config = ScriptableObject.CreateInstance<ShopConfig>();
            var shop = config.shop;
            shop.id = "belles_patisserie";
            shop.displayName = "Belle's Patisserie";
            shop.hasTutorial = false;
            shop.customerIntervalSeconds = 4.3f;
            shop.itemsPerOrder = 2;
            shop.pricePerItem = 600;
            shop.patienceSeconds = 24f;
            shop.startCoins = 30000;
            shop.stations.Add(Station("pantry", "Pantry Fridge", StationKind.Source, 2.5f, 6, 40,
                "Quick Restock", "Bigger Cartons", "Extra Fridge"));
            shop.stations.Add(Station("mixer", "Batter Mixer", StationKind.Processor, 3f, 6, 60,
                "Faster Whisking", "Bigger Bowls", "Add Mixer"));
            shop.stations.Add(Station("decorating", "Decorating Island", StationKind.Processor, 4f, 6, 80,
                "Swift Piping", "Fancier Cakes", "Add Station"));
            var counter = Station("counter", "Pastry Display", StationKind.Seller, 1.2f, 0, 0,
                "Charming Service", "New Recipes", "Add Cashier");
            counter.startsAutomated = true;
            shop.stations.Add(counter);
            for (int i = 0; i < 3; i++)
                shop.couriers.Add(new CourierDefinition { carry = 6, walkSeconds = 1.5f });
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        static void EnsurePatisserieGoals(ShopConfig config)
        {
            var goals = config.shop.goals;
            GoalDefinition G(GoalKind kind, double target, int station = 0, double coins = 0, int gems = 0) =>
                new() { kind = kind, target = target, station = station, rewardCoins = coins, rewardGems = gems };
            goals.Add(G(GoalKind.ServeCustomers, 10, coins: 12000));
            goals.Add(G(GoalKind.StationLevel, 2, station: 1, gems: 25));
            goals.Add(G(GoalKind.StationLevel, 2, station: 3, gems: 25));
            goals.Add(G(GoalKind.HireManager, 1, station: 0, coins: 20000));
            goals.Add(G(GoalKind.Renovations, 1, gems: 30));
            goals.Add(G(GoalKind.HireManager, 1, station: 1, coins: 36000));
            goals.Add(G(GoalKind.StationLevel, 4, station: 2, gems: 35));
            goals.Add(G(GoalKind.AddMachines, 1, station: 2, gems: 45));
            goals.Add(G(GoalKind.Renovations, 3, gems: 60));
            goals.Add(G(GoalKind.LifetimeCoins, 2000000, gems: 70));
            goals.Add(G(GoalKind.HireManager, 1, station: 2, coins: 120000));
            goals.Add(G(GoalKind.ServeCustomers, 300, coins: 240000));
            goals.Add(G(GoalKind.Renovations, 6, gems: 120));
            goals.Add(G(GoalKind.ShopLevel, 50, gems: 180));
            EditorUtility.SetDirty(config);
        }

        // Tea tables on both sides of the display: pink cups, plates, macarons and cake slices.
        static void BuildPatisserieDining(Transform world)
        {
            var dining = Group("Dining", world);
            void Table(Vector3 p)
            {
                Place("table_round_B", dining, p);
                Place("chair", dining, p + new Vector3(-1.05f, 0f, 0f), 90f);
                Place("chair", dining, p + new Vector3(1.05f, 0f, 0f), -90f);
                Place("mug_A_pink", dining, p + new Vector3(-0.24f, 1f, 0.12f));
                Place("plate", dining, p + new Vector3(0.26f, 1f, -0.1f));
                Place("cake_strawberry_slice", GoodsArt, dining, p + new Vector3(0.26f, 1.03f, -0.1f), 30f);
                Place("macaron_blue", dining, p + new Vector3(-0.05f, 1f, -0.28f));
            }
            Table(new Vector3(-5f, 0f, -1.4f));
            Table(new Vector3(-4.6f, 0f, -4.4f));
            Table(new Vector3(5.0f, 0f, -1.6f));
        }

        // Blush walls, mint tiles, lilac curtains, macarons on the shelves and soft morning light.
        static void StylePatisserie(Transform world, Camera cam)
        {
            var room = world.Find("Room");
            foreach (Transform child in room)
            {
                if (child.name.StartsWith("wall_"))
                    Tint(child.gameObject, PatisserieWall, "Patisserie");
                else if (child.name.StartsWith("floor_tiled"))
                    Tint(child.gameObject, PatisserieTiles, "Patisserie");
                else if (child.name.StartsWith("floor_"))
                    Tint(child.gameObject, PatisserieWood, "Patisserie");
                else if (child.name == "LowWalls")
                    foreach (Transform w in child.GetComponentsInChildren<Transform>(true))
                        if (w.name.StartsWith("wall_"))
                            Tint(w.gameObject, PatisserieWall, "Patisserie");
            }
            var dress = world.Find("Dressing");
            foreach (Transform child in dress)
            {
                if (child.name.StartsWith("curtains"))
                    Tint(child.gameObject, PatisserieLilac, "Lilac");
                if (child.name.StartsWith("tin_") || child.name.StartsWith("cookie_jar"))
                    Object.DestroyImmediate(child.gameObject);
            }
            // Macarons and a little cake stand on the back shelf.
            Place("macaron_pink", dress, new Vector3(-3.7f, 2.78f, 5.3f));
            Place("macaron_blue", dress, new Vector3(-3.4f, 2.78f, 5.3f));
            Place("macaron_yellow", dress, new Vector3(-3.1f, 2.78f, 5.3f));
            Place("pastry_stand_A_decorated", dress, new Vector3(-2.45f, 2.78f, 5.3f));
            foreach (var tin in room.Cast<Transform>().Where(c => c.name.StartsWith("tin_") || c.name.StartsWith("cookie_jar")).ToList())
            {
                var pos = tin.position;
                Object.DestroyImmediate(tin.gameObject);
                Place("macaron_pink", room, pos);
            }

            var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            if (sun != null)
            {
                sun.color = new Color(1f, 0.9f, 0.93f);
                sun.intensity = 1.0f;
                sun.transform.rotation = Quaternion.Euler(36f, -40f, 0f);
            }
            cam.backgroundColor = new Color(0.99f, 0.88f, 0.91f);
        }
    }
}
