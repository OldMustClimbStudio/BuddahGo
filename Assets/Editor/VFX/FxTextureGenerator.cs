using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuddahGo.VFX.EditorTools
{
    // FX textures generated in the editor (Docs/vfx/README.md section 7): noise, soft dot masks and, for now, the palm
    // seals. Shape textures must be authored (drawn, scanned or image-generated and cleaned); the sutra strip is baked from
    // real text by Tools/vfx/bake_sutra.py and the brush stroke is a cleaned scan (Tools/vfx/cut_brush.py).
    // TEMPORARY: the seals (MakePalmPrint) are a rasterised projection of the body mesh standing in until an authored
    // seal exists (palm doc 13.6); do not add further procedural shape textures here. Deterministic (fixed seeds).
    internal static class FxTextureGenerator
    {
        internal const string Folder = "Assets/VFX/Shared/Textures";
        internal const string NoisePacked = Folder + "/T_FX_Noise_Packed.png";
        internal const string BrushStroke = Folder + "/T_FX_Brush_Stroke.png";
        internal const string SutraGlyph = Folder + "/T_FX_Sutra_Glyph.png";
        internal const string EnsoMask = Folder + "/T_FX_Enso_Mask.png";
        internal const string InkDot = Folder + "/T_FX_InkDot_Mask.png";
        internal const string Petal = Folder + "/T_FX_Petal_Mask.png";
        internal const string PalmPrint = Folder + "/T_FX_PalmPrint_Mask.png";
        internal const string PalmPrintBroken = Folder + "/T_FX_PalmPrint_Broken_Mask.png";

        [MenuItem("Tools/BuddahGo/VFX/Generate FX Textures")]
        internal static void GenerateAll()
        {
            Directory.CreateDirectory(Folder);
            Write(NoisePacked, MakeNoisePacked(256), false, TextureWrapMode.Repeat);
            Write(InkDot, MakeInkDot(64), false, TextureWrapMode.Clamp);
            Write(Petal, MakePetal(64), false, TextureWrapMode.Clamp);
            Mesh palm = AssetDatabase.LoadAssetAtPath<Mesh>(PalmMeshBaker.OutputPath);
            if (palm == null)
                palm = PalmMeshBaker.Bake();
            Write(PalmPrint, MakePalmPrint(palm, 512, false), false, TextureWrapMode.Clamp);
            Write(PalmPrintBroken, MakePalmPrint(palm, 512, true), false, TextureWrapMode.Clamp);
            ConfigureAuthored();
            AssetDatabase.Refresh();
        }

        // Import settings for the textures baked outside Unity (Tools/vfx): linear data (R = along the stroke).
        internal static void ConfigureAuthored()
        {
            Configure(BrushStroke, TextureWrapMode.Clamp);
            Configure(EnsoMask, TextureWrapMode.Clamp);
            Configure(SutraGlyph, TextureWrapMode.Repeat);
        }

        static void Configure(string path, TextureWrapMode wrap)
        {
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.wrapMode = wrap;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        static void Write(string path, Texture2D texture, bool sRGB, TextureWrapMode wrap)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = sRGB;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.wrapMode = wrap;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        // ---- noise helpers (tileable value noise on a lattice of `period` cells) ----

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777215f;
            }
        }

        static float ValueNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int Wrap(int v) => ((v % period) + period) % period;
            float a = Hash(Wrap(x0), Wrap(y0), seed), b = Hash(Wrap(x0 + 1), Wrap(y0), seed);
            float c = Hash(Wrap(x0), Wrap(y0 + 1), seed), d = Hash(Wrap(x0 + 1), Wrap(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        // u, v in 0..1; tileable in both.
        static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int period = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += ValueNoise(u * period, v * period, period, seed + o * 17) * amp;
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }
            return sum / norm;
        }

        static float Cellular(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            float best = 10f;
            for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                int gx = cx + i, gy = cy + j;
                int wx = ((gx % period) + period) % period, wy = ((gy % period) + period) % period;
                float px = gx + Hash(wx, wy, seed), py = gy + Hash(wx, wy, seed + 7);
                best = Mathf.Min(best, Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y)));
            }
            return Mathf.Clamp01(best);
        }

        // GLSL-style smoothstep (Mathf.SmoothStep interpolates between its first two arguments instead).
        static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        static float Contrast(float v, float amount)
        {
            return Mathf.Clamp01((v - 0.5f) * amount + 0.5f);
        }

        // R tiling fbm, G cellular (soft mask), B dissolve fbm, A horizontal gradient.
        static Texture2D MakeNoisePacked(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                pixels[y * size + x] = new Color(
                    Contrast(Fbm(u, v, 4, 5, 11), 1.6f),
                    1f - Cellular(u, v, 6, 23),
                    Contrast(Fbm(u, v, 3, 6, 37), 1.8f),
                    u);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static void Line(float[] field, int width, int height, float ax, float ay, float bx, float by, float thickness)
        {
            int minX = Mathf.FloorToInt(Mathf.Min(ax, bx) - thickness), maxX = Mathf.CeilToInt(Mathf.Max(ax, bx) + thickness);
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ay, by) - thickness)), maxY = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(ay, by) + thickness));
            var a = new Vector2(ax, ay);
            Vector2 ab = new Vector2(bx, by) - a;
            float len2 = Mathf.Max(1e-4f, ab.sqrMagnitude);
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                float d = (p - (a + ab * t)).magnitude;
                float value = Mathf.Clamp01((thickness * 0.5f - d) + 0.5f);
                int wx = ((x % width) + width) % width;
                int index = y * width + wx;
                field[index] = Mathf.Max(field[index], value);
            }
        }

        // A lotus-petal / gold-leaf flake: a pointed vesica with a soft vein down the middle.
        static Texture2D MakePetal(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float halfWidth = 0.55f * Mathf.Sqrt(Mathf.Max(0f, 1f - v * v)) * (1f - 0.25f * v);
                float inside = Smooth(halfWidth, halfWidth - 0.12f, Mathf.Abs(u));
                float vein = 1f - 0.35f * Smooth(0.08f, 0f, Mathf.Abs(u));
                float value = inside * vein * Smooth(1f, 0.85f, Mathf.Abs(v));
                pixels[y * size + x] = new Color(value, value, value, value);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static Texture2D MakeInkDot(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float r = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
                float edge = 0.75f + (Fbm(u, v, 4, 2, 91) - 0.5f) * 0.3f;
                float value = Smooth(edge, edge - 0.25f, r);
                pixels[y * size + x] = new Color(value, value, value, value);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // TEMPORARY stand-in for an authored seal (see the class comment). The palm silhouette seen from the front,
        // rasterised from the baked body mesh (so the print always matches the
        // body), with a stamp texture: the ink coverage is uneven and speckled like a seal on paper. The broken
        // variant (anti palette) adds cracks and chipped edges.
        static Texture2D MakePalmPrint(Mesh mesh, int size, bool broken)
        {
            const int Supersample = 3;
            int s = size * Supersample;
            var coverage = new float[s * s];
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Bounds bounds = mesh.bounds;
            float scale = 0.9f * s / Mathf.Max(bounds.size.x, bounds.size.y);
            Vector2 ToPixel(Vector3 p) => new Vector2((p.x - bounds.center.x) * scale + s * 0.5f, (p.y - bounds.center.y) * scale + s * 0.5f);

            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector2 a = ToPixel(vertices[triangles[t]]), b = ToPixel(vertices[triangles[t + 1]]), c = ToPixel(vertices[triangles[t + 2]]);
                int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
                int maxX = Mathf.Min(s - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
                int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
                int maxY = Mathf.Min(s - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
                float area = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
                if (Mathf.Abs(area) < 1e-6f)
                    continue;
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = ((b.x - p.x) * (c.y - p.y) - (c.x - p.x) * (b.y - p.y)) / area;
                    float w1 = ((c.x - p.x) * (a.y - p.y) - (a.x - p.x) * (c.y - p.y)) / area;
                    float w2 = 1f - w0 - w1;
                    if (w0 >= 0f && w1 >= 0f && w2 >= 0f)
                        coverage[y * s + x] = 1f;
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            var pixels = new Color[size * size];
            var random = new System.Random(broken ? 7 : 3);
            var cracks = new float[size * size];
            if (broken)
            {
                for (int k = 0; k < 5; k++)
                {
                    float x = size * (0.3f + 0.4f * (float)random.NextDouble()), y = size * (0.25f + 0.5f * (float)random.NextDouble());
                    float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                    for (int step = 0; step < 40; step++)
                    {
                        float nx = x + Mathf.Cos(angle) * 8f, ny = y + Mathf.Sin(angle) * 8f;
                        Line(cracks, size, size, x, y, nx, ny, Mathf.Max(2f, 5f - step * 0.07f));
                        x = nx; y = ny;
                        angle += ((float)random.NextDouble() - 0.5f) * 0.9f;
                        if (x < 0 || y < 0 || x >= size || y >= size)
                            break;
                    }
                }
            }

            // Hard-edged like a seal impression (used with alpha cutout, so no grey ramps): a few small paste gaps
            // inside, crisp outline; the broken one adds chipped bites and cracks.
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float sum = 0f;
                for (int j = 0; j < Supersample; j++)
                for (int i = 0; i < Supersample; i++)
                    sum += coverage[(y * Supersample + j) * s + x * Supersample + i];
                float shape = sum / (Supersample * Supersample);
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float gaps = Fbm(u, v, 16, 3, broken ? 141 : 131);
                bool inked = shape > 0.5f && gaps > 0.24f;
                if (broken)
                    inked &= Fbm(u, v, 3, 3, 151) > 0.36f && cracks[y * size + x] < 0.5f;
                float value = inked ? 1f : 0f;
                pixels[y * size + x] = new Color(value, value, value, value);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
