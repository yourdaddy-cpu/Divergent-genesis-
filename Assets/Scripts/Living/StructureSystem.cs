using System.Collections.Generic;
using UnityEngine;
using DivergentGenesis.Core;
using DivergentGenesis.Player;
using DivergentGenesis.World;

namespace DivergentGenesis.Living
{
    /// <summary>
    /// Connects the static world to the living one.
    ///
    /// The structures themselves are baked into the terrain by
    /// <see cref="StructureGenerator"/>, so they cost nothing at runtime. What this
    /// does is notice when the player comes near one and tell the entity manager to
    /// keep it inhabited: villagers in the houses, guards on the walls, bandits in
    /// the camps, cute things in the Node.
    /// </summary>
    public sealed class StructureSystem : MonoBehaviour
    {
        public static StructureSystem Instance;

        [Header("Awareness")]
        public float AwarenessRadius = 150f;
        public float RescanInterval = 3f;

        public int KnownVillages { get; private set; }
        public int KnownCamps { get; private set; }
        public StructurePlan NearestSite { get; private set; }

        private ChunkManager _world;
        private EntityManager _entities;
        private PlayerController _player;
        private int _seed;
        private float _timer;
        private readonly List<StructurePlan> _plans = new List<StructurePlan>(32);

        public void Configure(ChunkManager world, EntityManager entities, PlayerController player, int seed)
        {
            Instance = this;
            _world = world;
            _entities = entities;
            _player = player;
            _seed = seed;
        }

        private void Update()
        {
            if (_world == null || _entities == null || _player == null) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = RescanInterval;

            Vector3 p = _player.transform.position;
            var gen = _world.Generator;
            if (gen == null) return;

            StructureGenerator.CollectNearby(p, AwarenessRadius, _seed, gen, DimensionState.NodeActive, _plans);

            KnownVillages = 0;
            KnownCamps = 0;
            bool found = false;
            float bestDist = float.MaxValue;

            for (int i = 0; i < _plans.Count; i++)
            {
                var plan = _plans[i];
                float d = Vector3.Distance(plan.Centre, p);
                if (d > AwarenessRadius) continue;

                if (d < bestDist) { bestDist = d; NearestSite = plan; found = true; }

                switch (plan.Kind)
                {
                    case StructureKind.Village:
                        KnownVillages++;
                        _entities.RegisterSite(plan.Centre, NpcSiteKind.Village, SiteId(plan));
                        break;
                    case StructureKind.NodeVillage:
                        KnownVillages++;
                        _entities.RegisterSite(plan.Centre, NpcSiteKind.NodeVillage, SiteId(plan));
                        break;
                    case StructureKind.BanditCamp:
                        KnownCamps++;
                        _entities.RegisterSite(plan.Centre, NpcSiteKind.BanditCamp, SiteId(plan));
                        break;
                    case StructureKind.NodeHollow:
                        // the arena itself is quiet until somebody uses the altar
                        break;
                }
            }

            if (!found) NearestSite = new StructurePlan();
        }

        private static int SiteId(StructurePlan plan)
        {
            unchecked { return plan.CellX * 100003 + plan.CellZ * 31 + (int)plan.Kind; }
        }

        /// <summary>Human readable name of whatever the player is standing in.</summary>
        public string DescribeLocation(Vector3 p)
        {
            if (!NearestSite.Valid) return null;
            float d = Vector3.Distance(NearestSite.Centre, p);
            if (d > NearestSite.Radius + 12f) return null;

            switch (NearestSite.Kind)
            {
                case StructureKind.Village: return "Village";
                case StructureKind.NodeVillage: return "Node Village";
                case StructureKind.BanditCamp: return "Bandit Camp";
                case StructureKind.Ruins: return "Old Ruins";
                case StructureKind.RitualSite: return "Ritual Site";
                case StructureKind.NodeShrine: return "Node Shrine";
                case StructureKind.GumdropGrove: return "Gumdrop Grove";
                case StructureKind.NodeHollow: return "The Node Hollow";
                default: return null;
            }
        }
    }
}