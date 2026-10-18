using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MergeResto.EditorTools
{
    /// <summary>
    /// Draws the game's UI art (panels, chunky buttons, pills, rings, icons, particle textures)
    /// with anti-aliased signed-distance shapes and writes them as sprites. Re-run to restyle.
    /// </summary>
    public static class UiArtGenerator
    {
        public const string Folder = "Assets/Art/UI/Sprites";

        // Bakery palette
        static readonly Color Outline = Hex("5B3A2E");
        static readonly Color Cream = Hex("FFF6EA");
        static readonly Color CreamShade = Hex("F3DFC6");
        public static readonly Color Green = Hex("6CC95A");
        public static readonly Color Orange = Hex("FF9447");
        public static readonly Color Blue = Hex("58A8F0");
        public static readonly Color Grey = Hex("C9BDB2");
        public static readonly Color Gold = Hex("FFC23D");
        public static readonly Color Red = Hex("F2645A");
        public static readonly Color Pink = Hex("FF8FB1");

        [MenuItem("Merge Resto/Art/Generate UI Art")]
        public static void GenerateAll()
        {
            LookSetup.EnsureFolder(Folder);

            Save("panel", 128, Panel, border: 44);
            Save("panel_inset", 128, Inset, border: 40);
            Save("shadow", 128, Shadow, border: 48);
            foreach (var (name, c) in new[] { ("green", Green), ("orange", Orange), ("blue", Blue), ("grey", Grey), ("gold", Gold), ("red", Red) })
                Save($"button_{name}", 128, (x, y) => Button(x, y, c), border: 44);
            Save("pill", 128, Pill, border: 60);
            Save("chip", 64, Chip, border: 30);
            Save("circle", 128, (x, y) => Disc(x, y, Color.white, 0f));
            Save("ring", 256, (x, y) => Ring(x, y, 0.36f, 0.48f, Color.white));
            Save("ring_outlined", 256, RingOutlined);
            Save("bubble", 256, Bubble);
            Save("ribbon", 256, Ribbon, borderX: 90, borderY: 0);
            Save("banner", 512, 160, Banner, borderX: 150, borderY: 0);
            Save("pill_dark", 256, 112, DarkPill, borderX: 56, borderY: 0);

            Save("icon_coin", 256, Coin);
            Save("icon_gem", 256, Gem);
            Save("icon_speed", 256, Bolt);
            Save("icon_output", 256, Stack);
            Save("icon_machine", 256, Gear);
            Save("icon_manager", 256, ChefHat);
            Save("icon_clock", 256, Clock);
            Save("icon_close", 128, Close);
            Save("icon_star", 256, StarIcon);
            Save("icon_arrow", 128, Arrow);

            Save("fx_puff", 128, SoftPuff);
            Save("fx_sparkle", 128, Sparkle);
            AssetDatabase.Refresh();
            Debug.Log("[Merge Resto] UI art generated.");
        }

        /// <summary>The merge board's covers: a cardboard box for hidden cells, cellophane for wrapped items.</summary>
        public static void GenerateBoardArt()
        {
            LookSetup.EnsureFolder(Folder);
            Save("board_box", 256, BoardBox);
            Save("board_wrap", 256, BoardWrap);
            AssetDatabase.Refresh();
        }

        public static Sprite Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{name}.png");

        // ------------------------------------------------------------------ Fonts

        public const string FontFolder = "Assets/Art/UI/Fonts";
        public const string DisplayFontPath = FontFolder + "/LilitaOne SDF.asset";
        public const string BodyFontPath = FontFolder + "/Fredoka SDF.asset";
        public const string DisplayOutlinedPath = FontFolder + "/LilitaOne Outlined.mat";
        public const string DisplayShadowPath = FontFolder + "/LilitaOne Shadow.mat";

        const string Charset =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~·×»";

        [MenuItem("Merge Resto/Art/Generate Fonts")]
        public static void GenerateFonts()
        {
            var display = MakeFont($"{FontFolder}/LilitaOne-Regular.ttf", DisplayFontPath, "LilitaOne SDF");
            var body = MakeFont($"{FontFolder}/Fredoka.ttf", BodyFontPath, "Fredoka SDF");
            var fallback = TMPro.TMP_Settings.defaultFontAsset;
            foreach (var f in new[] { display, body })
            {
                f.fallbackFontAssetTable = new List<TMPro.TMP_FontAsset> { fallback };
                EditorUtility.SetDirty(f);
            }

            var outlined = MaterialPreset(display, DisplayOutlinedPath);
            outlined.EnableKeyword("OUTLINE_ON");
            outlined.SetFloat("_OutlineWidth", 0.24f);
            outlined.SetColor("_OutlineColor", Outline);
            outlined.EnableKeyword("UNDERLAY_ON");
            outlined.SetColor("_UnderlayColor", new Color(0.25f, 0.13f, 0.1f, 0.55f));
            outlined.SetFloat("_UnderlayOffsetY", -0.9f);
            outlined.SetFloat("_UnderlaySoftness", 0.1f);
            outlined.SetFloat("_UnderlayDilate", 0.3f);
            outlined.SetFloat("_FaceDilate", 0.05f);

            var shadow = MaterialPreset(display, DisplayShadowPath);
            shadow.EnableKeyword("UNDERLAY_ON");
            shadow.SetColor("_UnderlayColor", new Color(0.25f, 0.13f, 0.1f, 0.3f));
            shadow.SetFloat("_UnderlayOffsetY", -0.6f);
            shadow.SetFloat("_UnderlaySoftness", 0.3f);

            EditorUtility.SetDirty(outlined);
            EditorUtility.SetDirty(shadow);
            AssetDatabase.SaveAssets();
            Debug.Log("[Merge Resto] Fonts generated.");
        }

        static TMPro.TMP_FontAsset MakeFont(string ttfPath, string assetPath, string name)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(assetPath);
            if (existing != null)
                return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            var fa = TMPro.TMP_FontAsset.CreateFontAsset(font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024, 1024, TMPro.AtlasPopulationMode.Dynamic, true);
            fa.name = name;
            AssetDatabase.CreateAsset(fa, assetPath);
            fa.atlasTextures[0].name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            fa.TryAddCharacters(Charset, out _);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        static Material MaterialPreset(TMPro.TMP_FontAsset font, string path)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(font.material);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.CopyPropertiesFromMaterial(font.material);
            mat.shaderKeywords = font.material.shaderKeywords;
            return mat;
        }

        // ------------------------------------------------------------------ Output

        delegate Color Painter(float x, float y);

        static void Save(string name, int size, Painter paint, int border = 0, int borderX = -1, int borderY = -1) =>
            Save(name, size, size, paint, border, borderX, borderY);

        static void Save(string name, int width, int height, Painter paint, int border = 0, int borderX = -1, int borderY = -1)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            float aspect = width / (float)height;
            _aa = 1f / height;
            for (int py = 0; py < height; py++)
            for (int px = 0; px < width; px++)
            {
                // Normalised coords: y -0.5..0.5 (up), x scaled by aspect. AA width is one pixel.
                float x = ((px + 0.5f) / width - 0.5f) * aspect;
                float y = (py + 0.5f) / height - 0.5f;
                pixels[py * width + px] = paint(x, y);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            string path = $"{Folder}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            int bx = borderX >= 0 ? borderX : border;
            int by = borderY >= 0 ? borderY : border;
            imp.spriteBorder = new Vector4(bx, by, bx, by);
            imp.SaveAndReimport();
        }

        // ------------------------------------------------------------------ SDF helpers

        static float _aa;

        static float Cover(float d) => Mathf.Clamp01(0.5f - d / _aa);

        static float RoundBox(float x, float y, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(x) - hx + r, qy = Mathf.Abs(y) - hy + r;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static float Circle(float x, float y, float cx, float cy, float r) =>
            Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

        static float Segment(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        // Convex or concave polygon (even-odd), signed distance.
        static float Polygon(float x, float y, Vector2[] v)
        {
            float d = (x - v[0].x) * (x - v[0].x) + (y - v[0].y) * (y - v[0].y);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                float ex = v[j].x - v[i].x, ey = v[j].y - v[i].y;
                float wx = x - v[i].x, wy = y - v[i].y;
                float h = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
                float bx = wx - ex * h, by = wy - ey * h;
                d = Mathf.Min(d, bx * bx + by * by);
                bool c1 = y >= v[i].y, c2 = y < v[j].y, c3 = ex * wy > ey * wx;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        static float Union(float a, float b) => Mathf.Min(a, b);
        static float Subtract(float a, float b) => Mathf.Max(a, -b);

        // Fill with an outline: shape distance d, outline thickness t (normalised).
        static Color Shape(float d, Color fill, float t, Color? outline = null)
        {
            var o = outline ?? Outline;
            float outer = Cover(d);
            float inner = Cover(d + t);
            var c = Color.Lerp(o, fill, inner);
            c.a = outer * Mathf.Lerp(o.a, fill.a, inner);
            return c;
        }

        static Color Over(Color top, Color bottom)
        {
            float a = top.a + bottom.a * (1f - top.a);
            if (a <= 0.0001f) return new Color(0, 0, 0, 0);
            var rgb = (new Vector3(top.r, top.g, top.b) * top.a + new Vector3(bottom.r, bottom.g, bottom.b) * bottom.a * (1f - top.a)) / a;
            return new Color(rgb.x, rgb.y, rgb.z, a);
        }

        static Color Darken(Color c, float k) => new(c.r * k, c.g * k, c.b * k, c.a);
        static Color Lighten(Color c, float k) => Color.Lerp(c, Color.white, k);

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;

        // ------------------------------------------------------------------ Panels & buttons

        static Color Panel(float x, float y)
        {
            float d = RoundBox(x, y, 0.47f, 0.47f, 0.3f);
            var fill = Color.Lerp(CreamShade, Cream, Mathf.Clamp01(y * 3f + 0.9f));
            var c = Shape(d, fill, 0.045f);
            // Soft inner highlight along the top edge.
            float hi = Cover(RoundBox(x, y - 0.33f, 0.36f, 0.06f, 0.06f)) * 0.35f;
            return Over(new Color(1, 1, 1, hi * c.a), c);
        }

        static Color Inset(float x, float y)
        {
            float d = RoundBox(x, y, 0.47f, 0.47f, 0.28f);
            var fill = Color.Lerp(Hex("F6E7D3"), Hex("FFFDF8"), Mathf.Clamp01(y * 2f + 0.6f));
            return Shape(d, fill, 0.03f, Hex("D9BFA4"));
        }

        static Color Shadow(float x, float y)
        {
            float d = RoundBox(x, y, 0.36f, 0.36f, 0.3f);
            float a = Mathf.Clamp01(1f - (d + 0.02f) / 0.12f);
            return new Color(0.25f, 0.13f, 0.1f, a * a * 0.45f);
        }

        // Chunky 3D button: darker lip at the bottom, glossy band at the top, dark outline.
        static Color Button(float x, float y, Color baseColor)
        {
            float outer = RoundBox(x, y, 0.47f, 0.47f, 0.3f);
            float face = RoundBox(x, y - 0.04f, 0.43f, 0.39f, 0.26f);
            var lip = Darken(baseColor, 0.72f);
            var c = Shape(outer, lip, 0.045f);
            var faceColor = Color.Lerp(Darken(baseColor, 0.92f), Lighten(baseColor, 0.12f), Mathf.Clamp01(y * 2.2f + 0.6f));
            c = Over(new Color(faceColor.r, faceColor.g, faceColor.b, Cover(face)), c);
            float gloss = Cover(RoundBox(x, y - 0.25f, 0.33f, 0.07f, 0.07f)) * 0.45f;
            return Over(new Color(1, 1, 1, gloss), c);
        }

        static Color Pill(float x, float y)
        {
            float d = RoundBox(x, y, 0.47f, 0.47f, 0.47f);
            var fill = Color.Lerp(CreamShade, Cream, Mathf.Clamp01(y * 3f + 0.8f));
            return Shape(d, fill, 0.05f);
        }

        static Color Chip(float x, float y)
        {
            float d = RoundBox(x, y, 0.46f, 0.46f, 0.46f);
            return Shape(d, Color.white, 0.07f, Darken(Color.white, 0.55f));
        }

        static Color Disc(float x, float y, Color c, float outline) =>
            Shape(Circle(x, y, 0, 0, 0.47f), c, outline, outline > 0 ? Outline : c);

        static Color Ring(float x, float y, float r0, float r1, Color c)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float d = Mathf.Max(r - r1, r0 - r);
            var col = c;
            col.a = Cover(d);
            return col;
        }

        static Color RingOutlined(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float d = Mathf.Max(r - 0.47f, 0.31f - r);
            return Shape(d, new Color(1f, 1f, 1f, 0.35f), 0.035f, new Color(0.36f, 0.23f, 0.18f, 0.85f));
        }

        static Color BoardBox(float x, float y)
        {
            var edge = Hex("A87A52");
            float body = RoundBox(x, y + 0.06f, 0.38f, 0.3f, 0.06f);
            var bodyFill = Color.Lerp(Hex("DDB283"), Hex("F0CFA0"), Mathf.Clamp01(y * 1.6f + 0.6f));
            var c = Shape(body, bodyFill, 0.02f, edge);
            // Lid, a little wider, on top.
            float lid = RoundBox(x, y - 0.25f, 0.43f, 0.09f, 0.05f);
            var lidC = Shape(lid, Color.Lerp(Hex("E8C08F"), Hex("F6DDB4"), Mathf.Clamp01((y + 0.25f) * 6f + 0.5f)), 0.02f, edge);
            c = Over(lidC, c);
            // Tape down the middle and a soft shine on the left.
            float tape = Mathf.Max(RoundBox(x, y - 0.02f, 0.075f, 0.33f, 0.01f), Union(body, lid));
            c = Over(new Color(1f, 0.95f, 0.84f, Cover(tape) * 0.85f * c.a), c);
            float shine = Mathf.Max(RoundBox(x + 0.27f, y + 0.1f, 0.035f, 0.14f, 0.03f), body);
            c = Over(new Color(1f, 1f, 1f, Cover(shine) * 0.28f), c);
            return c;
        }

        static Color BoardWrap(float x, float y)
        {
            float d = RoundBox(x, y, 0.46f, 0.46f, 0.16f);
            var c = Shape(d, new Color(0.86f, 0.95f, 1f, 0.22f), 0.022f, new Color(1f, 1f, 1f, 0.85f));
            // Diagonal glints, like light on cellophane.
            float glint = Mathf.Min(Segment(x, y, -0.33f, 0.1f, -0.1f, 0.33f, 0.035f), Segment(x, y, -0.34f, -0.06f, -0.26f, 0.02f, 0.025f));
            c = Over(new Color(1f, 1f, 1f, Cover(Mathf.Max(glint, d)) * 0.7f), c);
            float glint2 = Mathf.Max(Segment(x, y, 0.16f, -0.34f, 0.34f, -0.16f, 0.025f), d);
            c = Over(new Color(1f, 1f, 1f, Cover(glint2) * 0.45f), c);
            // A little pink bow on top.
            float bow = Union(Circle(x, y, -0.07f, 0.42f, 0.06f), Circle(x, y, 0.07f, 0.42f, 0.06f));
            c = Over(Shape(bow, Pink, 0.02f, Darken(Pink, 0.7f)), c);
            c = Over(Shape(Circle(x, y, 0f, 0.42f, 0.035f), Lighten(Pink, 0.3f), 0.015f, Darken(Pink, 0.7f)), c);
            return c;
        }

        static Color Bubble(float x, float y)
        {
            float body = RoundBox(x, y + 0.06f, 0.46f, 0.3f, 0.2f);
            float tail = Polygon(x, y, new[] { new Vector2(-0.1f, -0.2f), new Vector2(0.1f, -0.2f), new Vector2(0f, -0.44f) });
            float d = Union(body, tail);
            var fill = Color.Lerp(Darken(Orange, 0.9f), Lighten(Orange, 0.15f), Mathf.Clamp01(y * 2f + 0.5f));
            var c = Shape(d, fill, 0.03f, Color.white);
            float outer = Union(RoundBox(x, y + 0.06f, 0.48f, 0.32f, 0.22f), tail - 0.02f);
            return Over(c, new Color(Outline.r, Outline.g, Outline.b, Cover(outer) * (1f - c.a)));
        }

        static Color Ribbon(float x, float y)
        {
            float band = RoundBox(x, y, 0.47f, 0.2f, 0.06f);
            var fill = Color.Lerp(Darken(Orange, 0.88f), Lighten(Orange, 0.1f), Mathf.Clamp01(y * 3f + 0.5f));
            var c = Shape(band, fill, 0.025f);
            float stitch = Mathf.Abs(Mathf.Abs(y) - 0.13f) < 0.008f && Mathf.Repeat(x * 40f, 1f) < 0.55f && Mathf.Abs(x) < 0.43f ? 0.5f : 0f;
            return Over(new Color(1, 1, 1, stitch * c.a), c);
        }

        // Ribbon banner with folded tails: x spans -1.6..1.6, y -0.5..0.5.
        static Color Banner(float x, float y)
        {
            float halfW = 1.6f;
            // Tails sit behind, lower, with a V notch at the outer end.
            float ax = Mathf.Abs(x);
            float tailBox = RoundBox(ax - (halfW - 0.36f), y + 0.1f, 0.34f, 0.27f, 0.04f);
            float notch = Polygon(ax, y + 0.1f, new[] { new Vector2(halfW + 0.05f, 0.32f), new Vector2(halfW - 0.2f, 0f), new Vector2(halfW + 0.05f, -0.32f) });
            float tail = Subtract(tailBox, notch);
            var tailFill = Color.Lerp(Darken(Orange, 0.62f), Darken(Orange, 0.8f), Mathf.Clamp01(y + 0.5f));
            var c = Shape(tail, tailFill, 0.035f);
            // Fold shadow where the band meets the tail.
            float fold = Polygon(ax, y, new[] { new Vector2(halfW - 0.62f, -0.12f), new Vector2(halfW - 0.4f, -0.37f), new Vector2(halfW - 0.4f, -0.12f) });
            c = Over(Shape(fold, Darken(Orange, 0.45f), 0.03f), c);
            // Main band.
            float band = RoundBox(x, y + 0.03f, halfW - 0.42f, 0.33f, 0.08f);
            var bandFill = Color.Lerp(Darken(Orange, 0.86f), Lighten(Orange, 0.14f), Mathf.Clamp01(y * 1.6f + 0.55f));
            c = Over(Shape(band, bandFill, 0.04f), c);
            // Gloss and stitching.
            float gloss = RoundBox(x, y - 0.17f, halfW - 0.6f, 0.07f, 0.07f);
            c = Over(new Color(1, 1, 1, Cover(gloss) * 0.32f), c);
            bool stitchRow = Mathf.Abs(y + 0.03f - 0.25f) < 0.012f || Mathf.Abs(y + 0.03f + 0.25f) < 0.012f;
            if (stitchRow && Mathf.Repeat(x * 22f, 1f) < 0.55f && ax < halfW - 0.55f)
                c = Over(new Color(1f, 0.93f, 0.8f, 0.75f), c);
            return c;
        }

        // Dark counter pill for the top bar: x spans -1.14..1.14.
        static Color DarkPill(float x, float y)
        {
            float halfW = 1.14f;
            float d = RoundBox(x, y, halfW - 0.03f, 0.47f, 0.47f);
            var fill = Color.Lerp(Hex("4A2E24"), Hex("7A5243"), Mathf.Clamp01(y * 1.4f + 0.5f));
            var c = Shape(d, fill, 0.07f, Hex("FFF1DE"));
            float outer = RoundBox(x, y, halfW, 0.5f, 0.5f);
            c = Over(c, new Color(Outline.r, Outline.g, Outline.b, Cover(outer)));
            float gloss = RoundBox(x, y - 0.2f, halfW - 0.4f, 0.08f, 0.08f);
            return Over(new Color(1, 1, 1, Cover(gloss) * 0.14f), c);
        }

        // ------------------------------------------------------------------ Icons

        static Color Coin(float x, float y)
        {
            var c = Shape(Circle(x, y, 0, -0.02f, 0.42f), Hex("D9921F"), 0.035f);
            c = Over(Shape(Circle(x, y, 0, 0.02f, 0.38f), Hex("FFC23D"), 0f, Hex("FFC23D")), c);
            var inner = Shape(Circle(x, y, 0, 0.02f, 0.27f), Hex("FFD966"), 0.025f, Hex("E3A42B"));
            c = Over(inner, c);
            float star = StarDist(x, y - 0.02f, 0.15f, 0.07f);
            c = Over(Shape(star, Hex("F2B133"), 0.02f, Hex("D18A1A")), c);
            float shine = Subtract(Circle(x, y, -0.13f, 0.17f, 0.12f), Circle(x, y, -0.09f, 0.13f, 0.12f));
            return Over(new Color(1, 1, 1, Cover(shine) * 0.8f), c);
        }

        static Color Gem(float x, float y)
        {
            var outline = new[] { new Vector2(-0.38f, 0.12f), new Vector2(-0.2f, 0.34f), new Vector2(0.2f, 0.34f), new Vector2(0.38f, 0.12f), new Vector2(0f, -0.4f) };
            var c = Shape(Polygon(x, y, outline), Hex("5FB4FF"), 0.035f, Hex("2B4F8C"));
            // Facets
            var top = new[] { new Vector2(-0.2f, 0.34f), new Vector2(0.2f, 0.34f), new Vector2(0.12f, 0.12f), new Vector2(-0.12f, 0.12f) };
            c = Over(new Color(1, 1, 1, Cover(Polygon(x, y, top)) * 0.45f), c);
            var left = new[] { new Vector2(-0.38f, 0.12f), new Vector2(-0.12f, 0.12f), new Vector2(0f, -0.4f) };
            c = Over(new Color(0.1f, 0.25f, 0.6f, Cover(Polygon(x, y, left) + 0.01f) * 0.25f), c);
            float spark = StarDist(x - 0.12f, y - 0.2f, 0.08f, 0.02f);
            return Over(new Color(1, 1, 1, Cover(spark)), c);
        }

        static Color Bolt(float x, float y)
        {
            var pts = new[] { new Vector2(0.08f, 0.44f), new Vector2(-0.26f, -0.02f), new Vector2(-0.02f, -0.02f), new Vector2(-0.1f, -0.44f), new Vector2(0.28f, 0.06f), new Vector2(0.03f, 0.06f) };
            var fill = Color.Lerp(Hex("FFB02E"), Hex("FFE36B"), Mathf.Clamp01(y + 0.5f));
            return Shape(Polygon(x, y, pts), fill, 0.04f);
        }

        static Color Stack(float x, float y)
        {
            Color c = new(0, 0, 0, 0);
            float[] ys = { -0.24f, 0.0f, 0.24f };
            for (int i = 0; i < 3; i++)
            {
                float d = RoundBox(x, y - ys[i], 0.36f - i * 0.04f, 0.1f, 0.1f);
                var fill = Color.Lerp(Hex("D9893E"), Hex("F5B567"), i / 2f);
                c = Over(Shape(d, fill, 0.035f), c);
                float score = Segment(x, y, -0.12f + 0.0f, ys[i] + 0.035f, -0.04f, ys[i] - 0.035f, 0.012f);
                score = Union(score, Segment(x, y, 0.06f, ys[i] + 0.035f, 0.14f, ys[i] - 0.035f, 0.012f));
                c = Over(new Color(0.55f, 0.3f, 0.12f, Cover(score) * 0.7f * Cover(d)), c);
            }
            return c;
        }

        static Color Gear(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Atan2(y, x);
            float teeth = Mathf.Clamp01(Mathf.Cos(a * 8f) * 3f + 0.4f);
            float outerR = Mathf.Lerp(0.33f, 0.43f, teeth);
            float d = Mathf.Max(r - outerR, 0.12f - r);
            var fill = Color.Lerp(Hex("8E96A6"), Hex("C9D0DB"), Mathf.Clamp01(y + 0.6f));
            var c = Shape(d, fill, 0.035f, Hex("3E4350"));
            return Over(Shape(Circle(x, y, 0, 0, 0.19f) , new Color(0, 0, 0, 0), 0.03f, Hex("3E4350")), c);
        }

        static Color ChefHat(float x, float y)
        {
            float puff = Union(Circle(x, y, -0.18f, 0.1f, 0.18f), Union(Circle(x, y, 0.18f, 0.1f, 0.18f), Circle(x, y, 0f, 0.2f, 0.22f)));
            float band = RoundBox(x, y + 0.2f, 0.25f, 0.13f, 0.05f);
            float d = Union(puff, band);
            var c = Shape(d, Color.white, 0.04f);
            c = Over(new Color(0.86f, 0.82f, 0.8f, Cover(RoundBox(x, y + 0.2f, 0.21f, 0.09f, 0.04f)) * 0.6f), c);
            float line = Union(Segment(x, y, -0.08f, -0.12f, -0.08f, -0.28f, 0.012f), Segment(x, y, 0.08f, -0.12f, 0.08f, -0.28f, 0.012f));
            return Over(new Color(0.75f, 0.7f, 0.68f, Cover(line)), c);
        }

        static Color Clock(float x, float y)
        {
            var c = Shape(Circle(x, y, 0, 0, 0.42f), Hex("FFFDF5"), 0.045f);
            c = Over(Shape(Circle(x, y, 0, 0, 0.34f), new Color(0, 0, 0, 0), 0.02f, Hex("E8D8C4")), c);
            float hands = Union(Segment(x, y, 0, 0, 0, 0.24f, 0.03f), Segment(x, y, 0, 0, 0.17f, -0.08f, 0.03f));
            c = Over(new Color(Outline.r, Outline.g, Outline.b, Cover(hands)), c);
            return Over(Shape(Circle(x, y, 0, 0, 0.045f), Orange, 0f, Orange), c);
        }

        static Color Close(float x, float y)
        {
            float d = Union(Segment(x, y, -0.2f, -0.2f, 0.2f, 0.2f, 0.07f), Segment(x, y, -0.2f, 0.2f, 0.2f, -0.2f, 0.07f));
            return Shape(d, Color.white, 0.035f);
        }

        static Color StarIcon(float x, float y)
        {
            var fill = Color.Lerp(Hex("FFB02E"), Hex("FFE36B"), Mathf.Clamp01(y + 0.5f));
            return Shape(StarDist(x, y + 0.02f, 0.44f, 0.2f), fill, 0.04f);
        }

        static Color Arrow(float x, float y)
        {
            var pts = new[] { new Vector2(0f, 0.42f), new Vector2(0.36f, 0.02f), new Vector2(0.14f, 0.02f), new Vector2(0.14f, -0.4f), new Vector2(-0.14f, -0.4f), new Vector2(-0.14f, 0.02f), new Vector2(-0.36f, 0.02f) };
            return Shape(Polygon(x, y, pts), Hex("7BD957"), 0.05f, Hex("2F6B2A"));
        }

        static float StarDist(float x, float y, float outer, float inner)
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? outer : inner;
                pts[i] = new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
            }
            return Polygon(x, y, pts);
        }

        // ------------------------------------------------------------------ FX

        static Color SoftPuff(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y) / 0.48f;
            float a = Mathf.Clamp01(1f - r);
            return new Color(1, 1, 1, a * a * (3f - 2f * a));
        }

        static Color Sparkle(float x, float y)
        {
            float d = Polygon(x, y, new[]
            {
                new Vector2(0f, 0.46f), new Vector2(0.07f, 0.07f), new Vector2(0.46f, 0f), new Vector2(0.07f, -0.07f),
                new Vector2(0f, -0.46f), new Vector2(-0.07f, -0.07f), new Vector2(-0.46f, 0f), new Vector2(-0.07f, 0.07f),
            });
            float glow = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y) / 0.3f) * 0.5f;
            return new Color(1, 1, 1, Mathf.Max(Cover(d), glow * glow));
        }
    }
}
