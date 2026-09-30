using UnityEngine;
using DivergentGenesis.Core;

namespace DivergentGenesis.World
{
    /// <summary>
    /// A square tile of world at one LOD level.
    /// Level 0 tiles are 32 m and carry real voxels (caves, mining, block edits).
    /// Higher levels are 128 m .. 1024 m and are pure height fields, which is what
    /// makes a 2 km view distance cost ~200 GameObjects instead of ~4,000.
    /// </summary>
    public sealed class ChunkData
    {
        public int Cx;              // tile coordinate at this level
        public int Cz;
        public int Level;
        public int EdgeMeters;     // world size covered by this tile
        public int Samples;        // height field resolution (33 or 65)

        public ushort[] Heights;
        public byte[] Biomes;

        public byte[] Blocks;      // level 0 voxel tiles only
        public int MaxY;
        public int MinY = 160;
        public bool IsVoxel;

        public int WorldX { get { return Cx * EdgeMeters; } }
        public int WorldZ { get { return Cz * EdgeMeters; } }
        public int SampleCount { get { return Samples * Samples; } }
        public int Step { get { return EdgeMeters / (Samples - 1); } }

        [Core.MethodImplFast]
        public int HeightAtLocal(int lx, int lz)
        {
            int i = lz * Samples + lx;
            if ((uint)i >= (uint)Heights.Length) return 0;
            return Heights[i];
        }

        [Core.MethodImplFast]
        public BiomeType BiomeAtLocal(int lx, int lz)
        {
            int i = lz * Samples + lx;
            if ((uint)i >= (uint)Biomes.Length) return BiomeType.Plains;
            return (BiomeType)Biomes[i];
        }

        [Core.MethodImplFast]
        public byte GetBlockLocal(int x, int y, int z)
        {
            if (Blocks == null) return World.Blocks.Air;
            if ((uint)y >= (uint)WorldConfig.ChunkHeight) return World.Blocks.Air;
            int size = WorldConfig.ChunkSize;
            int i = x + z * size + y * size * size;
            if ((uint)i >= (uint)Blocks.Length) return World.Blocks.Air;
            return Blocks[i];
        }
    }

    /// <summary>Pure computation - safe to run on a worker thread.</summary>
    public static class ChunkGenerator
    {
        public const int CaveStep = 4;
        public const int OreStep = 4;

        private static int SamplesFor(int level)
        {
            return level == 0 ? 33 : 65;
        }

        public static int EdgeFor(int level)
        {
            switch (level)
            {
                case 0: return 32;
                case 1: return 128;
                case 2: return 256;
                case 3: return 512;
                default: return 1024;
            }
        }

        public static ChunkData Generate(int cx, int cz, int level, int seed, bool buildVoxels)
        {
            var gen = new TerrainGenerator(seed);
            int edge = EdgeFor(level);
            int samples = SamplesFor(level);
            int lod = level < 2 ? 0 : (level == 2 ? 1 : 2);

            var data = new ChunkData
            {
                Cx = cx, Cz = cz, Level = level, EdgeMeters = edge, Samples = samples,
                Heights = new ushort[samples * samples],
                Biomes = new byte[samples * samples]
            };

            var s = new ColumnSample();
            int minY = WorldConfig.ChunkHeight;

            for (int lz = 0; lz < samples; lz++)
            {
                int wz = data.WorldZ + lz * data.Step;
                for (int lx = 0; lx < samples; lx++)
                {
                    int wx = data.WorldX + lx * data.Step;
                    gen.Sample(wx, wz, lod, ref s);

                    int h = Mathf.Clamp(Mathf.FloorToInt(s.Height), 0, WorldConfig.ChunkHeight - 1);
                    int i = lz * samples + lx;
                    data.Heights[i] = (ushort)h;
                    data.Biomes[i] = (byte)s.Biome;
                    if (h < minY) minY = h;
                }
            }

            data.MaxY = Mathf.Min(WorldConfig.ChunkHeight - 1, minY + 90);
            data.MinY = Mathf.Max(0, minY - 90);

            if (buildVoxels && level == 0)
                BuildVoxels(data, seed);

            return data;
        }

        // ------------------------------------------------------------- voxels
        private static void BuildVoxels(ChunkData data, int seed)
        {
            int size = WorldConfig.ChunkSize;
            int slice = size * size;
            var blocks = new byte[size * size * WorldConfig.ChunkHeight];
            data.Blocks = blocks;
            data.IsVoxel = true;

            int gx = size / CaveStep + 1;
            int gy = WorldConfig.ChunkHeight / CaveStep + 1;
            var tunnel = new float[gx * gy * gx];
            var cavern = new float[gx * gy * gx];
            int gMaxY = Mathf.Min(gy, (data.MaxY / CaveStep) + 2);

            for (int gz = 0; gz < gx; gz++)
            for (int gyi = 0; gyi < gMaxY; gyi++)
            for (int gxi = 0; gxi < gx; gxi++)
            {
                float fx = (data.WorldX + gxi * CaveStep) * 0.021f;
                float fy = gyi * CaveStep * 0.030f;
                float fz = (data.WorldZ + gz * CaveStep) * 0.021f;
                int idx = gxi + gx * (gyi + gy * gz);
                tunnel[idx] = DGNoise.Fbm3(fx, fy, fz, seed + TerrainGenerator.SeedCaves, 3);
                cavern[idx] = DGNoise.Billow2(fx * 0.45f, fz * 0.45f, seed + TerrainGenerator.SeedCaverns, 2)
                              * (1f - Mathf.Clamp01(fy / 70f));
            }

            int ocx = size / OreStep;
            int ocy = WorldConfig.ChunkHeight / OreStep;
            var oreCells = new byte[ocx * ocy * ocx];

            for (int oz = 0; oz < ocx; oz++)
            for (int oy = 0; oy < ocy; oy++)
            for (int ox = 0; ox < ocx; ox++)
            {
                int wx = data.WorldX + ox * OreStep;
                int wz = data.WorldZ + oz * OreStep;
                float r = Hash.Float01(wx / OreStep, oy, wz / OreStep, seed + TerrainGenerator.SeedOres);
                oreCells[ox + ocx * (oy + ocy * oz)] = PickOre(r, oy * OreStep);
            }

            for (int z = 0; z < size; z++)
            {
                int wz = data.WorldZ + z;
                int gz = z / CaveStep;
                for (int x = 0; x < size; x++)
                {
                    int wx = data.WorldX + x;
                    int si = z * WorldConfig.ColumnSamples + x;

                    int h = data.Heights[si];
                    BiomeType biome = (BiomeType)data.Biomes[si];
                    BiomeInfo info = BiomeSystem.Get(biome);

                    byte top = info.SurfaceBlock;
                    if (top == World.Blocks.Stone && biome != BiomeType.Rocky && biome != BiomeType.Mountain &&
                        biome != BiomeType.Mesa) top = World.Blocks.Grass;

                    byte sub = World.Blocks.Dirt;
                    if (top == World.Blocks.Sand) sub = World.Blocks.Sand;
                    else if (biome == BiomeType.Desert) sub = World.Blocks.Sandstone;
                    else if (biome == BiomeType.Mesa) sub = World.Blocks.Terracotta;
                    else if (top == World.Blocks.Stone) sub = World.Blocks.Stone;

                    bool underWater = h < WorldConfig.SeaLevel;
                    if (underWater && (biome == BiomeType.DeepOcean || biome == BiomeType.Ocean))
                        sub = World.Blocks.Gravel;

                    int bedrockDepth = (int)(Hash.Float01(wx, wz, seed + 4242) * 3f);
                    int gxi = x / CaveStep;
                    int gxo = x / OreStep;
                    int gzo = z / OreStep;
                    int colBase = x + z * size;

                    for (int y = 0; y <= h; y++)
                    {
                        byte b;
                        if (y <= bedrockDepth) b = World.Blocks.Bedrock;
                        else if (y == h) b = top;
                        else if (y >= h - 3) b = sub;
                        else b = World.Blocks.Stone;

                        if (b == World.Blocks.Stone && y > 2)
                        {
                            int oy = y / OreStep;
                            byte ore = oreCells[gxo + ocx * (oy + ocy * gzo)];
                            if (ore != 0 && Hash.Float01(wx, y, wz, seed + 6161) < 0.62f) b = ore;
                        }

                        if (b != World.Blocks.Bedrock && y < h - 2 && y > 1)
                        {
                            int gi = gxi + gx * (y / CaveStep + gy * gz);
                            float jit = (Hash.Float01(wx, y, wz, seed + 9091) - 0.5f) * 0.22f;
                            bool carve = (tunnel[gi] + jit) < -0.58f ||
                                         ((cavern[gi] + jit) > 0.80f && y < 52 && !underWater);
                            if (carve) b = World.Blocks.Air;
                        }

                        if (b != World.Blocks.Air) blocks[colBase + y * slice] = b;
                    }

                    if (underWater)
                    {
                        for (int y = h + 1; y <= WorldConfig.SeaLevel; y++)
                            blocks[colBase + y * slice] = World.Blocks.Water;
                    }
                }
            }

            ApplyEdits(data);
        }

        private static void ApplyEdits(ChunkData data)
        {
            var blocks = data.Blocks;
            if (blocks == null) return;

            int size = WorldConfig.ChunkSize;
            int slice = size * size;
            int x0 = data.WorldX, z0 = data.WorldZ;
            var edits = BlockEdits.Serialize();

            for (int i = 0; i < edits.Count; i++)
            {
                BlockEdits.Entry e = edits[i];
                if (e.x < x0 || e.x >= x0 + size || e.z < z0 || e.z >= z0 + size) continue;
                if ((uint)e.y >= (uint)WorldConfig.ChunkHeight) continue;
                int idx = (e.x - x0) + (e.z - z0) * size + e.y * slice;
                blocks[idx] = e.removed != 0 ? World.Blocks.Air : e.block;
            }
        }

        private static byte PickOre(float r, int y)
        {
            if (y < 18 && r < 0.009f) return World.Blocks.DiamondOre;
            if (y < 58 && r < 0.040f) return World.Blocks.CopperOre;
            if (y < 46 && r < 0.033f) return World.Blocks.AluminiumOre;
            if (y < 82 && r < 0.044f) return World.Blocks.IronOre;
            if (y < 120 && r < 0.058f) return World.Blocks.CoalOre;
            return World.Blocks.Air;
        }

        public static float QueryHeight(TerrainGenerator gen, int wx, int wz)
        {
            return gen.HeightAt(wx + 0.5f, wz + 0.5f);
        }
    }
}
