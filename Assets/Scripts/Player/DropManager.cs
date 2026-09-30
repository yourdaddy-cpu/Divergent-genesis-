using System.Collections.Generic;
using UnityEngine;
using DivergentGenesis.World;
using DivergentGenesis.Items;
using DivergentGenesis.Render;

namespace DivergentGenesis.Player
{
    /// <summary>
    /// Item pickups: a small spinning cube that flies to the player when close,
    /// and is magnetised in when the inventory has room. Purely visual - nothing
    /// here needs physics.
    /// </summary>
    public sealed class DropManager : MonoBehaviour
    {
        public static DropManager Instance;

        private sealed class Drop
        {
            public Transform T;
            public ItemId Id;
            public int Count;
            public Vector3 Velocity;
            public float Age;
            public float PickupDelay;
        }

        private readonly List<Drop> _drops = new List<Drop>(64);
        private readonly List<Drop> _dead = new List<Drop>(16);
        private Mesh _cube;
        private PlayerController _player;
        private InventoryModel _inventory;
        private float _autosaveTimer;

        public int Count { get { return _drops.Count; } }

        private void Awake()
        {
            Instance = this;
            MaterialLibrary.Ensure();
            _cube = BuildCube();
        }

        public void Configure(PlayerController player, InventoryModel inventory)
        {
            _player = player;
            _inventory = inventory;
        }

        private static Mesh BuildCube()
        {
            var b = new Decor.PrimitiveMesher.Builder();
            b.Box(Vector3.zero, Vector3.one * 0.28f, new Color32(255, 255, 255, 255));
            return b.ToMesh("DropCube");
        }

        public static void Spawn(ChunkManager world, Vector3 position, ItemId id, int count)
        {
            if (Instance == null || world == null || count <= 0) return;
            Instance.SpawnInternal(position, id, count);
        }

        private void SpawnInternal(Vector3 position, ItemId id, int count)
        {
            var go = new GameObject("Drop_" + id);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _cube;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialLibrary.Terrain;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var d = new Drop
            {
                T = go.transform,
                Id = id,
                Count = count,
                Velocity = new Vector3(Random.Range(-0.6f, 0.6f), 2.2f, Random.Range(-0.6f, 0.6f)),
                PickupDelay = 0.6f
            };
            _drops.Add(d);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _dead.Clear();

            for (int i = 0; i < _drops.Count; i++)
            {
                var d = _drops[i];
                d.Age += dt;
                d.PickupDelay -= dt;

                if (d.T == null) { _dead.Add(d); continue; }

                // gentle gravity then hover
                d.Velocity += Vector3.down * 16f * dt;
                Vector3 p = d.T.position + d.Velocity * dt;

                var world = ChunkManager.Instance;
                if (world != null)
                {
                    int bx = Mathf.FloorToInt(p.x), by = Mathf.FloorToInt(p.y), bz = Mathf.FloorToInt(p.z);
                    if (world.GetBlock(bx, by, bz) != World.Blocks.Air) { d.Velocity = Vector3.zero; p = d.T.position; }
                }
                d.T.position = p;
                d.T.rotation = Quaternion.Euler(d.Age * 55f, d.Age * 80f, 0f);

                if (d.Age > 180f) { _dead.Add(d); continue; }

                // magnetise toward the player
                if (_player != null && d.PickupDelay <= 0f)
                {
                    Vector3 toPlayer = (_player.EyePosition - p);
                    float dist = toPlayer.magnitude;
                    if (dist < 2.2f)
                    {
                        d.T.position = p + toPlayer.normalized * (7f * dt);
                        if (dist < 0.8f)
                        {
                            if (_inventory != null && _inventory.Add(d.Id, d.Count) == 0)
                            {
                                _dead.Add(d);
                                continue;
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < _dead.Count; i++) if (_dead[i].T != null) Destroy(_dead[i].T.gameObject);
            for (int i = _dead.Count - 1; i >= 0; i--) _drops.Remove(_dead[i]);
        }
    }
}
