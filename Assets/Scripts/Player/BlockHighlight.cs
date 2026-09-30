using UnityEngine;
using DivergentGenesis.Render;

namespace DivergentGenesis.Player
{
    /// <summary>The wireframe box around whatever you are looking at.</summary>
    public sealed class BlockHighlight : MonoBehaviour
    {
        private MeshFilter _filter;
        private MeshRenderer _renderer;

        public static BlockHighlight Create()
        {
            var go = new GameObject("BlockHighlight");
            var h = go.AddComponent<BlockHighlight>();
            h.Build();
            return h;
        }

        private void Build()
        {
            _filter = gameObject.AddComponent<MeshFilter>();
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = MaterialLibrary.Highlight;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _filter.sharedMesh = BuildWireCube();
        }

        private static Mesh BuildWireCube()
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();

            Vector3[] c =
            {
                new Vector3(-0.5f,-0.5f,-0.5f), new Vector3( 0.5f,-0.5f,-0.5f),
                new Vector3( 0.5f, 0.5f,-0.5f), new Vector3(-0.5f, 0.5f,-0.5f),
                new Vector3(-0.5f,-0.5f, 0.5f), new Vector3( 0.5f,-0.5f, 0.5f),
                new Vector3( 0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
            };
            int[,] edges =
            {
                {0,1},{1,2},{2,3},{3,0},
                {4,5},{5,6},{6,7},{7,4},
                {0,4},{1,5},{2,6},{3,7}
            };

            const float t = 0.018f;
            for (int e = 0; e < 12; e++)
            {
                Vector3 a = c[edges[e, 0]];
                Vector3 b = c[edges[e, 1]];
                Vector3 dir = (b - a).normalized;
                Vector3 up = Mathf.Abs(dir.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 side = Vector3.Cross(dir, up).normalized;

                Vector3 o0 = a - dir * t, o1 = b + dir * t;
                Vector3 s0 = side * t, s1 = -side * t;
                int i = verts.Count;
                verts.Add(o0 + s0); verts.Add(o1 + s0); verts.Add(o1 - s0); verts.Add(o0 - s0);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
                tris.Add(i); tris.Add(i + 3); tris.Add(i + 2);
            }

            var m = new Mesh();
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
