using UnityEngine;
using DivergentGenesis.Core;
using DivergentGenesis.World;
using DivergentGenesis.Render;

namespace DivergentGenesis.Decor
{
    /// <summary>
    /// Grass tufts, flowers and dead bushes, drawn as one MultiMesh that follows
    /// the player. Only rebuilt when you actually move, so walking costs nothing.
    /// </summary>
    public sealed class GrassSystem : MonoBehaviour
    {
        public static GrassSystem Instance;

        public float Radius = 26f;
        public int CellSize = 2;
        public int TuftsPerCell = 7;

        private MultiMesh _multi;
        private MultiMeshRenderer _renderer;
        private Matrix4x4[] _buffer;
        private Vector3 _lastBuildPos = new Vector3(float.NaN, 0f, float.NaN);
        private int _seed;
        private int _density = 100;
        private int _frame;

        public int InstanceCount { get; private set; }

        private void Awake()
        {
            Instance = this;
            MaterialLibrary.Ensure();

            _multi = new MultiMesh();
            _multi.mesh = BuildTuft();
            _buffer = new Matrix4x4[6000];

            var go = new GameObject("Grass");
            go.transform.SetParent(transform, false);
            _renderer = go.AddComponent<MultiMeshRenderer>();
            _renderer.sharedMaterial = MaterialLibrary.Foliage;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        }

        public void Configure(int seed, QualityProfile quality)
        {
            _seed = seed;
            _density = quality != null ? quality.GrassDensity : 0;
            _lastBuildPos = new Vector3(float.NaN, 0f, float.NaN);
        }

        public void SetDensity(int density)
        {
            _density = density;
            _lastBuildPos = new Vector3(float.NaN, 0f, float.NaN);
        }

        private void LateUpdate()
        {
            _frame++;
            if (_frame % 8 != 0) return;
            if (_density <= 0) { _multi.instanceCount = 0; return; }

            var player = DivergentGenesis.Player.PlayerController.Instance;
            if (player == null) return;

            Vector3 p = player.transform.position;
            if ((p - _lastBuildPos).sqrMagnitude < 64f) return;   // 8 m
            Rebuild(p);
        }

        private void Rebuild(Vector3 center)
        {
            _lastBuildPos = center;
            var world = ChunkManager.Instance;
            if (world == null) { _multi.instanceCount = 0; return; }

            int n = 0;
            int cap = _buffer.Length;
            float r = Radius;
            int cells = Mathf.CeilToInt(r / CellSize);

            var column = new ColumnSample();
            var gen = new TerrainGenerator(_seed);

            for (int cz = -cells; cz <= cells; cz++)
            for (int cx = -cells; cx <= cells; cx++)
            {
                int wx = Mathf.FloorToInt(center.x) + cx * CellSize;
                int wz = Mathf.FloorToInt(center.z) + cz * CellSize;

                gen.Sample(wx + 0.5f, wz + 0.5f, 0, ref column);
                if (column.Height < WorldConfig.SeaLevel + 0.8f) continue;
                if (column.Height > 132f) continue;

                BiomeInfo info = BiomeSystem.Get(column.Biome);
                float localDensity = info.GrassDensity * (_density / 100f);
                if (localDensity <= 0.02f) continue;

                int count = Mathf.RoundToInt(TuftsPerCell * localDensity);
                for (int i = 0; i < count && n < cap; i++)
                {
                    float ox = Hash.Float01(wx, wz * 31 + i, _seed + 77);
                    float oz = Hash.Float01(wz, wx * 17 + i, _seed + 313);
                    float px = wx + ox * CellSize;
                    float pz = wz + oz * CellSize;

                    float h = gen.HeightAt(px, pz);
                    if (h < WorldConfig.SeaLevel + 0.8f) continue;

                    float rot = Hash.Float01((int)px, (int)pz, _seed + 991) * 360f;
                    float scale = 0.7f + Hash.Float01((int)pz, (int)px, _seed + 55) * 0.7f;

                    _buffer[n] = Matrix4x4.TRS(
                        new Vector3(px, h, pz),
                        Quaternion.Euler(0f, rot, 0f),
                        Vector3.one * scale);
                    n++;
                }
            }

            InstanceCount = n;
            _multi.Clear();
            _multi.instanceCount = n;
            if (n > 0) _multi.SetTransforms(_buffer);
        }

        private static Mesh BuildTuft()
        {
            var b = new PrimitiveMesher.Builder();
            Color32 g1 = new Color32(132, 188, 82, 255);
            Color32 g2 = new Color32(104, 160, 66, 255);

            // two crossed blades, each with uv.x inside the shader's blade band
            AddBlade(b, 0f, 0f, g1);
            AddBlade(b, 90f, 0f, g2);
            return b.ToMesh("GrassTuft");
        }

        private static void AddBlade(PrimitiveMesher.Builder b, float yaw, float z, Color32 col)
        {
            float h = 0.55f;
            float w = 0.30f;
            float c = Mathf.Cos(yaw * Mathf.Deg2Rad), s = Mathf.Sin(yaw * Mathf.Deg2Rad);
            Vector3 right = new Vector3(c, 0f, -s);
            Vector3 fwd = new Vector3(s, 0f, c);

            Vector3 baseMid = new Vector3(0f, 0f, z);
            Vector3 topMid = baseMid + new Vector3(0f, h, 0f);

            AddQuad(b, baseMid - right * w, baseMid + right * w, topMid + right * w * 0.35f, topMid - right * w * 0.35f, col);
            AddQuad(b, baseMid + fwd * w, baseMid - fwd * w, topMid - fwd * w * 0.35f, topMid + fwd * w * 0.35f, col);
        }

        private static void AddQuad(PrimitiveMesher.Builder b, Vector3 a, Vector3 c, Vector3 d, Vector3 e, Color32 col)
        {
            int i = b.V.Count;
            b.V.Add(a); b.V.Add(c); b.V.Add(d); b.V.Add(e);
            for (int k = 0; k < 4; k++) b.N.Add(Vector3.up);
            for (int k = 0; k < 4; k++) b.C.Add(col);
            // uv.x stays inside 0.18..0.62 so the foliage shader's blade mask passes
            b.Uv.Add(new Vector2(0.30f, 0f)); b.Uv.Add(new Vector2(0.50f, 0f));
            b.Uv.Add(new Vector2(0.50f, 1f)); b.Uv.Add(new Vector2(0.30f, 1f));
            b.T.Add(i); b.T.Add(i + 1); b.T.Add(i + 2);
            b.T.Add(i); b.T.Add(i + 2); b.T.Add(i + 3);
        }
    }
}
