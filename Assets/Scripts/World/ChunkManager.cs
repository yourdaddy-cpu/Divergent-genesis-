using System.Collections.Generic;
using UnityEngine;
using DivergentGenesis.Core;
using DivergentGenesis.Render;

namespace DivergentGenesis.World
{
    /// <summary>
    /// Streams the 140 km world.
    ///
    /// Tiles come in five sizes - 32, 128, 256, 512, 1024 m - and the manager
    /// only keeps the fine one where you are standing. A 2 km view costs roughly
    /// 200 GameObjects instead of the ~4,000 a uniform chunk size would need,
    /// which is the difference between 60 fps and 18 fps on a Dimensity 6500.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class ChunkManager : MonoBehaviour
    {
        public static ChunkManager Instance;

        [Header("World")]
        public int WorldSeed = 20260927;

        public QualityProfile Quality = QualityProfile.Create(GraphicsTier.Medium);

        [Header("Streaming")]
        public Transform Player;

        private readonly Dictionary<long, ChunkRuntime> _chunks = new Dictionary<long, ChunkRuntime>(512);
        private readonly HashSet<long> _inflight = new HashSet<long>();
        private readonly List<ChunkRuntime> _needsMesh = new List<ChunkRuntime>(128);

        private ChunkWorkerPool _pool;
        private ChunkMesher _mesher;
        private VoxelReader _reader;
        private TerrainGenerator _gen;

        private Transform _chunkRoot;
        private int _frame;
        private int _voxelUpgradesThisFrame;
        private bool _settingsDirty;
        private int _generation;

        public Transform ChunkRoot { get { return _chunkRoot; } }
        public int LoadedTileCount { get { return _chunks.Count; } }
        public int PendingTileCount { get { return _pool != null ? _pool.PendingCount : 0; } }
        public bool Busy { get { return _needsMesh.Count > 0 || PendingTileCount > 0; } }
        public int WorldSeedValue { get { return WorldSeed; } }

        /// <summary>The pure generator behind this world. Safe to sample from any thread.</summary>
        public TerrainGenerator Generator { get { return _gen; } }

        /// <summary>True once the ground under the player actually exists.</summary>
        public bool WorldReady
        {
            get
            {
                if (Player == null) return _chunks.Count > 0;
                ChunkCoord c = ChunkCoord.FromWorld(Player.position.x, Player.position.z);
                ChunkRuntime t;
                return _chunks.TryGetValue(ChunkRuntime.ChunkKey(c.X, c.Z, 0), out t)
                       && t.State == ChunkState.Meshed && t.Data != null && t.Data.IsVoxel;
            }
        }

        // ============================================================== lifecycle
        private bool _initialised;

        private void Awake()
        {
            Init();
        }

        /// <summary>
        /// Builds the streaming world. Safe to call twice - Awake() and the
        /// bootstrap both route here.
        /// </summary>
        public void Init()
        {
            if (_initialised) return;
            _initialised = true;
            Instance = this;
            if (Quality == null) Quality = QualityProfile.Create(GraphicsTier.Medium);

            var rootGo = new GameObject("Chunks");
            rootGo.transform.SetParent(transform, false);
            _chunkRoot = rootGo.transform;

            _mesher = new ChunkMesher();
            _gen = new TerrainGenerator(WorldSeed);
            _reader = new VoxelReader { Resolver = GetBlock };

            MaterialLibrary.Ensure();
            StartPool();
        }

        private void StartPool()
        {
            if (_pool != null) { _pool.Dispose(); _pool = null; }
            int threads = Mathf.Clamp(Quality.WorkerThreads, 1, 6);
            _pool = new ChunkWorkerPool(threads);
        }

        public void ApplyQuality(QualityProfile profile)
        {
            if (profile == null) return;
            Quality = profile;
            Application.targetFrameRate = profile.TargetFrameRate;
            if (_pool != null) _pool.Dispose();
            StartPool();
            _settingsDirty = true;
        }

        public void SetQualityTier(GraphicsTier tier)
        {
            ApplyQuality(QualityProfile.Create(tier));
        }

        public void SetViewDistance(int meters)
        {
            Quality.ViewDistanceMeters = meters;
            _settingsDirty = true;
        }

        /// <summary>
        /// Throws away every streamed tile and starts again. This is what a
        /// dimension change is: the same streaming code, a different world.
        ///
        /// The generation counter is what makes it safe - jobs still sitting on
        /// the worker threads belong to the old epoch and are dropped on arrival,
        /// so a tile from the overworld can never leak into the Node.
        /// </summary>
        public void RebuildAll()
        {
            _generation++;

            var kill = new List<ChunkRuntime>(_chunks.Values);
            for (int i = 0; i < kill.Count; i++)
            {
                if (kill[i] != null) kill[i].Release();
            }
            _chunks.Clear();
            _needsMesh.Clear();
            _inflight.Clear();

            // a fresh pool discards queued work from the plane we just left
            StartPool();
            _settingsDirty = true;
        }

        private void OnDestroy()
        {
            if (_pool != null) { _pool.Dispose(); _pool = null; }
            if (Instance == this) Instance = null;
        }

        // ================================================================== update
        private void Update()
        {
            _frame++;
            _voxelUpgradesThisFrame = 0;

            Vector3 p = Player != null ? Player.position : transform.position;
            p = DGBorder.Clamp(p);

            ApplyFinished(p);
            ProcessEdits();
            RequestTiles(p);
            BuildMeshes(p);
            MaintainVoxelRadius(p);

            if (_settingsDirty) { _settingsDirty = false; EnforceViewDistance(p); }

            if ((_frame & 15) == 0) UnloadFar(p);
        }

        /// <summary>Drop tiles that fall outside the new view distance right away.</summary>
        private void EnforceViewDistance(Vector3 p)
        {
            float view = Quality.ViewDistanceMeters * 1.35f;
            var kill = new List<ChunkRuntime>();
            foreach (var kv in _chunks)
            {
                var t = kv.Value;
                if (t.Data == null) continue;
                if (t.Level == 0) continue;                     // never drop the ground you stand on
                float max = Mathf.Min(Quality.LevelMaxDist[t.Level], view);
                float d = Distance2D(p, t.Center);
                if (d > max + t.Data.EdgeMeters) kill.Add(t);
            }
            for (int i = 0; i < kill.Count; i++) { Unload(kill[i]); }
        }

        private void ApplyFinished(Vector3 p)
        {
            GenResult r;
            int applied = 0;
            while (applied < 8 && _pool.TryDequeue(out r))
            {
                long key = ChunkRuntime.ChunkKey(r.Cx, r.Cz, r.Level);
                _inflight.Remove(key);

                // a tile built for the plane we already left is garbage
                if (r.Gen != _generation) continue;

                ChunkRuntime existing;
                if (_chunks.TryGetValue(key, out existing) && existing.Data != null)
                    continue;                                   // superseded by a newer request

                var tile = new ChunkRuntime { Cx = r.Cx, Cz = r.Cz, Level = r.Level, Data = r.Data, State = ChunkState.HasData };
                tile.Center = new Vector3(r.Cx * r.Data.EdgeMeters + r.Data.EdgeMeters * 0.5f, 0f,
                                          r.Cz * r.Data.EdgeMeters + r.Data.EdgeMeters * 0.5f);
                tile.Distance = Distance2D(p, tile.Center);
                _chunks[key] = tile;
                _needsMesh.Add(tile);
                applied++;
            }
        }

        private void RequestTiles(Vector3 p)
        {
            int budget = Quality.MaxChunksGeneratedPerFrame;
            float view = Quality.ViewDistanceMeters;

            for (int level = 0; level < QualityProfile.LevelCount; level++)
            {
                if (budget <= 0) break;

                int edge = QualityProfile.EdgeForLevel(level);
                float maxD = Mathf.Min(Quality.LevelMaxDist[level], view * 1.35f);
                float innerD = level > 0 ? Quality.LevelMaxDist[level - 1] * 0.88f : -1f;
                int radius = Mathf.CeilToInt(maxD / edge);

                int ccx = Mathf.FloorToInt(p.x / edge);
                int ccz = Mathf.FloorToInt(p.z / edge);

                // near-to-far ring order so the world appears around the player outwards
                for (int ring = 0; ring <= radius && budget > 0; ring++)
                {
                    for (int dz = -ring; dz <= ring && budget > 0; dz++)
                    {
                        for (int dx = -ring; dx <= ring && budget > 0; dx++)
                        {
                            // only the perimeter of this ring
                            if (ring > 0 && Mathf.Abs(dx) != ring && Mathf.Abs(dz) != ring) continue;

                            int cx = ccx + dx, cz = ccz + dz;
                            float cwx = cx * edge + edge * 0.5f;
                            float cwz = cz * edge + edge * 0.5f;
                            float d = Distance2D(p, new Vector3(cwx, 0f, cwz));
                            if (d > maxD) continue;
                            if (d <= innerD) continue;

                            long key = ChunkRuntime.ChunkKey(cx, cz, level);
                            if (_chunks.ContainsKey(key) || _inflight.Contains(key)) continue;

                            bool wantVoxels = level == 0 && d <= Quality.VoxelRadiusChunks * 32f;
                            _inflight.Add(key);
                            _pool.Enqueue(new GenJob { Cx = cx, Cz = cz, Level = level, Seed = WorldSeed, Voxels = wantVoxels, Gen = _generation });
                            budget--;
                        }
                    }
                }
            }
        }

        private void BuildMeshes(Vector3 p)
        {
            int budget = Quality.MaxChunksBuiltPerFrame;
            while (budget > 0 && _needsMesh.Count > 0)
            {
                ChunkRuntime tile = PickNearest(p);
                if (tile == null || tile.Data == null) break;

                Mesh mesh = null;
                if (tile.Data.IsVoxel)
                {
                    mesh = _mesher.BuildVoxelMesh(tile.Data, _reader);
                    tile.Build(mesh, MaterialLibrary.Terrain, MaterialLibrary.Water);
                }
                else
                {
                    bool withWater = tile.Level >= 1;
                    mesh = _mesher.BuildHeightMesh(tile.Data, withWater);
                    tile.Build(mesh, MaterialLibrary.Terrain, MaterialLibrary.Water);
                }

                budget--;
            }
        }

        private ChunkRuntime PickNearest(Vector3 p)
        {
            int best = -1;
            float bestD = float.MaxValue;
            int limit = Mathf.Min(_needsMesh.Count, 64);
            for (int i = 0; i < limit; i++)
            {
                var c = _needsMesh[i];
                if (c == null || c.Data == null) continue;
                float d = Distance2D(p, c.Center);
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best < 0) return null;
            var tile = _needsMesh[best];
            _needsMesh.RemoveAt(best);
            return tile;
        }

        /// <summary>Tiles crossing into the voxel radius get real caves + editable blocks.</summary>
        private void MaintainVoxelRadius(Vector3 p)
        {
            if (_voxelUpgradesThisFrame >= 2) return;
            float radius = Quality.VoxelRadiusChunks * 32f;

            foreach (var kv in _chunks)
            {
                var t = kv.Value;
                if (t.Level != 0 || t.Data == null) continue;
                float d = Distance2D(p, t.Center);
                t.Distance = d;

                if (!t.Data.IsVoxel && d <= radius)
                {
                    if (_voxelUpgradesThisFrame >= 2) return;
                    long key = t.Key;
                    Unload(t);
                    _chunks.Remove(key);
                    _inflight.Add(key);
                    _pool.Enqueue(new GenJob { Cx = t.Cx, Cz = t.Cz, Level = 0, Seed = WorldSeed, Voxels = true, Gen = _generation });
                    _voxelUpgradesThisFrame++;
                }
            }
        }

        private void ProcessEdits()
        {
            if (BlockEdits.DirtyChunks.Count == 0) return;

            var dirty = new List<long>(BlockEdits.DirtyChunks);
            BlockEdits.DirtyChunks.Clear();

            for (int i = 0; i < dirty.Count; i++)
            {
                int cx, cz;
                BlockEdits.UnpackChunk(dirty[i], out cx, out cz);
                ChunkRuntime t;
                if (!_chunks.TryGetValue(ChunkRuntime.ChunkKey(cx, cz, 0), out t)) continue;
                if (t.Data == null || !t.Data.IsVoxel) continue;

                long key = t.Key;
                Unload(t);
                _chunks.Remove(key);
                _inflight.Add(key);
                _pool.Enqueue(new GenJob { Cx = cx, Cz = cz, Level = 0, Seed = WorldSeed, Voxels = true, Gen = _generation });
            }
        }

        private void UnloadFar(Vector3 p)
        {
            var kill = new List<ChunkRuntime>();
            foreach (var kv in _chunks)
            {
                var t = kv.Value;
                if (t.Data == null) continue;
                float max = Mathf.Min(Quality.LevelMaxDist[t.Level], Quality.ViewDistanceMeters * 1.35f);
                float d = Distance2D(p, t.Center);
                t.Distance = d;
                bool tooFar = d > max + t.Data.EdgeMeters * 1.5f;
                bool deVoxel = t.Level == 0 && t.Data.IsVoxel &&
                               d > Quality.VoxelRadiusChunks * 32f + 64f;
                if (tooFar || deVoxel) kill.Add(t);
            }
            for (int i = 0; i < kill.Count; i++)
            {
                var t = kill[i];
                if (t.Level == 0 && t.Data != null && t.Data.IsVoxel &&
                    t.Distance <= Quality.VoxelRadiusChunks * 32f + 64f) continue;
                Unload(t);
            }
        }

        private void Unload(ChunkRuntime t)
        {
            _chunks.Remove(t.Key);
            if (t.State == ChunkState.Meshed || t.State == ChunkState.HasData) _needsMesh.Remove(t);
            t.Release();
        }

        // ================================================================== queries
        public static int FloorDiv(int a, int b)
        {
            int q = a / b;
            if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
            return q;
        }

        public static float Distance2D(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Any block in the world, near or far. This is the single source of truth.</summary>
        public byte GetBlock(int x, int y, int z)
        {
            if (y < 0 || y >= WorldConfig.ChunkHeight) return World.Blocks.Air;

            int size = WorldConfig.ChunkSize;
            int cx = FloorDiv(x, size);
            int cz = FloorDiv(z, size);

            ChunkRuntime t;
            if (_chunks.TryGetValue(ChunkRuntime.ChunkKey(cx, cz, 0), out t) &&
                t.Data != null && t.Data.IsVoxel)
            {
                return t.Data.GetBlockLocal(x - cx * size, y, z - cz * size);
            }
            return ProceduralBlock(x, y, z);
        }

        /// <summary>Fallback for cells outside the voxel radius - still exact, just recomputed.</summary>
        private byte ProceduralBlock(int x, int y, int z)
        {
            float h = _gen.HeightAt(x + 0.5f, z + 0.5f);
            int hi = Mathf.FloorToInt(h);
            int yi = y;

            bool node = DimensionState.NodeActive;
            int seaLevel = node ? Mathf.RoundToInt(NodeConfig.CreamSeaLevel)
                                : Mathf.RoundToInt(WorldConfig.SeaLevel);

            if (yi > hi) return yi <= seaLevel ? World.Blocks.Water : World.Blocks.Air;
            if (yi <= 0) return World.Blocks.Bedrock;

            var s = new ColumnSample();
            _gen.Sample(x + 0.5f, z + 0.5f, 2, ref s);
            BiomeType biome = s.Biome;
            BiomeInfo info = BiomeSystem.Get(biome);

            byte top = info.SurfaceBlock;
            if (node)
            {
                if (hi <= seaLevel) top = World.Blocks.Sand;
            }
            else
            {
                if (top == World.Blocks.Stone && biome != BiomeType.Rocky && biome != BiomeType.Mountain &&
                    biome != BiomeType.Mesa) top = World.Blocks.Grass;
                if (hi < seaLevel && (biome == BiomeType.DeepOcean || biome == BiomeType.Ocean)) top = World.Blocks.Gravel;
            }

            if (yi == hi) return top;
            if (yi >= hi - 3)
            {
                if (node) return top == World.Blocks.Sand ? World.Blocks.Sand : World.Blocks.CandyDirt;
                if (top == World.Blocks.Sand) return World.Blocks.Sand;
                if (biome == BiomeType.Desert) return World.Blocks.Sandstone;
                if (biome == BiomeType.Mesa) return World.Blocks.Terracotta;
                if (top == World.Blocks.Stone) return World.Blocks.Stone;
                return World.Blocks.Dirt;
            }
            return node ? World.Blocks.SherbetStone : World.Blocks.Stone;
        }

        public float GetTerrainHeight(int x, int z) { return _gen.HeightAt(x + 0.5f, z + 0.5f); }

        public BiomeType GetBiome(int x, int z)
        {
            var s = new ColumnSample();
            _gen.Sample(x + 0.5f, z + 0.5f, 1, ref s);
            return s.Biome;
        }

        public void SetBlock(int x, int y, int z, byte block)
        {
            BlockEdits.SetBlock(x, y, z, block);
        }

        public static bool IsSolidAt(int x, int y, int z)
        {
            var m = Instance;
            if (m == null) return false;
            byte b = m.GetBlock(x, y, z);
            return b != World.Blocks.Air && BlockDef.IsSolid(b);
        }

        /// <summary>Amanatides &amp; Woo voxel DDA. Far cheaper and far more precise than Physics.Raycast here.</summary>
        public bool RaycastBlocks(Vector3 origin, Vector3 dir, float maxDist, out BlockHit hit)
        {
            hit = new BlockHit();
            float len = dir.magnitude;
            if (len < 1e-5f) return false;
            dir /= len;

            int x = Mathf.FloorToInt(origin.x);
            int y = Mathf.FloorToInt(origin.y);
            int z = Mathf.FloorToInt(origin.z);

            int stepX = dir.x > 0 ? 1 : (dir.x < 0 ? -1 : 0);
            int stepY = dir.y > 0 ? 1 : (dir.y < 0 ? -1 : 0);
            int stepZ = dir.z > 0 ? 1 : (dir.z < 0 ? -1 : 0);

            float tMaxX = stepX == 0 ? float.PositiveInfinity : (stepX > 0 ? (x + 1 - origin.x) : (x - origin.x)) / dir.x;
            float tMaxY = stepY == 0 ? float.PositiveInfinity : (stepY > 0 ? (y + 1 - origin.y) : (y - origin.y)) / dir.y;
            float tMaxZ = stepZ == 0 ? float.PositiveInfinity : (stepZ > 0 ? (z + 1 - origin.z) : (z - origin.z)) / dir.z;
            float tDeltaX = stepX == 0 ? float.PositiveInfinity : Mathf.Abs(1f / dir.x);
            float tDeltaY = stepY == 0 ? float.PositiveInfinity : Mathf.Abs(1f / dir.y);
            float tDeltaZ = stepZ == 0 ? float.PositiveInfinity : Mathf.Abs(1f / dir.z);

            Vector3 normal = Vector3.zero;
            float t = 0f;

            for (int i = 0; i < 512; i++)
            {
                byte b = GetBlock(x, y, z);
                if (b != World.Blocks.Air && b != World.Blocks.Water)
                {
                    hit.Hit = true;
                    hit.X = x; hit.Y = y; hit.Z = z;
                    hit.Block = b;
                    hit.Normal = normal;
                    hit.Distance = t;
                    hit.Point = origin + dir * t;
                    return true;
                }

                if (tMaxX < tMaxY)
                {
                    if (tMaxX < tMaxZ) { x += stepX; t = tMaxX; tMaxX += tDeltaX; normal = new Vector3(-stepX, 0, 0); }
                    else { z += stepZ; t = tMaxZ; tMaxZ += tDeltaZ; normal = new Vector3(0, 0, -stepZ); }
                }
                else
                {
                    if (tMaxY < tMaxZ) { y += stepY; t = tMaxY; tMaxY += tDeltaY; normal = new Vector3(0, -stepY, 0); }
                    else { z += stepZ; t = tMaxZ; tMaxZ += tDeltaZ; normal = new Vector3(0, 0, -stepZ); }
                }

                if (t > maxDist) break;
            }
            return false;
        }
    }

    /// <summary>The 140 km world border. Clamps the player and draws the wall.</summary>
    public static class DGBorder
    {
        public static Vector3 Clamp(Vector3 p)
        {
            float lim = WorldConfig.WorldHalfExtent - 2f;
            if (p.x < -lim) p.x = -lim;
            if (p.x > lim) p.x = lim;
            if (p.z < -lim) p.z = -lim;
            if (p.z > lim) p.z = lim;
            return p;
        }

        public static float DistanceToEdge(Vector3 p)
        {
            float dx = WorldConfig.WorldHalfExtent - Mathf.Abs(p.x);
            float dz = WorldConfig.WorldHalfExtent - Mathf.Abs(p.z);
            return Mathf.Min(dx, dz);
        }
    }
}
