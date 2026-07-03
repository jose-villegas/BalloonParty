using System.IO;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Generates the tileable noise texture PuffCloud samples for every octave: one
    ///     periodic-Perlin octave (wrapped lattice — seamless by construction, not by edge
    ///     blending), histogram-matched to the distribution of the procedural simplex the
    ///     cloud thresholds were originally tuned against, and stored as 16-bit PNG so the
    ///     smoothstepped cloud gradients can't band.
    ///     The period must match the material's <c>_NoisePeriod</c>. As a single octave inside
    ///     the shader's three-scale differential-scroll blend, Perlin reads the same as simplex.
    /// </summary>
    internal static class CloudNoiseTextureGenerator
    {
        // 16-bit PNG, not EXR: importing a linear HDR file into a Gamma-color-space project
        // bakes a linear-to-gamma lift into the texel data (stored 0.5 samples as ~0.74),
        // which shifts the noise distribution the cloud thresholds are tuned against. A
        // 16-bit PNG round-trips raw and keeps the banding-free precision.
        private const string OutputPath = "Assets/Textures/Grid/CloudNoiseTileable.png";

        // 256 over period 8 = 32 texels per noise unit. The bilinear softening of the fine
        // octave at this density reads fine in-game (raise to 512 if crisper clouds are ever
        // wanted).
        private const int Resolution = 256;
        private const int Period = 8;
        private const int Seed = 1337;

        [MenuItem("Tools/BalloonParty/Generate Cloud Noise Texture")]
        private static void Generate()
        {
            var permutation = BuildPermutation();
            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBAHalf, false, true);
            var pixels = new Color[Resolution * Resolution];

            var raw = new float[Resolution * Resolution];
            for (var y = 0; y < Resolution; y++)
            {
                for (var x = 0; x < Resolution; x++)
                {
                    raw[y * Resolution + x] = PeriodicPerlin(
                        (float)x / Resolution * Period,
                        (float)y / Resolution * Period,
                        permutation);
                }
            }

            // The cloud material's edge thresholds are tuned against the shader simplex's
            // specific value distribution (Ashima, ×130) — a raw Perlin tile is narrower and
            // shifts cloud coverage. Histogram-match the tile onto the simplex distribution so
            // the baked octave is statistically indistinguishable from the procedural one.
            var matched = HistogramMatchToSimplex(raw);

            for (var i = 0; i < matched.Length; i++)
            {
                var value = Mathf.Clamp01(matched[i] * 0.5f + 0.5f);
                pixels[i] = new Color(value, 0f, 0f, 1f);
            }

            texture.SetPixels(pixels);
            texture.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllBytes(OutputPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(OutputPath);
            ConfigureImporter();

            Debug.Log($"[CloudNoiseTextureGenerator] {Resolution}×{Resolution} tileable octave, " +
                      $"period {Period} → {OutputPath}. Assign to PuffMain's Tileable Noise slot " +
                      $"and set Baked Noise Period to {Period}.");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(OutputPath));
        }

        private static void ConfigureImporter()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(OutputPath);

            // Linear data sampled by value — sRGB conversion or compression would distort the
            // field the smoothstep thresholds were tuned against.
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static int[] BuildPermutation()
        {
            var permutation = new int[256];
            for (var i = 0; i < permutation.Length; i++)
            {
                permutation[i] = i;
            }

            // Deterministic shuffle — regenerating always yields the identical texture.
            var random = new System.Random(Seed);
            for (var i = permutation.Length - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (permutation[i], permutation[j]) = (permutation[j], permutation[i]);
            }

            return permutation;
        }

        // Classic Perlin with lattice indices wrapped at Period — tiling is exact, not blended.
        private static float PeriodicPerlin(float x, float y, int[] permutation)
        {
            var cellX = Mathf.FloorToInt(x);
            var cellY = Mathf.FloorToInt(y);
            var fracX = x - cellX;
            var fracY = y - cellY;

            var x0 = cellX % Period;
            var y0 = cellY % Period;
            var x1 = (x0 + 1) % Period;
            var y1 = (y0 + 1) % Period;

            var d00 = GradientDot(Hash(permutation, x0, y0), fracX, fracY);
            var d10 = GradientDot(Hash(permutation, x1, y0), fracX - 1f, fracY);
            var d01 = GradientDot(Hash(permutation, x0, y1), fracX, fracY - 1f);
            var d11 = GradientDot(Hash(permutation, x1, y1), fracX - 1f, fracY - 1f);

            var u = Fade(fracX);
            var v = Fade(fracY);

            var value = Mathf.Lerp(Mathf.Lerp(d00, d10, u), Mathf.Lerp(d01, d11, u), v);

            // 2D Perlin spans ±√2/2 — normalize toward [-1, 1] to match the simplex contract.
            return Mathf.Clamp(value * 1.4142f, -1f, 1f);
        }

        // Rank-maps the tile's values onto the simplex value distribution: for each texel,
        // find its quantile within the tile, then take the same quantile of a large sorted
        // sample of the shader's actual simplex. Preserves the tile's spatial structure
        // (and therefore its tileability) while matching the marginal distribution exactly.
        private static float[] HistogramMatchToSimplex(float[] raw)
        {
            const int sampleCount = 65536;
            var simplexSamples = new float[sampleCount];
            var random = new System.Random(Seed * 31);
            for (var i = 0; i < sampleCount; i++)
            {
                var x = (float)(random.NextDouble() * 512.0 - 256.0);
                var y = (float)(random.NextDouble() * 512.0 - 256.0);
                simplexSamples[i] = SimplexNoise2D(new Vector2(x, y));
            }

            System.Array.Sort(simplexSamples);

            var sortedRaw = (float[])raw.Clone();
            System.Array.Sort(sortedRaw);

            var result = new float[raw.Length];
            for (var i = 0; i < raw.Length; i++)
            {
                var rank = System.Array.BinarySearch(sortedRaw, raw[i]);
                if (rank < 0)
                {
                    rank = ~rank;
                }

                var quantile = (float)rank / (raw.Length - 1);
                var index = Mathf.Clamp(Mathf.RoundToInt(quantile * (sampleCount - 1)), 0, sampleCount - 1);
                result[i] = simplexSamples[index];
            }

            return result;
        }

        // C# port of Assets/Shaders/BalloonParty/Noise/SimplexNoise2D.cginc (Ashima/Gustavson,
        // ×130 scale) — must stay numerically in step with the shader so the histogram match
        // targets the real distribution.
        private static float SimplexNoise2D(Vector2 v)
        {
            const float cx = 0.211324865405187f;
            const float cy = 0.366025403784439f;
            const float cz = -0.577350269189626f;
            const float cw = 0.024390243902439f;

            var skew = Vector2.Dot(v, new Vector2(cy, cy));
            var i = new Vector2(Mathf.Floor(v.x + skew), Mathf.Floor(v.y + skew));
            var unskew = Vector2.Dot(i, new Vector2(cx, cx));
            var x0 = v - i + new Vector2(unskew, unskew);

            var i1 = x0.x > x0.y ? new Vector2(1f, 0f) : new Vector2(0f, 1f);
            var x1 = new Vector2(x0.x + cx - i1.x, x0.y + cx - i1.y);
            var x2 = new Vector2(x0.x + cz, x0.y + cz);

            i = new Vector2(Mod289(i.x), Mod289(i.y));
            var p0 = Permute(Permute(i.y) + i.x);
            var p1 = Permute(Permute(i.y + i1.y) + i.x + i1.x);
            var p2 = Permute(Permute(i.y + 1f) + i.x + 1f);

            var m0 = CornerFalloff(x0);
            var m1 = CornerFalloff(x1);
            var m2 = CornerFalloff(x2);

            var g0 = CornerGradient(p0, x0, ref m0);
            var g1 = CornerGradient(p1, x1, ref m1);
            var g2 = CornerGradient(p2, x2, ref m2);

            return 130f * (m0 * g0 + m1 * g1 + m2 * g2);
        }

        private static float Mod289(float x)
        {
            return x - Mathf.Floor(x * (1f / 289f)) * 289f;
        }

        private static float Permute(float x)
        {
            return Mod289((x * 34f + 1f) * x);
        }

        private static float CornerFalloff(Vector2 x)
        {
            var m = Mathf.Max(0.5f - Vector2.Dot(x, x), 0f);
            m *= m;
            return m * m;
        }

        private static float CornerGradient(float p, Vector2 offset, ref float falloff)
        {
            var gx = 2f * Frac(p * 0.024390243902439f) - 1f;
            var h = Mathf.Abs(gx) - 0.5f;
            var ox = Mathf.Floor(gx + 0.5f);
            var a0 = gx - ox;

            falloff *= 1.79284291400159f - 0.85373472095314f * (a0 * a0 + h * h);
            return a0 * offset.x + h * offset.y;
        }

        private static float Frac(float x)
        {
            return x - Mathf.Floor(x);
        }

        private static int Hash(int[] permutation, int x, int y)
        {
            return permutation[(permutation[x & 255] + y) & 255];
        }

        private static float GradientDot(int hash, float x, float y)
        {
            // Eight unit-ish gradient directions.
            switch (hash & 7)
            {
                case 0: return x + y;
                case 1: return x - y;
                case 2: return -x + y;
                case 3: return -x - y;
                case 4: return x;
                case 5: return -x;
                case 6: return y;
                default: return -y;
            }
        }

        private static float Fade(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }
    }
}
