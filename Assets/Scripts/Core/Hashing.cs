using System;
using UnityEngine;

namespace DivergentGenesis.Core
{
    /// <summary>
    /// Deterministic, allocation-free hash + PRNG helpers.
    /// Everything the world generator does MUST be reproducible from
    /// (worldSeed, x, y, z) so a 140 km world can be explored forever without
    /// storing a single byte of terrain.
    /// </summary>
    public static class Hash
    {
        [MethodImplFast]
        public static uint Hash3(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)seed;
                h ^= 2166136261u;                 // FNV offset basis
                h *= 16777619u; h ^= (uint)x;
                h *= 16777619u; h ^= (uint)y;
                h *= 16777619u; h ^= (uint)z;
                // final avalanche (murmur3 fmix32)
                h ^= h >> 16; h *= 0x85ebca6bu;
                h ^= h >> 13; h *= 0xc2b2ae35u;
                h ^= h >> 16;
                return h;
            }
        }

        [MethodImplFast]
        public static uint Hash2(int x, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)seed;
                h ^= 2166136261u;
                h *= 16777619u; h ^= (uint)x;
                h *= 16777619u; h ^= (uint)z;
                h ^= h >> 16; h *= 0x85ebca6bu;
                h ^= h >> 13; h *= 0xc2b2ae35u;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>Uniform 0..1 float from a coordinate.</summary>
        [MethodImplFast]
        public static float Float01(int x, int z, int seed)
        {
            return (Hash2(x, z, seed) & 0x00FFFFFFu) * (1.0f / 16777216.0f);
        }

        /// <summary>Deterministic 0..1 value for a block position (ores, caves, features).</summary>
        [MethodImplFast]
        public static float Float01(int x, int y, int z, int seed)
        {
            return (Hash3(x, y, z, seed) & 0x00FFFFFFu) * (1.0f / 16777216.0f);
        }

        /// <summary>A stable 32-bit seed for a chunk, so each chunk decorates itself.</summary>
        [MethodImplFast]
        public static int SeedForChunk(int chunkX, int chunkZ, int worldSeed)
        {
            return unchecked((int)Hash2(chunkX, chunkZ, worldSeed ^ 0x5bf03635));
        }
    }

    /// <summary>Marker attribute documenting the hot path. Purely descriptive.</summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property)]
    public sealed class MethodImplFastAttribute : System.Attribute { }

    /// <summary>Small, fast, seedable PRNG (xorshift128). No allocations, no UnityEngine deps.</summary>
    public struct Rng
    {
        private uint x, y, z, w;

        public Rng(int seed)
        {
            unchecked
            {
                uint s = (uint)seed;
                if (s == 0) s = 0x9E3779B9u;
                x = s; y = s * 1812433253u + 1u;
                z = y * 1812433253u + 1u;
                w = z * 1812433253u + 1u;
                for (int i = 0; i < 8; i++) NextUInt();
            }
        }

        public uint NextUInt()
        {
            unchecked
            {
                uint t = x ^ (x << 11);
                x = y; y = z; z = w;
                w = w ^ (w >> 19) ^ t ^ (t >> 8);
                return w;
            }
        }

        /// <summary>Uniform float in [0,1).</summary>
        public float NextFloat()
        {
            return (NextUInt() & 0x00FFFFFFu) * (1.0f / 16777216.0f);
        }

        /// <summary>Uniform float in [min,max).</summary>
        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        /// <summary>Uniform int in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        }

        public bool Chance(float probability)
        {
            return NextFloat() < probability;
        }

        public T Pick<T>(T[] items)
        {
            return items[Range(0, items.Length)];
        }
    }

    public static class DGMath
    {
        [MethodImplFast]
        public static int FastFloor(float v)
        {
            int i = (int)v;
            return v < i ? i - 1 : i;
        }

        [MethodImplFast]
        public static float SmoothStep(float t)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return t * t * (3f - 2f * t);
        }

        [MethodImplFast]
        public static float SmoothStep(float edge0, float edge1, float v)
        {
            if (edge1 <= edge0) return v < edge0 ? 0f : 1f;
            return SmoothStep((v - edge0) / (edge1 - edge0));
        }

        [MethodImplFast]
        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        [MethodImplFast]
        public static float InverseLerp(float a, float b, float v)
        {
            if (Math.Abs(b - a) < 1e-6f) return 0f;
            return (v - a) / (b - a);
        }

        /// <summary>Remaps v from [a0,a1] into [b0,b1] with clamping.</summary>
        public static float Remap(float v, float a0, float a1, float b0, float b1)
        {
            return Lerp(b0, b1, InverseLerp(a0, a1, v));
        }

        public static float Remap01(float v, float a0, float a1)
        {
            return Mathf.Clamp01(InverseLerp(a0, a1, v));
        }

        /// <summary>Frame-rate independent exponential smoothing.</summary>
        public static float Damp(float current, float target, float smoothing, float dt)
        {
            if (dt <= 0f) return current;
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-smoothing * dt));
        }
    }
}
