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
            var cam = LookSetup.CreateLookRig(new Vector3(0.6f, 1f, 0.4f), 7.9f);

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
            BuildFx(world, stations, crowd, popups, coinFly);
            BuildDecor(world);

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

        // Customers are only served once they really stand at the counter, so the sim needs
        // the walking times of the actual path in this room.
        static void UpdateCustomerTimes(ShopConfig config, CustomerCrowd crowd)
        {
            float speed = crowd.walkSpeed;
            float walkIn = Vector3.Distance(crowd.entryPoint.position, crowd.queueSlots[0].position) / speed;
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
            var view = NewStation(world, "FlourStorage", new Vector3(-6.4f, 0f, 4.7f), new Vector3(-6.0f, 0f, 3.0f), 1.5f);
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
                var c = Character(CharacterLibrary.CashierLook, t, $"Cashier{m}");
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
            Plant("zzplant_plant_medium_potted", new Vector3(7.2f, 0f, -5.2f), -40f);
            Plant("monstera_plant_medium_potted", new Vector3(2.2f, 0f, 5.3f), 0f);

            // Welcome mat where customers come in.
            var mat = Place("rug", decor, new Vector3(6.6f, 0f, -5.2f), 90f);
            mat.transform.localScale = new Vector3(0.7f, 1f, 0.7f);
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

        static void BuildFx(Transform world, StationView[] stations, CustomerCrowd crowd, FloatingTextLayer popups, CoinFlyLayer coinFly)
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
