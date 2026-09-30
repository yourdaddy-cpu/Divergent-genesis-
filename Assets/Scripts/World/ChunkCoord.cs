using UnityEngine;

namespace DivergentGenesis.World
{
    /// <summary>Chunk coordinate helpers. The world is 4375 x 4375 chunks.</summary>
    public struct ChunkCoord
    {
        public int X;
        public int Z;

        public ChunkCoord(int x, int z) { X = x; Z = z; }

        public int WorldX { get { return X * Core.WorldConfig.ChunkSize; } }
        public int WorldZ { get { return Z * Core.WorldConfig.ChunkSize; } }
        public Vector3 WorldOrigin { get { return new Vector3(WorldX, 0f, WorldZ); } }
        public Vector3 Center { get { return new Vector3(WorldX + Core.WorldConfig.ChunkSize * 0.5f, 0f, WorldZ + Core.WorldConfig.ChunkSize * 0.5f); } }

        public static ChunkCoord FromWorld(float wx, float wz)
        {
            return new ChunkCoord(
                Mathf.FloorToInt(wx / Core.WorldConfig.ChunkSize),
                Mathf.FloorToInt(wz / Core.WorldConfig.ChunkSize));
        }

        public static bool operator ==(ChunkCoord a, ChunkCoord b) { return a.X == b.X && a.Z == b.Z; }
        public static bool operator !=(ChunkCoord a, ChunkCoord b) { return !(a == b); }
        public override bool Equals(object obj) { return obj is ChunkCoord && (ChunkCoord)obj == this; }
        public override int GetHashCode() { return X * 73856093 ^ Z * 19349663; }
        public override string ToString() { return "(" + X + ", " + Z + ")"; }

        public static ChunkCoord operator +(ChunkCoord a, ChunkCoord b) { return new ChunkCoord(a.X + b.X, a.Z + b.Z); }
        public static ChunkCoord operator -(ChunkCoord a, ChunkCoord b) { return new ChunkCoord(a.X - b.X, a.Z - b.Z); }
    }
}
