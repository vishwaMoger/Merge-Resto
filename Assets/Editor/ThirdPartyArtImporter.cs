using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace MergeResto.EditorTools
{
    /// <summary>
    /// Import rules for KayKit / Tiny Treats packs dropped under Assets/Art/ThirdParty.
    /// Models get the MergeResto/Toon shader with their atlas texture, lean mesh settings,
    /// and the shared gradient atlases are shrunk (they hold flat colour swatches).
    /// </summary>
    public class ThirdPartyArtImporter : AssetPostprocessor
    {
        public const string Root = "Assets/Art/ThirdParty/";
        const string ToonShaderName = "MergeResto/Toon";
        const int AtlasMaxSize = 256;

        // Bump to force every third-party asset to reimport with new rules.
        public override uint GetVersion() => 2;

        static bool IsThirdParty(string path) => path.Replace('\\', '/').StartsWith(Root);

        void OnPreprocessModel()
        {
            if (!IsThirdParty(assetPath))
                return;

            var model = (ModelImporter)assetImporter;
            model.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            model.materialLocation = ModelImporterMaterialLocation.InPrefab;
            model.importCameras = false;
            model.importLights = false;
            model.importBlendShapes = false;
            model.importVisibility = false;
            model.isReadable = false;
            model.meshCompression = ModelImporterMeshCompression.Medium;
            model.addCollider = false;

            // Props are static; rigged characters keep their animation.
            bool rigged = assetPath.ToLowerInvariant().Contains("character");
            model.importAnimation = rigged;
            if (!rigged)
                model.animationType = ModelImporterAnimationType.None;
        }

        // KayKit clips import as one-shots; these are the ones that must loop.
        static readonly string[] LoopingClips =
        {
            "Idle_A", "Idle_B", "Walking_A", "Walking_B", "Walking_C", "Running_A", "Running_B",
            "Holding_A", "Holding_B", "Holding_C", "Working_A", "Working_B", "Working_C",
            "Sit_Chair_Idle", "Sit_Floor_Idle", "Cheering", "Waving", "Interact", "Use_Item",
        };

        void OnPreprocessAnimation()
        {
            if (!IsThirdParty(assetPath) || !assetPath.Contains("CharacterAnimations"))
                return;
            var model = (ModelImporter)assetImporter;
            var clips = model.defaultClipAnimations;
            foreach (var clip in clips)
                clip.loopTime = System.Array.IndexOf(LoopingClips, clip.name) >= 0;
            model.clipAnimations = clips;
        }

        void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] clips)
        {
            if (!IsThirdParty(assetPath))
                return;

            var toon = Shader.Find(ToonShaderName);
            if (toon == null)
            {
                Debug.LogWarning($"[Merge Resto] {ToonShaderName} not found; {assetPath} keeps its default material.");
                return;
            }
            material.shader = toon;

            // Packs use one atlas texture as diffuse; keep the tint white unless the file sets one.
            if (description.TryGetProperty("DiffuseColor", out TexturePropertyDescription diffuseTex) && diffuseTex.texture != null)
            {
                material.SetTexture("_BaseMap", diffuseTex.texture);
                material.SetColor("_BaseColor", Color.white);
            }
            else if (description.TryGetProperty("DiffuseColor", out Vector4 diffuse))
            {
                material.SetColor("_BaseColor", new Color(diffuse.x, diffuse.y, diffuse.z, 1f));
            }

            if (description.TryGetProperty("EmissiveColor", out Vector4 emissive) && emissive.x + emissive.y + emissive.z > 0.01f)
                material.SetColor("_EmissionColor", new Color(emissive.x, emissive.y, emissive.z, 1f));

            material.enableInstancing = true;
        }

        void OnPreprocessTexture()
        {
            if (!IsThirdParty(assetPath))
                return;

            var tex = (TextureImporter)assetImporter;
            tex.textureType = TextureImporterType.Default;
            tex.sRGBTexture = true;
            tex.mipmapEnabled = false;
            tex.maxTextureSize = AtlasMaxSize;
            // Atlases hold soft gradients, so keep bilinear; UVs sit inside each swatch.
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.textureCompression = TextureImporterCompression.Compressed;
        }

        const string ShowcaseScenePath = "Assets/Scenes/ArtShowcase.unity";

        /// <summary>
        /// Lays out every third-party model in a grid, one block per pack, under the game's
        /// look rig, to check scale, colours and outlines at a glance.
        /// </summary>
        [MenuItem("Merge Resto/Art/Build Asset Showcase")]
        static void BuildShowcase()
        {
            if (!AssetDatabase.IsValidFolder(Root.TrimEnd('/')))
            {
                Debug.LogWarning($"[Merge Resto] {Root} does not exist yet. Copy the packs there first.");
                return;
            }
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            var root = new GameObject("Showcase").transform;

            var guids = AssetDatabase.FindAssets("t:Model", new[] { Root.TrimEnd('/') });
            var byPack = new System.Collections.Generic.SortedDictionary<string, System.Collections.Generic.List<string>>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var pack = path.Substring(Root.Length).Split('/')[0];
                if (!byPack.TryGetValue(pack, out var list))
                    byPack[pack] = list = new System.Collections.Generic.List<string>();
                list.Add(path);
            }

            const float rowWidth = 16f;
            const float gap = 0.4f;
            float packZ = 0f;
            var all = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;
            int count = 0;

            foreach (var pack in byPack)
            {
                var packRoot = new GameObject(pack.Key).transform;
                packRoot.SetParent(root, false);
                float x = 0f, z = packZ, rowDepth = 0f;
                pack.Value.Sort(System.StringComparer.OrdinalIgnoreCase);

                foreach (var path in pack.Value)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                        continue;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, packRoot);
                    var b = RendererBounds(go);
                    if (b.size == Vector3.zero)
                    {
                        Object.DestroyImmediate(go);
                        continue;
                    }
                    if (x > 0f && x + b.size.x > rowWidth)
                    {
                        x = 0f;
                        z -= rowDepth + gap;
                        rowDepth = 0f;
                    }
                    // Sit the model on the ground with its bounds' min corner at (x, z).
                    go.transform.position += new Vector3(x - b.min.x, -b.min.y, z - b.max.z);
                    x += b.size.x + gap;
                    rowDepth = Mathf.Max(rowDepth, b.size.z);

                    var placed = RendererBounds(go);
                    if (!hasBounds) { all = placed; hasBounds = true; } else all.Encapsulate(placed);
                    count++;
                }
                packZ = z - rowDepth - gap * 4f;
            }

            if (!hasBounds)
            {
                Debug.LogWarning($"[Merge Resto] No models found under {Root}.");
                return;
            }

            // Ground plate under everything.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.transform.SetParent(root, false);
            floor.transform.position = new Vector3(all.center.x, -0.05f, all.center.z);
            floor.transform.localScale = new Vector3(all.size.x + 2f, 0.1f, all.size.z + 2f);
            var floorMat = new Material(Shader.Find(ToonShaderName));
            floorMat.SetColor("_BaseColor", new Color(0.97f, 0.91f, 0.8f));
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

            var cam = LookSetup.CreateLookRig(all.center, 5f);
            FitOrtho(cam, all, 16f / 9f);

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, ShowcaseScenePath);
            Debug.Log($"[Merge Resto] Showcase with {count} models in {byPack.Count} packs saved to {ShowcaseScenePath}.");
        }

        static Bounds RendererBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers)
                b.Encapsulate(r.bounds);
            return b;
        }

        static void FitOrtho(Camera cam, Bounds b, float aspect)
        {
            float maxX = 0f, maxY = 0f;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                var local = cam.transform.InverseTransformPoint(corner) - cam.transform.InverseTransformPoint(b.center);
                maxX = Mathf.Max(maxX, Mathf.Abs(local.x));
                maxY = Mathf.Max(maxY, Mathf.Abs(local.y));
            }
            cam.orthographicSize = Mathf.Max(maxY, maxX / aspect) * 1.08f;
        }

        [MenuItem("Merge Resto/Art/Reimport Third-Party Art")]
        static void ReimportAll()
        {
            if (!AssetDatabase.IsValidFolder(Root.TrimEnd('/')))
            {
                Debug.LogWarning($"[Merge Resto] {Root} does not exist yet. Copy the packs there first.");
                return;
            }
            AssetDatabase.ImportAsset(Root.TrimEnd('/'), ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            Debug.Log("[Merge Resto] Third-party art reimported with toon materials.");
        }
    }
}
