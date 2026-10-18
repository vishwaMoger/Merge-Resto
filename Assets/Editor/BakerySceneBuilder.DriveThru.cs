using System.Collections.Generic;
using System.Linq;
using MergeResto.Game;
using MergeResto.Game.Views;
using MergeResto.Sim;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace MergeResto.EditorTools
{
    // Sunny's Drive-Thru, the fifth shop: burgers and shakes to the car window. Its customers are
    // cars: they pull in off the road on the left, queue along the lane that runs past the front of
    // the shop, stop at the pickup window at the right end of the front wall, right at the counter,
    // and drive off past the side lot. Inside, a diner kitchen: walk-in fridges, the burger grill and the packing counter
    // along the back wall, the pickup counter at the window, and a picnic corner for the staff. The
    // side lot is a little garden with the menu board. Expanding lights the lane in neon, then gives
    // the whole place a retro makeover.
    public static partial class BakerySceneBuilder
    {
        static readonly Vector3 DriveCounter = new(5.0f, 0f, -4.9f);
        const float DriveWindowX = 5.75f;      // the gap in the front wall, at the register
        const float DriveLaneZ = -7.6f;        // the lane along the front of the shop
        const float DriveSlotGap = 3.6f;
        const float DriveRoadZ = -10.9f;       // the curb lane of the street
        const float DriveCarLift = 0.175f;     // KayKit cars have their origin at the wheel centres

        static readonly Color DriveWall = new(1f, 0.95f, 0.8f);     // butter cream
        static readonly Color DriveTiles = new(1f, 0.86f, 0.84f);   // coral checks
        static readonly Color DriveRed = new(0.93f, 0.3f, 0.27f);

        static void BuildDriveStations(Transform world, StationView[] stations)
        {
            stations[0] = MoveStation(BuildWalkInFridge(world), new Vector3(-5.6f, 0f, 5.15f));
            stations[1] = MoveStation(BuildBurgerGrill(world), new Vector3(-1.8f, 0f, 5.0f));
            stations[2] = MoveStation(BuildPackingIsland(world), new Vector3(3.4f, 0f, 4.9f));
        }

        static Material DriveMat(string name, string hex, float rim = 0.2f, Color? emission = null) =>
            LookSetup.Mat(name, LookSetup.Hex(hex), emission: emission, rim: rim);

        static StationView BuildWalkInFridge(Transform world)
        {
            var view = NewStation(world, "WalkInFridge", Vector3.zero, new Vector3(0f, 0f, -1.65f), 2.9f);
            var t = view.transform;
            var slots = new List<GameObject>();
            for (int m = 0; m < 3; m++)
                slots.Add(Place(m == 1 ? "fridge_A_decorated" : "fridge_B", RestaurantArt, t, new Vector3(-1.15f + m * 1.15f, 0f, 0.1f), 180f, local: true));
            view.machineVisuals = slots.ToArray();
            LookSetup.Box("Cart", t, new Vector3(0f, 0.3f, -0.85f), new Vector3(2.9f, 0.6f, 0.5f), DriveMat("SteelCart", "C9CED6"));
            LookSetup.Box("CartTop", t, new Vector3(0f, 0.62f, -0.85f), new Vector3(2.98f, 0.05f, 0.56f), DriveMat("SteelTop", "EEF1F5"));
            view.stockVisuals = Row("food_ingredient_burger_uncooked", RestaurantArt, t, new Vector3(-1.25f, 0.66f, -0.85f), new Vector3(0.5f, 0f, 0f), 6, 0f);
            foreach (var p in view.stockVisuals)
                p.transform.localScale *= 1.3f;
            AddClickCollider(view);
            return view;
        }

        // Three grills under one hood, with burgers sizzling on the front shelf.
        static StationView BuildBurgerGrill(Transform world)
        {
            var view = NewStation(world, "BurgerGrill", Vector3.zero, new Vector3(0f, 0f, -1.7f), 3.0f);
            var t = view.transform;
            var slots = new List<GameObject>();
            for (int m = 0; m < 3; m++)
                slots.Add(Place("stove_multi_countertop", RestaurantArt, t, new Vector3(-1.1f + m * 1.1f, 0f, 0.1f), 180f, local: true));
            view.machineVisuals = slots.ToArray();
            var hood = Place("extractorhood", RestaurantArt, t, new Vector3(0f, 2.3f, 0.25f), 180f, local: true);
            hood.transform.localScale = Vector3.Scale(hood.transform.localScale, new Vector3(2.6f, 1f, 1f));
            LookSetup.Box("Shelf", t, new Vector3(0f, 0.45f, -0.75f), new Vector3(3.2f, 0.9f, 0.38f), DriveMat("SteelCart", "C9CED6"));
            LookSetup.Box("ShelfTop", t, new Vector3(0f, 0.92f, -0.75f), new Vector3(3.28f, 0.05f, 0.44f), DriveMat("SteelTop", "EEF1F5"));
            view.stockVisuals = Row("food_ingredient_burger_cooked", RestaurantArt, t, new Vector3(-1.25f, 0.95f, -0.75f), new Vector3(0.5f, 0f, 0f), 6, 0f);
            foreach (var p in view.stockVisuals)
                p.transform.localScale *= 1.3f;

            var glow = new GameObject("GrillGlow");
            glow.transform.SetParent(t, false);
            glow.transform.localPosition = new Vector3(-1.1f, 1.3f, -0.6f);
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.25f);
            light.range = 3.2f;
            light.intensity = 2.0f;
            light.shadows = LightShadows.None;
            glow.SetActive(false);
            var smoke = SteamEmitter(t, new Vector3(-1.1f, 1.6f, 0.1f));
            view.runningEffects = new[] { glow, smoke };
            AddClickCollider(view);
            return view;
        }

        // The packing counter: three trays being filled with a burger, fries and a shake.
        static StationView BuildPackingIsland(Transform world)
        {
            var view = NewStation(world, "PackingIsland", Vector3.zero, new Vector3(0f, 0f, -1.6f), 2.5f);
            var t = view.transform;
            LookSetup.Box("Island", t, new Vector3(0f, 0.45f, 0f), new Vector3(3.2f, 0.9f, 1.0f), DriveMat("DinerRed", "E6574A"));
            LookSetup.Box("IslandTop", t, new Vector3(0f, 0.93f, 0f), new Vector3(3.32f, 0.07f, 1.12f), DriveMat("ChromeTop", "F4F6F8", rim: 0.4f));
            LookSetup.Box("IslandStripe", t, new Vector3(0f, 0.6f, -0.505f), new Vector3(3.22f, 0.12f, 0.02f), DriveMat("DinerWhite", "FFFFFF"));
            var shelf = Place("wall_shelf_bakery_B", t, new Vector3(0f, 1.85f, 0.7f), 180f, local: true);
            Place("coffee_cup_takeaway_stacked", t, new Vector3(-0.6f, 1.88f, 0.4f), 0f, local: true);
            Place("ketchup", RestaurantArt, t, new Vector3(0.2f, 1.88f, 0.4f), 0f, local: true);
            Place("mustard", RestaurantArt, t, new Vector3(0.5f, 1.88f, 0.4f), 0f, local: true);
            var slots = new List<GameObject>();
            for (int m = 0; m < 3; m++)
            {
                var spot = Group($"Tray{m}", t);
                spot.localPosition = new Vector3(-1.05f + m * 1.05f, 0.97f, 0.12f);
                Place("serving_tray", spot, Vector3.zero, 0f, local: true);
                Place("food_burger", RestaurantArt, spot, new Vector3(-0.12f, 0.03f, 0f), 20f, local: true);
                Place("food_ingredient_potato_chopped", RestaurantArt, spot, new Vector3(0.16f, 0.03f, 0.05f), 0f, local: true);
                Place("coffee_cup_takeaway", spot, new Vector3(0.12f, 0.03f, -0.16f), 0f, local: true);
                slots.Add(spot.gameObject);
            }
            view.machineVisuals = slots.ToArray();
            view.stockVisuals = Row("food_burger", RestaurantArt, t, new Vector3(-1.25f, 0.97f, -0.38f), new Vector3(0.5f, 0f, 0f), 6, 0f);
            AddClickCollider(view);
            return view;
        }

        // ------------------------------------------------------------------ Cars

        static void BuildDriveCustomers(Transform root, CustomerCrowd crowd)
        {
            crowd.vehicles = true;
            crowd.walkSpeed = 5.5f;
            crowd.dineInChance = 0f;
            float tail = DriveWindowX - DriveSlotGap * 5f;
            crowd.entryPath = new[]
            {
                Marker("Entry_Road", root, new Vector3(-44f, 0f, DriveRoadZ)),
                Marker("Entry_Turn", root, new Vector3(tail - 4.2f, 0f, DriveRoadZ)),
                Marker("Entry_Lane", root, new Vector3(tail - 2.0f, 0f, DriveLaneZ)),
            };
            crowd.exitPath = new[]
            {
                Marker("Exit_Lane", root, new Vector3(9.6f, 0f, DriveLaneZ)),
                Marker("Exit_Turn", root, new Vector3(12.2f, 0f, DriveRoadZ)),
                Marker("Exit_Road", root, new Vector3(44f, 0f, DriveRoadZ)),
            };
            // The line, front first: at the window, then back along the front of the shop to the left.
            var slots = new List<Transform>();
            for (int i = 0; i < 6; i++)
            {
                var s = Marker($"QueueSlot{i}", root, new Vector3(DriveWindowX - DriveSlotGap * i, 0f, DriveLaneZ));
                s.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
                slots.Add(s);
            }
            crowd.queueSlots = slots.ToArray();

            var gold = DriveMat("CrownGold", "FFC83D", rim: 0.4f);
            var cloud = DriveMat("GrumpCloud", "7E7A86", rim: 0f);
            var templates = new List<GameObject>();
            foreach (var model in new[] { "car_sedan", "car_hatchback", "car_stationwagon", "car_taxi", "car_police" })
            {
                if (model == "car_police")
                    continue; // no police in the burger line
                var car = new GameObject($"Template_{model}");
                car.transform.SetParent(root, false);
                var body = Place(model, CityArt, car.transform, new Vector3(0f, DriveCarLift, 0f), 0f, local: true);
                body.transform.localScale *= CityScale;
                // VIPs get a golden crown on the roof; cars that give up leave under a little cloud.
                var crown = Group("Crown", car.transform);
                crown.localPosition = new Vector3(0f, 1.75f, 0f);
                LookSetup.Cylinder("Band", crown, Vector3.zero, new Vector3(0.55f, 0.12f, 0.55f), gold);
                for (int k = 0; k < 5; k++)
                {
                    float a = k * Mathf.PI * 2f / 5f;
                    LookSetup.Sphere("Point", crown, new Vector3(Mathf.Cos(a) * 0.24f, 0.2f, Mathf.Sin(a) * 0.24f), Vector3.one * 0.13f, gold);
                }
                var grump = Group("Grump", car.transform);
                grump.localPosition = new Vector3(0f, 2.1f, 0f);
                LookSetup.Sphere("Puff", grump, Vector3.zero, new Vector3(0.6f, 0.4f, 0.5f), cloud);
                LookSetup.Sphere("Puff", grump, new Vector3(0.3f, 0.06f, 0f), new Vector3(0.45f, 0.34f, 0.4f), cloud);
                LookSetup.Sphere("Puff", grump, new Vector3(-0.3f, 0.04f, 0f), new Vector3(0.42f, 0.3f, 0.4f), cloud);
                car.SetActive(false);
                templates.Add(car);
            }
            crowd.templates = templates.ToArray();
        }

        // ------------------------------------------------------------------ The lane

        static void BuildDriveLane(Transform world)
        {
            var lane = Group("DriveLane", world);
            var asphalt = DriveMat("LaneAsphalt", "6C6872", rim: 0f);
            var paint = DriveMat("LanePaint", "FFF6E2", rim: 0f);
            var curb = DriveMat("LaneCurb", "E3DCD2", rim: 0f);
            float tail = DriveWindowX - DriveSlotGap * 5f;
            float left = tail - 4.6f, right = 13.6f;
            // The lane over the front sidewalk, with ramps down to the street at both ends.
            LookSetup.Box("Lane", lane, new Vector3((left + right) / 2f, 0.01f, DriveLaneZ), new Vector3(right - left, 0.03f, 2.6f), asphalt);
            LookSetup.Box("RampIn", lane, new Vector3(tail - 3.2f, 0.005f, (DriveLaneZ + DriveRoadZ) / 2f), new Vector3(3.0f, 0.03f, DriveLaneZ - DriveRoadZ + 1.6f), asphalt);
            LookSetup.Box("RampOut", lane, new Vector3(11.0f, 0.005f, (DriveLaneZ + DriveRoadZ) / 2f), new Vector3(3.0f, 0.03f, DriveLaneZ - DriveRoadZ + 1.6f), asphalt);
            for (float x = left + 1.5f; x < right - 1f; x += 2.4f)
                LookSetup.Box("Dash", lane, new Vector3(x, 0.03f, DriveLaneZ), new Vector3(1.0f, 0.01f, 0.14f), paint);
            // A stop line at the window and a curb along the shop side.
            LookSetup.Box("StopLine", lane, new Vector3(DriveWindowX + 1.6f, 0.03f, DriveLaneZ), new Vector3(0.18f, 0.01f, 2.4f), paint);
            LookSetup.Box("ShopCurb", lane, new Vector3((left + right) / 2f, 0.06f, DriveLaneZ + 1.38f), new Vector3(right - left, 0.12f, 0.2f), curb);

            // The side lot is a little garden: bushes, a tree and a bench in the shade.
            foreach (var (model, pos, scale) in new[] { ("Bush_1_B_Color1", new Vector3(14.6f, 0f, -1.4f), 1.0f), ("Bush_2_C_Color1", new Vector3(10.2f, 0f, 3.9f), 0.9f),
                ("Bush_1_E_Color1", new Vector3(15.0f, 0f, 4.4f), 0.9f), ("Tree_2_A_Color1", new Vector3(14.2f, 0f, 1.6f), 0.9f) })
            {
                var plant = Place(model, ForestArt, lane, pos, pos.x * 30f);
                plant.transform.localScale *= scale;
                ApplySway(plant);
            }
            var bench = Place("bench", CityArt, lane, new Vector3(11.0f, 0f, 0.6f), -90f);
            bench.transform.localScale *= CityScale;

            BuildDriveSigns(lane);
            BuildDriveStages(world);
        }

        static TextMeshPro Words(Transform parent, Vector3 localPos, float yaw, string text, float size, Color color, Vector2 box)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiArtGenerator.DisplayFontPath);
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = box;
            return tmp;
        }

        // The pickup window's awning and sign, the menu board where cars order, and the entry sign.
        static void BuildDriveSigns(Transform lane)
        {
            var red = DriveMat("DinerRed", "E6574A");
            var white = DriveMat("DinerWhite", "FFFFFF");
            var wood = DriveMat("SignWood", "B07A52");
            // Striped awning over the window, the sign facing the street.
            var awning = Group("WindowAwning", lane);
            awning.position = new Vector3(DriveWindowX, 2.25f, -6.45f);
            for (int i = 0; i < 6; i++)
                LookSetup.Box("Stripe", awning, new Vector3(-1.25f + i * 0.5f, 0f, 0f), new Vector3(0.5f, 0.1f, 1.1f), i % 2 == 0 ? red : white);
            LookSetup.Box("Board", awning, new Vector3(0f, 0.42f, -0.55f), new Vector3(2.4f, 0.6f, 0.08f), white);
            Words(awning, new Vector3(0f, 0.42f, -0.6f), 0f, "PICK-UP", 3.4f, DriveRed, new Vector2(2.3f, 0.6f));
            foreach (float x in new[] { -1.3f, 1.3f })
                LookSetup.Box("AwningPost", awning, new Vector3(x, -1.1f, -0.5f), new Vector3(0.1f, 2.2f, 0.1f), white);

            // Menu board and speaker beside the line, where cars wait to order.
            var menu = Group("MenuBoard", lane);
            menu.position = new Vector3(-10.4f, 0f, -5.5f);
            LookSetup.Box("Post", menu, new Vector3(-0.6f, 0.75f, 0f), new Vector3(0.12f, 1.5f, 0.12f), wood);
            LookSetup.Box("Post", menu, new Vector3(0.6f, 0.75f, 0f), new Vector3(0.12f, 1.5f, 0.12f), wood);
            LookSetup.Box("Board", menu, new Vector3(0f, 1.75f, 0f), new Vector3(1.7f, 1.1f, 0.1f), DriveMat("MenuBoard", "3C3A44"));
            Words(menu, new Vector3(0f, 2.05f, -0.07f), 0f, "MENU", 3.2f, new Color(1f, 0.85f, 0.35f), new Vector2(1.6f, 0.4f));
            Words(menu, new Vector3(0f, 1.6f, -0.07f), 0f, "Burgers  Fries\nShakes  Coffee", 1.8f, Color.white, new Vector2(1.6f, 0.7f));
            LookSetup.Box("Speaker", menu, new Vector3(1.15f, 0.6f, -0.3f), new Vector3(0.3f, 1.2f, 0.3f), red);

            // "Drive-Thru" sign where cars turn in.
            var sign = Group("EntrySign", lane);
            sign.position = new Vector3(DriveWindowX - DriveSlotGap * 5f - 3.4f, 0f, -5.6f);
            LookSetup.Box("Pole", sign, new Vector3(0f, 1.3f, 0f), new Vector3(0.14f, 2.6f, 0.14f), white);
            LookSetup.Box("Board", sign, new Vector3(0f, 2.75f, 0f), new Vector3(2.1f, 0.75f, 0.1f), red);
            Words(sign, new Vector3(0f, 2.75f, -0.07f), 0f, "DRIVE-THRU", 3.2f, Color.white, new Vector2(2.0f, 0.6f));
            LookSetup.Box("Arrow", sign, new Vector3(0f, 2.15f, 0f), new Vector3(1.2f, 0.3f, 0.08f), white);
            Words(sign, new Vector3(0f, 2.15f, -0.07f), 0f, "IN  >", 2.2f, DriveRed, new Vector2(1.1f, 0.3f));
        }

        // Expansion stages: neon lights along the lane and a big glowing sign on the lot (1), then
        // a retro makeover inside: chrome trim, a jukebox, neon signs and a plant (2).
        static void BuildDriveStages(Transform world)
        {
            var neon = Group("NeonLane", world);
            neon.gameObject.AddComponent<StageGroup>().minStage = 1;
            var pole = DriveMat("ChromeTop", "F4F6F8", rim: 0.4f);
            var bulbPink = LookSetup.Mat("NeonPink", LookSetup.Hex("FF6FA0"), emission: new Color(0.9f, 0.2f, 0.45f), rim: 0f);
            var bulbYellow = LookSetup.Mat("NeonYellow", LookSetup.Hex("FFD54A"), emission: new Color(0.9f, 0.65f, 0.1f), rim: 0f);
            int n = 0;
            for (float x = DriveWindowX - DriveSlotGap * 5f - 2f; x <= 12f; x += 4f)
            {
                LookSetup.Box("LampPost", neon, new Vector3(x, 0.9f, DriveLaneZ - 1.55f), new Vector3(0.1f, 1.8f, 0.1f), pole);
                LookSetup.Sphere("Bulb", neon, new Vector3(x, 1.9f, DriveLaneZ - 1.55f), Vector3.one * 0.3f, n++ % 2 == 0 ? bulbPink : bulbYellow);
            }
            var big = Group("NeonSign", neon);
            big.position = new Vector3(12.4f, 0f, -3.6f);
            big.rotation = Quaternion.Euler(0f, -15f, 0f);
            LookSetup.Box("Post", big, new Vector3(-1.4f, 1.7f, 0f), new Vector3(0.18f, 3.4f, 0.18f), pole);
            LookSetup.Box("Post", big, new Vector3(1.4f, 1.7f, 0f), new Vector3(0.18f, 3.4f, 0.18f), pole);
            // A dark board with a pink neon frame and glowing letters.
            LookSetup.Box("Board", big, new Vector3(0f, 3.6f, 0f), new Vector3(3.6f, 1.1f, 0.18f), DriveMat("MenuBoard", "3C3A44"));
            LookSetup.Box("FrameTop", big, new Vector3(0f, 4.17f, -0.05f), new Vector3(3.7f, 0.08f, 0.12f), bulbPink);
            LookSetup.Box("FrameBottom", big, new Vector3(0f, 3.03f, -0.05f), new Vector3(3.7f, 0.08f, 0.12f), bulbPink);
            LookSetup.Box("FrameLeft", big, new Vector3(-1.82f, 3.6f, -0.05f), new Vector3(0.08f, 1.2f, 0.12f), bulbPink);
            LookSetup.Box("FrameRight", big, new Vector3(1.82f, 3.6f, -0.05f), new Vector3(0.08f, 1.2f, 0.12f), bulbPink);
            var letters = Words(big, new Vector3(0f, 3.62f, -0.11f), 0f, "SUNNY'S", 5.2f, new Color(1f, 0.55f, 0.75f), new Vector2(3.4f, 1.0f));
            letters.fontStyle = FontStyles.Bold;
            Words(big, new Vector3(0f, 3.2f, -0.11f), 0f, "BURGERS · SHAKES", 1.6f, new Color(1f, 0.85f, 0.4f), new Vector2(3.4f, 0.3f));

            var retro = Group("RetroMakeover", world);
            retro.gameObject.AddComponent<StageGroup>().minStage = 2;
            var chrome = pole;
            var red = DriveMat("DinerRed", "E6574A");
            // Chrome and red trim along the back and left walls.
            LookSetup.Box("TrimBack", retro, new Vector3(0f, 1.25f, 5.92f), new Vector3(16f, 0.14f, 0.06f), chrome);
            LookSetup.Box("TrimBackRed", retro, new Vector3(0f, 1.1f, 5.92f), new Vector3(16f, 0.12f, 0.06f), red);
            LookSetup.Box("TrimLeft", retro, new Vector3(-7.92f, 1.25f, 0f), new Vector3(0.06f, 0.14f, 12f), chrome);
            LookSetup.Box("TrimLeftRed", retro, new Vector3(-7.92f, 1.1f, 0f), new Vector3(0.06f, 0.12f, 12f), red);
            // A jukebox in the picnic corner.
            var juke = Group("Jukebox", retro);
            juke.position = new Vector3(-7.2f, 0f, 1.6f);
            juke.rotation = Quaternion.Euler(0f, 90f, 0f);
            LookSetup.Box("Body", juke, new Vector3(0f, 0.75f, 0f), new Vector3(1.0f, 1.5f, 0.6f), red);
            LookSetup.Sphere("Top", juke, new Vector3(0f, 1.5f, 0f), new Vector3(1.0f, 0.6f, 0.6f), bulbYellow);
            LookSetup.Box("Grille", juke, new Vector3(0f, 0.7f, -0.31f), new Vector3(0.7f, 0.7f, 0.02f), chrome);
            var sign = Group("OpenSign", retro);
            sign.position = new Vector3(-3.6f, 3.2f, 5.85f);
            sign.rotation = Quaternion.Euler(0f, 180f, 0f);
            LookSetup.Box("Back", sign, Vector3.zero, new Vector3(2.6f, 0.8f, 0.06f), DriveMat("MenuBoard", "3C3A44"));
            Words(sign, new Vector3(0f, 0f, 0.05f), 180f, "OPEN 24/7", 3.4f, new Color(1f, 0.55f, 0.75f), new Vector2(2.5f, 0.7f));
            var plant = Place("monstera_plant_large_potted", PlantArt, retro, new Vector3(-7.2f, 0f, -3.4f), -20f);
            ApplySway(plant);
            var lamp = new GameObject("RetroGlow");
            lamp.transform.SetParent(retro, false);
            lamp.transform.position = new Vector3(-3.5f, 2.6f, 0f);
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.75f);
            light.range = 7f;
            light.intensity = 1.4f;
            light.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------------ Config, room, look

        static ShopConfig CreateDriveConfig()
        {
            LookSetup.EnsureFolder("Assets/Game/Config");
            var config = ScriptableObject.CreateInstance<ShopConfig>();
            var shop = config.shop;
            shop.id = "drive_thru";
            shop.displayName = "Sunny's Drive-Thru";
            shop.hasTutorial = false;
            shop.customerIntervalSeconds = 3.8f;
            shop.itemsPerOrder = 2;
            shop.pricePerItem = 2000;
            shop.patienceSeconds = 24f;
            shop.startCoins = 100000;
            shop.stations.Add(Station("fridge", "Walk-in Fridge", StationKind.Source, 2.5f, 6, 50,
                "Quick Restock", "Bigger Boxes", "Extra Fridge"));
            shop.stations.Add(Station("grill", "Burger Grill", StationKind.Processor, 3f, 6, 75,
                "Hotter Grill", "Double Patty", "Add Grill"));
            shop.stations.Add(Station("packing", "Packing Island", StationKind.Processor, 3.5f, 6, 100,
                "Swift Packing", "Combo Meals", "Add Station"));
            var counter = Station("window", "Pickup Window", StationKind.Seller, 1.2f, 0, 0,
                "Speedy Service", "New Menu", "Add Cashier");
            counter.startsAutomated = true;
            shop.stations.Add(counter);
            for (int i = 0; i < 3; i++)
                shop.couriers.Add(new CourierDefinition { carry = 6, walkSeconds = 1.5f });
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        static void EnsureDriveGoals(ShopConfig config)
        {
            var goals = config.shop.goals;
            GoalDefinition G(GoalKind kind, double target, int station = 0, double coins = 0, int gems = 0) =>
                new() { kind = kind, target = target, station = station, rewardCoins = coins, rewardGems = gems };
            goals.Add(G(GoalKind.ServeCustomers, 10, coins: 40000));
            goals.Add(G(GoalKind.StationLevel, 2, station: 1, gems: 30));
            goals.Add(G(GoalKind.StationLevel, 2, station: 3, gems: 30));
            goals.Add(G(GoalKind.HireManager, 1, station: 0, coins: 70000));
            goals.Add(G(GoalKind.Renovations, 1, gems: 35));
            goals.Add(G(GoalKind.HireManager, 1, station: 1, coins: 120000));
            goals.Add(G(GoalKind.StationLevel, 4, station: 2, gems: 40));
            goals.Add(G(GoalKind.AddMachines, 1, station: 2, gems: 50));
            goals.Add(G(GoalKind.Renovations, 3, gems: 70));
            goals.Add(G(GoalKind.LifetimeCoins, 7000000, gems: 80));
            goals.Add(G(GoalKind.HireManager, 1, station: 2, coins: 400000));
            goals.Add(G(GoalKind.ServeCustomers, 300, coins: 800000));
            goals.Add(G(GoalKind.Renovations, 6, gems: 140));
            goals.Add(G(GoalKind.ShopLevel, 50, gems: 200));
            EditorUtility.SetDirty(config);
        }

        // A picnic corner for the staff: two tables with coffee and burgers.
        static void BuildDriveDining(Transform world)
        {
            var dining = Group("Dining", world);
            void Table(Vector3 p)
            {
                Place("table_round_A", dining, p);
                Place("chair", dining, p + new Vector3(-1.1f, 0f, 0f), 90f);
                Place("chair", dining, p + new Vector3(1.1f, 0f, 0f), -90f);
                Place("serving_tray", dining, p + new Vector3(0.2f, 1f, -0.05f));
                Place("food_burger", RestaurantArt, dining, p + new Vector3(0.2f, 1.03f, -0.05f), 30f);
                Place("coffee_cup_takeaway", dining, p + new Vector3(-0.3f, 1f, 0.15f));
            }
            Table(new Vector3(-4.9f, 0f, -1.8f));
            Table(new Vector3(-4.4f, 0f, -4.6f));
        }

        // Butter walls, coral checks, red curtains, ketchup and mustard on the shelves, bright noon sun.
        static void StyleDriveThru(Transform world, Camera cam)
        {
            var room = world.Find("Room");
            foreach (Transform child in room)
            {
                if (child.name.StartsWith("wall_"))
                    Tint(child.gameObject, DriveWall, "Drive");
                else if (child.name.StartsWith("floor_"))
                    Tint(child.gameObject, DriveTiles, "Drive");
                else if (child.name == "LowWalls")
                    foreach (Transform w in child.GetComponentsInChildren<Transform>(true))
                        if (w.name.StartsWith("wall_"))
                            Tint(w.gameObject, DriveWall, "Drive");
            }
            var dress = world.Find("Dressing");
            foreach (Transform child in dress)
            {
                if (child.name.StartsWith("curtains"))
                    Tint(child.gameObject, new Color(1f, 0.55f, 0.5f), "Red");
                if (child.name.StartsWith("tin_") || child.name.StartsWith("cookie_jar"))
                    Object.DestroyImmediate(child.gameObject);
            }
            Place("ketchup", RestaurantArt, dress, new Vector3(-3.6f, 2.78f, 5.3f));
            Place("mustard", RestaurantArt, dress, new Vector3(-3.25f, 2.78f, 5.3f));
            Place("coffee_cup_takeaway_stacked", dress, new Vector3(-2.7f, 2.78f, 5.3f));
            foreach (var tin in room.Cast<Transform>().Where(c => c.name.StartsWith("tin_") || c.name.StartsWith("cookie_jar")).ToList())
            {
                var pos = tin.position;
                Object.DestroyImmediate(tin.gameObject);
                Place("ketchup", RestaurantArt, room, pos);
            }

            var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            if (sun != null)
            {
                sun.color = new Color(1f, 0.97f, 0.9f);
                sun.intensity = 1.05f;
                sun.transform.rotation = Quaternion.Euler(44f, -32f, 0f);
            }
            cam.backgroundColor = new Color(0.85f, 0.93f, 1f);
        }
    }
}
