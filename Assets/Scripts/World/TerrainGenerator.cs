using UnityEngine;
using DivergentGenesis.Core;

namespace DivergentGenesis.World
{
    public struct ColumnSample
    {
        public float Height;        // terrain surface, metres
        public float River;         // 0..1 river strength
        public float Temperature;   // 0..1
        public float Humidity;      // 0..1
        public float Continent;     // -1..1 landmass field
        public float Erosion;       // 0..1 flatness
        public BiomeType Biome;
    }

    /// <summary>
    /// Pure, thread-safe world generator.
    ///
    /// 140 km x 140 km = 19.6 BILLION blocks. We never store that. Every column is
    /// a deterministic function of (seed, x, z) and every block is a function of
    /// (seed, x, y, z), so any point on the planet can be materialised on demand
    /// and the RAM cost is whatever the player is currently looking at.
    /// </summary>
    public sealed class TerrainGenerator
    {
        public const int SeedContinents = 1013;
        public const int SeedHills = 2027;
        public const int SeedDetail = 3041;
        public const int SeedRidges = 4057;
        public const int SeedErosion = 5077;
        public const int SeedRivers = 6089;
        public const int SeedLakes = 7103;
        public const int SeedTemp = 8117;
        public const int SeedHumid = 9133;
        public const int SeedWarp = 10151;
        public const int SeedCaves = 11267;
        public const int SeedCaverns = 12373;
        public const int SeedOres = 13487;

        // the Node dimension gets its own noise so its landscape never rhymes
        // with the overworld it was grown from
        public const int SeedNodeRoll = 14591;
        public const int SeedNodeDunes = 15733;
        public const int SeedNodeMounds = 16831;
        public const int SeedNodeBasin = 17939;
        public const int SeedNodeGrove = 19051;

        public readonly int Seed;

        public TerrainGenerator(int seed) { Seed = seed; }

        // ------------------------------------------------------------ heights
        /// <summary>
        /// lod 0 = full detail (right under the player)
        /// lod 1 = mid range, lod 2 = far range. Each step drops the most
        /// expensive high frequency octaves, which is where nearly all the time goes.
        /// </summary>
        public float HeightAt(float wx, float wz, int lod = 0)
        {
            if (DimensionState.NodeActive) return NodeHeight(wx, wz, lod);

            int cOct = lod == 0 ? 5 : (lod == 1 ? 4 : 3);
            int hOct = lod == 0 ? 4 : (lod == 1 ? 3 : 2);
            int dOct = lod == 0 ? 3 : (lod == 1 ? 2 : 1);
            int rOct = lod == 0 ? 4 : (lod == 1 ? 3 : 2);

            float continent = DGNoise.Fbm2(wx * 0.000045f, wz * 0.000045f, Seed + SeedContinents, cOct);
            float hills = DGNoise.Fbm2(wx * 0.00085f, wz * 0.00085f, Seed + SeedHills, hOct);
            float detail = DGNoise.Fbm2(wx * 0.0055f, wz * 0.0055f, Seed + SeedDetail, dOct);
            float ridge = DGNoise.Ridged2(wx * 0.00055f, wz * 0.00055f, Seed + SeedRidges, rOct);
            float erosion = DGNoise.Fbm2(wx * 0.00035f, wz * 0.00035f, Seed + SeedErosion, lod == 0 ? 3 : 2);

            float erosion01 = erosion * 0.5f + 0.5f;
            float mountainMask = DGMath.SmoothStep(0.16f, 0.62f, continent);

            float h = WorldConfig.SeaLevel
                    + continent * 40f
                    + hills * 13f * (1f - mountainMask * 0.55f)
                    + ridge * mountainMask * (48f + 62f * erosion01)
                    + detail * 3.2f;

            // Soft-compress the peaks so the highest summits sit near y=150
            if (h > 124f) h = 124f + (h - 124f) * 0.55f;

            h = ApplyRiversAndLakes(wx, wz, h, continent, erosion01, out _);
            return Mathf.Clamp(h, 1.5f, WorldConfig.MaxTerrainHeight);
        }

        private float ApplyRiversAndLakes(float wx, float wz, float h, float continent,
                                          float erosion01, out float riverStrength)
        {
            riverStrength = 0f;

            // --- meandering river channels -----------------------------------
            float rx = wx, rz = wz;
            DGNoise.Warp2(ref rx, ref rz, 26f, 0.0016f, Seed + SeedWarp);

            float riverNoise = DGNoise.Fbm2(rx * 0.00042f, rz * 0.00042f, Seed + SeedRivers, 3, 2.1f, 0.55f);
            float channel = 1f - Mathf.Abs(riverNoise);                 // 0..1, 1 on the centreline
            float mask = DGMath.SmoothStep(0.965f, 0.999f, channel);

            // Rivers only exist on land, and hug flatter ground more.
            float landMask = DGMath.SmoothStep(-0.02f, 0.14f, continent) * DGMath.Lerp(0.45f, 1f, erosion01);
            mask *= landMask;

            if (mask > 0.001f)
            {
                float bed = Mathf.Min(h, WorldConfig.SeaLevel + 3.2f);
                h = Mathf.Lerp(h, bed, DGMath.SmoothStep(0f, 1f, mask));
                riverStrength = mask;
            }

            // --- inland lakes / ponds ----------------------------------------
            if (h < WorldConfig.SeaLevel + 16f && h > WorldConfig.SeaLevel - 6f)
            {
                float lakeN = DGNoise.Fbm2(wx * 0.00033f, wz * 0.00033f, Seed + SeedLakes, 2);
                float lake = DGMath.SmoothStep(0.50f, 0.66f, lakeN) * DGMath.SmoothStep(0.02f, 0.12f, continent);
                if (lake > 0.001f)
                {
                    float bed = WorldConfig.SeaLevel - 3.5f;
                    h = Mathf.Lerp(h, bed, lake);
                    riverStrength = Mathf.Max(riverStrength, lake * 0.8f);
                }
            }

            return h;
        }

        // -------------------------------------------------------------- the Node
        /// <summary>
        /// The Node dimension's surface: soft rolling mounds, shallow cream seas
        /// and no rivers at all. Same noise engine, different weights - so it is
        /// exactly as deterministic and exactly as cheap as the overworld.
        /// </summary>
        private float NodeHeight(float wx, float wz, int lod)
        {
            int oct = lod == 0 ? 4 : (lod == 1 ? 3 : 2);
            int dOct = lod == 0 ? 3 : 2;

            float roll = DGNoise.Fbm2(wx * 0.0016f, wz * 0.0016f, Seed + SeedNodeRoll, oct);
            float dunes = DGNoise.Fbm2(wx * 0.0075f, wz * 0.0075f, Seed + SeedNodeDunes, dOct);
            float mounds = DGNoise.Ridged2(wx * 0.0021f, wz * 0.0021f, Seed + SeedNodeMounds, oct);

            float h = NodeConfig.BaseHeight + roll * 11f + dunes * 2.6f + mounds * 24f;

            // wide, shallow basins of cream where the ground would otherwise roll on
            float basin = DGNoise.Fbm2(wx * 0.00042f, wz * 0.00042f, Seed + SeedNodeBasin, 2);
            float sea = DGMath.SmoothStep(0.36f, 0.02f, basin);
            if (sea > 0.001f)
            {
                float bed = NodeConfig.CreamSeaLevel - 4.5f;
                h = Mathf.Lerp(h, Mathf.Min(h, bed), sea);
            }

            return Mathf.Clamp(h, 2f, NodeConfig.MaxHeight);
        }

        private static BiomeType NodeClassify(float height, float temperature, float humidity)
        {
            if (height <= NodeConfig.CreamSeaLevel + 1.5f) return BiomeType.NodeCream;
            if (height >= NodeConfig.BaseHeight + 24f) return BiomeType.NodeCrystal;
            if (humidity > 0.55f) return BiomeType.NodeForest;
            return BiomeType.NodeMeadow;
        }

        // ------------------------------------------------------------- climate
        public void Sample(float wx, float wz, int lod, ref ColumnSample s)
        {
            float continent = DGNoise.Fbm2(wx * 0.000045f, wz * 0.000045f, Seed + SeedContinents, lod == 0 ? 5 : (lod == 1 ? 4 : 3));
            float erosion = DGNoise.Fbm2(wx * 0.00035f, wz * 0.00035f, Seed + SeedErosion, lod == 0 ? 3 : 2) * 0.5f + 0.5f;

            s.Continent = continent;
            s.Erosion = erosion;

            if (DimensionState.NodeActive)
            {
                s.Height = NodeHeight(wx, wz, lod);
                s.River = 0f;

                int nOct = lod == 0 ? 3 : 2;
                float nTemp = DGNoise.Fbm2(wx * 0.00006f, wz * 0.00006f, Seed + SeedNodeGrove, nOct) * 0.5f + 0.5f;
                float nHumid = DGNoise.Fbm2(wx * 0.00009f, wz * 0.00009f, Seed + SeedNodeBasin + 7, nOct) * 0.5f + 0.5f;
                s.Temperature = Mathf.Clamp01(nTemp);
                s.Humidity = Mathf.Clamp01(nHumid);
                s.Biome = NodeClassify(s.Height, s.Temperature, s.Humidity);
                return;
            }

            // one pass: height + river strength together, so the climate fields
            // and the terrain never disagree about where the water is
            float river;
            float h = HeightInternal(wx, wz, lod, continent, erosion, out river);
            s.River = river;
            s.Height = h;

            int tOct = lod == 0 ? 3 : 2;
            float temp = DGNoise.Fbm2(wx * 0.00003f, wz * 0.00003f, Seed + SeedTemp, tOct) * 0.5f + 0.5f;
            temp -= Mathf.Max(0f, s.Height - WorldConfig.SeaLevel) / 240f;     // altitude lapse rate
            s.Temperature = Mathf.Clamp01(temp);

            float humid = DGNoise.Fbm2(wx * 0.00004f, wz * 0.00004f, Seed + SeedHumid, tOct) * 0.5f + 0.5f;
            humid += (1f - DGMath.Remap01(continent, -0.1f, 0.35f)) * 0.22f;   // oceans are humid
            humid += river * 0.18f;
            s.Humidity = Mathf.Clamp01(humid);

            s.Biome = Classify(ref s);
        }

        private float HeightInternal(float wx, float wz, int lod, float continent, float erosion01, out float riverStrength)
        {
            if (DimensionState.NodeActive)
            {
                riverStrength = 0f;
                return NodeHeight(wx, wz, lod);
            }

            int cOct = lod == 0 ? 5 : (lod == 1 ? 4 : 3);
            int hOct = lod == 0 ? 4 : (lod == 1 ? 3 : 2);
            int dOct = lod == 0 ? 3 : (lod == 1 ? 2 : 1);
            int rOct = lod == 0 ? 4 : (lod == 1 ? 3 : 2);

            float hills = DGNoise.Fbm2(wx * 0.00085f, wz * 0.00085f, Seed + SeedHills, hOct);
            float detail = DGNoise.Fbm2(wx * 0.0055f, wz * 0.0055f, Seed + SeedDetail, dOct);
            float ridge = DGNoise.Ridged2(wx * 0.00055f, wz * 0.00055f, Seed + SeedRidges, rOct);

            float mountainMask = DGMath.SmoothStep(0.16f, 0.62f, continent);

            float h = WorldConfig.SeaLevel
                    + continent * 40f
                    + hills * 13f * (1f - mountainMask * 0.55f)
                    + ridge * mountainMask * (48f + 62f * erosion01)
                    + detail * 3.2f;

            if (h > 124f) h = 124f + (h - 124f) * 0.55f;

            h = ApplyRiversAndLakes(wx, wz, h, continent, erosion01, out riverStrength);
            return Mathf.Clamp(h, 1.5f, WorldConfig.MaxTerrainHeight);
        }

        public void Sample(float wx, float wz, ref ColumnSample s)
        {
            Sample(wx, wz, 0, ref s);
        }

        public BiomeType Classify(ref ColumnSample s)
        {
            float h = s.Height;
            float t = s.Temperature;
            float w = s.Humidity;

            if (h < WorldConfig.SeaLevel - 24f) return BiomeType.DeepOcean;
            if (h < WorldConfig.SeaLevel - 0.2f) return BiomeType.Ocean;
            if (s.River > 0.22f && h < WorldConfig.SeaLevel + 4.5f) return BiomeType.River;
            if (h <= WorldConfig.BeachHeight) return BiomeType.Beach;

            // Altitude bands
            if (h > 132f) return BiomeType.SnowPeak;
            if (h > 112f) return t < 0.30f ? BiomeType.SnowForest : BiomeType.Mountain;
            if (h > 94f) return BiomeType.Rocky;
            if (h > 86f && t > 0.45f) return BiomeType.Mesa;

            // Climate bands
            if (t < 0.20f) return BiomeType.SnowForest;
            if (t < 0.32f) return w > 0.55f ? BiomeType.Taiga : BiomeType.SnowForest;
            if (t < 0.46f)
            {
                if (w > 0.72f) return BiomeType.Taiga;
                if (w > 0.40f) return BiomeType.BirchForest;
                return BiomeType.Plains;
            }
            if (t < 0.58f)
            {
                if (w > 0.74f) return BiomeType.Swamp;
                if (w > 0.52f) return BiomeType.Forest;
                if (w > 0.30f) return BiomeType.WetPlains;
                return BiomeType.Plains;
            }
            if (t < 0.74f)
            {
                if (w > 0.80f) return BiomeType.Forest;
                if (w > 0.55f) return BiomeType.Savanna;
                if (w > 0.28f) return BiomeType.Plains;
                return BiomeType.Desert;
            }
            // Hot
            if (w > 0.78f) return BiomeType.Swamp;
            if (w > 0.40f) return BiomeType.Savanna;
            return BiomeType.Desert;
        }

        // ------------------------------------------------------ gameplay query
        public int SurfaceBlockAt(int wx, int wz, out BiomeType biome, out bool isWater)
        {
            var s = new ColumnSample();
            Sample(wx, wz, ref s);
            biome = s.Biome;
            int hi = Mathf.FloorToInt(s.Height);
            isWater = hi < WorldConfig.SeaLevel;

            BiomeInfo info = BiomeSystem.Get(s.Biome);
            byte top = info.SurfaceBlock;
            if (top == Blocks.Stone && s.Biome != BiomeType.Rocky && s.Biome != BiomeType.Mountain) top = Blocks.Grass;
            if (isWater && (s.Biome == BiomeType.DeepOcean || s.Biome == BiomeType.Ocean)) top = Blocks.Gravel;
            return top;
        }
    }
}
