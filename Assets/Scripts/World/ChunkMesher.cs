using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using DivergentGenesis.Core;

namespace DivergentGenesis.World
{
    /// <summary>
    /// Builds GPU meshes out of chunk data.
    ///  - Level 0  -> real voxel mesh with ambient occlusion, caves and block edits
    ///  - Level 1+ -> smooth height-field mesh with skirts (hides LOD cracks)
    /// </summary>
    public sealed class ChunkMesher
    {
        public sealed class Buffers
        {
            public readonly List<Vector3> Positions = new List<Vector3>(16384);
            public readonly List<Vector3> Normals = new List<Vector3>(16384);
            public readonly List<Color32> Colors = new List<Color32>(16384);
            public readonly List<Vector2> Uvs = new List<Vector2>(8192);
            public readonly List<int> Triangles = new List<int>(49152);
            public readonly List<int> WaterTriangles = new List<int>(16384);

            public void Clear()
            {
                Positions.Clear(); Normals.Clear(); Colors.Clear(); Uvs.Clear();
                Triangles.Clear(); WaterTriangles.Clear();
            }
        }

        private readonly Buffers _b = new Buffers();
        public Buffers Shared { get { return _b; } }

        // Face table: normal, corner origin, and the two edge vectors.
        // cross(edgeU, edgeV) == normal, so triangles (0,1,2)+(0,2,3) come out
        // clockwise from outside, which is what Unity treats as front facing.
        private static readonly Vector3[] FaceN =
        {
            new Vector3( 0, 0,-1), new Vector3( 0, 0, 1), new Vector3( 0, 1, 0),
            new Vector3( 1, 0, 0), new Vector3(-1, 0, 0), new Vector3( 0,-1, 0)
        };
        private static readonly Vector3[] FaceP0 =
        {
            new Vector3(0,0,0), new Vector3(0,0,1), new Vector3(0,1,0),
            new Vector3(1,0,1), new Vector3(0,0,0), new Vector3(0,0,0)
        };
        private static readonly Vector3[] FaceU =
        {
            new Vector3(-1, 0, 0), new Vector3( 1, 0, 0), new Vector3( 0, 0, 1),
            new Vector3( 0, 0,-1), new Vector3( 0, 0, 1), new Vector3( 1, 0, 0)
        };
        private static readonly Vector3[] FaceV =
        {
            new Vector3( 0, 1, 0), new Vector3( 0, 1, 0), new Vector3( 1, 0, 0),
            new Vector3( 0, 1, 0), new Vector3( 0, 1, 0), new Vector3( 0, 0, 1)
        };
        private static readonly float[] FaceShade = { 0.82f, 0.82f, 1.00f, 0.70f, 0.70f, 0.52f };

        // ====================================================== height field LOD
        public Mesh BuildHeightMesh(ChunkData data, bool withWater)
        {
            Buffers b = _b;
            b.Clear();

            int n = data.Samples;
            int step = data.Step;
            int worldX = data.WorldX;
            int worldZ = data.WorldZ;
            float sea = WorldConfig.SeaLevel;

            for (int j = 0; j < n; j++)
            {
                int wz = worldZ + j * step;
                for (int i = 0; i < n; i++)
                {
                    int wx = worldX + i * step;
                    int idx = j * n + i;
                    float h = data.Heights[idx];
                    BiomeType biome = (BiomeType)data.Biomes[idx];

                    b.Positions.Add(new Vector3(i * step, h, j * step));
                    b.Normals.Add(Vector3.up);
                    b.Colors.Add(AlbedoFor(biome, h, h < sea));
                    b.Uvs.Add(new Vector2(wx, wz));
                }
            }

            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    int idx = j * n + i;
                    float hL = data.HeightAtLocal(i - 1, j);
                    float hR = data.HeightAtLocal(i + 1, j);
                    float hD = data.HeightAtLocal(i, j - 1);
                    float hU = data.HeightAtLocal(i, j + 1);
                    Vector3 nrm = new Vector3(hL - hR, 2f * step, hD - hU).normalized;
                    b.Normals[idx] = nrm;

                    float steep = Mathf.InverseLerp(0.80f, 0.45f, nrm.y);
                    if (steep > 0.001f)
                    {
                        BiomeType biome = (BiomeType)data.Biomes[idx];
                        Color32 rock = BiomeSystem.GetStoneColor(biome);
                        b.Colors[idx] = Color32.Lerp(b.Colors[idx], rock, steep * 0.85f);
                    }
                }
            }

            for (int j = 0; j < n - 1; j++)
            {
                int row = j * n;
                for (int i = 0; i < n - 1; i++)
                {
                    int a = row + i;
                    int c = a + 1;
                    int d = a + n;
                    int e = d + 1;
                    b.Triangles.Add(a); b.Triangles.Add(d); b.Triangles.Add(c);
                    b.Triangles.Add(c); b.Triangles.Add(d); b.Triangles.Add(e);
                }
            }

            // Skirts stop neighbouring LOD tiles from showing a hairline crack.
            float drop = step * 3f + 3f;
            AddSkirtLine(b, 0, n, drop);                                  // j = 0
            AddSkirtLine(b, (n - 1) * n, n, drop);                        // j = last
            for (int i = 0; i < n; i++) { AddSkirtPoint(b, i, drop); }     // i = 0
            for (int i = 0; i < n; i++) { AddSkirtPoint(b, i + (n - 1) * n, drop); } // i = last

            int terrainIndexCount = b.Triangles.Count;

            if (withWater) BuildWaterSurface(b, data);

            return Finish(b, terrainIndexCount);
        }

        /// <summary>Flat water quads for every cell below sea level. Second submesh, transparent shader.</summary>
        private void BuildWaterSurface(Buffers b, ChunkData data)
        {
            int n = data.Samples;
            int step = data.Step;
            float sea = WorldConfig.SeaLevel;
            int vStart = b.Positions.Count;

            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int idx = j * n + i;
                float h = data.Heights[idx];
                if (h >= sea) continue;

                float depth = Mathf.Clamp01((sea - h) / 22f);
                var c = new Color32(
                    (byte)Mathf.Lerp(70, 28, depth),
                    (byte)Mathf.Lerp(140, 92, depth),
                    (byte)Mathf.Lerp(200, 168, depth), 255);

                float x = i * step, z = j * step;
                b.Positions.Add(new Vector3(x, sea, z));
                b.Positions.Add(new Vector3(x, sea, z + step));
                b.Positions.Add(new Vector3(x + step, sea, z + step));
                b.Positions.Add(new Vector3(x + step, sea, z));
                for (int k = 0; k < 4; k++) { b.Normals.Add(Vector3.up); b.Colors.Add(c); }
                b.Uvs.Add(new Vector2(x, z));
                b.Uvs.Add(new Vector2(x, z + step));
                b.Uvs.Add(new Vector2(x + step, z + step));
                b.Uvs.Add(new Vector2(x + step, z));

                int v = b.Positions.Count - 4;
                b.WaterTriangles.Add(v + 0); b.WaterTriangles.Add(v + 1); b.WaterTriangles.Add(v + 2);
                b.WaterTriangles.Add(v + 0); b.WaterTriangles.Add(v + 2); b.WaterTriangles.Add(v + 3);
            }
        }

        private void AddSkirtPoint(Buffers b, int srcVertex, float drop)
        {
            Vector3 p = b.Positions[srcVertex];
            Color32 c = b.Colors[srcVertex];
            b.Positions.Add(new Vector3(p.x, p.y - drop, p.z));
            b.Normals.Add(Vector3.up);
            b.Colors.Add(new Color32((byte)(c.r * 0.65f), (byte)(c.g * 0.65f), (byte)(c.b * 0.65f), 255));
            b.Uvs.Add(new Vector2(p.x, p.z));
        }

        private void AddSkirtLine(Buffers b, int srcStart, int count, float drop)
        {
            int first = b.Positions.Count;
            for (int k = 0; k < count; k++) AddSkirtPoint(b, srcStart + k, drop);

            for (int k = 0; k < count - 1; k++)
            {
                int t0 = srcStart + k, t1 = srcStart + k + 1;
                int b0 = first + k, b1 = first + k + 1;
                // both windings - skirts are only a few dozen triangles and must
                // be visible no matter which way the player looks
                b.Triangles.Add(t0); b.Triangles.Add(b0); b.Triangles.Add(t1);
                b.Triangles.Add(t1); b.Triangles.Add(b0); b.Triangles.Add(b1);
                b.Triangles.Add(t0); b.Triangles.Add(t1); b.Triangles.Add(b0);
                b.Triangles.Add(t1); b.Triangles.Add(b1); b.Triangles.Add(b0);
            }
        }

        // ================================================================ voxel
        public Mesh BuildVoxelMesh(ChunkData data, VoxelReader reader)
        {
            Buffers b = _b;
            b.Clear();

            int size = WorldConfig.ChunkSize;
            int maxY = Mathf.Min(data.MaxY, WorldConfig.ChunkHeight - 1);
            int minY = Mathf.Max(1, data.MinY - 1);
            int wx0 = data.WorldX, wz0 = data.WorldZ;

            for (int y = minY; y <= maxY; y++)
            for (int z = 0; z < size; z++)
            {
                int wz = wz0 + z;
                for (int x = 0; x < size; x++)
                {
                    byte block = data.GetBlockLocal(x, y, z);
                    if (block == World.Blocks.Air) continue;

                    BlockDef def = BlockDef.Def(block);
                    int wx = wx0 + x;

                    if (def.Render == BlockRender.Cross) { AddCross(b, x, y, z, def); continue; }
                    if (def.Render == BlockRender.Liquid) { AddLiquid(b, x, y, z, def, reader, wx, wz); continue; }

                    // +Y
                    if (!Opaque(reader.Get(wx, y + 1, wz))) AddFace(b, x, y, z, 2, def, reader, wx, wz);
                    // -Y
                    if (y > 0 && !Opaque(reader.Get(wx, y - 1, wz))) AddFace(b, x, y, z, 5, def, reader, wx, wz);
                    // -Z / +Z
                    if (!Opaque(reader.Get(wx, y, wz - 1))) AddFace(b, x, y, z, 0, def, reader, wx, wz);
                    if (!Opaque(reader.Get(wx, y, wz + 1))) AddFace(b, x, y, z, 1, def, reader, wx, wz);
                    // -X / +X
                    if (!Opaque(reader.Get(wx - 1, y, wz))) AddFace(b, x, y, z, 4, def, reader, wx, wz);
                    if (!Opaque(reader.Get(wx + 1, y, wz))) AddFace(b, x, y, z, 3, def, reader, wx, wz);
                }
            }

            return Finish(b);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void Noop() { }

        private static bool Opaque(byte id)
        {
            return id != World.Blocks.Air && BlockDef.IsOpaque(id);
        }

        private void AddFace(Buffers b, int x, int y, int z, int face, BlockDef def, VoxelReader reader, int wx, int wz)
        {
            Color32 col = face == 2 ? def.TopColor : def.Color;
            float shade = FaceShade[face];
            Vector3 n = FaceN[face], p0 = FaceP0[face], eu = FaceU[face], ev = FaceV[face];

            Color32[] c = new Color32[4];
            for (int k = 0; k < 4; k++)
            {
                int a = (k == 1 || k == 2) ? 1 : 0;
                int bb = (k == 2 || k == 3) ? 1 : 0;
                float ao = Ambient(reader, wx + x, y, wz + z, n, eu, ev, a, bb);
                float s = shade * ao;
                c[k] = new Color32(
                    (byte)Mathf.Clamp(col.r * s, 0f, 255f),
                    (byte)Mathf.Clamp(col.g * s, 0f, 255f),
                    (byte)Mathf.Clamp(col.b * s, 0f, 255f), 255);
            }

            int v0 = b.Positions.Count;
            Vector3 origin = new Vector3(x, y, z);
            AddVertex(b, origin + p0, n, c[0]);
            AddVertex(b, origin + p0 + eu, n, c[1]);
            AddVertex(b, origin + p0 + eu + ev, n, c[2]);
            AddVertex(b, origin + p0 + ev, n, c[3]);

            b.Triangles.Add(v0 + 0); b.Triangles.Add(v0 + 1); b.Triangles.Add(v0 + 2);
            b.Triangles.Add(v0 + 0); b.Triangles.Add(v0 + 2); b.Triangles.Add(v0 + 3);
        }

        private static void AddVertex(Buffers b, Vector3 p, Vector3 n, Color32 c)
        {
            b.Positions.Add(p);
            b.Normals.Add(n);
            b.Colors.Add(c);
            b.Uvs.Add(new Vector2(p.x, p.z));
        }

        private static float Ambient(VoxelReader reader, int x, int y, int z,
                                     Vector3 n, Vector3 eu, Vector3 ev, int a, int bCorner)
        {
            int du = a == 1 ? 1 : -1;
            int dv = bCorner == 1 ? 1 : -1;

            Vector3 s1 = n + eu * du;
            Vector3 s2 = n + ev * dv;
            Vector3 co = n + eu * du + ev * dv;

            bool o1 = Opaque(reader.Get(x + (int)s1.x, y + (int)s1.y, z + (int)s1.z));
            bool o2 = Opaque(reader.Get(x + (int)s2.x, y + (int)s2.y, z + (int)s2.z));
            bool oc = Opaque(reader.Get(x + (int)co.x, y + (int)co.y, z + (int)co.z));

            int level = (o1 && o2) ? 0 : 3 - ((o1 ? 1 : 0) + (o2 ? 1 : 0) + (oc ? 1 : 0));
            return 0.55f + 0.15f * level;      // 0.55 .. 1.0
        }

        private void AddCross(Buffers b, int x, int y, int z, BlockDef def)
        {
            Color32 c = def.Color;
            Color32 c2 = new Color32((byte)(c.r * 0.85f), (byte)(c.g * 0.85f), (byte)(c.b * 0.85f), 255);
            int v0 = b.Positions.Count;
            float h = 0.9f;
            Vector3 n = Vector3.up;

            AddVertex(b, new Vector3(x + 0.15f, y, z + 0.15f), n, c);
            AddVertex(b, new Vector3(x + 0.85f, y + h, z + 0.85f), n, c2);
            AddVertex(b, new Vector3(x + 0.15f, y + h, z + 0.85f), n, c);

            AddVertex(b, new Vector3(x + 0.85f, y, z + 0.15f), n, c);
            AddVertex(b, new Vector3(x + 0.85f, y + h, z + 0.15f), n, c2);
            AddVertex(b, new Vector3(x + 0.15f, y + h, z + 0.15f), n, c);

            b.Triangles.Add(v0 + 0); b.Triangles.Add(v0 + 1); b.Triangles.Add(v0 + 2);
            b.Triangles.Add(v0 + 3); b.Triangles.Add(v0 + 4); b.Triangles.Add(v0 + 5);
        }

        private void AddLiquid(Buffers b, int x, int y, int z, BlockDef def, VoxelReader reader, int wx, int wz)
        {
            if (reader.Get(wx, y + 1, wz) == def.Id) return;   // only draw the surface layer

            int v0 = b.Positions.Count;
            float h = 0.88f;
            Color32 c = def.Color;

            AddVertex(b, new Vector3(x, y + h, z), Vector3.up, c);
            AddVertex(b, new Vector3(x, y + h, z + 1), Vector3.up, c);
            AddVertex(b, new Vector3(x + 1, y + h, z + 1), Vector3.up, c);
            AddVertex(b, new Vector3(x + 1, y + h, z), Vector3.up, c);

            b.Triangles.Add(v0 + 0); b.Triangles.Add(v0 + 1); b.Triangles.Add(v0 + 2);
            b.Triangles.Add(v0 + 0); b.Triangles.Add(v0 + 2); b.Triangles.Add(v0 + 3);
        }

        // ------------------------------------------------------------- output
        private Mesh Finish(Buffers b)
        {
            return Finish(b, b.Triangles.Count);
        }

        private Mesh Finish(Buffers b, int terrainIndexCount)
        {
            var m = new Mesh();
            m.indexFormat = b.Positions.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            m.SetVertices(b.Positions);
            m.SetNormals(b.Normals);
            m.SetColors(b.Colors);
            m.SetUVs(0, b.Uvs);

            if (b.WaterTriangles.Count > 0)
            {
                m.subMeshCount = 2;
                m.SetTriangles(b.Triangles, 0, true);
                m.SetTriangles(b.WaterTriangles, 1, true);
            }
            else
            {
                m.subMeshCount = 1;
                m.SetTriangles(b.Triangles, 0, true);
            }

            m.RecalculateBounds();
            m.MarkDynamic();
            return m;
        }

        public static Color32 AlbedoFor(BiomeType biome, float height, bool underwater)
        {
            BiomeInfo info = BiomeSystem.Get(biome);
            if (underwater) return info.DirtColor;
            switch (info.SurfaceBlock)
            {
                case World.Blocks.Snow: return new Color32(238, 244, 250, 255);
                case World.Blocks.Sand: return new Color32(216, 206, 154, 255);
                case World.Blocks.Stone: return info.StoneColor;
                case World.Blocks.Terracotta: return new Color32(178, 108, 62, 255);
                case World.Blocks.Ash: return new Color32(104, 100, 96, 255);
                case World.Blocks.FrostingGrass: return info.GrassColor;
                case World.Blocks.SherbetStone: return info.StoneColor;
                default: return info.GrassColor;
            }
        }
    }

    /// <summary>Seamless voxel lookup that can cross chunk borders.</summary>
    public sealed class VoxelReader
    {
        public System.Func<int, int, int, byte> Resolver;

        public byte Get(int x, int y, int z)
        {
            var r = Resolver;
            return r == null ? World.Blocks.Air : r(x, y, z);
        }
    }
}
