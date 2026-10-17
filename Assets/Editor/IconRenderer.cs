using System.IO;
using UnityEditor;
using UnityEngine;

namespace MergeResto.EditorTools
{
    /// <summary>
    /// Renders 3D models to transparent icon sprites (merge items, generators) with the game's
    /// toon shading and outlines, so board items look exactly like the props in the shop.
    /// Transparency comes from rendering twice, on black and on white, and comparing.
    /// </summary>
    public static class IconRenderer
    {
        public const string Folder = "Assets/Art/UI/Icons";
        const int Size = 256;

        public static Sprite Render(string modelPath, string iconName, float yaw = 35f, float pitch = 24f, float padding = 1.12f)
        {
            LookSetup.EnsureFolder(Folder);
            string path = $"{Folder}/{iconName}.png";

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (prefab == null)
            {
                Debug.LogError($"[Merge Resto] Icon model not found: {modelPath}");
                return null;
            }
            var origin = new Vector3(-400f, 0f, -400f);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.transform.SetPositionAndRotation(origin, Quaternion.Euler(0f, yaw, 0f));

            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);

            var camGo = new GameObject("IconCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 100f;
            camGo.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
            camGo.transform.position = bounds.center - camGo.transform.forward * 30f;

            // Fit the projected bounds.
            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x, (i & 2) == 0 ? bounds.min.y : bounds.max.y, (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                Vector3 local = camGo.transform.InverseTransformPoint(c);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            Vector2 centre = (min + max) * 0.5f;
            camGo.transform.position += camGo.transform.right * centre.x + camGo.transform.up * centre.y;
            cam.orthographicSize = Mathf.Max(max.x - min.x, max.y - min.y) * 0.5f * padding;

            var onBlack = Capture(cam, Color.black);
            var onWhite = Capture(cam, Color.white);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(model);

            var result = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var b = onBlack.GetPixels();
            var w = onWhite.GetPixels();
            var px = new Color[b.Length];
            for (int i = 0; i < px.Length; i++)
            {
                float alpha = Mathf.Clamp01(1f - ((w[i].r - b[i].r) + (w[i].g - b[i].g) + (w[i].b - b[i].b)) / 3f);
                px[i] = alpha > 0.004f ? new Color(b[i].r / alpha, b[i].g / alpha, b[i].b / alpha, alpha) : Color.clear;
            }
            result.SetPixels(px);
            result.Apply();
            File.WriteAllBytes(path, result.EncodeToPNG());
            Object.DestroyImmediate(onBlack);
            Object.DestroyImmediate(onWhite);
            Object.DestroyImmediate(result);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = Size;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Texture2D Capture(Camera cam, Color background)
        {
            cam.backgroundColor = background;
            var rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 4);
            cam.targetTexture = rt;
            cam.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }
    }
}
