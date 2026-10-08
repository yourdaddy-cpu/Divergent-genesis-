using UnityEngine;
using DivergentGenesis.Core;
using DivergentGenesis.Render;

namespace DivergentGenesis.Environment
{
    /// <summary>
    /// Day / night cycle, sky gradient, fog and the 140 km world border wall.
    /// Drives the shaders directly so the whole scene stays in sync with time of day.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public sealed class SkyController : MonoBehaviour
    {
        public static SkyController Instance;

        [Header("Time")]
        public float DayLength = WorldConfig.DayLengthSeconds;
        [Range(0f, 1f)] public float TimeOfDay = 0.28f;   // 0 = midnight, 0.25 = sunrise
        public bool TimeRunning = true;

        [Header("References")]
        public Transform Player;
        public Transform BorderRoot;

        private Material _skyMat;
        private Mesh _skyMesh;
        private Transform _sky;
        private Vector3 _sunDir = new Vector3(0.3f, 0.8f, 0.4f);
        private Color _sunColor = Color.white;
        private Color _ambient = Color.gray;
        private Color _fog = new Color(0.62f, 0.74f, 0.88f);

        public Vector3 SunDirection { get { return _sunDir; } }
        public float TimeInSeconds { get { return TimeOfDay * DayLength; } }

        // key colours sampled through the day
        private static readonly Color ZenithNight = new Color(0.02f, 0.03f, 0.09f);
        private static readonly Color ZenithDay = new Color(0.24f, 0.48f, 0.86f);
        private static readonly Color HorizonNight = new Color(0.05f, 0.06f, 0.14f);
        private static readonly Color HorizonDay = new Color(0.70f, 0.84f, 0.97f);
        private static readonly Color DawnGlow = new Color(0.98f, 0.55f, 0.32f);

        private void Awake()
        {
            Instance = this;
            MaterialLibrary.Ensure();
            BuildSkyDome();
            BuildBorder();
        }

        private void Update()
        {
            if (TimeRunning) TimeOfDay = Mathf.Repeat(TimeOfDay + Time.deltaTime / DayLength, 1f);

            float angle = (TimeOfDay - 0.25f) * Mathf.PI * 2f;
            _sunDir = new Vector3(Mathf.Cos(angle) * 0.45f, Mathf.Sin(angle), Mathf.Cos(angle) * 0.35f).normalized;
            if (_sunDir.y < -0.12f) _sunDir = -_sunDir;   // stop the sun going below the horizon

            float day = Mathf.Clamp01(_sunDir.y * 2.2f + 0.18f);
            float dawn = Mathf.Clamp01(1f - Mathf.Abs(_sunDir.y) * 4.5f) *
                         Mathf.Clamp01(1f - Mathf.Abs(TimeOfDay - 0.25f) * 8f);

            _sunColor = Color.Lerp(new Color(0.32f, 0.36f, 0.52f), new Color(1f, 0.97f, 0.90f), day);
            _sunColor = Color.Lerp(_sunColor, DawnGlow, dawn * 0.8f);
            _ambient = Color.Lerp(new Color(0.16f, 0.19f, 0.30f), new Color(0.50f, 0.57f, 0.68f), day);
            _ambient = Color.Lerp(_ambient, new Color(0.42f, 0.30f, 0.32f), dawn * 0.6f);

            _fog = Color.Lerp(HorizonNight, HorizonDay, day);
            _fog = Color.Lerp(_fog, DawnGlow * 0.85f, dawn * 0.55f);

            // dimension palette + ritual corruption, applied on top of the cycle
            SkyFX.Tick(Time.deltaTime);
            Color zenithCol = Color.Lerp(ZenithNight, ZenithDay, day);
            Color horizonCol = Color.Lerp(HorizonNight, HorizonDay, day);
            SkyFX.Apply(ref zenithCol, ref horizonCol, ref _fog, ref _ambient, ref _sunColor, day);

            float fogDensity = Mathf.Lerp(0.0028f, 0.0011f, day) * SkyFX.FogScale(DimensionState.NodeActive);

            MaterialLibrary.ApplyLighting(_sunDir, _sunColor, _ambient, _fog, fogDensity);

            if (_skyMat != null)
            {
                float h = Mathf.Clamp01(_sunDir.y);
                _skyMat.SetColor("_Zenith", zenithCol);
                _skyMat.SetColor("_Horizon", horizonCol);
                _skyMat.SetColor("_SunColor", _sunColor);
                _skyMat.SetVector("_SunDir", new Vector4(_sunDir.x, _sunDir.y, _sunDir.z, 0f));
                _skyMat.SetFloat("_Time", Time.time);

                // the tear: purple, cracked, and brighter the closer the boss is
                float crack = SkyFX.CrackAmount;
                _skyMat.SetFloat("_Corrupt", SkyFX.Corruption);
                _skyMat.SetFloat("_Cracks", crack);
                _skyMat.SetFloat("_CrackSeed", SkyFX.CrackSeed);
                _skyMat.SetColor("_CrackColor", NodeConfig.CrackColor);
                _skyMat.SetFloat("_Flare", SkyFX.Flare);
            }

            if (_sky != null && Player != null)
            {
                _sky.position = new Vector3(Player.position.x, _sky.position.y, Player.position.z);
            }
        }

        public void SetTimeOfDay(float t) { TimeOfDay = Mathf.Repeat(t, 1f); }

        // ------------------------------------------------------------ sky dome
        private void BuildSkyDome()
        {
            var sh = Shader.Find("DG/Sky");
            if (sh == null) sh = MaterialLibrary.LoadShader("DGTerrain");
            _skyMat = new Material(sh) { name = "DG_Sky" };

            var go = new GameObject("SkyDome");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, 0f, 0f);
            _sky = go.transform;

            _skyMesh = BuildDome();
            go.AddComponent<MeshFilter>().sharedMesh = _skyMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _skyMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        }

        private static Mesh BuildDome()
        {
            const int segs = 20;
            const int rings = 10;
            float radius = 3000f;
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            var uvs = new System.Collections.Generic.List<Vector2>();

            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings;
                float phi = v * Mathf.PI * 0.5f;
                for (int s = 0; s <= segs; s++)
                {
                    float u = s / (float)segs;
                    float theta = u * Mathf.PI * 2f;
                    float y = Mathf.Cos(phi);
                    float rad = Mathf.Sin(phi);
                    verts.Add(new Vector3(Mathf.Cos(theta) * rad, y, Mathf.Sin(theta) * rad) * radius);
                    uvs.Add(new Vector2(u, y));
                }
            }

            int stride = segs + 1;
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < segs; s++)
            {
                int a = r * stride + s;
                int b = a + 1;
                int c = a + stride;
                int d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }

            var m = new Mesh { name = "SkyDome" };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.SetUVs(0, uvs);
            m.RecalculateBounds();
            return m;
        }

        // ---------------------------------------------------------- world border
        private void BuildBorder()
        {
            var rootGo = new GameObject("WorldBorder");
            rootGo.transform.SetParent(transform, false);
            BorderRoot = rootGo.transform;

            float lim = WorldConfig.WorldHalfExtent;
            float h = WorldConfig.BorderWallHeight;
            float w = WorldConfig.MaxTerrainHeight + 60f;
            var b = new Decor.PrimitiveMesher.Builder();
            Color32 glow = new Color32(180, 60, 90, 255);

            // four walls, each a thin box just inside the limit
            b.Box(new Vector3(0f, w * 0.5f, -lim - 6f), new Vector3(lim * 2f + 24f, w, 6f), glow);
            b.Box(new Vector3(0f, w * 0.5f, lim + 6f), new Vector3(lim * 2f + 24f, w, 6f), glow);
            b.Box(new Vector3(-lim - 6f, w * 0.5f, 0f), new Vector3(6f, w, lim * 2f + 24f), glow);
            b.Box(new Vector3(lim + 6f, w * 0.5f, 0f), new Vector3(6f, w, lim * 2f + 24f), glow);

            var go = new GameObject("BorderWalls");
            go.transform.SetParent(BorderRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("BorderWalls");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialLibrary.BorderWall;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
