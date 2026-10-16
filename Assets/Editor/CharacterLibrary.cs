using System.Collections.Generic;
using System.IO;
using System.Linq;
using MergeResto.Game.Views;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MergeResto.EditorTools
{
    /// <summary>
    /// Turns the KayKit Adventurers into the bakery cast: recoloured outfits (swatch swaps in
    /// the gradient atlas), fantasy gear hidden, a chef hat for bakers, a shared animator, and
    /// one prefab per look under Assets/Art/Characters/Prefabs.
    /// </summary>
    public static class CharacterLibrary
    {
        const string ModelFolder = "Assets/Art/ThirdParty/KayKit_Characters/Models/";
        const string TextureFolder = "Assets/Art/ThirdParty/KayKit_Characters/Textures/";
        const string AnimFolder = "Assets/Art/ThirdParty/KayKit_CharacterAnimations/Rig_Medium/";
        const string OutRoot = "Assets/Art/Characters";
        const string ControllerPath = OutRoot + "/Animation/Bakery.controller";
        const string MaskPath = OutRoot + "/Animation/ArmsMask.mask";

        public const string BakerLook = "Baker";
        public const string CashierLook = "Cashier";
        public static readonly string[] CustomerLooks =
            { "Customer_Sky", "Customer_Lavender", "Customer_Teal", "Customer_Rose", "Customer_Hoodie", "Customer_Knight", "Customer_Strong" };

        class Look
        {
            public string name;
            public string model;
            public string texture;
            public string[] hide = new string[0];
            public (int col, int row, string hex)[] recolor = new (int, int, string)[0];
            public bool chefHat;
        }

        static readonly Look[] Looks =
        {
            new() { name = BakerLook, model = "Ranger", texture = "ranger_texture", chefHat = true,
                hide = new[] { "Ranger_Cape", "Ranger_Quiver" },
                recolor = new[] { (0, 1, "E8574A"), (1, 2, "E8574A") } },
            new() { name = CashierLook, model = "Rogue", texture = "rogue_texture",
                hide = new[] { "Rogue_Cape" },
                recolor = new[] { (0, 1, "F2817A"), (1, 1, "FFF1DE") } },
            new() { name = "Customer_Sky", model = "Rogue", texture = "rogue_texture",
                hide = new[] { "Rogue_Cape" },
                recolor = new[] { (0, 1, "6DB4F0"), (1, 1, "FFD25E") } },
            new() { name = "Customer_Lavender", model = "Ranger", texture = "ranger_texture",
                hide = new[] { "Ranger_Cape", "Ranger_Quiver" },
                recolor = new[] { (5, 0, "C8B4F0"), (6, 0, "C8B4F0"), (0, 1, "6B5BB5"), (1, 2, "6B5BB5") } },
            new() { name = "Customer_Teal", model = "Mage", texture = "mage_texture",
                hide = new[] { "Mage_Hat", "Mage_Cape" },
                recolor = new[] { (0, 1, "4CBFAE"), (1, 1, "4CBFAE") } },
            new() { name = "Customer_Rose", model = "Mage", texture = "mage_texture",
                hide = new[] { "Mage_Hat", "Mage_Cape" },
                recolor = new[] { (0, 1, "F59AC3"), (1, 1, "F59AC3") } },
            new() { name = "Customer_Hoodie", model = "Rogue_Hooded", texture = "rogue_texture",
                hide = new[] { "RogueHooded_Mask", "RogueHooded_Cape" },
                recolor = new[] { (1, 1, "F39A4A"), (0, 1, "8C5A3C") } },
            new() { name = "Customer_Knight", model = "Knight", texture = "knight_texture",
                hide = new[] { "Knight_Helmet", "Knight_HelmetVisor", "Knight_Cape" } },
            new() { name = "Customer_Strong", model = "Barbarian", texture = "barbarian_texture",
                hide = new[] { "Barbarian_BearHat" } },
        };

        [MenuItem("Merge Resto/Art/Build Character Library")]
        public static void BuildAll()
        {
            LookSetup.EnsureFolder(OutRoot + "/Animation");
            LookSetup.EnsureFolder(OutRoot + "/Textures");
            LookSetup.EnsureFolder(OutRoot + "/Materials");
            LookSetup.EnsureFolder(OutRoot + "/Prefabs");

            var controller = BuildController();
            foreach (var look in Looks)
                BuildLook(look, controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Merge Resto] Character library built: {Looks.Length} looks.");
        }

        /// <summary>Every look side by side in the idle pose, for checking outfits and hats.</summary>
        [MenuItem("Merge Resto/Art/Build Character Lineup")]
        public static void BuildLineup()
        {
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            var idle = Clip("General", "Idle_A");
            var floor = LookSetup.Box("Floor", null, new Vector3(0f, -0.05f, 0f), new Vector3(30f, 0.1f, 8f),
                LookSetup.Mat("LineupFloor", LookSetup.Hex("F7E7C8")));
            floor.transform.SetParent(null);
            var names = new[] { BakerLook, CashierLook }.Concat(CustomerLooks).ToArray();
            for (int i = 0; i < names.Length; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(names[i]));
                go.transform.position = new Vector3((i - (names.Length - 1) / 2f) * 1.5f, 0f, 0f);
                go.transform.rotation = Quaternion.Euler(0f, 200f, 0f);
                idle.SampleAnimation(go.transform.Find("Model").gameObject, 0.4f);
            }
            var cam = LookSetup.CreateLookRig(new Vector3(0f, 1.25f, 0f), 4f);
            cam.transform.rotation = Quaternion.Euler(16f, 0f, 0f);
            cam.transform.position = new Vector3(0f, 1.25f, 0f) - cam.transform.forward * 30f;
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, "Assets/Scenes/CharacterLineup.unity");
        }

        public static GameObject LoadPrefab(string look) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{OutRoot}/Prefabs/{look}.prefab");

        // ------------------------------------------------------------------ Animator

        static AnimationClip Clip(string file, string name) =>
            AssetDatabase.LoadAllAssetsAtPath($"{AnimFolder}Rig_Medium_{file}.fbx")
                .OfType<AnimationClip>().First(c => c.name == name);

        static AnimatorController BuildController()
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Working", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Sitting", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Cheer", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Wave", AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;
            var idle = sm.AddState("Idle", new Vector3(300, 0));
            idle.motion = Clip("General", "Idle_A");
            var walk = sm.AddState("Walk", new Vector3(300, 120));
            walk.motion = Clip("MovementBasic", "Walking_A");
            walk.speedParameter = "Speed";
            walk.speedParameterActive = true;
            var work = sm.AddState("Work", new Vector3(560, 0));
            work.motion = Clip("General", "Interact");
            var sit = sm.AddState("Sit", new Vector3(560, 120));
            sit.motion = Clip("Simulation", "Sit_Chair_Idle");
            var cheer = sm.AddState("Cheer", new Vector3(40, 120));
            cheer.motion = Clip("Simulation", "Cheering");
            var wave = sm.AddState("Wave", new Vector3(40, 0));
            wave.motion = Clip("Simulation", "Waving");
            sm.defaultState = idle;

            Transition(idle, walk, 0.12f, ("Speed", AnimatorConditionMode.Greater, 0.15f));
            Transition(walk, idle, 0.15f, ("Speed", AnimatorConditionMode.Less, 0.1f));
            Transition(idle, work, 0.15f, ("Working", AnimatorConditionMode.If, 0));
            Transition(work, idle, 0.15f, ("Working", AnimatorConditionMode.IfNot, 0));
            Transition(work, walk, 0.12f, ("Speed", AnimatorConditionMode.Greater, 0.15f));
            Transition(idle, sit, 0.2f, ("Sitting", AnimatorConditionMode.If, 0));
            Transition(sit, idle, 0.2f, ("Sitting", AnimatorConditionMode.IfNot, 0));

            foreach (var (state, trigger) in new[] { (cheer, "Cheer"), (wave, "Wave") })
            {
                var any = sm.AddAnyStateTransition(state);
                any.AddCondition(AnimatorConditionMode.If, 0, trigger);
                any.duration = 0.1f;
                any.canTransitionToSelf = false;
                var back = state.AddTransition(idle);
                back.hasExitTime = true;
                back.exitTime = 0.92f;
                back.duration = 0.15f;
            }

            // Arms-only layer: hold goods in front while the legs keep walking.
            var mask = BuildArmsMask();
            ctrl.AddLayer("Carry");
            var layers = ctrl.layers;
            layers[1].avatarMask = mask;
            layers[1].defaultWeight = 0f;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            ctrl.layers = layers;
            var hold = ctrl.layers[1].stateMachine.AddState("Hold");
            hold.motion = Clip("Tools", "Holding_A");
            ctrl.layers[1].stateMachine.defaultState = hold;

            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static void Transition(AnimatorState from, AnimatorState to, float duration, params (string p, AnimatorConditionMode mode, float threshold)[] conditions)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            foreach (var c in conditions)
                t.AddCondition(c.mode, c.threshold, c.p);
        }

        static AvatarMask BuildArmsMask()
        {
            AssetDatabase.DeleteAsset(MaskPath);
            var mask = new AvatarMask();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}Ranger.fbx");
            var rig = model.transform.Find("Rig_Medium");
            var all = rig.GetComponentsInChildren<Transform>(true);
            mask.transformCount = all.Length;
            for (int i = 0; i < all.Length; i++)
            {
                string path = AnimationUtility.CalculateTransformPath(all[i], model.transform);
                mask.SetTransformPath(i, path);
                mask.SetTransformActive(i, path.Contains("upperarm") || path.Contains("lowerarm") || path.Contains("wrist") || path.Contains("hand"));
            }
            AssetDatabase.CreateAsset(mask, MaskPath);
            return mask;
        }

        // ------------------------------------------------------------------ Looks

        static void BuildLook(Look look, AnimatorController controller)
        {
            var texture = RecolorTexture(look);
            string matPath = $"{OutRoot}/Materials/{look.name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("MergeResto/Toon"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_RimStrength", 0.32f);
            mat.SetFloat("_MidTone", 0.2f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}{look.model}.fbx");
            var root = new GameObject(look.name);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Model";

            foreach (var part in look.hide)
            {
                var t = model.transform.Find(part);
                if (t != null) Object.DestroyImmediate(t.gameObject);
                else Debug.LogWarning($"[Merge Resto] {look.name}: part '{part}' not found.");
            }
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                r.sharedMaterial = mat;
                r.updateWhenOffscreen = false;
            }

            if (!model.TryGetComponent<Animator>(out var animator))
                animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath($"{ModelFolder}{look.model}.fbx").OfType<Avatar>().FirstOrDefault();
            animator.applyRootMotion = false;

            var head = FindDeep(model.transform, "head");
            var chest = FindDeep(model.transform, "chest");
            float headTop = HeadTop(model, look.model);

            if (look.chefHat && head != null)
                BuildChefHat(head, headTop);

            var carry = new GameObject("CarryAnchor").transform;
            carry.SetParent(chest != null ? chest : model.transform, false);
            carry.position = new Vector3(0f, 1.05f, 0.62f);
            carry.rotation = Quaternion.identity;

            // Emote anchors for customers: shopping bag and a grumpy puff above the head.
            var hand = FindDeep(model.transform, "hand.r");
            var bag = LookSetup.Box("Bag", hand != null ? hand : model.transform, Vector3.zero, Vector3.one * 0.3f,
                LookSetup.Mat("PaperBag", LookSetup.Hex("D9A86C")));
            bag.transform.position = hand != null ? hand.position + new Vector3(0f, -0.15f, 0f) : new Vector3(0.4f, 0.9f, 0f);
            bag.transform.rotation = Quaternion.identity;
            bag.transform.localScale = Vector3.one;
            SetWorldScale(bag.transform, new Vector3(0.32f, 0.38f, 0.2f));
            bag.SetActive(false);
            var grump = new GameObject("Grump").transform;
            grump.SetParent(root.transform, false);
            grump.localPosition = new Vector3(0f, headTop + 0.35f, 0f);
            BuildGrumpCloud(grump);
            grump.gameObject.SetActive(false);

            var anim = root.AddComponent<ChibiAnimator>();
            anim.animator = animator;
            anim.carryAnchor = carry;
            anim.walkAnimSpeed = 1.7f;

            string prefabPath = $"{OutRoot}/Prefabs/{look.name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
        }

        static Texture2D RecolorTexture(Look look)
        {
            string src = $"{TextureFolder}{look.texture}.png";
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(src));
            // The atlas is 8 columns x 4 rows of gradient swatches; row 0 is the top of the image.
            int cellW = tex.width / 8;
            int cellH = tex.height / 4;
            foreach (var (col, row, hex) in look.recolor)
            {
                var baseColor = LookSetup.Hex(hex);
                Color.RGBToHSV(baseColor, out float h, out float s, out float v);
                var top = Color.HSVToRGB(h, s * 0.8f, Mathf.Min(1f, v * 1.12f));
                var bottom = Color.HSVToRGB(h, Mathf.Min(1f, s * 1.1f), v * 0.68f);
                int y0 = (3 - row) * cellH; // Texture2D rows start at the bottom
                for (int y = 0; y < cellH; y++)
                {
                    var c = Color.Lerp(bottom, top, y / (float)(cellH - 1));
                    for (int x = 0; x < cellW; x++)
                        tex.SetPixel(col * cellW + x, y0 + y, c);
                }
            }
            tex.Apply();
            string outPath = $"{OutRoot}/Textures/{look.name}.png";
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(outPath);
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 256;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        static float HeadTop(GameObject model, string modelName)
        {
            var headMesh = model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .FirstOrDefault(r => r.name.EndsWith("_Head"));
            if (headMesh == null)
                return 2.4f;
            var mesh = new Mesh();
            headMesh.BakeMesh(mesh);
            float top = mesh.vertices.Max(v => headMesh.transform.TransformPoint(v).y);
            Object.DestroyImmediate(mesh);
            return top - model.transform.position.y;
        }

        static void BuildChefHat(Transform head, float headTop)
        {
            var white = LookSetup.Mat("ChefHatCloth", LookSetup.Hex("FFFFFF"), rim: 0.35f);
            var hat = new GameObject("ChefHat").transform;
            hat.SetParent(head, false);
            hat.position = new Vector3(head.position.x, headTop - 0.12f, head.position.z - 0.02f);
            hat.rotation = Quaternion.identity;
            var band = LookSetup.Cylinder("Band", hat, Vector3.zero, Vector3.one, white);
            SetWorld(band.transform, hat.position + Vector3.up * 0.1f, new Vector3(0.78f, 0.13f, 0.78f));
            var puffs = new[]
            {
                new Vector3(0f, 0.42f, 0f), new Vector3(-0.2f, 0.36f, 0.05f), new Vector3(0.2f, 0.36f, 0.05f),
                new Vector3(0f, 0.36f, -0.18f), new Vector3(0f, 0.36f, 0.2f),
            };
            for (int i = 0; i < puffs.Length; i++)
            {
                var p = LookSetup.Sphere($"Puff{i}", hat, Vector3.zero, Vector3.one, white);
                SetWorld(p.transform, hat.position + puffs[i], Vector3.one * (i == 0 ? 0.62f : 0.46f));
            }
        }

        static void BuildGrumpCloud(Transform parent)
        {
            var red = LookSetup.Mat("GrumpCloud", LookSetup.Hex("6B5A63"), rim: 0.1f);
            Vector3[] puffs = { new(0f, 0f, 0f), new(-0.2f, -0.03f, 0f), new(0.2f, -0.02f, 0f), new(0.08f, 0.1f, 0f) };
            for (int i = 0; i < puffs.Length; i++)
            {
                var p = LookSetup.Sphere($"Cloud{i}", parent, puffs[i], Vector3.one * (i == 0 ? 0.32f : 0.24f), red);
                p.transform.localPosition = puffs[i];
            }
            var bolt = LookSetup.Box("Bolt", parent, new Vector3(0.05f, -0.22f, 0.02f), new Vector3(0.06f, 0.2f, 0.06f),
                LookSetup.Mat("GrumpBolt", LookSetup.Hex("FFD25E"), emission: new Color(0.9f, 0.7f, 0.1f)));
            bolt.transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
        }

        static void SetWorld(Transform t, Vector3 position, Vector3 worldScale)
        {
            t.position = position;
            t.rotation = Quaternion.identity;
            SetWorldScale(t, worldScale);
        }

        static void SetWorldScale(Transform t, Vector3 worldScale)
        {
            var parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(worldScale.x / parent.x, worldScale.y / parent.y, worldScale.z / parent.z);
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
