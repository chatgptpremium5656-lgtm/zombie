using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zombie.EditorTools
{
    /// <summary>
    /// Generates the small hand-made textures of the look probe from signed distance functions:
    /// rounded UI panels (with a soft shadow and an outline), crosshair, heart icon and particle
    /// sprites (soft dot, flame, smoke, leaf). Everything is anti-aliased and resolution independent.
    /// </summary>
    static class TextureFactory
    {
        public const string UiFolder = "Assets/Art/UI";
        public const string FxFolder = "Assets/Art/Textures";

        public const string Rounded = UiFolder + "/UI_Rounded.png";
        public const string RoundedShadow = UiFolder + "/UI_RoundedShadow.png";
        public const string RoundedOutline = UiFolder + "/UI_RoundedOutline.png";
        public const string Circle = UiFolder + "/UI_Circle.png";
        public const string Ring = UiFolder + "/UI_Ring.png";
        public const string Heart = UiFolder + "/UI_Heart.png";
        public const string SoftDot = FxFolder + "/FX_SoftDot.png";
        public const string Flame = FxFolder + "/FX_Flame.png";
        public const string Smoke = FxFolder + "/FX_Smoke.png";
        public const string Leaf = FxFolder + "/FX_Leaf.png";

        public static void BuildAll()
        {
            Folders.Ensure(UiFolder);
            Folders.Ensure(FxFolder);

            // 9-sliced panel: radius 24 px in a 64 px texture.
            Write(Rounded, 64, 64, (x, y) => White(Coverage(RoundBox(x - 32, y - 32, 32, 32, 24))), true, 26);
            Write(RoundedShadow, 128, 128, (x, y) =>
            {
                float d = RoundBox(x - 64, y - 64, 40, 40, 28);
                float a = Mathf.Clamp01(1f - Smooth(-14f, 22f, d));
                return new Color(0, 0, 0, a * a);
            }, true, 60);
            Write(RoundedOutline, 64, 64, (x, y) =>
            {
                float d = RoundBox(x - 32, y - 32, 32, 32, 24);
                return White(Coverage(Mathf.Abs(d + 3.5f) - 3.5f));
            }, true, 26);
            Write(Circle, 64, 64, (x, y) => White(Coverage(Length(x - 32, y - 32) - 30f)), true, 0);
            Write(Ring, 128, 128, (x, y) => White(Coverage(Mathf.Abs(Length(x - 64, y - 64) - 54f) - 6f)), true, 0);
            Write(Heart, 128, 128, HeartPixel, true, 0);

            Write(SoftDot, 64, 64, (x, y) =>
            {
                float r = Length(x - 32, y - 32) / 31f;
                float a = Mathf.Clamp01(1f - r);
                return White(a * a * (3f - 2f * a));
            }, false, 0);
            Write(Flame, 64, 128, FlamePixel, false, 0);
            Write(Smoke, 128, 128, (x, y) =>
            {
                float r = Length(x - 64, y - 64) / 60f;
                float n = Mathf.PerlinNoise(x * 0.06f, y * 0.06f) * 0.35f + Mathf.PerlinNoise(x * 0.15f + 7f, y * 0.15f) * 0.15f;
                float a = Mathf.Clamp01(1f - Smooth(0.35f, 1f, r + n * 0.5f));
                return White(a * 0.9f);
            }, false, 0);
            Write(Leaf, 64, 64, LeafPixel, false, 0);
        }

        static Color HeartPixel(float x, float y)
        {
            // Inigo Quilez heart SDF, scaled to the texture with an outline and a glossy highlight.
            float px = (x - 64f) / 88f;
            float py = (y - 12f) / 88f;
            float d = SdHeart(px, py) * 88f;
            float fill = Coverage(d);
            float outline = Coverage(d - 5f);
            var deep = new Color(0.78f, 0.12f, 0.2f);
            var bright = new Color(1f, 0.38f, 0.42f);
            Color c = Color.Lerp(deep, bright, Mathf.Clamp01(py * 0.9f));
            float hl = Coverage(Length(x - 42f, y - 86f) - 12f) * 0.7f;
            c = Color.Lerp(c, Color.white, hl);
            Color edge = new Color(0.32f, 0.05f, 0.1f);
            Color result = Color.Lerp(edge, c, fill);
            result.a = outline;
            return result;
        }

        static float SdHeart(float x, float y)
        {
            x = Mathf.Abs(x);
            if (y + x > 1f)
                return Length(x - 0.25f, y - 0.75f) - Mathf.Sqrt(2f) / 4f;
            float a = (x - 0f) * (x - 0f) + (y - 1f) * (y - 1f);
            float m = 0.5f * Mathf.Max(x + y, 0f);
            float b = (x - m) * (x - m) + (y - m) * (y - m);
            return Mathf.Sqrt(Mathf.Min(a, b)) * Mathf.Sign(x - y);
        }

        static Color FlamePixel(float x, float y)
        {
            // Teardrop: round bottom, tapering tip. Bright core for bloom.
            float u = (x - 32f) / 28f;
            float v = y / 128f;
            float width = Mathf.Lerp(1f, 0.05f, Mathf.Pow(Mathf.Clamp01((v - 0.28f) / 0.72f), 0.9f));
            float bottom = v < 0.28f ? Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((0.28f - v) / 0.26f, 2f))) : 1f;
            float w = width * bottom;
            float d = Mathf.Abs(u) - w;
            float a = Mathf.Clamp01(-d * 3.2f);
            a *= Mathf.Clamp01(v * 8f);
            float core = Mathf.Clamp01(1f - Mathf.Abs(u) / Mathf.Max(w, 0.01f)) * Mathf.Clamp01(1.2f - v * 1.3f);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(a * (0.55f + core * 0.45f)));
        }

        static Color LeafPixel(float x, float y)
        {
            // Vesica (two circle intersection) leaf with a darker midrib.
            float px = x - 32f;
            float py = y - 32f;
            float d1 = Length(px - 17f, py) - 27f;
            float d2 = Length(px + 17f, py) - 27f;
            float d = Mathf.Max(d1, d2);
            float a = Coverage(d);
            float rib = 1f - Coverage(Mathf.Abs(px) - 1.2f) * 0.25f;
            float shade = Mathf.Lerp(0.82f, 1f, (py + 28f) / 56f) * rib;
            return new Color(shade, shade, shade, a);
        }

        static float RoundBox(float x, float y, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(x) - hx + r;
            float qy = Mathf.Abs(y) - hy + r;
            float outside = Length(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f));
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static float Length(float x, float y) => Mathf.Sqrt(x * x + y * y);

        static float Coverage(float sd) => Mathf.Clamp01(0.5f - sd);

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static Color White(float a) => new Color(1f, 1f, 1f, a);

        static void Write(string path, int width, int height, Func<float, float, Color> pixel, bool ui, int border)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var colors = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 4x supersampling for clean edges.
                    Color sum = default;
                    for (int s = 0; s < 4; s++)
                        sum += pixel(x + 0.25f + (s % 2) * 0.5f, y + 0.25f + (s / 2) * 0.5f);
                    colors[y * width + x] = sum * 0.25f;
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true;
            if (ui)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.spriteBorder = new Vector4(border, border, border, border);
                importer.spritePixelsPerUnit = 100;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = true;
            }
            importer.SaveAndReimport();
        }
    }
}
