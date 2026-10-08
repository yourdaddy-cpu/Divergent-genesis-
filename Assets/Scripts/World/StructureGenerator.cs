using UnityEngine;
using DivergentGenesis.Core;

namespace DivergentGenesis.World
{
    public enum StructureKind : byte
    {
        None = 0,
        Village = 1,
        BanditCamp = 2,
        Ruins = 3,
        RitualSite = 4,
        NodeShrine = 5,
        GumdropGrove = 6,
        NodeVillage = 7,
        NodeHollow = 8
    }

    public struct StructurePlan
    {
        public StructureKind Kind;
        public Vector3 Centre;
        public float Radius;
        public int CellX, CellZ;
        public int Roll;
        public bool Valid { get { return Kind != StructureKind.None; } }
    }

    /// <summary>
    /// Deterministic world furniture: villages, camps, ruins and the ritual sites.
    ///
    /// Structures are stamped during chunk generation exactly like ores are, which
    /// means the whole thing is a pure function of (seed, x, z): no runtime state,
    /// no save bloat, and a village you walk back to ten hours later is bit-for-bit
    /// the village you left. Plans are also queryable at runtime so the entity
    /// manager can put villagers in the right houses.
    /// </summary>
    public static class StructureGenerator
    {
        public const int CellSize = 320;
        public const int CellMargin = 96;
        /// <summary>Anything further than this from a chunk cannot touch it.</summary>
        private const float StampReach = 96f;

        // ================================================================= plans
        private static readonly System.Collections.Generic.Dictionary<long, StructurePlan> PlanCache =
            new System.Collections.Generic.Dictionary<long, StructurePlan>(256);

        /// <summary>
        /// True when this spot belongs to a structure. Used by the prop system so
        /// it never plants a forest through somebody's living room.
        /// </summary>
        public static bool InsideStructure(float x, float z, int seed, TerrainGenerator gen, bool node)
        {
            if (gen == null) return false;
            int cx = Mathf.FloorToInt(x / CellSize);
            int cz = Mathf.FloorToInt(z / CellSize);

            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                var plan = PlanCached(cx + dx, cz + dz, seed, gen, node);
                if (!plan.Valid) continue;
                float ddx = plan.Centre.x - x, ddz = plan.Centre.z - z;
                if (ddx * ddx + ddz * ddz <= (plan.Radius + 3f) * (plan.Radius + 3f)) return true;
            }
            return false;
        }

        /// <summary>Memoised plan lookup - the prop system asks for the same cell a lot.</summary>
        public static StructurePlan PlanCached(int cx, int cz, int seed, TerrainGenerator gen, bool node)
        {
            long key = (((long)(cx + 1048576)) << 22) | (uint)(cz + 1048576);
            lock (PlanCache)
            {
                StructurePlan cached;
                if (PlanCache.TryGetValue(key, out cached)) return cached;
                var plan = Plan(cx, cz, seed, gen, node);
                if (PlanCache.Count > 4096) PlanCache.Clear();
                PlanCache[key] = plan;
                return plan;
            }
        }

        /// <summary>Every structure cell near a point, for runtime NPC spawning.</summary>
        public static void CollectNearby(Vector3 centre, float radius, int seed, TerrainGenerator gen,
                                         bool node, System.Collections.Generic.List<StructurePlan> into)
        {
            into.Clear();
            if (gen == null) return;
            int c0x = Mathf.FloorToInt((centre.x - radius) / CellSize);
            int c1x = Mathf.FloorToInt((centre.x + radius) / CellSize);
            int c0z = Mathf.FloorToInt((centre.z - radius) / CellSize);
            int c1z = Mathf.FloorToInt((centre.z + radius) / CellSize);

            for (int cz = c0z; cz <= c1z; cz++)
            for (int cx = c0x; cx <= c1x; cx++)
            {
                var plan = PlanCached(cx, cz, seed, gen, node);
                if (!plan.Valid) continue;
                if ((plan.Centre - centre).magnitude <= radius + plan.Radius) into.Add(plan);
            }
        }

        public static StructurePlan Plan(int cx, int cz, int seed, TerrainGenerator gen, bool node)
        {
            var plan = new StructurePlan { CellX = cx, CellZ = cz };
            plan.Kind = StructureKind.None;

            uint h = Hash.Hash2(cx, cz, seed + 8801);
            if (h % 100u >= (node ? 74u : 66u)) return plan;

            var rng = new Rng((int)(h & 0x7FFFFFFF));

            int baseX = cx * CellSize;
            int baseZ = cz * CellSize;
            int ox = CellMargin + rng.Range(0, CellSize - CellMargin * 2);
            int oz = CellMargin + rng.Range(0, CellSize - CellMargin * 2);
            float wx = baseX + ox + 0.5f;
            float wz = baseZ + oz + 0.5f;

            float height = gen.HeightAt(wx, wz);
            int hi = Mathf.FloorToInt(height);
            int seaLevel = node ? Mathf.RoundToInt(NodeConfig.CreamSeaLevel) : Mathf.RoundToInt(WorldConfig.SeaLevel);
            if (hi < seaLevel + 2) return plan;

            // reject steep sites: a village on a cliff looks like a bug
            float h1 = gen.HeightAt(wx + 12f, wz);
            float h2 = gen.HeightAt(wx - 12f, wz);
            float h3 = gen.HeightAt(wx, wz + 12f);
            float h4 = gen.HeightAt(wx, wz - 12f);
            float slope = Mathf.Max(Mathf.Max(Mathf.Abs(h1 - height), Mathf.Abs(h2 - height)),
                                    Mathf.Max(Mathf.Abs(h3 - height), Mathf.Abs(h4 - height)));
            if (slope > 6.5f) return plan;

            var sample = new ColumnSample();
            gen.Sample(wx, wz, 2, ref sample);
            BiomeType biome = sample.Biome;
            int roll = rng.Range(0, 100);

            StructureKind kind;
            if (node)
            {
                if (roll < 26) kind = StructureKind.NodeHollow;
                else if (roll < 62) kind = StructureKind.NodeVillage;
                else kind = StructureKind.GumdropGrove;
            }
            else
            {
                bool temperate = biome == BiomeType.Plains || biome == BiomeType.WetPlains ||
                                 biome == BiomeType.Forest || biome == BiomeType.BirchForest ||
                                 biome == BiomeType.Taiga || biome == BiomeType.Savanna ||
                                 biome == BiomeType.SnowForest;
                bool harsh = biome == BiomeType.Desert || biome == BiomeType.Mesa ||
                             biome == BiomeType.Rocky || biome == BiomeType.Mountain ||
                             biome == BiomeType.SnowPeak || biome == BiomeType.Ashlands;

                if (temperate && roll < 46) kind = StructureKind.Village;
                else if (temperate && roll < 56) kind = StructureKind.BanditCamp;
                else if (temperate && roll < 66) kind = StructureKind.Ruins;
                else if (roll < 76) kind = StructureKind.RitualSite;
                else if (roll < 86) kind = StructureKind.BanditCamp;
                else if (harsh) kind = StructureKind.Ruins;
                else kind = StructureKind.NodeShrine;
            }

            plan.Kind = kind;
            plan.Centre = new Vector3(wx, hi, wz);
            plan.Roll = rng.Range(0, 1 << 20);
            plan.Radius = RadiusOf(kind);
            return plan;
        }

        public static float RadiusOf(StructureKind kind)
        {
            switch (kind)
            {
                case StructureKind.Village: return 34f;
                case StructureKind.BanditCamp: return 16f;
                case StructureKind.Ruins: return 14f;
                case StructureKind.RitualSite: return 13f;
                case StructureKind.NodeShrine: return 10f;
                case StructureKind.GumdropGrove: return 20f;
                case StructureKind.NodeVillage: return 24f;
                case StructureKind.NodeHollow: return 22f;
                default: return 0f;
            }
        }

        /// <summary>Cells whose plan area could reach this chunk.</summary>
        public static void Stamp(ChunkData data, TerrainGenerator gen, int seed)
        {
            if (data.Blocks == null) return;
            bool node = DimensionState.NodeActive;

            int minCellX = Mathf.FloorToInt((data.WorldX - StampReach) / (float)CellSize);
            int maxCellX = Mathf.FloorToInt((data.WorldX + data.EdgeMeters + StampReach) / (float)CellSize);
            int minCellZ = Mathf.FloorToInt((data.WorldZ - StampReach) / (float)CellSize);
            int maxCellZ = Mathf.FloorToInt((data.WorldZ + data.EdgeMeters + StampReach) / (float)CellSize);

            if (maxCellX - minCellX > 4 || maxCellZ - minCellZ > 4) return;   // defensive

            var target = new Target(data);

            for (int cz = minCellZ; cz <= maxCellZ; cz++)
            for (int cx = minCellX; cx <= maxCellX; cx++)
            {
                var plan = Plan(cx, cz, seed, gen, node);
                if (!plan.Valid) continue;

                float dx = plan.Centre.x - Mathf.Clamp(plan.Centre.x, data.WorldX, data.WorldX + data.EdgeMeters);
                float dz = plan.Centre.z - Mathf.Clamp(plan.Centre.z, data.WorldZ, data.WorldZ + data.EdgeMeters);
                if (Mathf.Abs(dx) > plan.Radius + 2f || Mathf.Abs(dz) > plan.Radius + 2f) continue;

                Build(target, plan, gen);
            }
        }

        // ================================================================ builders
        private static void Build(Target t, StructurePlan plan, TerrainGenerator gen)
        {
            var rng = new Rng(plan.Roll ^ 0x5bf03635);
            switch (plan.Kind)
            {
                case StructureKind.Village: BuildVillage(t, plan, gen, rng, false); break;
                case StructureKind.NodeVillage: BuildVillage(t, plan, gen, rng, true); break;
                case StructureKind.BanditCamp: BuildBanditCamp(t, plan, gen, rng); break;
                case StructureKind.Ruins: BuildRuins(t, plan, gen, rng); break;
                case StructureKind.RitualSite: BuildRitualSite(t, plan, gen, false); break;
                case StructureKind.NodeShrine: BuildShrine(t, plan, gen); break;
                case StructureKind.GumdropGrove: BuildGrove(t, plan, gen, rng); break;
                case StructureKind.NodeHollow: BuildNodeHollow(t, plan, gen); break;
            }
        }

        // ---------------------------------------------------------------- village
        private static void BuildVillage(Target t, StructurePlan plan, TerrainGenerator gen, Rng rng, bool node)
        {
            int cx = Mathf.FloorToInt(plan.Centre.x);
            int cz = Mathf.FloorToInt(plan.Centre.z);
            int baseY = GroundY(gen, cx, cz);

            byte floorBlock = node ? Blocks.FrostingGrass : Blocks.Grass;
            byte subBlock = node ? Blocks.CandyDirt : Blocks.Dirt;
            byte roadBlock = node ? Blocks.SherbetStone : Blocks.Cobblestone;
            byte wallBlock = node ? Blocks.CottonBlock : Blocks.PlasterWall;
            byte beamBlock = node ? Blocks.SherbetStone : Blocks.Log;
            byte roofBlock = node ? Blocks.GumdropLeaves : Blocks.RoofTile;

            int radius = (int)plan.Radius;

            // --- clear and level the plot
            for (int z = cz - radius; z <= cz + radius; z++)
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
                if (d > radius) continue;

                int g = GroundY(gen, x, z);
                int want = baseY - 1;
                if (g > want) for (int y = want + 1; y <= g; y++) t.Set(x, y, z, Blocks.Air, true);
                if (g < want) for (int y = g + 1; y <= want; y++) t.Set(x, y, z, subBlock, true);

                // any stray tree trunks inside the plot go
                for (int y = baseY; y < baseY + 12 && y < WorldConfig.ChunkHeight; y++)
                    t.Set(x, y, z, Blocks.Air, true);

                t.Set(x, baseY - 1, z, d < radius - 1.5f ? floorBlock : subBlock, true);
            }

            // --- well at the centre
            if (!node)
            {
                for (int x = cx - 3; x <= cx + 3; x++)
                for (int z = cz - 3; z <= cz + 3; z++)
                {
                    bool edge = x == cx - 3 || x == cx + 3 || z == cz - 3 || z == cz + 3;
                    t.Set(x, baseY, z, edge ? Blocks.Cobblestone : Blocks.Water, true);
                }
                for (int i = 0; i < 4; i++)
                {
                    int px = cx + (i == 0 ? -3 : i == 1 ? 3 : 0);
                    int pz = cz + (i == 2 ? -3 : i == 3 ? 3 : 0);
                    for (int y = 1; y <= 3; y++) t.Set(px, baseY + y, pz, Blocks.Log, true);
                    t.Set(px, baseY + 4, pz, Blocks.Planks, true);
                }
                for (int x = cx - 3; x <= cx + 3; x++)
                for (int z = cz - 3; z <= cz + 3; z++)
                    t.Set(x, baseY + 5, z, Blocks.RoofTile, true);
            }
            else
            {
                t.Set(cx, baseY - 1, cz, Blocks.CottonBlock, true);
                t.Set(cx, baseY, cz, Blocks.Lamp, true);
                for (int i = 0; i < 4; i++)
                {
                    int px = cx + (i == 0 ? -1 : i == 1 ? 1 : 0);
                    int pz = cz + (i == 2 ? -1 : i == 3 ? 1 : 0);
                    t.Set(px, baseY, pz, Blocks.FrostingGrass, true);
                }
            }

            // --- roads: a cross plus a ring, so the houses read as a settlement
            for (int i = -radius + 2; i <= radius - 2; i++)
            for (int w = -1; w <= 1; w++)
            {
                t.Set(cx + i, baseY - 1, cz + w, roadBlock, true);
                t.Set(cx + w, baseY - 1, cz + i, roadBlock, true);
            }
            int ring = radius - 6;
            for (int a = 0; a < 96; a++)
            {
                float ang = a / 96f * Mathf.PI * 2f;
                int rx = cx + Mathf.RoundToInt(Mathf.Cos(ang) * ring);
                int rz = cz + Mathf.RoundToInt(Mathf.Sin(ang) * ring);
                t.Set(rx, baseY - 1, rz, roadBlock, true);
            }

            // --- street lamps
            for (int a = 0; a < 8; a++)
            {
                float ang = a / 8f * Mathf.PI * 2f + 0.4f;
                int lx = cx + Mathf.RoundToInt(Mathf.Cos(ang) * (ring - 3));
                int lz = cz + Mathf.RoundToInt(Mathf.Sin(ang) * (ring - 3));
                t.Set(lx, baseY, lz, Blocks.Fence, true);
                t.Set(lx, baseY + 1, lz, Blocks.Fence, true);
                t.Set(lx, baseY + 2, lz, Blocks.Lamp, true);
            }

            // --- houses
            int houses = rng.Range(4, 8);
            for (int i = 0; i < houses; i++)
            {
                float ang = (i / (float)houses) * Mathf.PI * 2f + rng.Range(-0.22f, 0.22f);
                float dist = rng.Range(11f, radius - 7f);
                int hx = cx + Mathf.RoundToInt(Mathf.Cos(ang) * dist);
                int hz = cz + Mathf.RoundToInt(Mathf.Sin(ang) * dist);
                int w = rng.Range(5, 8);
                int d = rng.Range(5, 8);
                int hgt = rng.Range(3, 5);
                bool hall = i == 0;

                House(t, hx, baseY, hz, w, d, hgt, cx, cz, rng,
                      wallBlock, beamBlock, roofBlock, floorBlock, hall, node);
            }

            // --- farm
            if (!node)
            {
                float fang = rng.Range(0f, Mathf.PI * 2f);
                int fx = cx + Mathf.RoundToInt(Mathf.Cos(fang) * (radius - 9f));
                int fz = cz + Mathf.RoundToInt(Mathf.Sin(fang) * (radius - 9f));
                for (int z = fz - 5; z <= fz + 5; z++)
                for (int x = fx - 5; x <= fx + 5; x++)
                {
                    bool fence = x == fx - 5 || x == fx + 5 || z == fz - 5 || z == fz + 5;
                    if (fence) { t.Set(x, baseY, z, Blocks.Fence, true); continue; }
                    t.Set(x, baseY - 1, z, Blocks.Dirt, true);
                    if ((x + z) % 2 == 0) t.Set(x, baseY, z, Blocks.Wheat, true);
                }
                t.Set(fx - 3, baseY, fz - 3, Blocks.Water, true);
            }
            else
            {
                float fang = rng.Range(0f, Mathf.PI * 2f);
                int fx = cx + Mathf.RoundToInt(Mathf.Cos(fang) * (radius - 9f));
                int fz = cz + Mathf.RoundToInt(Mathf.Sin(fang) * (radius - 9f));
                for (int i = 0; i < 12; i++)
                {
                    int gx = fx + rng.Range(-5, 6);
                    int gz = fz + rng.Range(-5, 6);
                    t.Set(gx, baseY, gz, rng.Chance(0.4f) ? Blocks.SherbetStone : Blocks.GumdropLeaves, true);
                }
            }

            // --- watchtower for the guards
            if (!node)
            {
                int tx = cx + (radius - 6);
                int tz = cz + 0;
                for (int i = 0; i < 4; i++)
                {
                    int px = tx + (i % 2 == 0 ? -1 : 1);
                    int pz = tz + (i < 2 ? -1 : 1);
                    for (int y = 1; y <= 5; y++) t.Set(px, baseY + y, pz, Blocks.Log, true);
                }
                for (int x = tx - 1; x <= tx + 1; x++)
                for (int z = tz - 1; z <= tz + 1; z++)
                    t.Set(x, baseY + 6, z, Blocks.Planks, true);
                t.Set(tx, baseY + 7, tz, Blocks.Lamp, true);
                for (int y = 1; y <= 5; y++) t.Set(tx + 1, baseY + y, tz + 2, Blocks.Ladder, true);
            }
        }

        private static void House(Target t, int hx, int baseY, int hz, int w, int d, int hgt,
                                  int centreX, int centreZ, Rng rng,
                                  byte wall, byte beam, byte roof, byte floor,
                                  bool hall, bool node)
        {
            int x0 = hx - w / 2, x1 = hx + w / 2;
            int z0 = hz - d / 2, z1 = hz + d / 2;

            // floor and walls
            for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
            {
                t.Set(x, baseY - 1, z, floor, true);
                bool corner = (x == x0 || x == x1) && (z == z0 || z == z1);
                bool edge = x == x0 || x == x1 || z == z0 || z == z1;
                for (int y = 0; y < hgt; y++)
                {
                    t.Set(x, baseY + y, z, Blocks.Air, true);
                    if (!edge) continue;
                    byte b = corner ? beam : wall;
                    t.Set(x, baseY + y, z, b, true);
                }
            }

            // windows
            for (int x = x0 + 2; x <= x1 - 2; x += 2)
            {
                for (int y = 1; y < hgt - 1; y++)
                {
                    t.Set(x, baseY + y, z0, Blocks.Glass, true);
                    t.Set(x, baseY + y, z1, Blocks.Glass, true);
                }
            }
            for (int z = z0 + 2; z <= z1 - 2; z += 2)
            {
                for (int y = 1; y < hgt - 1; y++)
                {
                    t.Set(x0, baseY + y, z, Blocks.Glass, true);
                    t.Set(x1, baseY + y, z, Blocks.Glass, true);
                }
            }

            // door on whichever side faces the well
            float dx = centreX - hx, dz = centreZ - hz;
            if (Mathf.Abs(dx) > Mathf.Abs(dz))
            {
                int dx0 = dx > 0 ? x1 : x0;
                t.Set(dx0, baseY, hz, Blocks.Door, true);
                t.Set(dx0, baseY + 1, hz, Blocks.Air, true);
            }
            else
            {
                int dz0 = dz > 0 ? z1 : z0;
                t.Set(hx, baseY, dz0, Blocks.Door, true);
                t.Set(hx, baseY + 1, dz0, Blocks.Air, true);
            }

            // gabled roof: each ring steps inward and rises
            int layers = Mathf.Min(w, d) / 2 + 1;
            for (int L = 0; L < layers; L++)
            {
                int rx0 = x0 - 1 + L, rx1 = x1 + 1 - L;
                int rz0 = z0 - 1 + L, rz1 = z1 + 1 - L;
                if (rx0 > rx1 || rz0 > rz1) break;
                int y = baseY + hgt + L;
                for (int x = rx0; x <= rx1; x++)
                for (int z = rz0; z <= rz1; z++)
                {
                    bool shell = x == rx0 || x == rx1 || z == rz0 || z == rz1;
                    if (shell || L == layers - 1) t.Set(x, y, z, roof, true);
                }
            }

            // ceiling lamp for light
            t.Set(hx, baseY + hgt - 1, hz, Blocks.Lamp, true);

            // furniture
            t.Set(x0 + 1, baseY, z0 + 1, Blocks.Bed, true);
            if (!node)
            {
                t.Set(x0 + 1, baseY, z1 - 1, Blocks.CraftingTable, true);
                t.Set(x1 - 1, baseY, z0 + 1, blocksFurnaceRng(rng), true);
            }
            else
            {
                t.Set(x0 + 1, baseY, z1 - 1, Blocks.CottonBlock, true);
                t.Set(x0 + 1, baseY + 1, z1 - 1, Blocks.Lamp, true);
            }

            if (hall)
            {
                // the meeting hall holds the village's shared storage and altar
                t.Set(x1 - 1, baseY, z0 + 1, Blocks.Chest, true);
                t.Set(x1 - 1, baseY, z0 + 2, Blocks.Chest, true);
                t.Set(x1 - 2, baseY, z1 - 1, Blocks.Bookshelf, true);
                t.Set(x1 - 2, baseY + 1, z1 - 1, Blocks.Bookshelf, true);
                t.Set(x1 - 1, baseY, z1 - 2, Blocks.Anvil, true);
                for (int y = 1; y < hgt; y++) t.Set(x0 + 2, baseY + y, z1 - 1, Blocks.Bookshelf, true);
            }
            else
            {
                t.Set(x1 - 1, baseY, z1 - 1, Blocks.Chest, true);
            }

            // a torch beside the door
            t.Set(centreX > hx ? x1 : x0, baseY + 2, hz + 1, Blocks.Torch, true);
        }

        private static byte blocksFurnaceRng(Rng rng)
        {
            return rng.Chance(0.5f) ? Blocks.Furnace : Blocks.CraftingTable;
        }

        // ------------------------------------------------------------ bandit camp
        private static void BuildBanditCamp(Target t, StructurePlan plan, TerrainGenerator gen, Rng rng)
        {
            int cx = Mathf.FloorToInt(plan.Centre.x);
            int cz = Mathf.FloorToInt(plan.Centre.z);
            int baseY = GroundY(gen, cx, cz);
            int r = (int)plan.Radius;

            for (int z = cz - r; z <= cz + r; z++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
                if (d > r + 1) continue;
                int g = GroundY(gen, x, z);
                int want = baseY - 1;
                if (g > want) for (int y = want + 1; y <= g; y++) t.Set(x, y, z, Blocks.Air, true);
                else for (int y = g + 1; y <= want; y++) t.Set(x, y, z, Blocks.Dirt, true);
                for (int y = baseY; y < baseY + 10 && y < WorldConfig.ChunkHeight; y++) t.Set(x, y, z, Blocks.Air, true);
                t.Set(x, baseY - 1, z, Blocks.Dirt, true);
                for (int y = baseY; y < baseY + 10; y++) t.Set(x, y, z, Blocks.Air, true);
            }

            // palisade with a gate
            for (int a = 0; a < 120; a++)
            {
                float ang = a / 120f * Mathf.PI * 2f;
                int px = cx + Mathf.RoundToInt(Mathf.Cos(ang) * r);
                int pz = cz + Mathf.RoundToInt(Mathf.Sin(ang) * r);
                if (Mathf.Abs(pz - cz) < 2 && px > cx) continue;      // gate facing +X
                for (int y = 0; y < 3; y++) t.Set(px, baseY + y, pz, Blocks.Log, true);
                t.Set(px, baseY + 3, pz, rng.Chance(0.5f) ? Blocks.Fence : Blocks.Air, true);
            }

            // tents
            int tents = rng.Range(3, 6);
            for (int i = 0; i < tents; i++)
            {
                float ang = i / (float)tents * Mathf.PI * 2f;
                float dist = rng.Range(4f, r - 4f);
                int tx = cx + Mathf.RoundToInt(Mathf.Cos(ang) * dist);
                int tz = cz + Mathf.RoundToInt(Mathf.Sin(ang) * dist);
                int w = rng.Range(4, 6);

                for (int x = tx - w / 2; x <= tx + w / 2; x++)
                for (int z = tz - w / 2; z <= tz + w / 2; z++)
                {
                    t.Set(x, baseY, z, Blocks.Planks, true);
                    bool edge = x == tx - w / 2 || x == tx + w / 2 || z == tz - w / 2 || z == tz + w / 2;
                    if (edge) t.Set(x, baseY + 1, z, Blocks.Fence, true);
                }
                for (int L = 0; L < 3; L++)
                for (int x = tx - w / 2 + L; x <= tx + w / 2 - L; x++)
                for (int z = tz - w / 2 + L; z <= tz + w / 2 - L; z++)
                    t.Set(x, baseY + 2 + L, z, Blocks.RoofTile, true);

                t.Set(tx, baseY + 1, tz - w / 2 + 1, Blocks.Bed, true);
                t.Set(tx + w / 2 - 1, baseY + 1, tz + w / 2 - 1, Blocks.Chest, true);
            }

            // campfire ring at the middle
            t.Set(cx, baseY, cz, Blocks.Campfire, true);
            for (int i = 0; i < 4; i++)
            {
                int lx = cx + (i == 0 ? -1 : i == 1 ? 1 : 0);
                int lz = cz + (i == 2 ? -1 : i == 3 ? 1 : 0);
                t.Set(lx, baseY, lz, Blocks.Log, true);
            }

            // watchtower
            int wx = cx - r + 3, wz = cz - r + 3;
            for (int i = 0; i < 4; i++)
            {
                int px = wx + (i % 2 == 0 ? -1 : 1);
                int pz = wz + (i < 2 ? -1 : 1);
                for (int y = 0; y < 6; y++) t.Set(px, baseY + y, pz, Blocks.Log, true);
            }
            for (int x = wx - 1; x <= wx + 1; x++)
            for (int z = wz - 1; z <= wz + 1; z++)
                t.Set(x, baseY + 6, z, Blocks.Planks, true);
            for (int y = 0; y < 6; y++) t.Set(wx + 1, baseY + y, wz + 2, Blocks.Ladder, true);

            // loot: the chief keeps the good stuff
            t.Set(cx + 1, baseY, cz - 1, Blocks.Chest, true);
        }

        // ------------------------------------------------------------------ ruins
        private static void BuildRuins(Target t, StructurePlan plan, TerrainGenerator gen, Rng rng)
        {
            int cx = Mathf.FloorToInt(plan.Centre.x);
            int cz = Mathf.FloorToInt(plan.Centre.z);
            int baseY = GroundY(gen, cx, cz);
            int r = (int)plan.Radius;

            bool node = DimensionState.NodeActive;
            byte wall = node ? Blocks.SherbetStone : Blocks.StoneBricks;
            byte baseB = node ? Blocks.SherbetStone : Blocks.Cobblestone;

            for (int x = cx - r; x <= cx + r; x++)
            for (int z = cz - r; z <= cz + r; z++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
                if (d > r) continue;
                int g = GroundY(gen, x, z);
                for (int y = baseY; y < baseY + 12 && y < WorldConfig.ChunkHeight; y++) t.Set(x, y, z, Blocks.Air, true);
                int want = baseY - 1;
                if (g > want) for (int y = want + 1; y <= g; y++) t.Set(x, y, z, Blocks.Air, true);
                else for (int y = g + 1; y <= want; y++) t.Set(x, y, z, baseB, true);
                t.Set(x, baseY - 1, z, d < r - 2f ? baseB : (node ? Blocks.FrostingGrass : Blocks.Grass), true);
            }

            // a broken rectangular hall
            int w = rng.Range(6, 10), d2 = rng.Range(6, 10);
            for (int i = 0; i < 4; i++)
            {
                bool horiz = i < 2;
                int along = horiz ? w : d2;
                int off = (i % 2 == 0) ? -1 : 1;
                for (int k = -along; k <= along; k++)
                {
                    int x = horiz ? cx + k : cx + off * w;
                    int z = horiz ? cz + off * d2 : cz + k;
                    int hgt = rng.Range(0, 5);
                    for (int y = 0; y < hgt; y++) t.Set(x, baseY + y, z, wall, true);
                }
            }

            // fallen columns
            for (int i = 0; i < 7; i++)
            {
                int px = cx + rng.Range(-r + 2, r - 2);
                int pz = cz + rng.Range(-r + 2, r - 2);
                int hgt = rng.Range(1, 6);
                for (int y = 0; y < hgt; y++) t.Set(px, baseY + y, pz, wall, true);
                if (rng.Chance(0.5f)) t.Set(px, baseY + hgt, pz, baseB, true);
            }

            t.Set(cx, baseY, cz, Blocks.Chest, true);
            if (node) t.Set(cx + 1, baseY, cz, Blocks.Lamp, true);
        }

        // ------------------------------------------------------------ ritual site
        private static void BuildRitualSite(Target t, StructurePlan plan, TerrainGenerator gen, bool node)
        {
            int cx = Mathf.FloorToInt(plan.Centre.x);
            int cz = Mathf.FloorToInt(plan.Centre.z);
            int baseY = GroundY(gen, cx, cz);
            int r = (int)plan.Radius;

            byte floor = node ? Blocks.SherbetStone : Blocks.StoneBricks;
            byte trim = node ? Blocks.SherbetStone : Blocks.CoalBlock;
            byte pillar = node ? Blocks.CottonBlock : Blocks.StoneBricks;

            for (int x = cx - r; x <= cx + r; x++)
            for (int z = cz - r; z <= cz + r; z++)
                for (int y = baseY; y < baseY + 14 && y < WorldConfig.ChunkHeight; y++)
                    t.Set(x, y, z, Blocks.Air, true);

            for (int x = cx - r; x <= cx + r; x++)
            for (int z = cz - r; z <= cz + r; z++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
                if (d > r - 0.5f) continue;
                int g = GroundY(gen, x, z);
                int want = baseY - 1;
                if (g > want) for (int y = want + 1; y <= g; y++) t.Set(x, y, z, Blocks.Air, true);
                else for (int y = g + 1; y <= want; y++) t.Set(x, y, z, floor, true);
                t.Set(x, baseY - 1, z, d < r - 3f ? floor : trim, true);
            }

            // two raised steps up to the altar
            for (int x = cx - 2; x <= cx + 2; x++)
            for (int z = cz - 2; z <= cz + 2; z++)
                t.Set(x, baseY, z, floor, true);

            // the altar itself
            t.Set(cx, baseY + 1, cz, Blocks.RitualAltar, true);

            // four pedestals, one per sacrificed tool
            int[] px = { -4, 4, 0, 0 };
            int[] pz = { 0, 0, -4, 4 };
            for (int i = 0; i < 4; i++)
            {
                t.Set(cx + px[i], baseY, cz + pz[i], Blocks.RitualPedestal, true);
                t.Set(cx + px[i], baseY + 1, cz + pz[i], Blocks.Air, true);
            }

            // pillars and a lamp ring
            for (int a = 0; a < 8; a++)
            {
                float ang = a / 8f * Mathf.PI * 2f;
                int lx = cx + Mathf.RoundToInt(Mathf.Cos(ang) * (r - 3));
                int lz = cz + Mathf.RoundToInt(Mathf.Sin(ang) * (r - 3));
                for (int y = 0; y < 5; y++) t.Set(lx, baseY + y, lz, pillar, true);
                t.Set(lx, baseY + 5, lz, Blocks.Lamp, true);
            }

            // a ring of rune marks on the floor
            for (int a = 0; a < 64; a++)
            {
                float ang = a / 64f * Mathf.PI * 2f;
                int lx = cx + Mathf.RoundToInt(Mathf.Cos(ang) * 7f);
                int lz = cz + Mathf.RoundToInt(Mathf.Sin(ang) * 7f);
                t.Set(lx, baseY - 1, lz, trim, true);
            }
        }

        // ------------------------------------------------------------ node shrine
        private static void BuildShrine(Target t, StructurePlan plan, TerrainGenerator gen)
        {
            int cx = Mathf.FloorToInt(plan.Centre.x);
            int cz = Mathf.FloorToInt(plan.Centre.z);
            int baseY = GroundY(gen, cx, cz);
            int r = (int)plan.Radius;

            for (int x = cx - r; x <= cx + r; x++)
            for (int z = cz - r; z <= cz + r; z++)
                for (int y = baseY; y < baseY + 12 && y < WorldConfig.ChunkHeight; y++)
                    t.Set(x, y, z, Blocks.Air, true);

            for (int x = cx - 3; x <= cx + 3; x++)
            for (int z = cz - 3; z <= cz + 3; z++)
            {
                int g = GroundY(gen, x, z);
                int want = baseY - 1;
                if (g > want) for (int y = want + 1; y <= g; y++) t.Set(x, y, z, Blocks.Air, true);
                else for (int y = g + 1; y <= want; y++) t.Set(x, y, z, Blocks.StoneBricks, true);
                t.Set(x, baseY - 1, z, Blocks.StoneBricks, true);
                t.Set(x, baseY, z, Blocks.Air, true);
            }

            // portal frame: two pillars and a lintel, with the rift in the middle
            for (int y = 0; y < 5; y++)
            {
                t.Set(cx - 2, baseY + y, cz, Blocks.RitualPedestal, true);
                t.Set(cx + 2, baseY + y, cz, Blocks.RitualPedestal, true);
            }
            for (int x = cx - 2; x <= cx + 2; x++) t.Set(x, baseY + 5, cz, Blocks.RitualPedestal, true);

            for (int y = 1; y < 5; y++)
            for (int x = cx - 1; x <= cx + 1; x++)
                t.Set(x, baseY + y, cz, Blocks.NodePortal, true);

            t.Set(cx, baseY + 5, cz, Blocks.Lamp, true);
        }

        // ----------------------------------------------------------- gumdrop grove
        private static void BuildGrove(Target t, StructurePlan plan, TerrainGenerator gen, Rng rng)
        {
            int cx = Mathf.FloorToInt(plan.Centre.x);
            int cz = Mathf.FloorToInt(plan.Centre.z);
            int baseY = GroundY(gen, cx, cz);
            int r = (int)plan.Radius;

            for (int i = 0; i < 16; i++)
            {
                float ang = rng.Range(0f, Mathf.PI * 2f);
                float dist = rng.Range(2f, r - 3f);
                int tx = cx + Mathf.RoundToInt(Mathf.Cos(ang) * dist);
                int tz = cz + Mathf.RoundToInt(Mathf.Sin(ang) * dist);
                int g = GroundY(gen, tx, tz);
                int trunk = rng.Range(4, 8);

                for (int y = 0; y < trunk; y++) t.Set(tx, g + y, tz, Blocks.GumdropLog, true);

                int cr = rng.Range(2, 4);
                for (int x = tx - cr; x <= tx + cr; x++)
                for (int z = tz - cr; z <= tz + cr; z++)
                for (int y = 0; y <= cr; y++)
                {
                    float d = Mathf.Sqrt((x - tx) * (x - tx) + (z - tz) * (z - tz) + (y - cr * 0.5f) * (y - cr * 0.5f));
                    if (d > cr + 0.4f) continue;
                    t.Set(x, g + trunk + y, z, Blocks.GumdropLeaves, true);
                }

                if (rng.Chance(0.35f))
                    t.Set(tx, g + trunk + cr + 1, tz, Blocks.Lamp, true);
            }

            // floating cotton clouds
            for (int i = 0; i < 6; i++)
            {
                int bx = cx + rng.Range(-r, r);
                int bz = cz + rng.Range(-r, r);
                int by = baseY + rng.Range(14, 24);
                int br = rng.Range(2, 5);
                for (int x = bx - br; x <= bx + br; x++)
                for (int z = bz - br; z <= bz + br; z++)
                for (int y = 0; y < 3; y++)
                {
                    float d = Mathf.Sqrt((x - bx) * (x - bx) + (z - bz) * (z - bz));
                    if (d > br) continue;
                    t.Set(x, by + y, z, Blocks.CottonBlock, true);
                }
            }

            // candy cane fence around the grove
            for (int a = 0; a < 72; a++)
            {
                float ang = a / 72f * Mathf.PI * 2f;
                int fx = cx + Mathf.RoundToInt(Mathf.Cos(ang) * r);
                int fz = cz + Mathf.RoundToInt(Mathf.Sin(ang) * r);
                int g = GroundY(gen, fx, fz);
                t.Set(fx, g, fz, Blocks.SherbetStone, true);
                t.Set(fx, g + 1, fz, Blocks.SherbetStone, true);
            }
        }

        // ------------------------------------------------------------ node hollow
        private static void BuildNodeHollow(Target t, StructurePlan plan, TerrainGenerator gen)
        {
            int cx = Mathf.FloorToInt(plan.Centre.x);
            int cz = Mathf.FloorToInt(plan.Centre.z);
            int baseY = GroundY(gen, cx, cz);
            int r = (int)plan.Radius;

            for (int x = cx - r; x <= cx + r; x++)
            for (int z = cz - r; z <= cz + r; z++)
                for (int y = baseY; y < baseY + 26 && y < WorldConfig.ChunkHeight; y++)
                    t.Set(x, y, z, Blocks.Air, true);

            // a shallow bowl of sherbet with a raised dais in the middle
            for (int x = cx - r; x <= cx + r; x++)
            for (int z = cz - r; z <= cz + r; z++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
                if (d > r - 0.5f) continue;

                int g = GroundY(gen, x, z);
                int want = baseY - 1 - Mathf.RoundToInt(Mathf.Clamp(r - d, 0f, 6f) * 0.5f);
                if (g > want) for (int y = want + 1; y <= g; y++) t.Set(x, y, z, Blocks.Air, true);
                else for (int y = g + 1; y <= want; y++) t.Set(x, y, z, Blocks.SherbetStone, true);
                t.Set(x, want, z, Blocks.FrostingGrass, true);
            }

            // dais
            for (int x = cx - 4; x <= cx + 4; x++)
            for (int z = cz - 4; z <= cz + 4; z++)
                t.Set(x, baseY, z, Blocks.SherbetStone, true);
            for (int x = cx - 2; x <= cx + 2; x++)
            for (int z = cz - 2; z <= cz + 2; z++)
                t.Set(x, baseY + 1, z, Blocks.SherbetStone, true);

            // the altar and its four pedestals
            t.Set(cx, baseY + 2, cz, Blocks.RitualAltar, true);
            int[] px = { -6, 6, 0, 0 };
            int[] pz = { 0, 0, -6, 6 };
            for (int i = 0; i < 4; i++)
            {
                t.Set(cx + px[i], baseY + 1, cz + pz[i], Blocks.RitualPedestal, true);
                t.Set(cx + px[i], baseY + 2, cz + pz[i], Blocks.Air, true);
            }

            // monolith ring
            for (int a = 0; a < 10; a++)
            {
                float ang = a / 10f * Mathf.PI * 2f;
                int lx = cx + Mathf.RoundToInt(Mathf.Cos(ang) * (r - 4));
                int lz = cz + Mathf.RoundToInt(Mathf.Sin(ang) * (r - 4));
                int hgt = 6 + (a % 3);
                for (int y = 0; y < hgt; y++) t.Set(lx, baseY + y, lz, Blocks.SherbetStone, true);
                t.Set(lx, baseY + hgt, lz, Blocks.Lamp, true);
                if (a % 2 == 0) t.Set(lx, baseY + hgt + 1, lz, Blocks.NodePortal, true);
            }

            // candy cane arches over the dais
            for (int i = 0; i < 4; i++)
            {
                int ax = cx + (i == 0 ? -3 : i == 1 ? 3 : 0);
                int az = cz + (i == 2 ? -3 : i == 3 ? 3 : 0);
                for (int y = 2; y < 7; y++) t.Set(ax, baseY + y, az, Blocks.SherbetStone, true);
            }
            for (int x = cx - 3; x <= cx + 3; x++) t.Set(x, baseY + 7, cz, Blocks.SherbetStone, true);
            for (int z = cz - 3; z <= cz + 3; z++) t.Set(cx, baseY + 7, z, Blocks.SherbetStone, true);
            t.Set(cx, baseY + 7, cz, Blocks.Lamp, true);
        }

        // ================================================================= helpers
        private static int GroundY(TerrainGenerator gen, int x, int z)
        {
            int h = Mathf.FloorToInt(gen.HeightAt(x + 0.5f, z + 0.5f));
            return Mathf.Clamp(h, 1, WorldConfig.ChunkHeight - 40);
        }

        /// <summary>Write-back window into one chunk's voxel array.</summary>
        private struct Target
        {
            private readonly byte[] _blocks;
            private readonly int _x0, _z0, _size;

            public Target(ChunkData data)
            {
                _blocks = data.Blocks;
                _x0 = data.WorldX;
                _z0 = data.WorldZ;
                _size = WorldConfig.ChunkSize;
            }

            public void Set(int x, int y, int z, byte block, bool replace)
            {
                if (x < _x0 || x >= _x0 + _size) return;
                if (z < _z0 || z >= _z0 + _size) return;
                if ((uint)y >= (uint)WorldConfig.ChunkHeight) return;

                int idx = (x - _x0) + (z - _z0) * _size + y * _size * _size;
                if (!replace && _blocks[idx] != Blocks.Air) return;
                _blocks[idx] = block;
            }
        }
    }
}