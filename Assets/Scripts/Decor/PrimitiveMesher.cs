using System.Collections.Generic;
using UnityEngine;

namespace DivergentGenesis.Decor
{
    /// <summary>Tiny procedural mesh builder used for trees, rocks and props. No mesh assets required.</summary>
    public static class PrimitiveMesher
    {
        public sealed class Builder
        {
            public readonly List<Vector3> V = new List<Vector3>(2048);
            public readonly List<Vector3> N = new List<Vector3>(2048);
            public readonly List<Color32> C = new List<Color32>(2048);
            public readonly List<Vector2> Uv = new List<Vector2>(2048);
            public readonly List<int> T = new List<int>(4096);

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.indexFormat = V.Count > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16;
                m.SetVertices(V);
                m.SetNormals(N);
                m.SetColors(C);
                m.SetUVs(0, Uv);
                m.SetTriangles(T, 0, true);
                m.RecalculateBounds();
                return m;
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 col, Vector2 uvScale)
            {
                int i = V.Count;
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                for (int k = 0; k < 4; k++) { N.Add(n); C.Add(col); }
                Uv.Add(new Vector2(0, 0)); Uv.Add(new Vector2(uvScale.x, 0));
                Uv.Add(new Vector2(uvScale.x, uvScale.y)); Uv.Add(new Vector2(0, uvScale.y));
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
                T.Add(i); T.Add(i + 2); T.Add(i + 3);
            }

            public void Box(Vector3 center, Vector3 size, Color32 col)
            {
                Vector3 h = size * 0.5f;
                Vector3 p0 = center - h, p1 = center + h;
                Quad(new Vector3(p0.x, p0.y, p1.z), new Vector3(p1.x, p0.y, p1.z),
                     new Vector3(p1.x, p1.y, p1.z), new Vector3(p0.x, p1.y, p1.z), col, new Vector2(1, 1)); // +Z
                Quad(new Vector3(p1.x, p0.y, p0.z), new Vector3(p0.x, p0.y, p0.z),
                     new Vector3(p0.x, p1.y, p0.z), new Vector3(p1.x, p1.y, p0.z), col, new Vector2(1, 1)); // -Z
                Quad(new Vector3(p1.x, p0.y, p1.z), new Vector3(p1.x, p0.y, p0.z),
                     new Vector3(p1.x, p1.y, p0.z), new Vector3(p1.x, p1.y, p1.z), col, new Vector2(1, 1)); // +X
                Quad(new Vector3(p0.x, p0.y, p0.z), new Vector3(p0.x, p0.y, p1.z),
                     new Vector3(p0.x, p1.y, p1.z), new Vector3(p0.x, p1.y, p0.z), col, new Vector2(1, 1)); // -X
                Quad(new Vector3(p0.x, p1.y, p1.z), new Vector3(p1.x, p1.y, p1.z),
                     new Vector3(p1.x, p1.y, p0.z), new Vector3(p0.x, p1.y, p0.z), col, new Vector2(1, 1)); // +Y
                Quad(new Vector3(p0.x, p0.y, p0.z), new Vector3(p1.x, p0.y, p0.z),
                     new Vector3(p1.x, p0.y, p1.z), new Vector3(p0.x, p0.y, p1.z), col, new Vector2(1, 1)); // -Y
            }

            /// <summary>Tapered cylinder / cone. radiusTop == 0 gives a cone.</summary>
            public void Cylinder(Vector3 baseCenter, float radiusBottom, float radiusTop, float height, int sides, Color32 col)
            {
                int ringA = V.Count;
                for (int i = 0; i < sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    V.Add(baseCenter + new Vector3(c * radiusBottom, 0f, s * radiusBottom));
                    N.Add(new Vector3(c, 0.25f, s).normalized);
                    C.Add(col);
                    Uv.Add(new Vector2(i / (float)sides, 0f));
                }
                int ringB = V.Count;
                for (int i = 0; i < sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    V.Add(baseCenter + new Vector3(c * radiusTop, height, s * radiusTop));
                    N.Add(new Vector3(c, 0.25f, s).normalized);
                    C.Add(col);
                    Uv.Add(new Vector2(i / (float)sides, 1f));
                }
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    T.Add(ringA + i); T.Add(ringB + i); T.Add(ringA + j);
                    T.Add(ringA + j); T.Add(ringB + i); T.Add(ringB + j);
                }
                // caps
                AddDisc(baseCenter, radiusBottom, sides, col, false);
                if (radiusTop > 0.01f) AddDisc(baseCenter + new Vector3(0, height, 0), radiusTop, sides, col, true);
            }

            private void AddDisc(Vector3 center, float radius, int sides, Color32 col, bool up)
            {
                int c0 = V.Count;
                V.Add(center); N.Add(up ? Vector3.up : Vector3.down); C.Add(col); Uv.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i < sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    V.Add(center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
                    N.Add(up ? Vector3.up : Vector3.down);
                    C.Add(col);
                    Uv.Add(new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
                }
                for (int i = 0; i < sides; i++)
                {
                    int a = c0 + 1 + i;
                    int b = c0 + 1 + (i + 1) % sides;
                    if (up) { T.Add(c0); T.Add(a); T.Add(b); }
                    else { T.Add(c0); T.Add(b); T.Add(a); }
                }
            }

            /// <summary>Low poly blob used for tree canopies and boulders.</summary>
            public void Sphere(Vector3 center, float radius, int segs, int rings, Color32 col, float squashY = 1f)
            {
                int baseIndex = V.Count;
                for (int r = 0; r <= rings; r++)
                {
                    float v = r / (float)rings;
                    float phi = v * Mathf.PI;
                    for (int s = 0; s <= segs; s++)
                    {
                        float u = s / (float)segs;
                        float theta = u * Mathf.PI * 2f;
                        Vector3 n = new Vector3(
                            Mathf.Sin(phi) * Mathf.Cos(theta),
                            Mathf.Cos(phi),
                            Mathf.Sin(phi) * Mathf.Sin(theta));
                        V.Add(center + Vector3.Scale(n, new Vector3(radius, radius * squashY, radius)));
                        N.Add(n);
                        C.Add(col);
                        Uv.Add(new Vector2(u, v));
                    }
                }
                int stride = segs + 1;
                for (int r = 0; r < rings; r++)
                {
                    for (int s = 0; s < segs; s++)
                    {
                        int a = baseIndex + r * stride + s;
                        int b = a + 1;
                        int c = a + stride;
                        int d = c + 1;
                        T.Add(a); T.Add(c); T.Add(b);
                        T.Add(b); T.Add(c); T.Add(d);
                    }
                }
            }
        }
    }
}
