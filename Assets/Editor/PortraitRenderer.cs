using System.IO;
using UnityEditor;
using UnityEngine;

namespace MergeResto.EditorTools
{
    /// <summary>
    /// Renders a character's head and shoulders to a PNG sprite (dialogue portraits), using the
    /// game's own toon shading and outlines so the portrait matches the 3D cast exactly.
    /// Needs the scene's lighting, so it runs from the scene builder.
    /// </summary>
    public static class PortraitRenderer
    {
        public const string Folder = "Assets/Art/UI/Portraits";
        const int Size = 512;

        public static Sprite Render(string look, Color background, float yaw = 200f)
        {
            LookSetup.EnsureFolder(Folder);
            string path = $"{Folder}/{look}.png";

            var prefab = CharacterLibrary.LoadPrefab(look);
            var character = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var origin = new Vector3(400f, 0f, 400f);
            character.transform.SetPositionAndRotation(origin, Quaternion.Euler(0f, yaw, 0f));
            CharacterLibrary.SamplePose(character, "General", "Idle_A", 0.4f);

            var bounds = new Bounds(origin, Vector3.zero);
            foreach (var r in character.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(r.bounds);

            // Head and shoulders: the top ~45% of the figure, looking slightly down at the face.
            float frame = bounds.size.y * 0.3f;
            var focus = new Vector3(bounds.center.x, bounds.max.y - frame * 0.95f, bounds.center.z);
            var camGo = new GameObject("PortraitCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = frame;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            camGo.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
            camGo.transform.position = focus - camGo.transform.forward * 10f;

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
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(character);

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = Size;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
