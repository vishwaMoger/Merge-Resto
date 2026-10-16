using System.Collections.Generic;
using System.Linq;
using MergeResto.Look;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MergeResto.EditorTools
{
    /// <summary>
    /// Sets up the cute toon look: outline renderer feature, post-processing profile,
    /// and a placeholder bakery room for judging the style. All steps are re-runnable.
    /// </summary>
    public static class LookSetup
    {
        const string MaterialsFolder = "Assets/Art/Materials/StyleTest";
        const string OutlineMaterialPath = "Assets/Art/Materials/ToonOutline.mat";
        const string ProfilePath = "Assets/Art/Look/LookVolumeProfile.asset";
        const string StyleScenePath = "Assets/Scenes/StyleTest.unity";
        const string OutlineFeatureName = "ToonOutline";

        static readonly string[] RendererPaths =
        {
            "Assets/Settings/PC_Renderer.asset",
            "Assets/Settings/Mobile_Renderer.asset",
        };

        [MenuItem("Merge Resto/Look/Install Outline + Post Processing")]
        public static void InstallRendering()
        {
            EnsureFolder("Assets/Art/Materials");
            EnsureFolder("Assets/Art/Look");

            var outlineShader = Shader.Find("MergeResto/ToonOutline");
            var outlineMat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (outlineMat == null)
            {
                outlineMat = new Material(outlineShader) { name = "ToonOutline" };
                AssetDatabase.CreateAsset(outlineMat, OutlineMaterialPath);
            }

            foreach (var path in RendererPaths)
                AddOutlineFeature(path, outlineMat);

            CreateVolumeProfile();
            AssetDatabase.SaveAssets();
            Debug.Log("[Merge Resto] Outline feature and look profile installed.");
        }

        static void AddOutlineFeature(string rendererPath, Material outlineMat)
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (data == null)
            {
                Debug.LogWarning($"[Merge Resto] Renderer not found: {rendererPath}");
                return;
            }

            var feature = data.rendererFeatures.OfType<FullScreenPassRendererFeature>()
                .FirstOrDefault(f => f.name == OutlineFeatureName);
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = OutlineFeatureName;
                AssetDatabase.AddObjectToAsset(feature, data);

                var so = new SerializedObject(data);
                var features = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
            feature.fetchColorBuffer = true;
            feature.passMaterial = outlineMat;
            feature.passIndex = 0;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            data.SetDirty();
            EditorUtility.SetDirty(data);
        }

        static VolumeProfile CreateVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            var bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.7f);
            bloom.scatter.Override(0.65f);
            bloom.tint.Override(new Color(1f, 0.9f, 0.75f));

            // No tonemapping: the toon palette is already authored in display colours,
            // and filmic curves flatten it into a hazy look.
            var tone = GetOrAdd<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.None);

            var color = GetOrAdd<ColorAdjustments>(profile);
            color.postExposure.Override(0f);
            color.saturation.Override(10f);
            color.contrast.Override(8f);
            color.colorFilter.Override(Color.white);

            if (profile.TryGet(out WhiteBalance wb))
                wb.active = false;

            var vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(new Color(0.35f, 0.2f, 0.25f));

            EditorUtility.SetDirty(profile);
            return profile;
        }

        static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>(true);
                component.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            return component;
        }

        // ---------------------------------------------------------------- Style test room

        static readonly Dictionary<string, Material> Mats = new();

        [MenuItem("Merge Resto/Look/Build Style Test Room")]
        public static void BuildStyleTestRoom()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            InstallRendering();
            EnsureFolder(MaterialsFolder);
            Mats.Clear();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera: orthographic isometric view into the back-left corner.
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.2f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.99f, 0.91f, 0.84f);
            camGo.transform.rotation = Quaternion.Euler(32f, -45f, 0f);
            camGo.transform.position = new Vector3(0f, 1.1f, 0.3f) - camGo.transform.forward * 25f;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camGo.AddComponent<AudioListener>();

            // Light: warm key light from the front right.
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.84f);
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.9f;
            sunGo.transform.rotation = Quaternion.Euler(48f, -20f, 0f);
            sunGo.AddComponent<UniversalAdditionalLightData>();

            var lookGo = new GameObject("Look");
            lookGo.AddComponent<ToonLookSettings>().ambient = new Color(0.07f, 0.06f, 0.08f);
            var volume = lookGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.4f, 0.36f, 0.42f);

            var room = new GameObject("Bakery").transform;
            BuildShell(room);
            BuildKitchen(room);
            BuildDining(room);
            BuildRundownCorner(room);
            BuildCharacters(room);

            EditorSceneManager.SaveScene(scene, StyleScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Merge Resto] Style test room saved to {StyleScenePath}.");
        }

        static void BuildShell(Transform room)
        {
            var shell = Group("Shell", room);

            // Checker floor in two warm creams.
            for (int x = 0; x < 5; x++)
            for (int z = 0; z < 4; z++)
            {
                var tint = (x + z) % 2 == 0 ? Hex("F7E7C8") : Hex("F2D2B0");
                Box($"Tile_{x}_{z}", shell, new Vector3(-4f + x * 2f, -0.1f, -3f + z * 2f), new Vector3(1.98f, 0.2f, 1.98f),
                    Mat((x + z) % 2 == 0 ? "FloorA" : "FloorB", tint));
            }

            // Back wall: left part restored, right part still run-down.
            Box("BackWall", shell, new Vector3(-1.5f, 1.6f, 4.1f), new Vector3(7f, 3.2f, 0.2f), Mat("Wall", Hex("F8C8A8")));
            Box("BackWainscot", shell, new Vector3(-1.5f, 0.5f, 3.98f), new Vector3(7f, 1f, 0.06f), Mat("Wainscot", Hex("D98E6B")));
            Box("BackWall_Worn", shell, new Vector3(3.5f, 1.6f, 4.1f), new Vector3(3f, 3.2f, 0.2f), Mat("WallWorn", Hex("F8C8A8"), wear: 0.85f));
            Box("BackWainscot_Worn", shell, new Vector3(3.5f, 0.5f, 3.98f), new Vector3(3f, 1f, 0.06f), Mat("WainscotWorn", Hex("D98E6B"), wear: 0.85f));

            Box("LeftWall", shell, new Vector3(-5.1f, 1.6f, 0f), new Vector3(0.2f, 3.2f, 8.4f), Mat("Wall", Hex("F8C8A8")));
            Box("LeftWainscot", shell, new Vector3(-4.98f, 0.5f, 0f), new Vector3(0.06f, 1f, 8.4f), Mat("Wainscot", Hex("D98E6B")));

            // Window with a soft sky glow.
            Box("WindowFrame", shell, new Vector3(-1.2f, 2.05f, 3.97f), new Vector3(1.6f, 1.1f, 0.08f), Mat("Trim", Hex("FFF6EA")));
            Box("WindowGlass", shell, new Vector3(-1.2f, 2.05f, 3.92f), new Vector3(1.35f, 0.85f, 0.04f),
                Mat("Glass", Hex("BFE6F2"), emission: Hex("BFE6F2") * 0.35f));
            Box("WindowBar", shell, new Vector3(-1.2f, 2.05f, 3.9f), new Vector3(0.06f, 0.85f, 0.04f), Mat("Trim", Hex("FFF6EA")));

            // Shelf with jars.
            Box("Shelf", shell, new Vector3(1.3f, 2.0f, 3.8f), new Vector3(2f, 0.08f, 0.4f), Mat("Wood", Hex("B9774E")));
            string[] jarColors = { "F49AC1", "8FD3C1", "FFD36E", "F6A06B" };
            for (int i = 0; i < jarColors.Length; i++)
                Cylinder($"Jar{i}", shell, new Vector3(0.65f + i * 0.43f, 2.22f, 3.8f), new Vector3(0.24f, 0.18f, 0.24f), Mat($"Jar{i}", Hex(jarColors[i])));

            // Hanging lamps over the counter.
            for (int i = 0; i < 2; i++)
            {
                var pos = new Vector3(-2.6f + i * 1.8f, 2.75f, 1.5f);
                Cylinder($"LampCord{i}", shell, pos + Vector3.up * 0.55f, new Vector3(0.03f, 0.5f, 0.03f), Mat("Cord", Hex("4A3A34")));
                Sphere($"Lamp{i}", shell, pos, new Vector3(0.36f, 0.3f, 0.36f), Mat("LampGlow", Hex("FFE2A6"), emission: new Color(2.6f, 1.9f, 1.0f)));
                var lampLight = new GameObject($"LampLight{i}").AddComponent<Light>();
                lampLight.transform.SetParent(shell, false);
                lampLight.transform.position = pos + Vector3.down * 0.3f;
                lampLight.type = LightType.Point;
                lampLight.color = new Color(1f, 0.8f, 0.55f);
                lampLight.range = 3.2f;
                lampLight.intensity = 1.6f;
                lampLight.shadows = LightShadows.None;
            }
        }

        static void BuildKitchen(Transform room)
        {
            var kitchen = Group("Kitchen", room);

            // Counter with pastries and a register.
            Box("CounterBody", kitchen, new Vector3(-1.6f, 0.5f, 1.5f), new Vector3(3.2f, 1f, 0.9f), Mat("Counter", Hex("EE8E7A")));
            Box("CounterTop", kitchen, new Vector3(-1.6f, 1.06f, 1.5f), new Vector3(3.4f, 0.12f, 1.1f), Mat("CounterTop", Hex("FFF4E2")));
            Box("CounterStripe", kitchen, new Vector3(-1.6f, 0.75f, 1.04f), new Vector3(3.2f, 0.12f, 0.02f), Mat("Stripe", Hex("FFF4E2")));
            Box("Register", kitchen, new Vector3(-0.45f, 1.3f, 1.55f), new Vector3(0.55f, 0.36f, 0.45f), Mat("Mint", Hex("8FD3C1")));
            for (int i = 0; i < 4; i++)
                Sphere($"Bun{i}", kitchen, new Vector3(-2.9f + i * 0.32f, 1.22f, 1.45f), new Vector3(0.26f, 0.2f, 0.26f), Mat("Bun", Hex("E9A55C")));
            for (int i = 0; i < 3; i++)
                Cylinder($"Donut{i}", kitchen, new Vector3(-1.6f + i * 0.33f, 1.15f, 1.45f), new Vector3(0.26f, 0.04f, 0.26f), Mat("Donut", Hex("F59DC3")));

            // Oven with a glowing window.
            Box("OvenBody", kitchen, new Vector3(-3.9f, 0.85f, 3.35f), new Vector3(1.5f, 1.7f, 1.1f), Mat("Oven", Hex("E6DCCF")));
            Box("OvenTop", kitchen, new Vector3(-3.9f, 1.75f, 3.35f), new Vector3(1.6f, 0.1f, 1.2f), Mat("Steel", Hex("B9B3C2")));
            Box("OvenWindow", kitchen, new Vector3(-3.9f, 0.9f, 2.79f), new Vector3(0.95f, 0.55f, 0.04f),
                Mat("OvenGlow", Hex("FF9A4D"), emission: new Color(3.2f, 1.3f, 0.35f)));
            Cylinder("OvenPipe", kitchen, new Vector3(-4.3f, 2.4f, 3.6f), new Vector3(0.22f, 0.6f, 0.22f), Mat("Steel", Hex("B9B3C2")));

            // Mixer with a spinning bowl.
            Box("MixerBase", kitchen, new Vector3(-2.2f, 0.4f, 3.45f), new Vector3(0.7f, 0.8f, 0.7f), Mat("Mint", Hex("8FD3C1")));
            var bowl = Sphere("MixerBowl", kitchen, new Vector3(-2.2f, 0.98f, 3.45f), new Vector3(0.62f, 0.38f, 0.62f), Mat("Bowl", Hex("FFFFFF")));
            bowl.AddComponent<Spinner>();

            // Plant by the kitchen.
            Plant("Plant_Kitchen", kitchen, new Vector3(-4.4f, 0f, -3.2f), 1.1f);
        }

        static void BuildDining(Transform room)
        {
            var dining = Group("Dining", room);
            Table("Table_A", dining, new Vector3(1.4f, 0f, -1.6f), Mat("TableTop", Hex("FFF6EA")), Mat("Coral", Hex("F58F7C")));
            Table("Table_B", dining, new Vector3(-1.4f, 0f, -2.4f), Mat("TableTop", Hex("FFF6EA")), Mat("Mint", Hex("8FD3C1")));
            Plant("Plant_Dining", dining, new Vector3(4.4f, 0f, -3.3f), 0.9f);

            // A coffee cup and a cake slice on table A.
            Cylinder("Cup", dining, new Vector3(1.25f, 0.86f, -1.55f), new Vector3(0.16f, 0.08f, 0.16f), Mat("Cup", Hex("FFFFFF")));
            Box("Cake", dining, new Vector3(1.6f, 0.86f, -1.7f), new Vector3(0.22f, 0.16f, 0.16f), Mat("Cake", Hex("FFC9DE")));
        }

        static void BuildRundownCorner(Transform room)
        {
            var corner = Group("RundownCorner", room);

            // A machine still under its dust sheet.
            var sheet = Mat("DustSheet", Hex("E8E0D2"), wear: 0.55f);
            Box("CoveredMachine", corner, new Vector3(3.6f, 0.7f, 3.2f), new Vector3(1.3f, 1.4f, 1.0f), sheet);
            Box("SheetDrape", corner, new Vector3(3.6f, 0.05f, 3.2f), new Vector3(1.55f, 0.1f, 1.25f), sheet);

            // Broken table and old crates.
            var oldWood = Mat("OldWood", Hex("C88A5E"), wear: 1f);
            Cylinder("BrokenTableTop", corner, new Vector3(3.3f, 0.55f, 1.0f), new Vector3(1.0f, 0.04f, 1.0f), oldWood)
                .transform.rotation = Quaternion.Euler(0f, 0f, 14f);
            Cylinder("BrokenTableLeg", corner, new Vector3(3.3f, 0.27f, 1.0f), new Vector3(0.1f, 0.27f, 0.1f), oldWood);
            Box("Crate1", corner, new Vector3(4.4f, 0.3f, 1.9f), new Vector3(0.6f, 0.6f, 0.6f), oldWood);
            Box("Crate2", corner, new Vector3(4.35f, 0.82f, 1.95f), new Vector3(0.45f, 0.45f, 0.45f), oldWood)
                .transform.rotation = Quaternion.Euler(0f, 18f, 0f);
            Box("Plank1", corner, new Vector3(2.4f, 0.04f, 2.6f), new Vector3(1.6f, 0.06f, 0.18f), oldWood)
                .transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            Box("Plank2", corner, new Vector3(4.6f, 0.9f, 3.85f), new Vector3(0.18f, 1.8f, 0.06f), oldWood)
                .transform.rotation = Quaternion.Euler(0f, 0f, 12f);
        }

        static void BuildCharacters(Transform room)
        {
            var cast = Group("Characters", room);

            var baker = Chibi("Baker", cast, Hex("FFFFFF"), Hex("FFE0C2"), chefHat: true);
            Walker(baker, new[] { new Vector3(-3.4f, 0f, 2.4f), new Vector3(-0.9f, 0f, 2.3f) }, 1.0f);

            var barista = Chibi("Barista", cast, Hex("8FD3C1"), Hex("F4C9A6"), chefHat: false);
            barista.transform.position = new Vector3(-0.2f, 0f, 2.35f);
            barista.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            Walker(barista, null, 0f);

            var customer = Chibi("Customer", cast, Hex("7FA7E8"), Hex("E8B48E"), chefHat: false);
            Walker(customer, new[] { new Vector3(3.6f, 0f, -2.8f), new Vector3(-1.0f, 0f, 0.55f), new Vector3(0.6f, 0f, -0.9f) }, 1.3f);

            var dog = Dog("Biscuit", cast);
            dog.transform.position = new Vector3(-3.6f, 0f, -2.4f);
            dog.transform.rotation = Quaternion.Euler(0f, 135f, 0f);
            dog.transform.localScale = Vector3.one * 1.35f;

            foreach (var chibi in new[] { baker, barista, customer })
                chibi.transform.localScale = Vector3.one * 1.25f;
        }

        // ---------------------------------------------------------------- Builders

        static GameObject Chibi(string name, Transform parent, Color outfit, Color skin, bool chefHat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);

            var outfitMat = Mat($"{name}_Outfit", outfit, rim: 0.35f);
            var skinMat = Mat($"Skin_{ColorUtility.ToHtmlStringRGB(skin)}", skin, rim: 0.35f);
            var dark = Mat("Eyes", Hex("2B2024"), rim: 0f);
            var blush = Mat("Blush", Hex("FF9AA8"), rim: 0f);

            Capsule("Torso", body, new Vector3(0f, 0.36f, 0f), new Vector3(0.46f, 0.3f, 0.42f), outfitMat);
            Sphere("Head", body, new Vector3(0f, 0.98f, 0f), Vector3.one * 0.62f, skinMat);
            Sphere("EyeL", body, new Vector3(-0.11f, 1.0f, 0.28f), new Vector3(0.07f, 0.09f, 0.05f), dark);
            Sphere("EyeR", body, new Vector3(0.11f, 1.0f, 0.28f), new Vector3(0.07f, 0.09f, 0.05f), dark);
            Sphere("BlushL", body, new Vector3(-0.19f, 0.92f, 0.25f), new Vector3(0.08f, 0.05f, 0.04f), blush);
            Sphere("BlushR", body, new Vector3(0.19f, 0.92f, 0.25f), new Vector3(0.08f, 0.05f, 0.04f), blush);
            if (chefHat)
            {
                var white = Mat("ChefHat", Hex("FFFFFF"), rim: 0.3f);
                Cylinder("HatBand", body, new Vector3(0f, 1.3f, 0f), new Vector3(0.42f, 0.07f, 0.42f), white);
                Sphere("HatPuff", body, new Vector3(0f, 1.44f, 0f), new Vector3(0.52f, 0.3f, 0.52f), white);
            }
            else
            {
                // A cap of hair sitting on top and back of the head, leaving the face clear.
                var hair = Mat($"{name}_Hair", Hex("6B4636"), rim: 0.3f);
                Sphere("Hair", body, new Vector3(0f, 1.16f, -0.06f), new Vector3(0.66f, 0.36f, 0.64f), hair);
                Sphere("HairBack", body, new Vector3(0f, 0.98f, -0.12f), new Vector3(0.6f, 0.5f, 0.42f), hair);
            }
            return root;
        }

        static GameObject Dog(string name, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);

            var fur = Mat("DogFur", Hex("F3D29E"), rim: 0.4f);
            var ear = Mat("DogEar", Hex("C98A55"), rim: 0.3f);
            var dark = Mat("Eyes", Hex("2B2024"), rim: 0f);
            Sphere("Belly", body, new Vector3(0f, 0.22f, -0.05f), new Vector3(0.4f, 0.36f, 0.48f), fur);
            Sphere("Head", body, new Vector3(0f, 0.5f, 0.12f), Vector3.one * 0.38f, fur);
            Sphere("EarL", body, new Vector3(-0.17f, 0.58f, 0.08f), new Vector3(0.1f, 0.2f, 0.14f), ear);
            Sphere("EarR", body, new Vector3(0.17f, 0.58f, 0.08f), new Vector3(0.1f, 0.2f, 0.14f), ear);
            Sphere("Snout", body, new Vector3(0f, 0.45f, 0.3f), new Vector3(0.16f, 0.12f, 0.12f), Mat("DogSnout", Hex("FFF1DD"), rim: 0.2f));
            Sphere("Nose", body, new Vector3(0f, 0.49f, 0.36f), new Vector3(0.06f, 0.045f, 0.04f), dark);
            Sphere("EyeL", body, new Vector3(-0.08f, 0.55f, 0.29f), Vector3.one * 0.05f, dark);
            Sphere("EyeR", body, new Vector3(0.08f, 0.55f, 0.29f), Vector3.one * 0.05f, dark);
            Sphere("Tail", body, new Vector3(0f, 0.3f, -0.3f), new Vector3(0.1f, 0.1f, 0.18f), fur);
            Walker(root, null, 0f);
            return root;
        }

        static void Walker(GameObject character, Vector3[] path, float speed)
        {
            var walker = character.AddComponent<BouncyWalker>();
            walker.body = character.transform.Find("Body");
            walker.waypoints = path;
            walker.speed = speed;
            if (path != null && path.Length > 0)
                character.transform.position = path[0];
        }

        static void Table(string name, Transform parent, Vector3 pos, Material top, Material chair)
        {
            var t = Group(name, parent);
            t.position = pos;
            Cylinder("Top", t, new Vector3(0f, 0.76f, 0f), new Vector3(1.0f, 0.04f, 1.0f), top);
            Cylinder("Leg", t, new Vector3(0f, 0.38f, 0f), new Vector3(0.1f, 0.38f, 0.1f), Mat("Wood", Hex("B9774E")));
            Cylinder("Foot", t, new Vector3(0f, 0.03f, 0f), new Vector3(0.45f, 0.03f, 0.45f), Mat("Wood", Hex("B9774E")));
            for (int i = 0; i < 2; i++)
            {
                var offset = i == 0 ? new Vector3(-0.85f, 0f, 0.1f) : new Vector3(0.75f, 0f, -0.45f);
                Cylinder($"ChairSeat{i}", t, offset + new Vector3(0f, 0.45f, 0f), new Vector3(0.45f, 0.06f, 0.45f), chair);
                Cylinder($"ChairLeg{i}", t, offset + new Vector3(0f, 0.22f, 0f), new Vector3(0.08f, 0.22f, 0.08f), Mat("Wood", Hex("B9774E")));
            }
        }

        static void Plant(string name, Transform parent, Vector3 pos, float size)
        {
            var p = Group(name, parent);
            p.position = pos;
            p.localScale = Vector3.one * size;
            Cylinder("Pot", p, new Vector3(0f, 0.25f, 0f), new Vector3(0.5f, 0.25f, 0.5f), Mat("Pot", Hex("DB7650")));
            var leaf = Mat("Leaves", Hex("86C46E"), sway: true);
            Sphere("Bush", p, new Vector3(0f, 0.75f, 0f), new Vector3(0.75f, 0.65f, 0.75f), leaf);
            Sphere("BushTop", p, new Vector3(0.08f, 1.12f, -0.04f), new Vector3(0.5f, 0.45f, 0.5f), leaf);
        }

        static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static GameObject Box(string n, Transform p, Vector3 pos, Vector3 scale, Material m) => Prim(PrimitiveType.Cube, n, p, pos, scale, m);
        static GameObject Sphere(string n, Transform p, Vector3 pos, Vector3 scale, Material m) => Prim(PrimitiveType.Sphere, n, p, pos, scale, m);
        static GameObject Cylinder(string n, Transform p, Vector3 pos, Vector3 scale, Material m) => Prim(PrimitiveType.Cylinder, n, p, pos, scale, m);
        static GameObject Capsule(string n, Transform p, Vector3 pos, Vector3 scale, Material m) => Prim(PrimitiveType.Capsule, n, p, pos, scale, m);

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static Material Mat(string name, Color color, float wear = 0f, Color? emission = null, bool sway = false, float rim = 0.22f)
        {
            if (Mats.TryGetValue(name, out var cached))
                return cached;

            string path = $"{MaterialsFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("MergeResto/Toon"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Wear", wear);
            mat.SetFloat("_GrimeScale", 5f);
            mat.SetFloat("_RimStrength", rim);
            mat.SetColor("_EmissionColor", emission ?? Color.black);
            mat.SetFloat("_Sway", sway ? 1f : 0f);
            if (sway) mat.EnableKeyword("_SWAY"); else mat.DisableKeyword("_SWAY");
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            Mats[name] = mat;
            return mat;
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
