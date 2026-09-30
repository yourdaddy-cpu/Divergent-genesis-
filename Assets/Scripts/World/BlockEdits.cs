using System.Collections.Generic;
using UnityEngine;

namespace DivergentGenesis.World
{
    /// <summary>
    /// Sparse store of every block the player has changed.
    ///
    /// The 140 km world is NOT stored - it is a pure function of the seed. Only the
    /// handful of blocks a player digs out or places are kept, so a world with
    /// millions of edits still costs a few hundred KB.
    /// </summary>
    public static class BlockEdits
    {
        private const int CoordBias = 1 << 18;   // 262144, covers +/-131071 m

        private static readonly Dictionary<long, byte> Placed = new Dictionary<long, byte>(1024);
        private static readonly Dictionary<long, byte> Removed = new Dictionary<long, byte>(1024);

        /// <summary>Chunks whose voxels no longer match the stored edits.</summary>
        public static readonly HashSet<long> DirtyChunks = new HashSet<long>();

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void Noop() { }

        [Core.MethodImplFast]
        public static long Pack(int x, int y, int z)
        {
            unchecked
            {
                return ((long)(x + CoordBias) << 30) | ((long)(z + CoordBias) << 12) | (uint)(y & 0xFFF);
            }
        }

        [Core.MethodImplFast]
        public static long PackChunk(int cx, int cz)
        {
            unchecked
            {
                return ((long)(cx + CoordBias) << 22) | (uint)(cz + CoordBias);
            }
        }

        public static void UnpackChunk(long key, out int cx, out int cz)
        {
            cx = (int)((key >> 22) & 0x3FFFF) - CoordBias;
            cz = (int)(key & 0x3FFFF) - CoordBias;
        }

        public static void SetBlock(int x, int y, int z, byte block)
        {
            long key = Pack(x, y, z);
            if (block == Blocks.Air) Placed.Remove(key);
            else Placed[key] = block;

            Removed.Remove(key);
            MarkDirty(x, z);
        }

        /// <summary>Hides a procedurally generated block (trees, ores) without recording a value.</summary>
        public static void RemoveGenerated(int x, int y, int z)
        {
            long key = Pack(x, y, z);
            Placed.Remove(key);
            Removed[key] = 0;
            MarkDirty(x, z);
        }

        [Core.MethodImplFast]
        public static bool IsGeneratedHidden(int x, int y, int z)
        {
            return Removed.ContainsKey(Pack(x, y, z));
        }

        /// <summary>Returns the edited block, or -1 when the world generator owns this cell.</summary>
        [Core.MethodImplFast]
        public static int GetEdited(int x, int y, int z)
        {
            long key = Pack(x, y, z);
            byte b;
            if (Placed.TryGetValue(key, out b)) return b;
            return -1;
        }

        private static void MarkDirty(int x, int z)
        {
            int cx = Mathf.FloorToInt(x / (float)Core.WorldConfig.ChunkSize);
            int cz = Mathf.FloorToInt(z / (float)Core.WorldConfig.ChunkSize);
            DirtyChunks.Add(PackChunk(cx, cz));
        }

        public static int EditCount { get { return Placed.Count + Removed.Count; } }

        public static void Clear()
        {
            Placed.Clear();
            Removed.Clear();
            DirtyChunks.Clear();
        }

        // ------------------------------------------------------------ save data
        [System.Serializable]
        public struct Entry
        {
            public int x, y, z;
            public byte block;
            public byte removed;   // 1 = hide a generated block
        }

        public static List<Entry> Serialize()
        {
            var list = new List<Entry>(Placed.Count + Removed.Count);
            foreach (var kv in Placed)
            {
                Entry e = new Entry();
                Unpack(kv.Key, out e.x, out e.y, out e.z);
                e.block = kv.Value;
                e.removed = 0;
                list.Add(e);
            }
            foreach (var kv in Removed)
            {
                Entry e = new Entry();
                Unpack(kv.Key, out e.x, out e.y, out e.z);
                e.block = 0;
                e.removed = 1;
                list.Add(e);
            }
            return list;
        }

        public static void Deserialize(List<Entry> list)
        {
            Clear();
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                Entry e = list[i];
                long key = Pack(e.x, e.y, e.z);
                if (e.removed != 0) Removed[key] = 0;
                else Placed[key] = e.block;
                MarkDirty(e.x, e.z);
            }
        }

        private static void Unpack(long key, out int x, out int y, out int z)
        {
            x = (int)((key >> 30) & 0x3FFFF) - CoordBias;
            z = (int)((key >> 12) & 0x3FFFF) - CoordBias;
            y = (int)(key & 0xFFF);
        }
    }
}
