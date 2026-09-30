using UnityEngine;

namespace DivergentGenesis.Core
{
    /// <summary>
    /// Every tunable number for the 140 x 140 km world lives here so balance
    /// passes never require hunting through gameplay code.
    /// </summary>
    public static class WorldConfig
    {
        // ---------------------------------------------------------------- world
        public const int WorldSizeKm = 140;
        public const int WorldSizeMeters = WorldSizeKm * 1000;          // 140_000
        public const int WorldHalfExtent = WorldSizeMeters / 2;         // 70_000
        public const float WorldMin = -WorldHalfExtent;
        public const float WorldMax = WorldHalfExtent;

        /// <summary>Hard wall the player physically cannot pass.</summary>
        public const float BorderWarningDistance = 120f;
        public const float BorderWallHeight = 420f;
        public const float BorderWallThickness = 12f;

        // --------------------------------------------------------------- chunks
        public const int ChunkSize = 32;                                 // metres per chunk edge
        public const int ChunksPerAxis = WorldSizeMeters / ChunkSize;   // 4375
        public const int ChunkWorldMinY = 0;
        public const int ChunkHeight = 160;                              // blocks
        public const int ChunkVolume = ChunkSize * ChunkSize * ChunkHeight;

        /// <summary>Sampling resolution of the height field, in metres.</summary>
        public const int ColumnSamples = ChunkSize + 1;                   // 33 -> 1 m resolution

        // ----------------------------------------------------------- terrain
        public const float SeaLevel = 64f;
        public const float BeachHeight = SeaLevel + 2.5f;
        public const float MaxTerrainHeight = 152f;
        public const int WaterBlockId = 9;

        /// <summary>Below this Y a chunk is meshed as real voxels (caves, mining).</summary>
        public const int VoxelRadiusChunks = 5;                          // 5 * 32 m = 160 m
        /// <summary>Only chunks this close get a collider.</summary>
        public const int ColliderRadiusChunks = 4;

        // --------------------------------------------------------- rendering
        public const float DayLengthSeconds = 900f;                       // 15 real minutes

        public static readonly int[] ViewDistancesMeters = { 1024, 2048, 4096 };

        public static int ChunksForViewDistance(int viewDistanceMeters)
        {
            int r = viewDistanceMeters / ChunkSize;
            return Mathf.Clamp(r, 8, 128);
        }

        public static string ViewDistanceLabel(int viewDistanceMeters)
        {
            return (viewDistanceMeters / 1000f).ToString("0.#") + " km view";
        }
    }

    /// <summary>Graphics tiers, auto-picked from the device then overridable in the in-game menu.</summary>
    public enum GraphicsTier
    {
        Potato = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Ultra = 4
    }

    public sealed class QualityProfile
    {
        public GraphicsTier Tier;
        public string Name;
        public int ViewDistanceMeters;
        public int TargetFrameRate;
        public int VoxelRadiusChunks;    // how far real voxels (caves, mining) exist
        /// <summary>Max view distance in metres for LOD levels 0..4 (tiles are 32/128/256/512/1024 m).</summary>
        public float[] LevelMaxDist;
        public int TreeDensity;          // 0..100 scale
        public int GrassDensity;         // 0..100 scale
        public int PropDensity;          // 0..100 scale
        public int WaterGridStep;        // metres per water quad
        public int MaxChunksBuiltPerFrame;
        public int MaxChunksGeneratedPerFrame;
        public int WorkerThreads;
        public bool AnimatedWater;
        public bool Shadows;
        public int AntiAliasing;

        public static QualityProfile Create(GraphicsTier tier)
        {
            switch (tier)
            {
                case GraphicsTier.Potato:
                    return new QualityProfile
                    {
                        Tier = tier, Name = "Potato (battery saver)",
                        ViewDistanceMeters = 1024, TargetFrameRate = 30,
                        VoxelRadiusChunks = 2,
                        LevelMaxDist = new[] { 48f, 128f, 320f, 768f, 99999f },
                        TreeDensity = 45, GrassDensity = 0, PropDensity = 40,
                        WaterGridStep = 16, MaxChunksBuiltPerFrame = 2, MaxChunksGeneratedPerFrame = 1,
                        WorkerThreads = 1, AnimatedWater = false, Shadows = false, AntiAliasing = 0
                    };
                case GraphicsTier.Low:
                    return new QualityProfile
                    {
                        Tier = tier, Name = "Low",
                        ViewDistanceMeters = 1024, TargetFrameRate = 30,
                        VoxelRadiusChunks = 3,
                        LevelMaxDist = new[] { 64f, 160f, 448f, 1024f, 99999f },
                        TreeDensity = 65, GrassDensity = 40, PropDensity = 60,
                        WaterGridStep = 8, MaxChunksBuiltPerFrame = 2, MaxChunksGeneratedPerFrame = 2,
                        WorkerThreads = 2, AnimatedWater = true, Shadows = false, AntiAliasing = 0
                    };
                case GraphicsTier.Medium:
                    return new QualityProfile
                    {
                        Tier = tier, Name = "Medium",
                        ViewDistanceMeters = 2048, TargetFrameRate = 30,
                        VoxelRadiusChunks = 4,
                        LevelMaxDist = new[] { 80f, 256f, 640f, 1536f, 99999f },
                        TreeDensity = 85, GrassDensity = 70, PropDensity = 80,
                        WaterGridStep = 8, MaxChunksBuiltPerFrame = 3, MaxChunksGeneratedPerFrame = 3,
                        WorkerThreads = 2, AnimatedWater = true, Shadows = false, AntiAliasing = 0
                    };
                case GraphicsTier.High:
                    return new QualityProfile
                    {
                        Tier = tier, Name = "High",
                        ViewDistanceMeters = 2048, TargetFrameRate = 60,
                        VoxelRadiusChunks = 5,
                        LevelMaxDist = new[] { 96f, 320f, 768f, 2048f, 99999f },
                        TreeDensity = 100, GrassDensity = 100, PropDensity = 100,
                        WaterGridStep = 4, MaxChunksBuiltPerFrame = 4, MaxChunksGeneratedPerFrame = 4,
                        WorkerThreads = 3, AnimatedWater = true, Shadows = false, AntiAliasing = 0
                    };
                default:
                    return new QualityProfile
                    {
                        Tier = GraphicsTier.Ultra, Name = "Ultra",
                        ViewDistanceMeters = 4096, TargetFrameRate = 60,
                        VoxelRadiusChunks = 6,
                        LevelMaxDist = new[] { 128f, 448f, 1024f, 3072f, 99999f },
                        TreeDensity = 100, GrassDensity = 100, PropDensity = 100,
                        WaterGridStep = 4, MaxChunksBuiltPerFrame = 6, MaxChunksGeneratedPerFrame = 6,
                        WorkerThreads = 4, AnimatedWater = true, Shadows = true, AntiAliasing = 2
                    };
            }
        }

        /// <summary>Longest world-space ray the player may interact with.</summary>
        public float BuildDistance
        {
            get { return Mathf.Lerp(3.5f, 7.0f, (float)Tier / 4f); }
        }

        public static int LevelCount { get { return 5; } }
        public static int EdgeForLevel(int level) { return ChunkGeneratorEdge(level); }

        private static int ChunkGeneratorEdge(int level)
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
    }
}
