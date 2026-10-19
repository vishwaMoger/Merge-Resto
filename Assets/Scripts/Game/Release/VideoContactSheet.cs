#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace MergeResto.Game.Release
{
    /// <summary>
    /// Editor-only check for a recorded trailer: plays the video and lays one frame per second out
    /// on a contact sheet next to it (trailer.mp4 -> trailer_sheet.png).
    /// </summary>
    public class VideoContactSheet : MonoBehaviour
    {
        public static bool Done { get; private set; } = true;

        string _video;
        VideoPlayer _player;
        readonly List<Texture2D> _thumbs = new();
        int _thumbW, _thumbH;

        public static void Make(string videoPath)
        {
            Done = false;
            var go = new GameObject("VideoContactSheet");
            DontDestroyOnLoad(go);
            var sheet = go.AddComponent<VideoContactSheet>();
            sheet._video = Path.GetFullPath(videoPath);
            sheet.Play();
        }

        void Play()
        {
            _player = gameObject.AddComponent<VideoPlayer>();
            _player.source = VideoSource.Url;
            _player.url = "file:///" + _video.Replace('\\', '/');
            _player.renderMode = VideoRenderMode.APIOnly;
            _player.audioOutputMode = VideoAudioOutputMode.None;
            _player.skipOnDrop = false;
            _player.sendFrameReadyEvents = true;
            _player.frameReady += OnFrame;
            _player.loopPointReached += _ => Finish();
            _player.errorReceived += (_, message) => { Debug.LogError($"[Merge Resto] Video: {message}"); Finish(); };
            _player.Play();
        }

        void OnFrame(VideoPlayer player, long frame)
        {
            int fps = Mathf.Max(1, Mathf.RoundToInt((float)player.frameRate));
            if (frame % fps != 0 || player.texture == null)
                return;
            var src = player.texture;
            _thumbW = 384;
            _thumbH = Mathf.RoundToInt(384f * src.height / src.width);
            var rt = RenderTexture.GetTemporary(_thumbW, _thumbH);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var thumb = new Texture2D(_thumbW, _thumbH, TextureFormat.RGB24, false);
            thumb.ReadPixels(new Rect(0, 0, _thumbW, _thumbH), 0, 0);
            thumb.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            _thumbs.Add(thumb);
        }

        void Finish()
        {
            if (Done)
                return;
            if (_thumbs.Count > 0)
            {
                const int Columns = 6;
                int rows = (_thumbs.Count + Columns - 1) / Columns;
                var sheet = new Texture2D(Columns * _thumbW, rows * _thumbH, TextureFormat.RGB24, false);
                var fill = new Color32[sheet.width * sheet.height];
                for (int i = 0; i < fill.Length; i++)
                    fill[i] = new Color32(40, 30, 30, 255);
                sheet.SetPixels32(fill);
                for (int i = 0; i < _thumbs.Count; i++)
                {
                    int x = i % Columns * _thumbW;
                    int y = (rows - 1 - i / Columns) * _thumbH;
                    sheet.SetPixels(x, y, _thumbW, _thumbH, _thumbs[i].GetPixels());
                }
                sheet.Apply();
                File.WriteAllBytes(Path.ChangeExtension(_video, null) + "_sheet.png", sheet.EncodeToPNG());
                Debug.Log($"[Merge Resto] Contact sheet: {_thumbs.Count} frames.");
            }
            Done = true;
            Destroy(gameObject);
        }
    }
}
#endif
