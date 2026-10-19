#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using UnityEngine;

namespace MergeResto.Game.Release
{
    /// <summary>
    /// Editor-only: where a frame's time goes, over a few seconds (game scripts, rendering, the
    /// player loop as a whole, the editor around it, garbage collection).
    /// </summary>
    public class FrameProbe : MonoBehaviour
    {
        public static string Result { get; private set; } = "";
        public static bool Done { get; private set; } = true;

        public static void Run(float seconds)
        {
            Done = false;
            var go = new GameObject("FrameProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<FrameProbe>().StartCoroutine(go.GetComponent<FrameProbe>().Probe(seconds));
        }

        IEnumerator Probe(float seconds)
        {
            var names = new (ProfilerCategory cat, string name, string label)[]
            {
                (ProfilerCategory.Internal, "Main Thread", "main thread"),
                (ProfilerCategory.Internal, "PlayerLoop", "player loop"),
                (ProfilerCategory.Scripts, "BehaviourUpdate", "scripts (Update)"),
                (ProfilerCategory.Scripts, "PreLateUpdate.ScriptRunBehaviourLateUpdate", "scripts (LateUpdate)"),
                (ProfilerCategory.Render, "Camera.Render", "rendering (camera)"),
                (ProfilerCategory.Gui, "UGUI.Rendering.UpdateBatches", "UI batching"),
                (ProfilerCategory.Gui, "Canvas.SendWillRenderCanvases", "UI layout/rebuild"),
                (ProfilerCategory.Internal, "GC.Collect", "garbage collection"),
                (ProfilerCategory.Internal, "EditorLoop", "editor (not the game)"),
                (ProfilerCategory.Render, "RenderPipelineManager.DoRenderLoop_Internal()", "render pipeline (CPU)"),
                (ProfilerCategory.Render, "Gfx.WaitForPresentOnGfxThread", "waiting for the GPU"),
                (ProfilerCategory.Render, "Gfx.PresentFrame", "present"),
                (ProfilerCategory.Internal, "PostLateUpdate.FinishFrameRendering", "finish frame rendering"),
                (ProfilerCategory.Internal, "Update.ScriptRunBehaviourUpdate", "script Update loop"),
            };
            var recorders = names.Select(n => ProfilerRecorder.StartNew(n.cat, n.name, 600)).ToArray();
            var gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 600);
            var sums = new double[names.Length];
            var maxes = new double[names.Length];
            double allocSum = 0;
            int frames = 0;
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                frames++;
                for (int i = 0; i < recorders.Length; i++)
                {
                    double ms = recorders[i].Valid ? recorders[i].LastValue / 1e6 : -1;
                    sums[i] += ms;
                    maxes[i] = System.Math.Max(maxes[i], ms);
                }
                if (gcAlloc.Valid)
                    allocSum += gcAlloc.LastValue;
            }
            var lines = new List<string>();
            for (int i = 0; i < names.Length; i++)
                lines.Add(recorders[i].Valid ? $"{names[i].label}: avg {sums[i] / frames:0.00} ms, worst {maxes[i]:0.0} ms" : $"{names[i].label}: n/a");
            lines.Add($"garbage per frame: {allocSum / frames / 1024:0.0} KB");
            Result = string.Join("\n", lines);
            foreach (var r in recorders)
                r.Dispose();
            gcAlloc.Dispose();
            Debug.Log("[Merge Resto] Frame probe\n" + Result);
            Done = true;
            Destroy(gameObject);
        }
    }
}
#endif
