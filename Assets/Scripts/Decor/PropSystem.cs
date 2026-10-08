using System.Collections.Generic;
using UnityEngine;
using DivergentGenesis.Core;
using DivergentGenesis.World;
using DivergentGenesis.Render;

namespace DivergentGenesis.Decor
{
    public enum PropType : byte
    {
        OakTree = 0, BirchTree = 1, PineTree = 2, SavannaTree = 3,
        DeadTree = 4, Cactus = 5, Boulder = 6, Rock = 7, DeadBush = 8
    }

    public struct PropInstance
    {
        public PropType Type;
        public Vector3 Base;      // world position of the trunk base
        public float Scale;
        public float Yaw;
        public float Radius;      // collision / pick radius
        public float Height;
        public bool Removed;
    }

    public struct PropHit
    {
        public bool Hit;
        public Vector3 Point;
        public Vector3 Normal;
        public int Cx, Cz, Level;
        public int Index;
        public PropType Type;
        public float Distance;
    }

    /// <summary>
    /// Every tree, boulder and cactus in the 140 km world, generated per tile and
    /// drawn with one MultiMesh per (tile, type). Break a tree and it rebuilds
    /// instantly; walk into a trunk and you get pushed out.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class PropSystem : MonoBehaviour
    {
        public static PropSystem Instance;

        private sealed class Tile
        {
            public int Cx, Cz, Level;
            public GameObject Go;
            public Transform T;
            public readonly List<PropInstance> Instances = new List<PropInstance>(64);
            public readonly List<PropType> Types = new List<PropType>(4);
            public readonly List<MultiMesh> Meshes = new List<MultiMesh>(4);
            public readonly List<MultiMeshRenderer> Renderers = new List<MultiMeshRenderer>(4);
            public Matrix4x4[] MatrixBuffer = new Matrix4x4[64];
        }

        private readonly Dictionary<long, Tile> _tiles = new Dictionary<long, Tile>(128);
        private static readonly Mesh[] PropMeshes = new Mesh[9];
        private static readonly float[] PropRadii = { 0.42f, 0.32f, 0.40f, 0.40f, 0.30f, 0.34f, 0f, 0f, 0f };
        private static readonly float[] PropHeights = { 7.5f, 8.0f, 10.0f, 6.0f, 5.0f, 4.0f, 2.2f, 0.8f, 0.8f };

        private Transform _root;
        private TerrainGenerator _gen;
        private int _seed;
        private QualityProfile _quality;
        private int _frame;

        public int TileCount { get { return _tiles.Count; } }

        private void Awake()
        {
            Instance = this;
            var go = new GameObject("Props");
            go.transform.SetParent(transform, false);
            _root = go.transform;
            MaterialLibrary.Ensure();
        }

        public void Configure(int seed, QualityProfile quality)
        {
            _seed = seed;
            _quality = quality;
            _gen = new TerrainGenerator(seed);
            Clear();
        }

        public void Clear()
        {
            foreach (var kv in _tiles)
            {
                if (kv.Value.Go != null) Destroy(kv.Value.Go);
                foreach (var mm in kv.Value.Meshes) if (mm != null) Destroy(mm);
            }
            _tiles.Clear();
        }

        // ================================================================ update
        public void UpdateStreaming(Vector3 playerPos, int viewDistanceMeters)
        {
            _frame++;
            _gen = _gen ?? new TerrainGenerator(_seed);

            int levels = QualityProfile.LevelCount;
            float baseRange = Mathf.Min(_quality != null ? _quality.LevelMaxDist[1] : 320f, viewDistanceMeters);

            for (int level = 0; level < 2; level++)
            {
                int edge = QualityProfile.EdgeForLevel(level);
                float maxD = level == 0
                    ? (_quality != null ? _quality.LevelMaxDist[0] : 96f)
                    : baseRange;
                int radius = Mathf.CeilToInt(maxD / edge);
                int ccx = Mathf.FloorToInt(playerPos.x / edge);
                int ccz = Mathf.FloorToInt(playerPos.z / edge);

                var wanted = new List<long>(64);
                for (int dz = -radius; dz <= radius; dz++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int cx = ccx + dx, cz = ccz + dz;
                    float wx = cx * edge + edge * 0.5f, wz = cz * edge + edge * 0.5f;
                    float d = ChunkManager.Distance2D(playerPos, new Vector3(wx, 0f, wz));
                    if (d > maxD) continue;
                    if (level == 1 && d < (_quality != null ? _quality.LevelMaxDist[0] : 96f) * 0.8f) continue;

                    long key = ChunkRuntime.ChunkKey(cx, cz, level);
                    wanted.Add(key);
                    if (!_tiles.ContainsKey(key)) BuildTile(cx, cz, level);
                }

                // drop props that fell out of range
                if ((_frame & 31) == 0) Prune(wanted, level);
            }
        }

        private void Prune(List<long> wanted, int level)
        {
            var kill = new List<Tile>();
            foreach (var kv in _tiles)
            {
                if (kv.Value.Level != level) continue;
                if (!wanted.Contains(kv.Key)) kill.Add(kv.Value);
            }
            for (int i = 0; i < kill.Count; i++) DestroyTile(kill[i]);
        }

        private void DestroyTile(Tile t)
        {
            _tiles.Remove(ChunkRuntime.ChunkKey(t.Cx, t.Cz, t.Level));
            if (t.Go != null) Destroy(t.Go);
            foreach (var mm in t.Meshes) if (mm != null) Destroy(mm);
        }

        // ============================================================= generation
        private void BuildTile(int cx, int cz, int level)
        {
            var tile = new Tile { Cx = cx, Cz = cz, Level = level };
            _tiles[ChunkRuntime.ChunkKey(cx, cz, level)] = tile;

            int edge = QualityProfile.EdgeForLevel(level);
            int wx0 = cx * edge, wz0 = cz * edge;

            int chunkSeed = Hash.SeedForChunk(cx, cz, _seed + level * 977);
            var rng = new Rng(chunkSeed);

            float treeScale = (_quality != null ? _quality.TreeDensity : 85) / 100f;
            float propScale = (_quality != null ? _quality.PropDensity : 80) / 100f;
            if (level == 1) { treeScale *= 0.5f; propScale *= 0.55f; }

            float areaChunks = (edge / 32f) * (edge / 32f);

            var column = new ColumnSample();
            float sampleX = wx0 + 0.5f, sampleZ = wz0 + 0.5f;
            _gen.Sample(sampleX, sampleZ, level == 0 ? 0 : 1, ref column);
            BiomeInfo biome = BiomeSystem.Get(column.Biome);

            int treeTarget = Mathf.RoundToInt(biome.TreeDensity * treeScale * areaChunks);
            int propTarget = Mathf.RoundToInt(6f * propScale * areaChunks);

            Place(tile, rng, treeTarget, edge, wx0, wz0, level, true);
            Place(tile, rng, propTarget, edge, wx0, wz0, level, false);

            BuildRenderers(tile, edge);
        }

        private void Place(Tile tile, Rng rng, int target, int edge, int wx0, int wz0, int level, bool trees)
        {
            // The Node grows its scenery as blocks (gumdrop groves, cotton clouds),
            // so the prop pass stays out of it entirely.
            if (trees && DimensionState.NodeActive) return;

            bool node = DimensionState.NodeActive;
            float sea = node ? NodeConfig.CreamSeaLevel : WorldConfig.SeaLevel;

            for (int i = 0; i < target; i++)
            {
                float fx = rng.NextFloat();
                float fz = rng.NextFloat();
                int wx = wx0 + Mathf.FloorToInt(fx * edge);
                int wz = wz0 + Mathf.FloorToInt(fz * edge);

                var column = new ColumnSample();
                _gen.Sample(wx + 0.5f, wz + 0.5f, 1, ref column);
                float h = column.Height;
                if (h < sea + 0.6f) continue;                             // no trees in the sea
                if (h > 138f) continue;                                   // nothing on the ice caps

                // reject steep ground
                float h1 = _gen.HeightAt(wx + 3.5f, wz + 0.5f);
                float h2 = _gen.HeightAt(wx - 3.5f, wz + 0.5f);
                float h3 = _gen.HeightAt(wx + 0.5f, wz + 3.5f);
                float h4 = _gen.HeightAt(wx + 0.5f, wz - 3.5f);
                float slope = Mathf.Max(Mathf.Max(Mathf.Abs(h1 - h), Mathf.Abs(h2 - h)),
                                        Mathf.Max(Mathf.Abs(h3 - h), Mathf.Abs(h4 - h)));
                if (slope > (trees ? 3.2f : 6f)) continue;

                // never plant a forest through a village
                if (StructureGenerator.InsideStructure(wx + 0.5f, wz + 0.5f, _seed, _gen, node)) continue;

                PropType type;
                if (trees)
                {
                    type = PickTree(column, rng);
                    if (type == PropType.DeadTree && rng.Chance(0.45f)) continue;
                }
                else
                {
                    type = rng.Chance(0.28f) ? PropType.Rock : PropType.Boulder;
                    if (h < sea + 1.5f) type = PropType.Rock;
                }

                int idx = (int)type;
                var inst = new PropInstance
                {
                    Type = type,
                    Base = new Vector3(wx, Mathf.FloorToInt(h), wz),
                    Scale = rng.Range(0.85f, 1.25f),
                    Yaw = rng.NextFloat() * 360f,
                    Radius = PropRadii[idx] * rng.Range(0.9f, 1.15f),
                    Height = PropHeights[idx],
                    Removed = false
                };

                tile.Instances.Add(inst);
                if (!tile.Types.Contains(type))
                {
                    tile.Types.Add(type);
                    tile.Meshes.Add(null);
                    tile.Renderers.Add(null);
                }
            }
        }

        private static PropType PickTree(in ColumnSample column, Rng rng)
        {
            switch (column.Biome)
            {
                case BiomeType.Taiga:
                case BiomeType.SnowForest:
                    return PropType.PineTree;
                case BiomeType.BirchForest:
                case BiomeType.Plains:
                case BiomeType.WetPlains:
                    return rng.Chance(0.55f) ? PropType.BirchTree : PropType.OakTree;
                case BiomeType.Savanna:
                case BiomeType.Mesa:
                    return PropType.SavannaTree;
                case BiomeType.Desert:
                    return rng.Chance(0.8f) ? PropType.Cactus : PropType.DeadTree;
                case BiomeType.Swamp:
                    return rng.Chance(0.35f) ? PropType.DeadTree : PropType.OakTree;
                case BiomeType.Rocky:
                case BiomeType.Mountain:
                    return rng.Chance(0.6f) ? PropType.PineTree : PropType.DeadTree;
                default:
                    return rng.Chance(0.75f) ? PropType.OakTree : PropType.BirchTree;
            }
        }

        // =============================================================== rendering
        private void BuildRenderers(Tile tile, int edge)
        {
            tile.Go = new GameObject("Props_L" + tile.Level + "_" + tile.Cx + "_" + tile.Cz);
            tile.Go.transform.SetParent(_root, false);
            tile.T = tile.Go.transform;
            tile.T.position = new Vector3(tile.Cx * edge, 0f, tile.Cz * edge);

            for (int t = 0; t < tile.Types.Count; t++)
            {
                PropType type = tile.Types[t];
                int count = 0;
                for (int i = 0; i < tile.Instances.Count; i++)
                    if (tile.Instances[i].Type == type && !tile.Instances[i].Removed) count++;

                if (count == 0) continue;

                var mm = tile.Meshes[t] ?? new MultiMesh();
                tile.Meshes[t] = mm;
                mm.Clear();
                FillMatrixBuffer(tile, type, count);
                mm.instanceCount = count;
                mm.SetTransforms(tile.MatrixBuffer);
                mm.mesh = GetPropMesh(type);

                var go = new GameObject(type.ToString());
                go.transform.SetParent(tile.T, false);
                var mr = go.AddComponent<MultiMeshRenderer>();
                mr.sharedMaterial = MaterialLibrary.Terrain;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                tile.Renderers[t] = mr;
            }
        }

        private static void FillMatrixBuffer(Tile tile, PropType type, int count)
        {
            if (tile.MatrixBuffer.Length < count)
                tile.MatrixBuffer = new Matrix4x4[Mathf.NextPowerOfTwo(Mathf.Max(64, count))];

            int n = 0;
            for (int i = 0; i < tile.Instances.Count; i++)
            {
                var inst = tile.Instances[i];
                if (inst.Type != type || inst.Removed) continue;
                tile.MatrixBuffer[n] = Matrix4x4.TRS(inst.Base, Quaternion.Euler(0f, inst.Yaw, 0f), Vector3.one * inst.Scale);
                n++;
            }
        }

        public static Mesh GetPropMesh(PropType type)
        {
            int i = (int)type;
            if (PropMeshes[i] != null) return PropMeshes[i];
            PropMeshes[i] = BuildPropMesh(type);
            return PropMeshes[i];
        }

        private static Mesh BuildPropMesh(PropType type)
        {
            var b = new PrimitiveMesher.Builder();
            var bark = new Color32(104, 78, 48, 255);
            var barkLight = new Color32(126, 98, 62, 255);
            var leafGreen = new Color32(74, 132, 56, 255);
            var leafLight = new Color32(96, 158, 66, 255);
            var leafDark = new Color32(48, 104, 54, 255);
            var leafPine = new Color32(46, 92, 62, 255);
            var leafPineLight = new Color32(62, 112, 74, 255);
            var leafOlive = new Color32(140, 148, 70, 255);
            var stone = new Color32(126, 126, 130, 255);
            var stoneDark = new Color32(98, 98, 102, 255);

            switch (type)
            {
                case PropType.OakTree:
                    b.Cylinder(Vector3.zero, 0.34f, 0.26f, 4.6f, 7, bark);
                    b.Sphere(new Vector3(0, 5.4f, 0), 1.9f, 7, 5, leafGreen, 0.78f);
                    b.Sphere(new Vector3(0.9f, 4.7f, 0.5f), 1.25f, 6, 4, leafLight, 0.8f);
                    b.Sphere(new Vector3(-0.8f, 4.9f, -0.6f), 1.15f, 6, 4, leafDark, 0.8f);
                    b.Sphere(new Vector3(0.1f, 6.6f, -0.2f), 1.1f, 6, 4, leafLight, 0.8f);
                    break;

                case PropType.BirchTree:
                    b.Cylinder(Vector3.zero, 0.26f, 0.20f, 6.0f, 6, new Color32(226, 222, 210, 255));
                    b.Sphere(new Vector3(0, 6.8f, 0), 1.6f, 7, 5, new Color32(140, 176, 92, 255), 0.85f);
                    b.Sphere(new Vector3(0.7f, 6.0f, 0.6f), 1.05f, 6, 4, new Color32(160, 192, 104, 255), 0.85f);
                    break;

                case PropType.PineTree:
                    b.Cylinder(Vector3.zero, 0.30f, 0.20f, 9.0f, 6, barkLight);
                    b.Cylinder(new Vector3(0, 2.2f, 0), 2.3f, 0f, 2.8f, 8, leafPine);
                    b.Cylinder(new Vector3(0, 4.2f, 0), 1.85f, 0f, 2.6f, 8, leafPineLight);
                    b.Cylinder(new Vector3(0, 6.1f, 0), 1.35f, 0f, 2.4f, 8, leafPine);
                    b.Sphere(new Vector3(0, 8.6f, 0), 0.75f, 6, 4, leafPineLight, 1.1f);
                    break;

                case PropType.SavannaTree:
                    b.Cylinder(Vector3.zero, 0.32f, 0.24f, 3.4f, 6, new Color32(118, 96, 62, 255));
                    b.Sphere(new Vector3(0, 4.2f, 0), 2.4f, 8, 4, leafOlive, 0.45f);
                    b.Sphere(new Vector3(1.1f, 3.9f, 0.3f), 1.3f, 6, 4, new Color32(158, 162, 82, 255), 0.45f);
                    break;

                case PropType.DeadTree:
                    b.Cylinder(Vector3.zero, 0.28f, 0.16f, 4.2f, 6, new Color32(96, 84, 68, 255));
                    b.Cylinder(new Vector3(0, 2.6f, 0), 0.10f, 0.05f, 1.7f, 4, new Color32(96, 84, 68, 255));
                    b.Cylinder(new Vector3(0.5f, 3.2f, 0.2f), 0.10f, 0.04f, 1.4f, 4, new Color32(88, 78, 64, 255));
                    b.Cylinder(new Vector3(-0.4f, 3.6f, -0.3f), 0.09f, 0.04f, 1.2f, 4, new Color32(88, 78, 64, 255));
                    break;

                case PropType.Cactus:
                    b.Box(new Vector3(0, 1.5f, 0), new Vector3(0.7f, 3.0f, 0.7f), new Color32(58, 122, 62, 255));
                    b.Box(new Vector3(0.55f, 1.1f, 0), new Vector3(0.9f, 0.4f, 0.4f), new Color32(58, 122, 62, 255));
                    b.Box(new Vector3(0.95f, 1.7f, 0), new Vector3(0.4f, 1.2f, 0.4f), new Color32(64, 132, 66, 255));
                    break;

                case PropType.Boulder:
                    b.Sphere(new Vector3(0, 0.75f, 0), 1.5f, 7, 5, stone, 0.72f);
                    b.Sphere(new Vector3(0.7f, 0.42f, 0.4f), 0.7f, 6, 4, stoneDark, 0.8f);
                    break;

                case PropType.Rock:
                    b.Sphere(new Vector3(0, 0.22f, 0), 0.55f, 6, 4, stoneDark, 0.7f);
                    break;

                default: // DeadBush
                    b.Box(new Vector3(0, 0.30f, 0), new Vector3(0.55f, 0.60f, 0.55f), new Color32(128, 104, 58, 255));
                    b.Box(new Vector3(0.22f, 0.22f, -0.18f), new Vector3(0.40f, 0.44f, 0.40f), new Color32(116, 94, 52, 255));
                    break;
            }

            return b.ToMesh("Prop_" + type);
        }

        // ============================================================ interaction
        /// <summary>Ray vs vertical cylinders. Used for chopping trees.</summary>
        public bool Raycast(Vector3 origin, Vector3 dir, float maxDist, out PropHit hit)
        {
            hit = new PropHit();
            bool found = false;
            float best = maxDist;

            int edge = WorldConfig.ChunkSize;
            int ccx = Mathf.FloorToInt(origin.x / edge);
            int ccz = Mathf.FloorToInt(origin.z / edge);

            for (int dz = -1; dz <= 1 && !found; dz++)
            for (int dx = -1; dx <= 1 && !found; dx++)
            {
                Tile t;
                if (!_tiles.TryGetValue(ChunkRuntime.ChunkKey(ccx + dx, ccz + dz, 0), out t)) continue;

                for (int i = 0; i < t.Instances.Count; i++)
                {
                    var inst = t.Instances[i];
                    if (inst.Removed || inst.Radius <= 0.01f) continue;

                    Vector3 c = inst.Base + new Vector3(0f, inst.Height * inst.Scale * 0.5f, 0f);
                    float r = inst.Radius * inst.Scale * 1.6f;
                    float h = inst.Height * inst.Scale;

                    // ray vs vertical cylinder
                    Vector2 o = new Vector2(origin.x - c.x, origin.z - c.z);
                    Vector2 d = new Vector2(dir.x, dir.z);
                    float a = d.sqrMagnitude;
                    if (a < 1e-8f) continue;
                    float bq = Vector2.Dot(o, d);
                    float cq = o.sqrMagnitude - r * r;
                    float disc = bq * bq - a * cq;
                    if (disc < 0f) continue;

                    float sq = Mathf.Sqrt(disc);
                    float t0 = (-bq - sq) / a;
                    float t1 = (-bq + sq) / a;
                    if (t1 < 0f) continue;
                    if (t0 < 0f) t0 = 0f;
                    if (t0 >= best) continue;

                    float y = origin.y + dir.y * t0;
                    if (y < c.y - h * 0.5f || y > c.y + h * 0.5f) continue;

                    best = t0;
                    hit.Hit = true;
                    hit.Point = origin + dir * t0;
                    hit.Normal = new Vector3(origin.x - c.x, 0f, origin.z - c.z).normalized;
                    hit.Cx = t.Cx; hit.Cz = t.Cz; hit.Level = t.Level;
                    hit.Index = i; hit.Type = inst.Type; hit.Distance = t0;
                    found = true;
                }
            }
            return hit.Hit;
        }

        /// <summary>Removes a prop and rebuilds its tile. Returns what it was.</summary>
        public PropType Remove(int cx, int cz, int level, int index)
        {
            Tile t;
            if (!_tiles.TryGetValue(ChunkRuntime.ChunkKey(cx, cz, level), out t)) return PropType.Rock;
            if (index < 0 || index >= t.Instances.Count) return PropType.Rock;
            if (t.Instances[index].Removed) return PropType.Rock;

            PropType type = t.Instances[index].Type;
            var inst = t.Instances[index];
            inst.Removed = true;
            t.Instances[index] = inst;

            BuildRenderers(t, QualityProfile.EdgeForLevel(level));
            return type;
        }

        /// <summary>Pushes a capsule out of tree trunks. Cheap because only one tile is scanned.</summary>
        public void ResolveCollision(ref Vector3 pos, float radius)
        {
            int edge = WorldConfig.ChunkSize;
            Tile t;
            if (!_tiles.TryGetValue(ChunkRuntime.ChunkKey(
                    Mathf.FloorToInt(pos.x / edge), Mathf.FloorToInt(pos.z / edge), 0), out t)) return;

            for (int i = 0; i < t.Instances.Count; i++)
            {
                var inst = t.Instances[i];
                if (inst.Removed || inst.Radius <= 0.01f) continue;

                float dx = pos.x - inst.Base.x;
                float dz = pos.z - inst.Base.z;
                float minD = (inst.Radius + radius) * inst.Scale;
                float d2 = dx * dx + dz * dz;
                if (d2 >= minD * minD || d2 < 1e-6f) continue;

                float d = Mathf.Sqrt(d2);
                float push = (minD - d) / d;
                pos.x += dx * push;
                pos.z += dz * push;
            }
        }
    }
}
