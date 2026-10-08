using System.Collections.Generic;
using UnityEngine;
using DivergentGenesis.Audio;
using DivergentGenesis.Core;
using DivergentGenesis.Decor;
using DivergentGenesis.Items;
using DivergentGenesis.Player;
using DivergentGenesis.Render;
using DivergentGenesis.World;

namespace DivergentGenesis.Living
{
    /// <summary>Where NPCs belong. Registered by the structure system as you explore.</summary>
    public enum NpcSiteKind : byte { Village = 0, BanditCamp = 1, NodeVillage = 2, Shrine = 3 }

    public struct NpcSite
    {
        public Vector3 Center;
        public NpcSiteKind Kind;
        public int Id;
        public int Population;
        public int Spawned;
    }

    /// <summary>
    /// Owns every living thing that is not the player: streaming, spawning,
    /// despawning, projectiles and the boss lifecycle.
    ///
    /// Entities are pooled per species and only exist within a ring around the
    /// player, exactly like chunks. Nothing here allocates in the steady state.
    /// </summary>
    public sealed class EntityManager : MonoBehaviour
    {
        public static EntityManager Instance;
        public static MobContext Context = new MobContext();

        public readonly List<LivingEntity> All = new List<LivingEntity>(64);

        [Header("Streaming")]
        [Tooltip("Hard cap on simultaneous creatures. Quality tier scales this.")]
        public int MaxEntities = 30;
        public float SpawnMinDistance = 26f;
        public float SpawnMaxDistance = 74f;
        public float DespawnDistance = 116f;
        public float SpawnInterval = 0.55f;

        /// <summary>The active boss, if the ritual has been performed.</summary>
        public LivingEntity Boss { get; private set; }
        public int BossPhase { get; private set; }
        public float BossHealth01 { get { return Boss != null && Boss.Alive ? Mathf.Clamp01(Boss.Health / Mathf.Max(1f, Boss.Def.Health)) : 0f; } }

        public event System.Action<LivingEntity> BossSpawned;
        public event System.Action<LivingEntity> BossDefeated;
        public event System.Action<LivingEntity, int> BossPhaseChanged;
        public event System.Action<LivingEntity, float> EntityDamaged;
        /// <summary>Raised when the player taps a villager: the HUD opens the trade window.</summary>
        public event System.Action<LivingEntity> TradeRequested;

        private ChunkManager _world;
        private PlayerController _player;
        private PlayerStats _stats;
        private InventoryModel _inventory;
        private int _seed;
        private int _nextId = 1;
        private float _spawnTimer;
        private float _siteTimer;

        private readonly Dictionary<MobId, Stack<LivingEntity>> _pool = new Dictionary<MobId, Stack<LivingEntity>>();
        private readonly List<NpcSite> _sites = new List<NpcSite>(16);
        private readonly List<Projectile> _bullets = new List<Projectile>(32);
        private readonly MobDef[] _rollTable = new MobDef[MobDatabase.Count];
        private readonly List<MobId> _nearby = new List<MobId>(24);

        private Transform _root;

        // ================================================================= setup
        public void Configure(int seed, ChunkManager world, PlayerController player, PlayerStats stats,
                              InventoryModel inventory, GraphicsTier tier)
        {
            Instance = this;
            _seed = seed;
            _world = world;
            _player = player;
            _stats = stats;
            _inventory = inventory;

            MaxEntities = tier >= GraphicsTier.High ? 42 : tier >= GraphicsTier.Medium ? 32 : tier >= GraphicsTier.Low ? 22 : 14;
            SpawnMaxDistance = tier >= GraphicsTier.Medium ? 78f : 60f;

            _root = transform;
            BuildProjectilePool();
        }

        public void Reset()
        {
            for (int i = 0; i < All.Count; i++) Despawn(All[i], true);
            All.Clear();
            _sites.Clear();
            Boss = null;
            BossPhase = 0;
            _bullets.Clear();
            _nextId = 1;
        }

        // ================================================================ spawning
        public LivingEntity Spawn(MobId id, Vector3 position)
        {
            if (!MobDatabase.IsDefined(id)) return null;
            var def = MobDatabase.Get(id);

            LivingEntity e;
            Stack<LivingEntity> pool;
            if (_pool.TryGetValue(id, out pool) && pool.Count > 0)
            {
                e = pool.Pop();
                e.Def = def;
                e.Position = position;
                e.Velocity = Vector3.zero;
                e.Health = def.Health;
                e.Alive = true;
                e.Despawn = false;
                e.Age = 0f;
                e.Id = _nextId++;
                e.State = 0; e.StateTimer = 0f; e.FireTimer = 0f; e.Phase = 0;
                e.FleeTimer = 0f; e.TargetTimer = 0f; e.IsPet = false; e.Sleeping = false;
                e.HasWander = false; e.ThinkTimer = 0f; e.HurtTimer = 0f; e.AttackTimer = 0f;
                e.Flying = def.Flying;
                e.Rig.SetVisible(true);
                e.Rig.Root.transform.position = position;
            }
            else
            {
                var rig = EntityMeshFactory.Build(def);
                rig.Root.transform.SetParent(_root, false);
                e = LivingEntity.Create(def, position, rig, _nextId++);
            }

            // settle onto the ground so nothing spawns inside a hill
            float y;
            var world = ChunkManager.Instance;
            if (!def.Flying && !def.Aquatic && LivingEntity.SampleGround(world, position.x, position.z, out y))
                e.Position = new Vector3(position.x, y, position.z);

            e.Height = def.Height;
            e.Radius = def.Radius;
            e.BodyY = e.Rig != null ? e.Rig.BodyY : def.Height * 0.5f;
            e.HomeVillage = -1;

            All.Add(e);
            return e;
        }

        /// <summary>Spawns the boss and announces it.</summary>
        public LivingEntity SpawnBoss(MobId id, Vector3 position)
        {
            if (Boss != null && Boss.Alive) return Boss;
            var e = Spawn(id, position);
            if (e == null) return null;

            Boss = e;
            BossPhase = 0;
            e.HomePoint = position;

            var a = AudioBank.Instance;
            if (a != null) a.Play("dragon_roar");

            if (BossSpawned != null) BossSpawned(e);
            return e;
        }

        public bool BossAlive { get { return Boss != null && Boss.Alive; } }

        private void Despawn(LivingEntity e, bool immediate)
        {
            if (e == null) return;
            e.Alive = false;
            e.Despawn = true;

            Stack<LivingEntity> pool;
            if (!_pool.TryGetValue(e.Def.Id, out pool))
            {
                pool = new Stack<LivingEntity>(8);
                _pool[e.Def.Id] = pool;
            }

            if (e.Rig != null) e.Rig.SetVisible(false);
            pool.Push(e);
        }

        // =============================================================== NPC sites
        public void RegisterSite(Vector3 center, NpcSiteKind kind, int id)
        {
            for (int i = 0; i < _sites.Count; i++)
            {
                var s = _sites[i];
                if (s.Id == id && Vector3.Distance(s.Center, center) < 8f) return;
            }

            _sites.Add(new NpcSite
            {
                Center = center,
                Kind = kind,
                Id = id,
                Population = kind == NpcSiteKind.Village ? Random.Range(5, 9)
                           : kind == NpcSiteKind.NodeVillage ? Random.Range(4, 8)
                           : kind == NpcSiteKind.BanditCamp ? Random.Range(3, 7) : 1
            });
        }

        public void SpawnVillageAt(Vector3 center, NpcSiteKind kind)
        {
            int guardCount = kind == NpcSiteKind.NodeVillage ? 1 : 2;
            int villagerCount = Random.Range(3, 6);

            for (int i = 0; i < villagerCount; i++)
            {
                MobId id;
                if (kind == NpcSiteKind.NodeVillage) id = MobId.Fluff;
                else
                {
                    float r = Random.value;
                    id = r < 0.38f ? MobId.Villager : r < 0.60f ? MobId.Farmer : r < 0.78f ? MobId.Smith : MobId.Scribe;
                }
                SpawnAtSite(id, center, 16f, kind);
            }

            if (kind != NpcSiteKind.NodeVillage)
            {
                for (int i = 0; i < guardCount; i++) SpawnAtSite(MobId.Guard, center, 18f, kind);
            }
        }

        public void SpawnBanditsAt(Vector3 center)
        {
            int n = Random.Range(3, 7);
            for (int i = 0; i < n; i++)
            {
                float r = Random.value;
                MobId id = r < 0.55f ? MobId.Bandit : r < 0.85f ? MobId.BanditArcher : MobId.BanditChief;
                SpawnAtSite(id, center, 14f, NpcSiteKind.BanditCamp);
            }
        }

        private void SpawnAtSite(MobId id, Vector3 center, float radius, NpcSiteKind kind)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float a = Random.value * Mathf.PI * 2f;
                float r = Random.Range(3f, radius);
                var p = new Vector3(center.x + Mathf.Cos(a) * r, center.y, center.z + Mathf.Sin(a) * r);
                var e = Spawn(id, p);
                if (e == null) continue;
                if (Mathf.Abs(e.Position.y - center.y) > 6f)
                {
                    Despawn(e, true);
                    All.Remove(e);
                    continue;
                }
                e.HomeVillage = 0;
                e.HomePoint = e.Position;
                return;
            }
        }

        // ================================================================= update
        private void Update()
        {
            if (Instance != this) Instance = this;
            Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            if (_world == null || _player == null) return;

            Context.World = _world;
            Context.Player = _player;
            Context.Stats = _stats;
            Context.Inventory = _inventory;
            Context.Seed = _seed;
            Context.ViewDistance = _world.Quality != null ? _world.Quality.ViewDistanceMeters : 1024f;

            Vector3 pp = _player.transform.position;

            for (int i = All.Count - 1; i >= 0; i--)
            {
                var e = All[i];

                if (!e.Alive || e.Despawn)
                {
                    if (e == Boss) HandleBossGone(e);
                    Despawn(e, false);
                    All.RemoveAt(i);
                    continue;
                }

                float d = Vector3.Distance(e.Position, pp);
                if (d > DespawnDistance && !e.IsPet && !e.IsBoss)
                {
                    Despawn(e, false);
                    All.RemoveAt(i);
                    continue;
                }

                // frozen beyond the spawn ring: no cost, no pop-in animation
                if (d < SpawnMaxDistance + 40f || e.IsBoss || e.IsPet)
                    e.Step(Context, dt);
                else
                    e.Rig.SetVisible(false);
            }

            UpdateProjectiles(dt);
            UpdateSites(dt, pp);
            UpdateSpawning(dt, pp);
        }

        private void HandleBossGone(LivingEntity e)
        {
            if (e == Boss) Boss = null;
            BossPhase = 0;
            if (BossDefeated != null) BossDefeated(e);
        }

        // ================================================================ spawning
        private void UpdateSpawning(float dt, Vector3 pp)
        {
            _spawnTimer -= dt;
            if (_spawnTimer > 0f) return;
            _spawnTimer = SpawnInterval;

            if (All.Count >= MaxEntities) return;

            bool node = DimensionState.NodeActive;
            bool night = Context.Night;
            int count = MobDatabase.FillTable(_rollTable, night, node, !node);
            if (count <= 0) return;

            // weighted pick, biased to whatever is already around, so herds form
            GatherNearbySpecies(pp);

            int attempts = 4;
            for (int i = 0; i < attempts; i++)
            {
                var def = PickWeighted(count, pp);
                if (def == null) continue;

                float a = Random.value * Mathf.PI * 2f;
                float r = Random.Range(Mathf.Max(SpawnMinDistance, def.MinSpawnDistance),
                                       Mathf.Min(SpawnMaxDistance, def.MaxSpawnDistance));
                var p = new Vector3(pp.x + Mathf.Cos(a) * r, pp.y, pp.z + Mathf.Sin(a) * r);

                float y;
                if (def.Flying)
                {
                    y = _world.GetTerrainHeight(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z)) + Random.Range(8f, 22f);
                }
                else if (def.Aquatic)
                {
                    y = WorldConfig.SeaLevel - 0.5f;
                }
                else if (!LivingEntity.SampleGround(_world, p.x, p.z, out y))
                {
                    continue;
                }
                p.y = y;

                if (!IsHabitatOk(def, p, node)) continue;

                Spawn(def.Id, p);
                if (All.Count >= MaxEntities) return;
            }
        }

        private void GatherNearbySpecies(Vector3 pp)
        {
            _nearby.Clear();
            for (int i = 0; i < All.Count; i++)
            {
                var e = All[i];
                if (e.IsBoss) continue;
                if (Vector3.Distance(e.Position, pp) < 44f) _nearby.Add(e.Def.Id);
            }
        }

        private MobDef PickWeighted(int count, Vector3 pp)
        {
            if (count <= 0) return null;

            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                var d = _rollTable[i];
                float w = d.SpawnWeight;
                // herds: if this species is already here, it is more likely again
                for (int k = 0; k < _nearby.Count; k++) if (_nearby[k] == d.Id) { w *= 1.7f; break; }
                total += w;
            }

            float roll = Random.value * total;
            for (int i = 0; i < count; i++)
            {
                var d = _rollTable[i];
                float w = d.SpawnWeight;
                for (int k = 0; k < _nearby.Count; k++) if (_nearby[k] == d.Id) { w *= 1.7f; break; }
                roll -= w;
                if (roll <= 0f) return d;
            }
            return _rollTable[count - 1];
        }

        private bool IsHabitatOk(MobDef def, Vector3 p, bool node)
        {
            if (!node)
            {
                var sample = new ColumnSample();
                var gen = _world.Generator;
                if (gen != null)
                {
                    gen.Sample(p.x, p.z, 2, ref sample);
                    BiomeType b = sample.Biome;
                    if (def.Aquatic && (b == BiomeType.Desert || b == BiomeType.Mesa || b == BiomeType.SnowPeak)) return false;
                    if (def.Behaviour == EntityBehaviour.Hostile && b == BiomeType.Ocean) return false;
                }
                // hostiles do not spawn on top of the player's campfire
                if (def.HostileToPlayer && Vector3.Distance(p, _player.transform.position) < 20f) return false;
            }

            // never in the middle of a block
            byte here = _world.GetBlock(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));
            if (BlockDef.IsSolid(here)) return false;
            byte above = _world.GetBlock(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y) + 1, Mathf.FloorToInt(p.z));
            if (BlockDef.IsSolid(above)) return false;

            return true;
        }

        // ================================================================== sites
        private void UpdateSites(float dt, Vector3 pp)
        {
            _siteTimer -= dt;
            if (_siteTimer > 0f) return;
            _siteTimer = 2.5f;

            for (int i = _sites.Count - 1; i >= 0; i--)
            {
                var s = _sites[i];
                float d = Vector3.Distance(new Vector3(s.Center.x, pp.y, s.Center.z), pp);
                if (d > 260f) { _sites.RemoveAt(i); continue; }
                if (d > 96f || s.Spawned >= s.Population) continue;

                int live = 0;
                for (int k = 0; k < All.Count; k++)
                    if (All[k].HomeVillage == 0 && Vector3.Distance(All[k].HomePoint, s.Center) < 30f) live++;

                if (live >= s.Population) { s.Spawned = live; _sites[i] = s; continue; }

                if (s.Kind == NpcSiteKind.BanditCamp) SpawnBanditsAt(s.Center);
                else SpawnVillageAt(s.Center, s.Kind);

                s.Spawned = s.Population;
                _sites[i] = s;
            }
        }

        // ============================================================== querying
        public LivingEntity Nearest(Vector3 p, float maxDistance, System.Func<LivingEntity, bool> filter)
        {
            LivingEntity best = null;
            float bestD = maxDistance;
            for (int i = 0; i < All.Count; i++)
            {
                var e = All[i];
                if (!e.Alive) continue;
                if (filter != null && !filter(e)) continue;
                float d = Vector3.Distance(e.Position, p);
                if (d < bestD) { bestD = d; best = e; }
            }
            return best;
        }

        public LivingEntity NearestPrey(LivingEntity hunter, float range)
        {
            LivingEntity best = null;
            float bestD = range;
            for (int i = 0; i < All.Count; i++)
            {
                var e = All[i];
                if (!e.Alive || e == hunter) continue;
                var b = e.Def.Behaviour;
                if (b != EntityBehaviour.Passive && b != EntityBehaviour.Skittish && b != EntityBehaviour.Cute) continue;
                if (e.Def.Body == EntityArchetype.Dragon) continue;
                float d = Vector3.Distance(e.Position, hunter.Position);
                if (d < bestD) { bestD = d; best = e; }
            }
            return best;
        }

        /// <summary>Anything a guard would draw a sword on.</summary>
        public LivingEntity NearestHostileTo(LivingEntity defender, float range)
        {
            LivingEntity best = null;
            float bestD = range;
            for (int i = 0; i < All.Count; i++)
            {
                var e = All[i];
                if (!e.Alive || e == defender) continue;
                var b = e.Def.Behaviour;
                if (b != EntityBehaviour.Hostile && b != EntityBehaviour.Bandit) continue;
                float d = Vector3.Distance(e.Position, defender.Position);
                if (d < bestD) { bestD = d; best = e; }
            }
            return best;
        }

        public LivingEntity NearestVillager(LivingEntity from, float range)
        {
            LivingEntity best = null;
            float bestD = range;
            for (int i = 0; i < All.Count; i++)
            {
                var e = All[i];
                if (!e.Alive || e == from) continue;
                if (!e.IsVillager) continue;
                float d = Vector3.Distance(e.Position, from.Position);
                if (d < bestD) { bestD = d; best = e; }
            }
            return best;
        }

        /// <summary>The creature under the crosshair, or null.</summary>
        public LivingEntity Raycast(Vector3 origin, Vector3 direction, float maxDistance, out float hitDistance)
        {
            LivingEntity best = null;
            hitDistance = maxDistance;
            direction = direction.normalized;

            for (int i = 0; i < All.Count; i++)
            {
                var e = All[i];
                if (!e.Alive) continue;
                if (!e.Rig.Root.activeSelf) continue;

                Vector3 toCentre = e.Center - origin;
                float along = Vector3.Dot(toCentre, direction);
                if (along < 0f || along > maxDistance) continue;

                float radius = Mathf.Max(0.35f, e.Radius + 0.22f);
                float lateral = (toCentre - direction * along).magnitude;
                if (lateral > radius) continue;

                if (along < hitDistance) { hitDistance = along; best = e; }
            }
            return best;
        }

        // ============================================================ projectiles
        private sealed class Projectile
        {
            public GameObject Go;
            public Vector3 Position;
            public Vector3 Target;
            public Vector3 Velocity;
            public float Damage;
            public float Life;
            public LivingEntity Owner;
            public bool TargetsPlayer;
            public bool Active;
        }

        private void BuildProjectilePool()
        {
            var meshBuilder = new PrimitiveMesher.Builder();
            meshBuilder.Sphere(Vector3.zero, 0.22f, 8, 6, new Color32(255, 226, 150, 255));
            meshBuilder.Sphere(Vector3.zero, 0.34f, 6, 4, new Color32(255, 150, 70, 255));
            var mesh = meshBuilder.ToMesh("DGProjectile");
            for (int i = 0; i < 24; i++)
            {
                var go = new GameObject("Projectile" + i);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = MaterialLibrary.EntityGlow;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.SetActive(false);
                _bullets.Add(new Projectile { Go = go });
            }
        }

        public void SpawnProjectile(Vector3 from, Vector3 to, float damage, Color32 color, LivingEntity owner, bool targetsPlayer)
        {
            Projectile p = null;
            for (int i = 0; i < _bullets.Count; i++) if (!_bullets[i].Active) { p = _bullets[i]; break; }
            if (p == null) return;

            p.Active = true;
            p.Position = from;
            p.Target = to;
            p.Damage = damage;
            p.Owner = owner;
            p.TargetsPlayer = targetsPlayer;
            p.Life = 4f;
            p.Velocity = (to - from).normalized * 22f;
            p.Go.SetActive(true);
            p.Go.transform.position = from;
        }

        private void UpdateProjectiles(float dt)
        {
            Vector3 pp = _player != null ? _player.transform.position + Vector3.up : Vector3.zero;

            for (int i = 0; i < _bullets.Count; i++)
            {
                var p = _bullets[i];
                if (!p.Active) continue;

                p.Life -= dt;
                p.Position += p.Velocity * dt;

                bool done = false;

                if (p.TargetsPlayer)
                {
                    if (Vector3.Distance(p.Position, pp) < 1.1f)
                    {
                        if (_stats != null)
                        {
                            Vector3 knock = p.Velocity.normalized * 4.5f;
                            _stats.Damage(p.Damage, knock);
                        }
                        done = true;
                    }
                }
                else
                {
                    for (int k = 0; k < All.Count; k++)
                    {
                        var e = All[k];
                        if (!e.Alive || e == p.Owner) continue;
                        if (Vector3.Distance(p.Position, e.Center) > e.Radius + 0.6f) continue;
                        e.TakeDamage(p.Damage, p.Position, p.Velocity.normalized * 3f);
                        done = true;
                        break;
                    }
                }

                if ((p.Position - p.Target).sqrMagnitude < 1.4f) done = true;
                if (p.Life <= 0f) done = true;

                byte here = BlockDef.IsSolid(_world.GetBlock(Mathf.FloorToInt(p.Position.x), Mathf.FloorToInt(p.Position.y), Mathf.FloorToInt(p.Position.z))) ? (byte)1 : (byte)0;
                if (here != 0) done = true;

                if (done)
                {
                    // a fireball that lands on terrain leaves a small fire
                    if (p.Owner != null && p.Owner.Def.Body == EntityArchetype.Dragon && Random.value < 0.35f)
                        IgniteGround(p.Position);

                    p.Active = false;
                    p.Go.SetActive(false);
                    continue;
                }

                p.Go.transform.position = p.Position;
                float s = 1f + Mathf.Sin(p.Life * 22f) * 0.18f;
                p.Go.transform.localScale = new Vector3(s, s, s);
            }
        }

        private void IgniteGround(Vector3 at)
        {
            var world = _world;
            if (world == null) return;
            int fx = Mathf.FloorToInt(at.x), fy = Mathf.FloorToInt(at.y), fz = Mathf.FloorToInt(at.z);
            for (int dy = -1; dy <= 1; dy++)
            {
                byte below = world.GetBlock(fx, fy + dy, fz);
                byte above = world.GetBlock(fx, fy + dy + 1, fz);
                if (below != Blocks.Air && below != Blocks.Water && BlockDef.IsSolid(below) && above == Blocks.Air)
                {
                    world.SetBlock(fx, fy + dy + 1, fz, Blocks.Campfire);
                    break;
                }
            }
        }

        // ================================================================== events
        public void NotifyDamageTaken(LivingEntity e, Vector3 from, float amount)
        {
            // nearby allies of the victim jump in
            for (int i = 0; i < All.Count; i++)
            {
                var other = All[i];
                if (other == e || !other.Alive) continue;
                if (other.Def.Id != e.Def.Id) continue;
                if (Vector3.Distance(other.Position, e.Position) > 16f) continue;
                other.TargetTimer = 10f;
            }

            if (EntityDamaged != null) EntityDamaged(e, amount);
        }

        public void NotifyDeath(LivingEntity e)
        {
            if (e == Boss)
            {
                Boss = null;
                BossPhase = 0;
                if (BossDefeated != null) BossDefeated(e);
            }
        }

        public void NotifyBossPhase(LivingEntity e, int phase)
        {
            if (e != Boss) return;
            BossPhase = phase;
            if (BossPhaseChanged != null) BossPhaseChanged(e, phase);
        }

        /// <summary>Pets follow you: called by PlayerInteraction when a cute mob is petted.</summary>
        public void PetEntity(LivingEntity e)
        {
            if (e == null) return;
            e.IsPet = true;
            e.FleeTimer = 0f;
            var a = AudioBank.Instance;
            if (a != null) a.Play("pet");
        }

        /// <summary>The player tapped a villager: ask the HUD to open a trade window.</summary>
        public void RequestTrade(LivingEntity e)
        {
            if (e == null || !e.Def.Tradable) return;
            e.FaceTowards(_player != null ? _player.transform.position : e.Position, 1f, 4f);
            if (TradeRequested != null) TradeRequested(e);
        }

        /// <summary>Nearest pettable creature, for the interact prompt.</summary>
        public LivingEntity NearestPetable(Vector3 p, float range)
        {
            LivingEntity best = null;
            float bestD = range;
            for (int i = 0; i < All.Count; i++)
            {
                var e = All[i];
                if (!e.Alive || !e.Def.Petable) continue;
                float d = Vector3.Distance(e.Position, p);
                if (d < bestD) { bestD = d; best = e; }
            }
            return best;
        }

        public int Population { get { return All.Count; } }
    }
}
