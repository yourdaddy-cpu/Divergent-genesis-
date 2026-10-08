using UnityEngine;
using DivergentGenesis.Audio;
using DivergentGenesis.Core;
using DivergentGenesis.Decor;
using DivergentGenesis.Environment;
using DivergentGenesis.Living;
using DivergentGenesis.Player;
using DivergentGenesis.World;

namespace DivergentGenesis.Dimension
{
    /// <summary>
    /// The Overworld and the Node.
    ///
    /// There is only ever one world loaded. Travelling swaps the edit store, flips
    /// the generator's dimension flag, throws away the streamed chunks and rebuilds
    /// - which is why the Node costs no extra memory and can be as large as the
    /// overworld without costing anything to keep alive.
    /// </summary>
    public sealed class DimensionManager : MonoBehaviour
    {
        public static DimensionManager Instance;

        [Header("Travel")]
        [Tooltip("Seconds of immunity after arriving, so a rift cannot bounce you back.")]
        public float PortalCooldown = 2.5f;
        public float PadRadius = 3f;

        public DimensionId Current { get; private set; }
        public int Transitions { get; private set; }
        public event System.Action<DimensionId> Changed;

        private ChunkManager _world;
        private PropSystem _props;
        private GrassSystem _grass;
        private PlayerController _player;
        private PlayerStats _stats;
        private EntityManager _entities;
        private SkyController _sky;
        private int _seed;
        private float _cooldown;
        private Vector3 _overworldReturn;
        private bool _configured;

        public void Configure(ChunkManager world, PropSystem props, GrassSystem grass,
                              PlayerController player, PlayerStats stats, EntityManager entities,
                              SkyController sky, int seed)
        {
            Instance = this;
            _world = world;
            _props = props;
            _grass = grass;
            _player = player;
            _stats = stats;
            _entities = entities;
            _sky = sky;
            _seed = seed;
            Current = DimensionId.Overworld;
            _overworldReturn = player != null ? player.transform.position : Vector3.zero;
            _configured = true;
        }

        private void Update()
        {
            if (!_configured) return;
            if (_cooldown > 0f) _cooldown -= Time.deltaTime;
            CheckPortal();
        }

        // =============================================================== travel
        /// <summary>Walks the player through a rift and rebuilds the world around them.</summary>
        public void TravelTo(DimensionId target)
        {
            if (!_configured || _cooldown > 0f) return;
            if (target == Current) return;

            if (Current == DimensionId.Overworld && _player != null)
                _overworldReturn = _player.transform.position;

            Apply(target);

            if (_player != null)
                _player.Teleport(ArrivalPoint(target, _overworldReturn));

            _cooldown = PortalCooldown;
            Transitions++;

            var audio = AudioBank.Instance;
            if (audio != null) audio.Play("rift");

            if (Changed != null) Changed(Current);
        }

        /// <summary>Used when loading a save: no cooldown, no fade, no teleport.</summary>
        public void SetImmediate(DimensionId target)
        {
            if (!_configured) { Current = target; return; }
            if (Current == target) return;
            Apply(target);
            _cooldown = PortalCooldown;
        }

        private void Apply(DimensionId target)
        {
            Current = target;

            // 1. the world function itself
            DimensionState.NodeActive = target == DimensionId.Node;
            BlockEdits.SwitchDimension((int)target);

            // 2. sky palette (the boss's corruption is handled separately)
            SkyFX.SetDimension(target == DimensionId.Node);

            // 3. nothing living survives the crossing
            if (_entities != null) _entities.Reset();

            // 4. throw the streamed world away and rebuild it in the new palette
            if (_props != null) _props.Configure(_seed, _world.Quality);
            if (_grass != null) _grass.Configure(_seed, _world.Quality);
            if (_world != null) _world.RebuildAll();
        }

        // =============================================================== portals
        private void CheckPortal()
        {
            if (_cooldown > 0f || _player == null || _world == null) return;

            Vector3 p = _player.transform.position;
            int bx = Mathf.FloorToInt(p.x);
            int bz = Mathf.FloorToInt(p.z);

            for (int dy = 0; dy <= 1; dy++)
            {
                int by = Mathf.FloorToInt(p.y) + dy;
                if (_world.GetBlock(bx, by, bz) == Blocks.NodePortal)
                {
                    TravelTo(Current == DimensionId.Overworld ? DimensionId.Node : DimensionId.Overworld);
                    return;
                }
            }
        }

        /// <summary>
        /// Where you come out. If the destination has no pad yet, one is built, so
        /// a player can never arrive inside a mountain or thirty metres up.
        /// </summary>
        public Vector3 ArrivalPoint(DimensionId target, Vector3 preferred)
        {
            int x, z;
            if (target == DimensionId.Node)
            {
                // a fixed, deterministic landing field in the Node
                x = 8; z = 8;
                if (!SafeColumn(x, z)) { x = 40; z = -24; }
                if (!SafeColumn(x, z)) { x = -64; z = 48; }
            }
            else
            {
                x = Mathf.RoundToInt(preferred.x);
                z = Mathf.RoundToInt(preferred.z);
                if (!SafeColumn(x, z)) { x = 0; z = 0; }
            }

            float ground = GroundAt(x, z);
            BuildPad(x, Mathf.FloorToInt(ground), z, target);
            return new Vector3(x + 0.5f, ground + 1.2f, z + 0.5f);
        }

        private bool SafeColumn(int x, int z)
        {
            if (_world == null) return true;
            float h = _world.GetTerrainHeight(x, z);
            int sea = DimensionState.NodeActive ? Mathf.RoundToInt(NodeConfig.CreamSeaLevel)
                                               : Mathf.RoundToInt(WorldConfig.SeaLevel);
            if (h < sea + 2f) return false;
            if (h > 132f) return false;
            float h1 = _world.GetTerrainHeight(x + 6, z);
            float h2 = _world.GetTerrainHeight(x, z + 6);
            return Mathf.Abs(h1 - h) < 5f && Mathf.Abs(h2 - h) < 5f;
        }

        private float GroundAt(int x, int z)
        {
            if (_world == null) return 90f;
            return Mathf.Max(2f, _world.GetTerrainHeight(x, z));
        }

        /// <summary>Builds (or rebuilds) the arrival pad: a stone disc with a way home.</summary>
        public void BuildPad(int cx, int cy, int cz, DimensionId target)
        {
            if (_world == null) return;
            bool node = target == DimensionId.Node;
            byte floor = node ? Blocks.SherbetStone : Blocks.Cobblestone;
            byte wall = node ? Blocks.CottonBlock : Blocks.PlasterWall;
            byte trim = node ? Blocks.Lamp : Blocks.Torch;

            int r = Mathf.Max(2, Mathf.RoundToInt(PadRadius));
            for (int x = cx - r; x <= cx + r; x++)
            for (int z = cz - r; z <= cz + r; z++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
                if (d > r) continue;
                _world.SetBlock(x, cy, z, floor);

                for (int y = 1; y <= 3; y++) _world.SetBlock(x, cy + y, z, Blocks.Air);

                // rim
                if (d > r - 1.2f) _world.SetBlock(x, cy + 1, z, wall);
            }

            // the rift you came through, and the one that takes you back
            _world.SetBlock(cx + r, cy + 1, cz, Blocks.NodePortal);
            _world.SetBlock(cx + r, cy + 2, cz, Blocks.NodePortal);

            _world.SetBlock(cx - r, cy + 2, cz, trim);
            _world.SetBlock(cx + r, cy + 1, cz - r, trim);
            _world.SetBlock(cx + r, cy + 1, cz + r, trim);
        }

        // ================================================================ saving
        public bool NodeActive { get { return Current == DimensionId.Node; } }
    }
}