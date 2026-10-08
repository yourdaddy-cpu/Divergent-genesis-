using UnityEngine;
using DivergentGenesis.Audio;
using DivergentGenesis.Core;
using DivergentGenesis.Items;
using DivergentGenesis.Player;
using DivergentGenesis.World;

namespace DivergentGenesis.Living
{
    /// <summary>Everything the AI needs to know about the world this frame.</summary>
    public sealed class MobContext
    {
        public ChunkManager World;
        public PlayerController Player;
        public PlayerStats Stats;
        public InventoryModel Inventory;
        public float TimeOfDay = 0.3f;
        public int Seed;
        public float ViewDistance = 1024f;

        public bool Night { get { return TimeOfDay < 0.22f || TimeOfDay > 0.80f; } }

        public Vector3 PlayerPosition
        {
            get { return Player != null ? Player.transform.position : Vector3.zero; }
        }

        public bool PlayerExists { get { return Player != null && Stats != null && Stats.IsAlive; } }
    }

    /// <summary>
    /// The behaviour table. One function per way of being alive, all sharing the
    /// same steering, attacking and target-acquisition helpers.
    /// </summary>
    public static class MobBrains
    {
        /// <summary>Returns the velocity the entity wants: x/z horizontal, y = jump or altitude.</summary>
        public static Vector3 Think(LivingEntity e, MobContext ctx, float dt)
        {
            if (ctx == null || ctx.World == null) return Vector3.zero;

            if (e.FleeTimer > 0f) e.FleeTimer -= dt;

            switch (e.Def.Behaviour)
            {
                case EntityBehaviour.Passive: return Passive(e, ctx, dt);
                case EntityBehaviour.Skittish: return Skittish(e, ctx, dt);
                case EntityBehaviour.Predator: return Predator(e, ctx, dt);
                case EntityBehaviour.Hostile: return Hostile(e, ctx, dt);
                case EntityBehaviour.Villager: return Villager(e, ctx, dt);
                case EntityBehaviour.Guard: return Guard(e, ctx, dt);
                case EntityBehaviour.Bandit: return Bandit(e, ctx, dt);
                case EntityBehaviour.Cute: return Cute(e, ctx, dt);
                case EntityBehaviour.Boss: return DragonBrain.Think(e, ctx, dt);
                default:
                    if (e.Def.Body == EntityArchetype.Dragon) return DragonBrain.Think(e, ctx, dt);
                    return Passive(e, ctx, dt);
            }
        }

        public static void SetFleeing(LivingEntity e, Vector3 from)
        {
            e.FleeFrom = from;
            e.FleeTimer = 7f;
        }

        // ============================================================== steering
        public static Vector3 Steer(LivingEntity e, Vector3 target, float speed, float dt, float stopDist)
        {
            Vector3 d = target - e.Position;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist < stopDist) return Vector3.zero;

            Vector3 dir = d / dist;
            Vector3 v = dir * speed;
            e.FaceTowards(target, dt, 7f);

            // hop over anything a step cannot clear
            var world = ChunkManager.Instance;
            if (world != null && e.Grounded && !e.Flying)
            {
                float probeDist = e.Radius + 0.65f;
                int ax = Mathf.FloorToInt(e.Position.x + dir.x * probeDist);
                int az = Mathf.FloorToInt(e.Position.z + dir.z * probeDist);
                int ay = Mathf.FloorToInt(e.Position.y + 0.35f);
                byte b = world.GetBlock(ax, ay, az);
                if (b != Blocks.Air && b != Blocks.Water && BlockDef.IsSolid(b)) v.y = 7.4f;
            }
            return v;
        }

        public static Vector3 Flee(LivingEntity e, MobContext ctx, float dt)
        {
            Vector3 away = e.Position - e.FleeFrom;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
            {
                float a = (e.Id * 0.77f) % (Mathf.PI * 2f);
                away = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            }
            Vector3 dir = away.normalized;
            Vector3 target = e.Position + dir * 12f;

            float y;
            if (LivingEntity.SampleGround(ctx.World, target.x, target.z, out y) &&
                Mathf.Abs(y - e.Position.y) < 6f)
                target.y = y;

            return Steer(e, target, e.Def.Speed * 1.25f, dt, 1.0f);
        }

        // =============================================================== habits
        private static Vector3 Wander(LivingEntity e, MobContext ctx, float dt, float radius, float speedScale)
        {
            if (e.ThinkTimer <= 0f)
            {
                e.ThinkTimer = Random.Range(2.4f, 6.5f);
                if (Random.value < 0.32f)
                {
                    e.HasWander = false;
                }
                else
                {
                    Vector3 centre = e.HomeVillage >= 0 ? e.HomePoint : e.Position;
                    float a = Random.value * Mathf.PI * 2f;
                    float r = Random.Range(radius * 0.35f, radius);
                    float x = centre.x + Mathf.Cos(a) * r;
                    float z = centre.z + Mathf.Sin(a) * r;
                    float y;
                    if (LivingEntity.SampleGround(ctx.World, x, z, out y) && Mathf.Abs(y - e.Position.y) < 7f)
                    {
                        e.WanderTarget = new Vector3(x, y, z);
                        e.HasWander = true;
                    }
                    else e.HasWander = false;
                }
            }

            if (!e.HasWander) return Vector3.zero;
            return Steer(e, e.WanderTarget, e.Def.Speed * speedScale, dt, 1.4f);
        }

        private static bool PlayerNear(LivingEntity e, MobContext ctx, float range, out Vector3 pos)
        {
            pos = Vector3.zero;
            if (!ctx.PlayerExists) return false;
            pos = ctx.PlayerPosition;
            float dx = pos.x - e.Position.x, dz = pos.z - e.Position.z, dy = pos.y - e.Position.y;
            if (Mathf.Abs(dy) > 14f) return false;
            return dx * dx + dz * dz <= range * range;
        }

        // ============================================================ behaviours
        private static Vector3 Passive(LivingEntity e, MobContext ctx, float dt)
        {
            Vector3 threat;
            if (e.FleeTimer > 0f) return Flee(e, ctx, dt);
            if (PlayerNear(e, ctx, e.Def.FleeRange * 0.55f, out threat) && e.Def.Body != EntityArchetype.Dragon)
            {
                // grazers shuffle aside rather than panicking
                return Steer(e, e.Position + (e.Position - threat).normalized * 6f, e.Def.Speed * 0.9f, dt, 1.0f);
            }
            return Wander(e, ctx, dt, 12f, 0.55f);
        }

        private static Vector3 Skittish(LivingEntity e, MobContext ctx, float dt)
        {
            Vector3 threat;
            if (e.FleeTimer > 0f) return Flee(e, ctx, dt);
            if (PlayerNear(e, ctx, e.Def.FleeRange, out threat))
            {
                SetFleeing(e, threat);
                return Flee(e, ctx, dt);
            }
            return Wander(e, ctx, dt, 14f, 0.6f);
        }

        private static Vector3 Predator(LivingEntity e, MobContext ctx, float dt)
        {
            // hunt the nearest edible thing, unless the player has annoyed it
            bool wantPlayer = e.TargetTimer > 0f;
            LivingEntity prey = EntityManager.Instance != null
                ? EntityManager.Instance.NearestPrey(e, e.Def.SightRange)
                : null;

            if (wantPlayer && PlayerNear(e, ctx, e.Def.AggroRange, out Vector3 pp))
            {
                float dist = Vector3.Distance(e.Position, pp);
                if (dist <= e.Def.AttackRange + 1.1f) MeleeAttackOnPlayer(e, ctx, pp);
                return Steer(e, pp, e.Def.Speed, dt, e.Def.AttackRange * 0.7f);
            }

            if (!wantPlayer && prey != null)
            {
                MeleeAttackOnEntity(e, ctx, prey);
                return Steer(e, prey.Position, e.Def.Speed, dt, prey.Radius + 0.7f);
            }

            // bears will still take a swing at you if you get in their face
            if (e.Def.HostileToPlayer && PlayerNear(e, ctx, e.Def.AggroRange, out Vector3 p2))
            {
                MeleeAttackOnPlayer(e, ctx, p2);
                return Steer(e, p2, e.Def.Speed, dt, e.Def.AttackRange * 0.7f);
            }

            return Wander(e, ctx, dt, 20f, 0.7f);
        }

        private static Vector3 Hostile(LivingEntity e, MobContext ctx, float dt)
        {
            bool awake = !e.Def.Nocturnal || ctx.Night || e.TargetTimer > 0f;
            if (!awake) return Wander(e, ctx, dt, 10f, 0.4f);

            if (PlayerNear(e, ctx, e.Def.AggroRange, out Vector3 pp))
            {
                float dist = Vector3.Distance(e.Position, pp);
                if (e.Def.AttackRange > 6f)
                {
                    // archers and casters keep their distance and fire
                    RangedAttack(e, ctx, pp);
                    if (dist < e.Def.AttackRange * 0.55f)
                        return Steer(e, e.Position + (e.Position - pp).normalized * 8f, e.Def.Speed, dt, 1f);
                    return Steer(e, pp, e.Def.Speed * 0.5f, dt, e.Def.AttackRange * 0.6f);
                }

                if (dist <= e.Def.AttackRange + 1.0f) MeleeAttackOnPlayer(e, ctx, pp);
                return Steer(e, pp, e.Def.Speed, dt, e.Def.AttackRange * 0.7f);
            }

            return Wander(e, ctx, dt, 16f, 0.6f);
        }

        private static Vector3 Villager(LivingEntity e, MobContext ctx, float dt)
        {
            // hit by something? run for the nearest wall
            if (e.FleeTimer > 0f)
            {
                Vector3 home = e.HomeVillage >= 0 ? e.HomePoint : e.Position;
                return Steer(e, home, e.Def.Speed * 1.2f, dt, 2.5f);
            }

            if (ctx.Night)
            {
                e.Sleeping = true;
                Vector3 home = e.HomeVillage >= 0 ? e.HomePoint : e.Position;
                float d = Vector3.Distance(e.Position, home);
                if (d > 3.5f) return Steer(e, home, e.Def.Speed, dt, 3f);
                return Vector3.zero;                       // tucked up for the night
            }

            e.Sleeping = false;
            return Wander(e, ctx, dt, 16f, 0.45f);
        }

        private static Vector3 Guard(LivingEntity e, MobContext ctx, float dt)
        {
            LivingEntity foe = EntityManager.Instance != null
                ? EntityManager.Instance.NearestHostileTo(e, e.Def.AggroRange)
                : null;

            if (foe != null)
            {
                MeleeAttackOnEntity(e, ctx, foe);
                Vector3 home = e.HomeVillage >= 0 ? e.HomePoint : e.Position;
                if (Vector3.Distance(e.Position, home) > 30f)
                    return Steer(e, foe.Position, e.Def.Speed, dt, foe.Radius + 0.8f);
                return Steer(e, foe.Position, e.Def.Speed, dt, foe.Radius + 0.8f);
            }

            if (e.TargetTimer > 0f && PlayerNear(e, ctx, e.Def.AggroRange, out Vector3 pp))
            {
                MeleeAttackOnPlayer(e, ctx, pp);
                return Steer(e, pp, e.Def.Speed, dt, e.Def.AttackRange * 0.7f);
            }

            return Wander(e, ctx, dt, 22f, 0.5f);
        }

        private static Vector3 Bandit(LivingEntity e, MobContext ctx, float dt)
        {
            // villagers first: a raid is more interesting than a chase
            LivingEntity villager = EntityManager.Instance != null
                ? EntityManager.Instance.NearestVillager(e, 44f)
                : null;

            if (PlayerNear(e, ctx, e.Def.AggroRange, out Vector3 pp))
            {
                float dist = Vector3.Distance(e.Position, pp);
                if (e.Def.AttackRange > 6f)
                {
                    RangedAttack(e, ctx, pp);
                    return Steer(e, pp, e.Def.Speed * 0.6f, dt, e.Def.AttackRange * 0.7f);
                }
                if (dist <= e.Def.AttackRange + 1.0f) MeleeAttackOnPlayer(e, ctx, pp);
                return Steer(e, pp, e.Def.Speed, dt, e.Def.AttackRange * 0.7f);
            }

            if (villager != null)
            {
                MeleeAttackOnEntity(e, ctx, villager);
                return Steer(e, villager.Position, e.Def.Speed, dt, villager.Radius + 0.7f);
            }

            // otherwise drift towards the nearest village to look for trouble
            if (e.HomeVillage >= 0)
            {
                float d = Vector3.Distance(e.Position, e.HomePoint);
                if (d > 26f) return Steer(e, e.HomePoint, e.Def.Speed * 0.7f, dt, 20f);
            }

            return Wander(e, ctx, dt, 26f, 0.6f);
        }

        private static Vector3 Cute(LivingEntity e, MobContext ctx, float dt)
        {
            if (e.FleeTimer > 0f) return Flee(e, ctx, dt);

            // a petted cute mob follows its friend around
            if (e.IsPet && PlayerNear(e, ctx, 26f, out Vector3 pp))
            {
                float d = Vector3.Distance(e.Position, pp);
                if (d < 2.6f) return Vector3.zero;
                Vector3 v = Steer(e, pp, e.Def.Speed * 1.15f, dt, 2.2f);
                // little hops while trotting along
                if (e.Grounded && Mathf.Sin(e.Age * 4.1f) > 0.72f) v.y = 6.2f;
                return v;
            }

            Vector3 wander = Wander(e, ctx, dt, 11f, 0.55f);
            // bouncy little hops, and the occasional full spin, are the whole point
            if (e.Grounded && wander.sqrMagnitude > 0.01f && Mathf.Sin(e.Age * 3.3f) > 0.78f) wander.y = 6.0f;
            return wander;
        }

        // ============================================================== attacking
        private static void MeleeAttackOnPlayer(LivingEntity e, MobContext ctx, Vector3 playerPos)
        {
            if (e.AttackTimer > 0f || ctx.Stats == null) return;
            if (Vector3.Distance(e.Position, playerPos) > e.Def.AttackRange + 0.9f) return;

            e.AttackTimer = e.Def.AttackCooldown;
            e.TriggerAttackAnim();

            Vector3 knock = playerPos - e.Position;
            knock.y = 0f;
            knock = knock.normalized * 3.4f;
            ctx.Stats.Damage(e.Def.Damage, knock);

            var a = AudioBank.Instance;
            if (a != null) a.Play("mob_attack");
        }

        private static void MeleeAttackOnEntity(LivingEntity e, MobContext ctx, LivingEntity foe)
        {
            if (foe == null || !foe.Alive) return;
            if (e.AttackTimer > 0f) return;
            if (Vector3.Distance(e.Position, foe.Position) > e.Def.AttackRange + 1.0f) return;

            e.AttackTimer = e.Def.AttackCooldown;
            e.TriggerAttackAnim();

            Vector3 knock = foe.Position - e.Position;
            knock.y = 0f;
            foe.TakeDamage(e.Def.Damage, e.Center, knock.normalized * 3.2f);
        }

        private static void RangedAttack(LivingEntity e, MobContext ctx, Vector3 playerPos)
        {
            if (e.AttackTimer > 0f) return;
            if (Vector3.Distance(e.Position, playerPos) > e.Def.AttackRange) return;

            e.AttackTimer = e.Def.AttackCooldown * 1.6f;
            e.TriggerAttackAnim();

            var mgr = EntityManager.Instance;
            if (mgr == null) return;
            mgr.SpawnProjectile(e.Center + new Vector3(0f, 0.4f, 0f), playerPos + new Vector3(0f, 0.9f, 0f),
                                e.Def.Damage, e.Def.Accent, e, true);

            var a = AudioBank.Instance;
            if (a != null) a.Play("bow");
        }
    }

    /// <summary>
    /// Dragon flight, breath and boss phases.
    ///
    /// The flight model is a simple one: a dragon holds an altitude band around
    /// its target, banks into turns, and only commits to a dive when it is lined
    /// up. The Elder Dragon escalates through three phases as its health falls,
    /// and every phase change is broadcast to the HUD.
    /// </summary>
    public static class DragonBrain
    {
        private const float StrafeAltitude = 15f;
        private const float PerchAltitude = 2.2f;

        public static Vector3 Think(LivingEntity e, MobContext ctx, float dt)
        {
            bool boss = e.IsBoss;

            if (e.StateTimer > 0f) e.StateTimer -= dt;
            if (e.FireTimer > 0f) e.FireTimer -= dt;

            if (boss) UpdatePhase(e, ctx);

            // pick a target: the player, or the nearest villager to terrorise
            if (e.TargetTimer <= 0f)
            {
                e.Target = null;
                if (ctx.PlayerExists) e.TargetTimer = 6f;
                else e.Target = EntityManager.Instance != null ? EntityManager.Instance.NearestVillager(e, 60f) : null;
            }

            Vector3 focus = e.TargetTimer > 0f ? ctx.PlayerPosition : (e.Target != null ? e.Target.Position : e.Position);
            float dist = Vector3.Distance(e.Position, focus);
            float altitude = e.Position.y - focus.y;

            if (e.StateTimer <= 0f) ChooseState(e, boss, dist, altitude);

            switch (e.State)
            {
                case 0: return Strafe(e, ctx, focus, dt);
                case 1: return Dive(e, ctx, focus, dt, dist, altitude);
                case 2: return Perch(e, ctx, focus, dt, dist);
                default: return Nova(e, ctx, focus, dt);
            }
        }

        private static void ChooseState(LivingEntity e, bool boss, float dist, float altitude)
        {
            // tiers below the boss only ever strafe and dive
            if (!boss)
            {
                e.State = (dist < 12f && altitude < 4f) ? 2 : (dist < 26f ? 1 : 0);
                e.StateTimer = Random.Range(2.6f, 5.2f);
                return;
            }

            switch (e.Phase)
            {
                case 2:
                    // desperation: alternating novas and strafing runs
                    e.State = Random.value < 0.55f ? 3 : 1;
                    e.StateTimer = Random.Range(3.2f, 5.4f);
                    break;
                case 1:
                    e.State = Random.value < 0.5f ? 1 : 2;
                    e.StateTimer = Random.Range(4f, 7f);
                    break;
                default:
                    e.State = Random.value < 0.7f ? 0 : 1;
                    e.StateTimer = Random.Range(5f, 9f);
                    break;
            }
        }

        private static void UpdatePhase(LivingEntity e, MobContext ctx)
        {
            float frac = e.Health / Mathf.Max(1f, e.Def.Health);
            int want = frac > 0.66f ? 0 : (frac > 0.33f ? 1 : 2);
            if (want == e.Phase) return;

            e.Phase = want;
            e.State = 3;
            e.StateTimer = 2.6f;

            var mgr = EntityManager.Instance;
            if (mgr != null) mgr.NotifyBossPhase(e, want);

            var a = AudioBank.Instance;
            if (a != null) a.Play("dragon_roar");

            // the Elder calls friends when it gets desperate
            if (want == 2 && mgr != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    float ang = i / 3f * Mathf.PI * 2f;
                    Vector3 p = e.Position + new Vector3(Mathf.Cos(ang) * 7f, 2f, Mathf.Sin(ang) * 7f);
                    mgr.Spawn(MobId.NodeGuardian, p);
                }
            }
        }

        // ------------------------------------------------------------- states
        private static Vector3 Strafe(LivingEntity e, MobContext ctx, Vector3 focus, float dt)
        {
            float orbit = e.Age * 0.55f + e.Id * 0.7f;
            float radius = e.IsBoss ? 22f : 16f;
            Vector3 target = focus + new Vector3(Mathf.Cos(orbit) * radius, StrafeAltitude, Mathf.Sin(orbit) * radius);

            Vector3 v = FlyTowards(e, target, e.Def.Speed, dt);

            // fire a volley while circling
            if (e.FireTimer <= 0f)
            {
                e.FireTimer = e.IsBoss ? 2.1f : 3.4f;
                Fireball(e, ctx, focus, 1);
            }
            return v;
        }

        private static Vector3 Dive(LivingEntity e, MobContext ctx, Vector3 focus, float dt, float dist, float altitude)
        {
            Vector3 target = focus + new Vector3(0f, 1.6f, 0f);
            Vector3 v = FlyTowards(e, target, e.Def.Speed * 1.6f, dt);

            if (dist < e.Def.AttackRange + 1.8f && e.AttackTimer <= 0f)
            {
                e.AttackTimer = e.Def.AttackCooldown;
                e.TriggerAttackAnim();
                if (ctx.Stats != null)
                {
                    Vector3 knock = focus - e.Position;
                    knock.y = 0f;
                    ctx.Stats.Damage(e.Def.Damage, knock.normalized * 5.5f);
                }
                var a = AudioBank.Instance;
                if (a != null) a.Play("dragon_bite");
                e.StateTimer = Mathf.Min(e.StateTimer, 0.5f);
            }

            // climb back out after the pass
            if (altitude < 3f && dist > 6f) v.y = Mathf.Max(v.y, 6.5f);
            return v;
        }

        private static Vector3 Perch(LivingEntity e, MobContext ctx, Vector3 focus, float dt, float dist)
        {
            if (e.Grounded)
            {
                if (dist > 5.5f) return MobBrains.Steer(e, focus, e.Def.Speed * 0.9f, dt, 3.2f);

                if (e.FireTimer <= 0f)
                {
                    e.FireTimer = e.IsBoss ? 1.5f : 2.6f;
                    Fireball(e, ctx, focus, e.IsBoss ? 3 : 1);
                }
                return Vector3.zero;
            }

            Vector3 land = focus;
            float y;
            if (LivingEntity.SampleGround(ctx.World, focus.x, focus.z, out y)) land.y = y;
            else land.y = focus.y + PerchAltitude;
            return FlyTowards(e, land, e.Def.Speed * 1.2f, dt);
        }

        private static Vector3 Nova(LivingEntity e, MobContext ctx, Vector3 focus, float dt)
        {
            // hover, then erupt: a ring of fireballs plus terrain fire
            if (e.StateTimer > 1.1f)
                return FlyTowards(e, e.Position + new Vector3(0f, 3f, 0f), e.Def.Speed * 0.4f, dt);

            if (e.FireTimer <= 0f)
            {
                e.FireTimer = 0.18f;
                int count = 10;
                for (int i = 0; i < count; i++)
                {
                    float a = i / (float)count * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(a), -0.25f, Mathf.Sin(a));
                    SpawnFireball(e, e.Center + dir * 2f, e.Center + dir * 24f);
                }
                var a2 = AudioBank.Instance;
                if (a2 != null) a2.Play("dragon_roar");
            }

            return FlyTowards(e, focus + new Vector3(0f, 6f, 0f), e.Def.Speed * 0.5f, dt);
        }

        // -------------------------------------------------------------- helpers
        private static Vector3 FlyTowards(LivingEntity e, Vector3 target, float speed, float dt)
        {
            Vector3 d = target - e.Position;
            float dist = d.magnitude;
            if (dist < 0.6f) return Vector3.zero;

            Vector3 dir = d / dist;
            Vector3 v = dir * Mathf.Min(speed, dist * 3.2f);

            // banking: turn the body into the turn instead of snapping
            float bank = Vector3.Dot(Vector3.Cross(Vector3.up, dir), e.Velocity.normalized);
            e.BankTarget = Mathf.Clamp(-bank * 45f, -48f, 48f);

            e.FaceTowards(target, dt, 3.4f);
            return v;
        }

        private static void Fireball(LivingEntity e, MobContext ctx, Vector3 focus, int count)
        {
            var mgr = EntityManager.Instance;
            if (mgr == null) return;

            for (int i = 0; i < count; i++)
            {
                float jitter = (i - (count - 1) * 0.5f) * 1.6f;
                Vector3 to = focus + new Vector3(jitter, 0.8f, jitter * 0.6f);
                SpawnFireball(e, e.Center + new Vector3(0f, 0.2f, 0f), to);
            }
        }

        private static void SpawnFireball(LivingEntity e, Vector3 from, Vector3 to)
        {
            var mgr = EntityManager.Instance;
            if (mgr == null) return;
            mgr.SpawnProjectile(from, to, e.Def.Damage * 0.55f, e.Def.Accent, e, true);
        }
    }
}
