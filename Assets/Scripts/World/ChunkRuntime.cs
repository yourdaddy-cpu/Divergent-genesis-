using UnityEngine;
using DivergentGenesis.Core;

namespace DivergentGenesis.World
{
    public enum ChunkState : byte { Requested = 0, Generating = 1, HasData = 2, Meshed = 3 }

    /// <summary>One loaded tile: data, GameObject and mesh.</summary>
    public sealed class ChunkRuntime
    {
        public int Cx, Cz, Level;
        public ChunkData Data;
        public ChunkState State;

        public GameObject Go;
        public MeshFilter Filter;
        public MeshRenderer Renderer;
        public Mesh Mesh;
        public Vector3 Center;
        public float Distance;
        public bool MarkedForRemoval;

        public long Key { get { return ChunkKey(Cx, Cz, Level); } }

        public static long ChunkKey(int cx, int cz, int level)
        {
            unchecked
            {
                return ((long)(cx + (1 << 20)) << 24) ^ ((long)(cz + (1 << 20)) << 3) ^ level;
            }
        }

        public void Build(Mesh mesh, Material terrainMat, Material waterMat)
        {
            Mesh = mesh;

            if (Go == null)
            {
                Go = new GameObject("Chunk_L" + Level + "_" + Cx + "_" + Cz);
                var root = ChunkManager.Instance != null ? ChunkManager.Instance.ChunkRoot : null;
                Go.transform.SetParent(root, false);
                Go.transform.position = new Vector3(Cx * Data.EdgeMeters, 0f, Cz * Data.EdgeMeters);
                Go.transform.rotation = Quaternion.identity;

                Filter = Go.AddComponent<MeshFilter>();
                Renderer = Go.AddComponent<MeshRenderer>();
                Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Renderer.receiveShadows = false;
                Renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                Renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                Renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }

            if (mesh != null && mesh.subMeshCount > 1)
                Renderer.sharedMaterials = new[] { terrainMat, waterMat };
            else
                Renderer.sharedMaterials = new[] { terrainMat };

            Filter.sharedMesh = mesh;
            Renderer.enabled = true;
            State = ChunkState.Meshed;
        }

        public void Release()
        {
            State = ChunkState.Requested;
            Data = null;
            if (Filter != null) Filter.sharedMesh = null;
            if (Mesh != null) Object.Destroy(Mesh);
            if (Go != null) Object.Destroy(Go);
            Go = null; Filter = null; Renderer = null; Mesh = null;
        }
    }

    public struct BlockHit
    {
        public bool Hit;
        public Vector3 Point;
        public Vector3 Normal;
        public int X, Y, Z;
        public byte Block;
        public float Distance;
    }
}
