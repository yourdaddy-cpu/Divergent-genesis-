using UnityEngine;

namespace DivergentGenesis.World
{
    /// <summary>Biome ids. Kept as a byte for compact per-column storage.</summary>
    public enum BiomeType : byte
    {
        DeepOcean = 0,
        Ocean = 1,
        River = 2,
        Beach = 3,
        WetPlains = 4,
        Plains = 5,
        Forest = 6,
        BirchForest = 7,
        Taiga = 8,
        SnowForest = 9,
        Swamp = 10,
        Desert = 11,
        Savanna = 12,
        Mesa = 13,
        Rocky = 14,
        Mountain = 15,
        SnowPeak = 16,
        Ashlands = 17,

        // --- the Node dimension ---------------------------------------------
        NodeMeadow = 18,
        NodeForest = 19,
        NodeCream = 20,
        NodeCrystal = 21
    }

    public struct BiomeInfo
    {
        public BiomeType Type;
        public string DisplayName;
        public Color32 GrassColor;
        public Color32 FoliageColor;
        public Color32 DirtColor;
        public Color32 StoneColor;
        public byte SurfaceBlock;
        public float TreeDensity;      // trees per 32x32 chunk at density 1.0
        public float GrassDensity;     // 0..1
        public bool Swimmable;
        public bool Snowy;
    }

    /// <summary>
    /// Climate classification. Temperature/humidity/continentalness fields are sampled at
    /// world scale (a few hundred metres per feature) so biomes are huge and believable.
    /// </summary>
    public static class BiomeSystem
    {
        public const int Count = 22;

        private static readonly BiomeInfo[] Table = new BiomeInfo[Count];

        private static void Set(int idx, string name, byte grassR, byte grassG, byte grassB,
                               byte folR, byte folG, byte folB, byte dirtR, byte dirtG, byte dirtB,
                               byte stoneR, byte stoneG, byte stoneB, byte surf,
                               float treeDensity, float grassDensity, bool snowy)
        {
            BiomeInfo b;
            b.Type = (BiomeType)idx;
            b.DisplayName = name;
            b.GrassColor = new Color32(grassR, grassG, grassB, 255);
            b.FoliageColor = new Color32(folR, folG, folB, 255);
            b.DirtColor = new Color32(dirtR, dirtG, dirtB, 255);
            b.StoneColor = new Color32(stoneR, stoneG, stoneB, 255);
            b.SurfaceBlock = surf;
            b.TreeDensity = treeDensity;
            b.GrassDensity = grassDensity;
            b.Swimmable = false;
            b.Snowy = snowy;
            Table[idx] = b;
        }

        static BiomeSystem()
        {
            //          name          grass            foliage           dirt             stone            surface        treeDens grassDens snowy
            Set(0, "Deep Ocean", 38, 62, 71, 36, 58, 66, 70, 60, 45, 84, 86, 92, Blocks.Stone, 0f, 0f, false);
            Set(1, "Ocean", 44, 96, 112, 42, 90, 104, 96, 86, 64, 96, 98, 104, Blocks.Sand, 0f, 0f, false);
            Set(2, "River", 62, 110, 96, 58, 104, 88, 96, 84, 62, 96, 98, 104, Blocks.Sand, 1.2f, 0.25f, false);
            Set(3, "Beach", 214, 200, 140, 196, 190, 132, 186, 160, 106, 128, 126, 120, Blocks.Sand, 0.4f, 0.05f, false);
            Set(4, "Wet Plains", 106, 158, 76, 96, 148, 70, 108, 84, 56, 122, 122, 124, Blocks.Grass, 1.5f, 0.8f, false);
            Set(5, "Plains", 142, 186, 96, 132, 178, 88, 122, 96, 64, 128, 128, 130, Blocks.Grass, 0.6f, 0.9f, false);
            Set(6, "Forest", 84, 140, 62, 72, 128, 54, 100, 80, 54, 118, 118, 122, Blocks.Grass, 9f, 0.85f, false);
            Set(7, "Birch Forest", 126, 172, 84, 118, 162, 76, 116, 92, 60, 124, 124, 126, Blocks.Grass, 7f, 0.85f, false);
            Set(8, "Taiga", 74, 118, 72, 62, 102, 62, 84, 70, 52, 112, 114, 118, Blocks.Grass, 8f, 0.5f, false);
            Set(9, "Snow Forest", 176, 192, 190, 150, 178, 176, 122, 116, 108, 128, 130, 136, Blocks.Grass, 5f, 0.12f, true);
            Set(10, "Swamp", 92, 112, 62, 78, 98, 54, 74, 66, 50, 84, 88, 88, Blocks.Grass, 3.2f, 0.7f, false);
            Set(11, "Desert", 216, 200, 130, 204, 186, 116, 200, 176, 118, 150, 138, 112, Blocks.Sand, 0.15f, 0.04f, false);
            Set(12, "Savanna", 188, 176, 84, 178, 164, 72, 156, 130, 78, 128, 126, 118, Blocks.Grass, 1.1f, 0.75f, false);
            Set(13, "Mesa", 196, 132, 76, 186, 118, 66, 168, 96, 56, 148, 96, 68, Blocks.Terracotta, 0.3f, 0.05f, false);
            Set(14, "Rocky", 138, 142, 128, 120, 130, 112, 128, 118, 102, 124, 124, 126, Blocks.Stone, 0.5f, 0.15f, false);
            Set(15, "Mountain", 120, 134, 112, 104, 124, 100, 112, 104, 92, 118, 118, 122, Blocks.Stone, 1.4f, 0.2f, false);
            Set(16, "Snow Peak", 224, 232, 238, 200, 212, 224, 150, 152, 156, 140, 146, 154, Blocks.Snow, 0.4f, 0.02f, true);
            Set(17, "Ashlands", 86, 80, 78, 74, 70, 68, 62, 56, 52, 78, 74, 72, Blocks.Ash, 0.6f, 0.06f, false);

            //         name             grass            foliage           dirt             stone            surface                 treeDens grassDens snowy
            Set(18, "Node Meadow", 190, 240, 196, 178, 232, 188, 244, 214, 224, 232, 176, 148, Blocks.FrostingGrass, 2.2f, 0.35f, false);
            Set(19, "Gumdrop Wood", 172, 232, 208, 164, 224, 200, 226, 196, 216, 220, 168, 186, Blocks.FrostingGrass, 11f, 0.20f, false);
            Set(20, "Cream Shallows", 214, 226, 240, 206, 218, 234, 236, 232, 240, 244, 218, 226, Blocks.Sand, 0.3f, 0.0f, false);
            Set(21, "Cotton Highlands", 238, 244, 255, 230, 238, 252, 244, 244, 250, 250, 250, 255, Blocks.Snow, 0.8f, 0.05f, false);

            // Ocean water is swimmable.
            Table[0].Swimmable = true;
            Table[1].Swimmable = true;
            Table[2].Swimmable = true;
            Table[20].Swimmable = true;
        }

        public static BiomeInfo Get(BiomeType type)
        {
            int i = (int)type;
            if (i < 0 || i >= Count) i = 5;
            return Table[i];
        }

        public static Color32 GetGrassColor(BiomeType t) { return Get(t).GrassColor; }
        public static Color32 GetFoliageColor(BiomeType t) { return Get(t).FoliageColor; }
        public static Color32 GetStoneColor(BiomeType t) { return Get(t).StoneColor; }
        public static Color32 GetDirtColor(BiomeType t) { return Get(t).DirtColor; }
    }
}
