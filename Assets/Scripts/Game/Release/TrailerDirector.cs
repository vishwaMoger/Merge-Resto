#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using MergeResto.Game.UI;
using MergeResto.Game.Views;
using MergeResto.Sim;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MergeResto.Game.Release
{
    /// <summary>
    /// Records the CrazyGames preview videos (about 18.5 s, no sound, real speed, no cursor): it
    /// opens on the cover, then the busy bakery, merges on the board (boxes popping open, new
    /// items), a renovation playing out in the shop, and a quick tour of the other four shops.
    /// Each shop is staged through its save and warmed up off camera, then filmed at a fixed
    /// 30 frames per second, so everything on screen runs at normal speed. Editor only: the
    /// Release menu starts it and hands in the editor helpers (Game view size, cover rendering).
    /// </summary>
    public class TrailerDirector : MonoBehaviour
    {
        const int Fps = 30;
        public const string Folder = "Release/Trailer";
        public const string CoverFolder = "Release/Covers";

        /// <summary>Editor helpers, set by the Release menu.</summary>
        public static Action<int, int> SetGameViewSize;
        public static Action RenderCovers;
        public static Action<string, int, int> RenderCover;

        int _width, _height;
        bool _portrait;
        bool _recording;
        MediaEncoder _encoder;
        Texture2D _frame;
        RenderTexture _scaled;
        int _frames;

        public static void RecordLandscape() => Begin(1920, 1080, false);
        public static void RecordPortrait() => Begin(1080, 1620, true);

        public static bool Running { get; private set; }

        /// <summary>The covers, from the bakery at its best: every job done, staff hired, full of customers.</summary>
        public static void RenderStagedCovers()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[Merge Resto] Enter play mode first.");
                return;
            }
            SetGameViewSize?.Invoke(1920, 1080);
            var go = new GameObject("CoverDirector");
            DontDestroyOnLoad(go);
            var d = go.AddComponent<TrailerDirector>();
            Running = true;
            d.StartCoroutine(d.Covers());
        }

        IEnumerator Covers()
        {
            yield return new WaitForSecondsRealtime(1f);
            SaveStore.Save("lessons", "renovate,goal,expand,shops,vip,boost,boxes");
            DailyRewardStore.Claim();
            SaveStore.Delete("shop_grandmas_bakery");
            yield return Stage("Bakery", sim =>
            {
                PrepareCommon(sim, 8);
                for (int r = 0; r < sim.Board.Def.renovations.Count; r++)
                    sim.Board.State.renovationsDone.Add(r);
                for (int a = 0; a < sim.Board.Def.areas.Count; a++)
                    sim.Board.State.areasDone.Add(a);
            });
            yield return WarmUp(9f);
            Time.timeScale = 0f;
            yield return null;
            RenderCovers?.Invoke();
            Time.timeScale = 1f;
            Running = false;
            Destroy(gameObject);
        }

        static void Begin(int width, int height, bool portrait)
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[Merge Resto] Enter play mode first.");
                return;
            }
            SetGameViewSize?.Invoke(width, height);
            var go = new GameObject("TrailerDirector");
            DontDestroyOnLoad(go);
            var d = go.AddComponent<TrailerDirector>();
            d._width = width;
            d._height = height;
            d._portrait = portrait;
            Running = true;
            d.StartCoroutine(d.Run());
        }

        // ------------------------------------------------------------------ Recording

        IEnumerator Capture()
        {
            var wait = new WaitForEndOfFrame();
            while (true)
            {
                yield return wait;
                if (!_recording || _encoder == null)
                    continue;
                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                if (shot.width != _width || shot.height != _height)
                {
                    // The Game view is a different size: scale the frame to the video size.
                    Graphics.Blit(shot, _scaled);
                    var prev = RenderTexture.active;
                    RenderTexture.active = _scaled;
                    _frame.ReadPixels(new Rect(0, 0, _width, _height), 0, 0);
                    RenderTexture.active = prev;
                }
                else
                    _frame.SetPixels32(shot.GetPixels32());
                _frame.Apply();
                Destroy(shot);
                _encoder.AddFrame(_frame);
                _frames++;
            }
        }

        IEnumerator Film(int frames)
        {
            Time.captureFramerate = Fps;
            Time.timeScale = 1f;
            _recording = true;
            for (int i = 0; i < frames; i++)
                yield return null;
            _recording = false;
        }

        // Runs the shop fast and off camera until it is busy.
        static IEnumerator WarmUp(float realSeconds)
        {
            Time.captureFramerate = 0;
            Time.timeScale = 4f;
            yield return new WaitForSecondsRealtime(realSeconds);
            Time.timeScale = 1f;
        }

        // ------------------------------------------------------------------ Staging

        static void PrepareCommon(ShopSim sim, int levels)
        {
            sim.State.tutorialStep = ShopState.TutorialFinished;
            for (int i = 0; i < sim.State.stations.Count; i++)
            {
                var st = sim.State.stations[i];
                st.hasManager = true;
                st.speedLevel = levels;
                st.outputLevel = levels;
            }
            if (sim.State.coins < 50000)
                sim.State.coins = 50000;
        }

        // Writes the prepared state as the shop's save and opens the shop again, so everything
        // (renovations, the expansion stage, the board) shows from the start.
        IEnumerator Stage(string scene, System.Action<ShopSim> prepare)
        {
            SceneManager.LoadScene(scene);
            yield return null;
            yield return null;
            var gc = GameController.Instance;
            var sim = gc.Sim;
            prepare(sim);
            sim.State.lastSavedUnixSeconds = 0; // no "welcome back" summary
            gc.enabled = false;
            SaveStore.Save($"shop_{sim.Def.id}", JsonUtility.ToJson(sim.State));
            SceneManager.LoadScene(scene);
            yield return null;
            yield return null;
            FitForVideo();
        }

        void FitForVideo()
        {
            if (!_portrait)
                return;
            // The game asks phones to turn sideways; the portrait video shows it upright anyway.
            foreach (var fit in FindObjectsByType<ScreenFit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                fit.enabled = false;
                var prompt = new SerializedObject(fit).FindProperty("rotatePrompt").objectReferenceValue as GameObject;
                if (prompt != null)
                    prompt.SetActive(false);
            }
            // The HUD laid out for a 1400-wide screen: the top bar fits and the board stays readable.
            foreach (var scaler in FindObjectsByType<CanvasScaler>(FindObjectsSortMode.None))
            {
                scaler.referenceResolution = new Vector2(1400f, 1400f * _height / _width);
                scaler.matchWidthOrHeight = 0f;
            }
            Time.timeScale = 1f;
        }

        // A slow push-in towards the middle of the view.
        static void PushIn(float zoom)
        {
            var cam = Camera.main;
            var director = cam.GetComponent<CameraDirector>();
            var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (director != null && new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float d))
                director.Focus(ray.GetPoint(d), cam.orthographicSize * zoom);
        }

        // A drag from one cell to another, eased, with a little arc, as a finger would do it.
        static IEnumerator Drag(MergeBoardView board, int from, int to, int frames)
        {
            Vector2 P(int cell)
            {
                var rt = board.Cell(cell);
                return rt.TransformPoint(rt.rect.center);
            }
            var a = P(from);
            var b = P(to);
            var e = new PointerEventData(EventSystem.current) { position = a, pointerId = -1 };
            board.OnDragBegin(from, e);
            for (int i = 1; i <= frames; i++)
            {
                float t = Mathf.SmoothStep(0f, 1f, i / (float)frames);
                e.position = Vector2.Lerp(a, b, t) + Vector2.up * Mathf.Sin(t * Mathf.PI) * 40f;
                board.OnDragMove(e);
                yield return null;
            }
            board.OnDragEnd(e);
        }

        Texture2D LoadCover()
        {
            string file = _portrait ? Path.Combine(Folder, "cover_portrait_1080x1620.png") : Path.Combine(CoverFolder, "cover_landscape_1920x1080.png");
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(file));
            return tex;
        }

        static (GameObject root, CanvasGroup group) CoverOverlay(Texture2D cover)
        {
            var root = new GameObject("TrailerCover", typeof(RectTransform));
            DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000;
            var group = root.AddComponent<CanvasGroup>();
            var img = new GameObject("Cover", typeof(RectTransform)).AddComponent<RawImage>();
            img.transform.SetParent(root.transform, false);
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            img.texture = cover;
            img.raycastTarget = false;
            return (root, group);
        }

        // ------------------------------------------------------------------ The trailer

        IEnumerator Run()
        {
            Directory.CreateDirectory(Folder);
            CameraDirector.FitOverride = _portrait ? 1.5f : -1f;
            yield return new WaitForSecondsRealtime(1f); // let the Game view settle at its new size
            // A clean slate: no lessons, Grandma's gift claimed, new items announced.
            SaveStore.Save("lessons", "renovate,goal,expand,shops,vip,boost,boxes");
            DailyRewardStore.Claim();
            PlayerPrefs.DeleteKey("board_discovered");
            PlayerPrefs.SetInt("board_opened", 1);
            foreach (var id in new[] { "grandmas_bakery", "corner_cafe", "nonnos_pizzeria", "belles_patisserie", "drive_thru" })
            {
                SaveStore.Delete($"shop_{id}");
                WorldState.Unlock(id);
            }

            // The bakery, a few jobs in, with a staged board for the merge shot.
            yield return Stage("Bakery", sim =>
            {
                PrepareCommon(sim, 4);
                foreach (int r in new[] { 0, 1, 2 })
                    sim.Board.State.renovationsDone.Add(r);
                sim.Board.State.areasDone.Add(0);
                var cells = sim.Board.State.cells;
                foreach (int i in new[] { 9, 10, 11, 15, 16, 17, 18, 19, 23, 24, 25, 26 })
                    cells[i] = BoardItem.Empty.code;
                void Put(int cell, int chain, int tier) => cells[cell] = BoardItem.Item(chain, tier).code;
                Put(25, 0, 0); Put(26, 0, 0);   // cookies (the merge opens the box below 26)
                Put(16, 2, 0); Put(9, 2, 0);    // succulents (opens the box above 9)
                Put(10, 2, 1);                  // a little cactus, for the pothos the job needs
                Put(23, 1, 1); Put(18, 1, 0); Put(19, 0, 1);
                sim.Board.State.energy = sim.Board.Def.energyMax;
            });
            yield return WarmUp(6f);

            if (_portrait && !File.Exists(Path.Combine(Folder, "cover_portrait_1080x1620.png")))
            {
                // The portrait video opens on a cover made for its own shape.
                RenderCover?.Invoke(Path.Combine(Folder, "cover_portrait_1080x1620.png"), 1080, 1620);
            }

            var cover = LoadCover();
            var (overlay, group) = CoverOverlay(cover);
            var path = Path.Combine(Folder, _portrait ? "trailer_portrait_1080x1620.mp4" : "trailer_landscape_1920x1080.mp4");
            if (File.Exists(path))
                File.Delete(path);
            _frame = new Texture2D(_width, _height, TextureFormat.RGBA32, false);
            _scaled = new RenderTexture(_width, _height, 0);
            _encoder = new MediaEncoder(path, new VideoTrackAttributes
            {
                frameRate = new MediaRational(Fps),
                width = (uint)_width,
                height = (uint)_height,
                includeAlpha = false,
                bitRateMode = VideoBitrateMode.High,
            });
            StartCoroutine(Capture());

            // 1. The cover, then it fades into the game.
            yield return Film(36);
            Time.captureFramerate = Fps;
            _recording = true;
            for (int i = 0; i < 12; i++)
            {
                group.alpha = 1f - (i + 1) / 12f;
                yield return null;
            }
            Destroy(overlay);

            // 2. The busy bakery, the camera drifting in.
            PushIn(0.86f);
            yield return Film(80);

            // 3. Merging: cookies, then two succulents, then the cactus into a pothos.
            var board = FindAnyObjectByType<MergeBoardView>();
            board.SetOpen(true);
            yield return Film(16);
            _recording = true;
            yield return Drag(board, 25, 26, 16);
            yield return Film(12);
            _recording = true;
            yield return Drag(board, 16, 9, 16);
            yield return Film(12);
            _recording = true;
            yield return Drag(board, 9, 10, 16);
            yield return Film(18);

            // 4. Fix: the board closes and the window plant comes back in the shop.
            var fix = board.FixButtonFor(3);
            if (fix != null)
                fix.GetComponent<Button>().onClick.Invoke();
            yield return Film(118);
            _recording = false;

            // 5. Main Street: the other shops, grown and fixed up.
            foreach (var scene in new[] { "Cafe", "Pizzeria", "Patisserie", "DriveThru" })
            {
                yield return Stage(scene, sim =>
                {
                    PrepareCommon(sim, 8);
                    sim.State.expansion = 2;
                    for (int r = 0; r < sim.Board.Def.renovations.Count; r++)
                        if (!sim.Board.State.renovationsDone.Contains(r))
                            sim.Board.State.renovationsDone.Add(r);
                    for (int a = 0; a < sim.Board.Def.areas.Count; a++)
                        sim.Board.State.areasDone.Add(a);
                });
                yield return WarmUp(5f);
                PushIn(0.9f);
                yield return Film(39);
            }

            _encoder.Dispose();
            _encoder = null;
            CameraDirector.FitOverride = -1f;
            Time.captureFramerate = 0;
            Time.timeScale = 1f;
            Debug.Log($"[Merge Resto] Trailer written to {Path.GetFullPath(path)} ({_frames} frames, {_frames / (float)Fps:0.0} s).");
            Running = false;
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            CameraDirector.FitOverride = -1f;
            _encoder?.Dispose();
            Running = false;
            if (_scaled != null)
                _scaled.Release();
        }
    }
}
#endif
