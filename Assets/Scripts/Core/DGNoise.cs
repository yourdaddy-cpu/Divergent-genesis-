using UnityEngine;

namespace DivergentGenesis.Core
{
    /// <summary>
    /// Self-contained Perlin / fBm / ridged / cellular noise.
    /// No UnityEngine.PerlinNoise: this must run identically on background
    /// threads, every device, every Unity version, and stay reproducible.
    /// </summary>
    public static class DGNoise
    {
        private const float Sqrt2 = 1.41421356f;

        // ------------------------------------------------------------ gradient
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void Noop() { }

        [Core.MethodImplFast]
        private static float Grad2(int ix, int iy, float dx, float dy, int seed)
        {
            uint h = Hash.Hash2(ix, iy, seed);
            switch (h & 7u)
            {
                case 0u: return (dx + dy) * Sqrt2;
                case 1u: return (-dx + dy) * Sqrt2;
                case 2u: return (dx - dy) * Sqrt2;
                case 3u: return (-dx - dy) * Sqrt2;
                case 4u: return dx * 2f;
                case 5u: return -dx * 2f;
                case 6u: return dy * 2f;
                default: return -dy * 2f;
            }
        }

        [Core.MethodImplFast]
        private static float Grad3(int ix, int iy, int iz, float dx, float dy, float dz, int seed)
        {
            uint h = Hash.Hash3(ix, iy, iz, seed);
            switch (h & 15u)
            {
                case 0u: return (dx + dy);
                case 1u: return (-dx + dy);
                case 2u: return (dx - dy);
                case 3u: return (-dx - dy);
                case 4u: return (dx + dz);
                case 5u: return (-dx + dz);
                case 6u: return (dx - dz);
                case 7u: return (-dx - dz);
                case 8u: return (dy + dz);
                case 9u: return (-dy + dz);
                case 10u: return (dy - dz);
                case 11u: return (-dy - dz);
                case 12u: return (dx + dy + dz) * 0.577f;
                case 13u: return (-dx + dy - dz) * 0.577f;
                case 14u: return (dx - dy + dz) * 0.577f;
                default: return (-dx - dy - dz) * 0.577f;
            }
        }

        // -------------------------------------------------------------- perlin
        /// <summary>2D Perlin noise, roughly -1..1.</summary>
        public static float Perlin2(float x, float y, int seed)
        {
            int x0 = DGMath.FastFloor(x);
            int y0 = DGMath.FastFloor(y);
            float fx = x - x0;
            float fy = y - y0;

            float u = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f);
            float v = fy * fy * fy * (fy * (fy * 6f - 15f) + 10f);

            float n00 = Grad2(x0, y0, fx, fy, seed);
            float n10 = Grad2(x0 + 1, y0, fx - 1f, fy, seed);
            float n01 = Grad2(x0, y0 + 1, fx, fy - 1f, seed);
            float n11 = Grad2(x0 + 1, y0 + 1, fx - 1f, fy - 1f, seed);

            float nx0 = n00 + u * (n10 - n00);
            float nx1 = n01 + u * (n11 - n01);
            return nx0 + v * (nx1 - nx0);
        }

        /// <summary>3D Perlin noise, roughly -1..1.</summary>
        public static float Perlin3(float x, float y, float z, int seed)
        {
            int x0 = DGMath.FastFloor(x);
            int y0 = DGMath.FastFloor(y);
            int z0 = DGMath.FastFloor(z);
            float fx = x - x0, fy = y - y0, fz = z - z0;

            float u = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f);
            float v = fy * fy * fy * (fy * (fy * 6f - 15f) + 10f);
            float w = fz * fz * fz * (fz * (fz * 6f - 15f) + 10f);

            float n000 = Grad3(x0, y0, z0, fx, fy, fz, seed);
            float n100 = Grad3(x0 + 1, y0, z0, fx - 1f, fy, fz, seed);
            float n010 = Grad3(x0, y0 + 1, z0, fx, fy - 1f, fz, seed);
            float n110 = Grad3(x0 + 1, y0 + 1, z0, fx - 1f, fy - 1f, fz, seed);
            float n001 = Grad3(x0, y0, z0 + 1, fx, fy, fz - 1f, seed);
            float n101 = Grad3(x0 + 1, y0, z0 + 1, fx - 1f, fy, fz - 1f, seed);
            float n011 = Grad3(x0, y0 + 1, z0 + 1, fx, fy - 1f, fz - 1f, seed);
            float n111 = Grad3(x0 + 1, y0 + 1, z0 + 1, fx - 1f, fy - 1f, fz - 1f, seed);

            float x00 = n000 + u * (n100 - n000);
            float x10 = n010 + u * (n110 - n010);
            float x01 = n001 + u * (n101 - n001);
            float x11 = n011 + u * (n111 - n011);
            float y0v = x00 + v * (x10 - x00);
            float y1v = x01 + v * (x11 - x01);
            return y0v + w * (y1v - y0v);
        }

        // ----------------------------------------------------------------- fBm
        public static float Fbm2(float x, float y, int seed, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Perlin2(x * freq, y * freq, seed + i * 7919);
                norm += amp;
                amp *= gain;
                freq *= lacunarity;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        public static float Fbm3(float x, float y, float z, int seed, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Perlin3(x * freq, y * freq, z * freq, seed + i * 6151);
                norm += amp;
                amp *= gain;
                freq *= lacunarity;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>Ridged multifractal, 0..1. Gives mountain spines.</summary>
        public static float Ridged2(float x, float y, int seed, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, freq = 1f, norm = 0f, prev = 1f;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Mathf.Abs(Perlin2(x * freq, y * freq, seed + i * 4909));
                n *= n;
                sum += n * amp * prev;
                prev = n;
                norm += amp;
                amp *= gain;
                freq *= lacunarity;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>Billowy noise, 0..1. Good for clouds and canopy clumps.</summary>
        public static float Billow2(float x, float y, int seed, int octaves)
        {
            float sum = 0f, amp = 1f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Mathf.Abs(Perlin2(x * freq, y * freq, seed + i * 3571));
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>Pushes sample positions around with another noise field. Kills grid artifacts.</summary>
        public static void Warp2(ref float x, ref float y, float strength, float frequency, int seed)
        {
            float wx = Perlin2(x * frequency, y * frequency, seed);
            float wy = Perlin2(x * frequency + 31.7f, y * frequency - 17.3f, seed + 991);
            x += wx * strength;
            y += wy * strength;
        }

        // ------------------------------------------------------------- cellular
        /// <summary>
        /// 3D Worley/cellular noise. Returns distance to nearest feature point (0..~1.4).
        /// Used for ore veins and cave tubes.
        /// </summary>
        public static float Worley3(float x, float y, float z, int seed)
        {
            int xi = DGMath.FastFloor(x);
            int yi = DGMath.FastFloor(y);
            int zi = DGMath.FastFloor(z);
            float best = 8f;

            for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int cx = xi + dx, cy = yi + dy, cz = zi + dz;
                float hx = Hash.Hash3(cx, cy, cz, seed);
                float hy = Hash.Hash3(cx, cy, cz, seed + 77);
                float hz = Hash.Hash3(cx, cy, cz, seed + 991);
                float px = cx + hx;
                float py = cy + hy;
                float pz = cz + hz;
                float ddx = px - x, ddy = py - y, ddz = pz - z;
                float d = ddx * ddx + ddy * ddy + ddz * ddz;
                if (d < best) best = d;
            }
            return Mathf.Sqrt(best);
        }

        /// <summary>
        /// 2D Worley. Returns x = distance to nearest point, y = distance to 2nd nearest.
        /// Cell edge distance (x - y) is perfect for rivers and coastlines.
        /// </summary>
        public static void Worley2F2(float x, float y, int seed, out float f1, out float f2)
        {
            int xi = DGMath.FastFloor(x);
            int yi = DGMath.FastFloor(y);
            f1 = 8f; f2 = 8f;

            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int cx = xi + dx, cy = yi + dy;
                float px = cx + Hash.Float01(cx, cy, seed);
                float py = cy + Hash.Float01(cx, cy, seed + 5501);
                float ddx = px - x, ddy = py - y;
                float d = Mathf.Sqrt(ddx * ddx + ddy * ddy);
                if (d < f1) { f2 = f1; f1 = d; }
                else if (d < f2) { f2 = d; }
            }
        }
    }
}
