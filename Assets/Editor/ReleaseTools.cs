using System.IO;
using System.Linq;
using MergeResto.Game;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MergeResto.EditorTools
{
    /// <summary>
    /// Release prep: the game's title in the build and web page, the branded boot screen, and
    /// the store cover images (rendered from the live game, title on top).
    /// </summary>
    public static class ReleaseTools
    {
        const string BootScenePath = "Assets/Scenes/Boot.unity";
        const string SweetLand = "Assets/Art/UI/SweetLand/Sprites/";
        public const string CoverFolder = "Release/Covers";
        const string TemplateName = "MainStreet";

        static readonly Color Cream = new(1f, 0.95f, 0.88f);
        static readonly Color Brown = new(0.42f, 0.25f, 0.16f);
        static readonly Color SoftBrown = new(0.6f, 0.42f, 0.3f);

        [MenuItem("Merge Resto/Release/Apply Title And Web Page")]
        public static void ApplyTitle()
        {
            PlayerSettings.productName = GameInfo.Title;
            PlayerSettings.bundleVersion = GameInfo.Version;
            PlayerSettings.WebGL.template = $"PROJECT:{TemplateName}";
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.runInBackground = false;
            AssetDatabase.SaveAssets();
            Debug.Log($"[Merge Resto] Title set to \"{GameInfo.Title}\" {GameInfo.Version}, web page template {TemplateName}.");
        }

        static Sprite SL(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(SweetLand + path + ".png");

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Image Img(RectTransform rt, Sprite sprite, Color color, float slice = 0f)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (slice > 0f && sprite != null)
            {
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = slice;
            }
            else
                img.preserveAspect = true;
            return img;
        }

        static TMP_Text Label(RectTransform parent, string name, string text, float size, bool display, Color color, bool outlined = false)
        {
            var rt = Rect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(display ? UiArtGenerator.DisplayFontPath : UiArtGenerator.BodyFontPath);
            if (outlined)
                tmp.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(UiArtGenerator.DisplayOutlinedPath);
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            if (!display)
                tmp.fontStyle = FontStyles.Bold;
            return tmp;
        }

        // The title on its orange banner (shared by the boot screen and the covers).
        static RectTransform TitleBanner(RectTransform parent, Vector2 pos, float width, float height, float fontSize)
        {
            var banner = Rect("Banner", parent);
            banner.anchoredPosition = pos;
            banner.sizeDelta = new Vector2(width, height);
            Img(banner, SL("Buttons/250px/Button_Honeycomb_Orange_250px"), Color.white, 204f / height);
            var title = Label(banner, "Title", GameInfo.Title, fontSize, true, Color.white, outlined: true);
            title.rectTransform.anchorMin = Vector2.zero;
            title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.offsetMin = new Vector2(60f, height * 0.08f);
            title.rectTransform.offsetMax = new Vector2(-60f, 0f);
            title.enableAutoSizing = true;
            title.fontSizeMin = fontSize * 0.5f;
            title.fontSizeMax = fontSize;
            return banner;
        }

        static RectTransform Portrait(RectTransform parent, Vector2 pos, float size)
        {
            var frame = Rect("Portrait", parent);
            frame.anchoredPosition = pos;
            frame.sizeDelta = new Vector2(size, size);
            Img(frame, SL("Frames/250px/Frame_Circle_Orange_250px"), Color.white);
            // The square portrait is clipped to a disc inside the orange ring.
            var mask = Rect("Mask", frame);
            mask.sizeDelta = new Vector2(size * 0.8f, size * 0.8f);
            Img(mask, SL("Frames/250px/Frame_Circle_White_250px"), new Color(1f, 0.85f, 0.66f));
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var face = Rect("Face", mask);
            face.sizeDelta = new Vector2(size * 0.92f, size * 0.92f);
            face.anchoredPosition = new Vector2(0f, -size * 0.04f);
            Img(face, AssetDatabase.LoadAssetAtPath<Sprite>($"{PortraitRenderer.Folder}/{CharacterLibrary.GrandmaLook}.png"), Color.white);
            return frame;
        }

        /// <summary>A quick local web build for measuring (no compression, faster linking), in Builds/PerfTest.</summary>
        [MenuItem("Merge Resto/Release/Quick Perf Build")]
        public static void QuickPerfBuild()
        {
            var keepCompression = PlayerSettings.WebGL.compressionFormat;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            try
            {
                var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = "Builds/PerfTest",
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None,
                });
                Debug.Log($"[Merge Resto] Perf build: {report.summary.result} in {report.summary.totalTime.TotalMinutes:0.0} min.");
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = keepCompression;
                AssetDatabase.SaveAssets();
            }
        }

        [MenuItem("Merge Resto/Release/Build Boot Screen")]
        public static void BuildBootScreen()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects().Where(g => g.GetComponent<Canvas>() != null || g.name == "BootScreen").ToList())
                Object.DestroyImmediate(go);
            var cam = Object.FindAnyObjectByType<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Cream;
            }

            var root = new GameObject("BootScreen", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var rt = (RectTransform)root.transform;

            var bg = Rect("Background", rt);
            bg.anchorMin = Vector2.zero;
            bg.anchorMax = Vector2.one;
            bg.offsetMin = bg.offsetMax = Vector2.zero;
            Img(bg, null, Cream);
            var shine = Rect("Shine", rt);
            shine.anchoredPosition = new Vector2(0f, 190f);
            shine.sizeDelta = new Vector2(1300f, 1300f);
            Img(shine, SL("BG_Shining"), new Color(1f, 0.78f, 0.45f, 0.55f));

            // Treats bobbing along the bottom: everything you'll make down Main Street.
            var parade = Rect("Parade", rt);
            parade.anchorMin = new Vector2(0f, 0f);
            parade.anchorMax = new Vector2(1f, 0f);
            parade.pivot = new Vector2(0.5f, 0f);
            parade.anchoredPosition = Vector2.zero;
            parade.sizeDelta = new Vector2(0f, 150f);
            Img(parade, null, new Color(1f, 0.86f, 0.7f));
            var treats = new System.Collections.Generic.List<RectTransform>();
            string[] icons = { "sweets_0", "sweets_2", "kitchen_1", "sweets_3", "plants_2", "sweets_4", "kitchen_3", "sweets_5", "plants_4", "sweets_6", "sweets_1", "kitchen_2" };
            for (int i = 0; i < icons.Length; i++)
            {
                var icon = Rect($"Treat{i}", parade);
                icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
                icon.sizeDelta = new Vector2(120f, 120f);
                Img(icon, AssetDatabase.LoadAssetAtPath<Sprite>($"{IconRenderer.Folder}/{icons[i]}.png"), Color.white);
                treats.Add(icon);
            }

            var portrait = Portrait(rt, new Vector2(0f, 190f), 340f);
            var banner = TitleBanner(rt, new Vector2(0f, -80f), 1180f, 190f, 104f);
            var tagline = Label(rt, "Tagline", GameInfo.Tagline, 36f, false, SoftBrown);
            tagline.rectTransform.anchoredPosition = new Vector2(0f, -205f);
            tagline.rectTransform.sizeDelta = new Vector2(1400f, 60f);

            var bar = Rect("Bar", rt);
            bar.anchoredPosition = new Vector2(0f, -290f);
            bar.sizeDelta = new Vector2(900f, 58f);
            Img(bar, SL("Frames/250px/Frame_Square_White_250px"), new Color(0.66f, 0.42f, 0.27f), 2.4f);
            var fillArea = Rect("FillArea", bar);
            fillArea.anchorMin = Vector2.zero;
            fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = new Vector2(8f, 8f);
            fillArea.offsetMax = new Vector2(-8f, -8f);
            var fill = Rect("Fill", fillArea);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.1f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            Img(fill, SL("Frames/250px/Frame_Square_White_250px"), new Color(0.45f, 0.82f, 0.36f), 3.2f);
            var status = Label(bar, "Status", "Warming up the ovens...", 30f, true, Color.white, outlined: true);
            status.rectTransform.anchorMin = Vector2.zero;
            status.rectTransform.anchorMax = Vector2.one;
            status.rectTransform.offsetMin = new Vector2(0f, 4f);
            status.rectTransform.offsetMax = Vector2.zero;
            var tip = Label(rt, "Tip", "", 30f, false, Brown);
            tip.rectTransform.anchoredPosition = new Vector2(0f, -360f);
            tip.rectTransform.sizeDelta = new Vector2(1500f, 50f);

            var screen = root.AddComponent<BootScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("tip").objectReferenceValue = tip;
            so.FindProperty("status").objectReferenceValue = status;
            so.FindProperty("portrait").objectReferenceValue = portrait;
            so.FindProperty("banner").objectReferenceValue = banner;
            so.FindProperty("rays").objectReferenceValue = shine;
            var treatsProp = so.FindProperty("treats");
            treatsProp.arraySize = treats.Count;
            for (int i = 0; i < treats.Count; i++)
                treatsProp.GetArrayElementAtIndex(i).objectReferenceValue = treats[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            var loader = Object.FindAnyObjectByType<BootLoader>();
            if (loader != null)
            {
                loader.screen = screen;
                EditorUtility.SetDirty(loader);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Merge Resto] Boot screen built.");
        }

        /// <summary>
        /// Renders the store covers from the shop that is playing (enter play mode first, let some
        /// customers come in): 1920x1080, 800x1200 and 800x800, the HUD hidden and the title on top.
        /// </summary>
        static void Hook()
        {
            Game.Release.TrailerDirector.SetGameViewSize = GameViewSize.Set;
            Game.Release.TrailerDirector.RenderCovers = RenderCovers;
            Game.Release.TrailerDirector.RenderCover = (path, w, h) =>
                RenderCoverTo(path, w, h, w < h ? 10.6f : 6.6f, new Vector2(0f, w < h ? 0.38f : 0.36f), w * 0.9f, w < h ? 190f : 190f, w < h ? 92f : 104f);
        }

        [MenuItem("Merge Resto/Release/Render Staged Covers (in play mode)")]
        static void StagedCovers()
        {
            Hook();
            Game.Release.TrailerDirector.RenderStagedCovers();
        }

        [MenuItem("Merge Resto/Release/Record Trailer Landscape (in play mode)")]
        static void TrailerLandscape()
        {
            Hook();
            Game.Release.TrailerDirector.RecordLandscape();
        }

        [MenuItem("Merge Resto/Release/Record Trailer Portrait (in play mode)")]
        static void TrailerPortrait()
        {
            Hook();
            Game.Release.TrailerDirector.RecordPortrait();
        }

        [MenuItem("Merge Resto/Release/Render Covers (in play mode)")]
        public static void RenderCovers()
        {
            Directory.CreateDirectory(CoverFolder);
            RenderCover("cover_landscape_1920x1080", 1920, 1080, 6.6f, new Vector2(0f, 0.36f), 1100f, 190f, 104f);
            RenderCover("cover_portrait_800x1200", 800, 1200, 10.6f, new Vector2(0f, 0.38f), 760f, 150f, 74f);
            RenderCover("cover_square_800x800", 800, 800, 7.6f, new Vector2(0f, 0.36f), 760f, 140f, 70f);
            Debug.Log($"[Merge Resto] Covers written to {Path.GetFullPath(CoverFolder)}.");
        }

        static void RenderCover(string name, int width, int height, float orthoSize, Vector2 titleAnchor, float bannerW, float bannerH, float fontSize) =>
            RenderCoverTo(Path.Combine(CoverFolder, name + ".png"), width, height, orthoSize, titleAnchor, bannerW, bannerH, fontSize);

        static void RenderCoverTo(string path, int width, int height, float orthoSize, Vector2 titleAnchor, float bannerW, float bannerH, float fontSize)
        {
            var cam = Camera.main;
            var hidden = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.enabled).ToList();
            foreach (var c in hidden)
                c.enabled = false;
            float keepSize = cam.orthographicSize;
            float keepAspect = cam.aspect;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            GameObject overlay = null, titleCamGo = null;
            try
            {
                // 1. The shop, with the HUD hidden.
                cam.targetTexture = target;
                cam.aspect = width / (float)height;
                cam.orthographicSize = orthoSize;
                cam.Render();
                var scene = Grab(target, width, height);

                // 2. The title on its own camera and layer, without post effects, once over black and once
                //    over white: the difference gives its exact transparency for a clean paste.
                const int TitleLayer = 31;
                titleCamGo = new GameObject("CoverTitleCam");
                var titleCam = titleCamGo.AddComponent<Camera>();
                titleCam.cullingMask = 1 << TitleLayer;
                titleCam.clearFlags = CameraClearFlags.SolidColor;
                titleCam.orthographic = true;
                titleCam.targetTexture = target;
                var urp = titleCamGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                urp.renderPostProcessing = false;
                urp.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;

                overlay = new GameObject("CoverTitle", typeof(RectTransform));
                overlay.layer = TitleLayer;
                var canvas = overlay.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = titleCam;
                canvas.planeDistance = 1f;
                var scaler = overlay.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(width, height);
                scaler.matchWidthOrHeight = 0.5f;
                var rt = (RectTransform)overlay.transform;
                rt.sizeDelta = new Vector2(width, height);
                var banner = TitleBanner(rt, new Vector2(titleAnchor.x * width, titleAnchor.y * height), bannerW, bannerH, fontSize);
                banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 0.5f);
                foreach (var t in overlay.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = TitleLayer;

                titleCam.backgroundColor = Color.black;
                Canvas.ForceUpdateCanvases();
                titleCam.Render();
                var onBlack = Grab(target, width, height);
                titleCam.backgroundColor = Color.white;
                titleCam.Render();
                var onWhite = Grab(target, width, height);

                var outPixels = scene.GetPixels();
                var b = onBlack.GetPixels();
                var w = onWhite.GetPixels();
                for (int i = 0; i < outPixels.Length; i++)
                {
                    float a = Mathf.Clamp01(1f - (w[i].r - b[i].r + w[i].g - b[i].g + w[i].b - b[i].b) / 3f);
                    if (a <= 0.002f)
                        continue;
                    var c = new Color(b[i].r / a, b[i].g / a, b[i].b / a);
                    outPixels[i] = Color.Lerp(outPixels[i], c, a);
                }
                scene.SetPixels(outPixels);
                scene.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, scene.EncodeToPNG());
                Object.DestroyImmediate(scene);
                Object.DestroyImmediate(onBlack);
                Object.DestroyImmediate(onWhite);
            }
            finally
            {
                cam.targetTexture = null;
                cam.orthographicSize = keepSize;
                cam.aspect = keepAspect;
                cam.ResetAspect();
                if (overlay != null) Object.DestroyImmediate(overlay);
                if (titleCamGo != null) Object.DestroyImmediate(titleCamGo);
                target.Release();
                Object.DestroyImmediate(target);
                foreach (var c in hidden)
                    c.enabled = true;
            }
        }

        static Texture2D Grab(RenderTexture rt, int width, int height)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }
    }
}
