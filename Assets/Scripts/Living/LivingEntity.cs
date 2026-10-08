using UnityEngine;
using DivergentGenesis.Core;
using DivergentGenesis.Audio;
using DivergentGenesis.Items;
using DivergentGenesis.Player;
using DivergentGenesis.World;

namespace DivergentGenesis.Living
{
    /// <summary>
    /// One living thing: body, physics, health, animation and the state its brain
    /// needs. Deliberately not a MonoBehaviour - entities are plain objects driven
    /// by <see cref="EntityManager"/>, so spawning a hundred of them costs no
    /// scene-graph bookkeeping beyond the rig they are attached to.
    /// </summary>
    public sealed class LivingEntity : IDamageable
    {
        public MobDef Def;
        public int Id;
        public int Variant;

        public Vector3 Position;          // feet centre
        public Vector3 Velocity;
        public float Yaw;
        public float Health;
        public bool Alive = true;
        public bool Despawn;

        public bool Grounded;
        public bool InWater;
        public bool Flying;

        public float Age;
        public float AttackTimer;
        public float HurtTimer;
        public float IdleTimer;
        public float ThinkTimer;
        public float AnimPhase;
        public float BoredomTimer;

        public Vector3 WanderTarget;
        public bool HasWander;
        public LivingEntity Target;
        public float TargetTimer;

        /// <summary>Where this entity is running from, and for how much longer.</summary>
        public Vector3 FleeFrom;
        public float FleeTimer;
        public bool Hungry;

        // --- dragon / boss state ------------------------------------------------
        /// <summary>0 strafe, 1 dive, 2 perch, 3 nova. Only dragons use it.</summary>
        public int State;
        public float StateTimer;
        public float FireTimer;
        /// <summary>Boss escalation: 0 aerial, 1 grounded, 2 desperate.</summary>
        public int Phase;
        /// <summary>Current and desired roll, so a dragon banks into turns.</summary>
        public float Bank;
        public float BankTarget;

        /// <summary>Set when a player pets a cute mob: it follows them afterwards.</summary>
        public bool IsPet;

        /// <summary>Village this NPC belongs to, or -1.</summary>
        public int HomeVillage = -1;
        public Vector3 HomePoint;
        public bool Sleeping;

        public EntityRig Rig;
        public float Height;
        public float Radius;
        public float BodyY;

        // ------------------------------------------------------------ IDamageable
        public Vector3 Center { get { return Position + new Vector3(0f, Height * 0.5f, 0f); } }
        float IDamageable.Radius { get { return Mathf.Max(0.25f, Radius); } }
        public bool IsAlive { get { return Alive; } }
        public string DisplayName { get { return Def != null ? Def.Name : "?"; } }

        public bool IsBoss { get { return Def != null && Def.Behaviour == EntityBehaviour.Boss; } }
        public bool IsVillager { get { return Def != null && (Def.Behaviour == EntityBehaviour.Villager || Def.Behaviour == EntityBehaviour.Guard); } }
        public bool IsHostile { get { return Def != null && (Def.HostileToPlayer || Def.Behaviour == EntityBehaviour.Hostile || Def.Behaviour == EntityBehaviour.Bandit); } }

        // =============================================================== lifecycle
        public static LivingEntity Create(MobDef def, Vector3 position, EntityRig rig, int id)
        {
            var e = new LivingEntity
            {
                Def = def,
                Position = position,
                Health = def.Health,
                Rig = rig,
                Id = id,
                BodyY = rig != null ? rig.BodyY : def.Height * 0.5f
            };
            e.Height = def.Height;
            e.Radius = def.Radius;
            e.Yaw = Hash.Float01(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z), id) * 360f;
            e.Flying = def.Flying;
            e.Variant = (int)(Hash.Hash2(id, id * 7 + 13, 991) % 4u);

            if (rig != null && rig.Root != null)
            {
                rig.Root.transform.position = position;
                rig.Root.transform.rotation = Quaternion.Euler(0f, e.Yaw, 0f);
            }
            return e;
        }

        // ================================================================== update
        public void Step(MobContext ctx, float dt)
        {
            if (!Alive) return;

            Age += dt;
            AttackTimer -= dt;
            HurtTimer -= dt;
            TargetTimer -= dt;
            ThinkTimer -= dt;
            AnimPhase += dt;

            Vector3 wish = MobBrains.Think(this, ctx, dt);

            // knockback decays fast so hits feel like hits, not like ice
            if (HurtTimer > 0f) {
                Velocity.x = Mathf.MoveTowards(Velocity.x, 0f, 6f * dt);
                Velocity.z = Mathf.MoveTowards(Velocity.z, 0f, 6f * dt);
            } else if (!Flying)
            {
                Velocity.x = wish.x;
                Velocity.z = wish.z;
            }
            else
            {
                Velocity.x = Mathf.MoveTowards(Velocity.x, wish.x, 9f * dt);
                Velocity.z = Mathf.MoveTowards(Velocity.z, wish.z, 9f * dt);
            }

            // Flyers take their altitude straight from the brain. Walkers keep
            // whatever gravity gave them, and only a jump request overrides it -
            // so a mob that walks off a cliff actually falls.
            if (Flying) Velocity.y = wish.y;
            else if (wish.y > 0.01f && Grounded) Velocity.y = wish.y;

            Integrate(ctx, dt);
            Animate(dt);

            if (Position.y < -6f) Kill(ctx, false);
        }

        // ================================================================= physics
        private void Integrate(MobContext ctx, float dt)
        {
            var world = ChunkManager.Instance;
            if (world == null) return;

            Vector3 p = Position;

            if (Def.Aquatic)
            {
                InWater = world.GetBlock(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z)) == Blocks.Water;
                if (InWater)
                {
                    Velocity.y = Mathf.Clamp(Velocity.y, -2.5f, 3f);
                    Velocity.y += Mathf.Sin(Age * 1.6f) * 0.6f;
                }
            }

            // ---- vertical
            float fromY = p.y;
            float toY = fromY + Velocity.y * dt;
            p.y = toY;
            if (Collides(world, p))
            {
                p.y = SweepY(world, fromY, toY);
                if (Velocity.y < 0f) { Grounded = true; }
                Velocity.y = 0f;
            }
            else
            {
                Grounded = false;
            }

            // ---- horizontal, one axis at a time
            float fromX = p.x;
            float toX = fromX + Velocity.x * dt;
            p.x = toX;
            if (Collides(world, p))
            {
                float solved = SweepX(world, fromX, toX);
                if (!TryStepUp(world, solved)) { p.x = solved; Velocity.x = 0f; }
                else p = _stepPos;
            }

            float fromZ = p.z;
            float toZ = fromZ + Velocity.z * dt;
            p.z = toZ;
            if (Collides(world, p))
            {
                float solved = SweepZ(world, fromZ, toZ);
                if (!TryStepUpZ(world, solved)) { p.z = solved; Velocity.z = 0f; }
                else p = _stepPos;
            }

            p.y = Mathf.Clamp(p.y, 0f, WorldConfig.ChunkHeight - 2f);
            Position = p;

            // no swimming out of the world: the border is a wall for mobs too
            Position = DGBorder.Clamp(Position);

            if (!Grounded && !Flying && !InWater) Velocity.y += -26f * dt;
        }

        private Vector3 _stepPos;

        private bool TryStepUp(ChunkManager world, float solvedX)
        {
            if (Flying || !Grounded) return false;
            float step = 1.05f;
            Vector3 probe = new Vector3(solvedX, Position.y + step, Position.z);
            if (Collides(world, probe)) return false;
            // settle back down onto the step
            for (int i = 0; i < 6; i++)
            {
                Vector3 lower = new Vector3(probe.x, probe.y - 0.2f, probe.z);
                if (Collides(world, lower)) break;
                probe = lower;
            }
            _stepPos = probe;
            return true;
        }

        private bool TryStepUpZ(ChunkManager world, float solvedZ)
        {
            if (Flying || !Grounded) return false;
            float step = 1.05f;
            Vector3 probe = new Vector3(Position.x, Position.y + step, solvedZ);
            if (Collides(world, probe)) return false;
            for (int i = 0; i < 6; i++)
            {
                Vector3 lower = new Vector3(probe.x, probe.y - 0.2f, probe.z);
                if (Collides(world, lower)) break;
                probe = lower;
            }
            _stepPos = probe;
            return true;
        }

        private bool Collides(ChunkManager world, Vector3 p)
        {
            float r = Mathf.Max(0.2f, Radius);
            int minX = Mathf.FloorToInt(p.x - r), maxX = Mathf.FloorToInt(p.x + r);
            int minY = Mathf.FloorToInt(p.y + 0.02f), maxY = Mathf.FloorToInt(p.y + Height - 0.02f);
            int minZ = Mathf.FloorToInt(p.z - r), maxZ = Mathf.FloorToInt(p.z + r);

            for (int y = minY; y <= maxY; y++)
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                byte b = world.GetBlock(x, y, z);
                if (b == Blocks.Air || b == Blocks.Water) continue;
                if (BlockDef.IsSolid(b)) return true;
            }
            return false;
        }

        private float SweepY(ChunkManager world, float from, float to)
        {
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 6; i++)
            {
                float mid = (lo + hi) * 0.5f;
                var p = Position; p.y = Mathf.Lerp(from, to, mid);
                if (Collides(world, p)) hi = mid; else lo = mid;
            }
            return Mathf.Lerp(from, to, lo);
        }

        private float SweepX(ChunkManager world, float from, float to)
        {
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 6; i++)
            {
                float mid = (lo + hi) * 0.5f;
                var p = Position; p.x = Mathf.Lerp(from, to, mid);
                if (Collides(world, p)) hi = mid; else lo = mid;
            }
            return Mathf.Lerp(from, to, lo);
        }

        private float SweepZ(ChunkManager world, float from, float to)
        {
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 6; i++)
            {
                float mid = (lo + hi) * 0.5f;
                var p = Position; p.z = Mathf.Lerp(from, to, mid);
                if (Collides(world, p)) hi = mid; else lo = mid;
            }
            return Mathf.Lerp(from, to, lo);
        }

        /// <summary>Walks forward looking for the ground, used by spawn placement and wandering.</summary>
        public static bool SampleGround(ChunkManager world, float x, float z, out float y)
        {
            y = 0f;
            if (world == null) return false;
            int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z);
            for (int yy = WorldConfig.ChunkHeight - 2; yy > 1; yy--)
            {
                byte b = world.GetBlock(ix, yy, iz);
                if (b == Blocks.Air || b == Blocks.Water) continue;
                if (!BlockDef.IsSolid(b)) continue;
                y = yy + 1f;
                return true;
            }
            return false;
        }

        // ================================================================ interaction
        public void FaceTowards(Vector3 target, float dt, float turnSpeed)
        {
            float dx = target.x - Position.x, dz = target.z - Position.z;
            if (Mathf.Abs(dx) + Mathf.Abs(dz) < 1e-4f) return;
            float want = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            Yaw = Mathf.LerpAngle(Yaw, want, Mathf.Clamp01(turnSpeed * dt));
        }

        public void Hurt(Vector3 direction, float amount)
        {
            Velocity += direction.normalized * amount * 0.55f + Vector3.up * 2.2f;
            HurtTimer = 0.35f;
        }

        // ==================================================================== damage
        public void TakeDamage(float amount, Vector3 from, Vector3 knockback)
        {
            if (!Alive) return;

            Health -= amount;
            HurtTimer = 0.32f;

            Vector3 dir = Position - from;
            dir.y = 0.3f;
            Hurt(dir, Mathf.Min(amount, 12f));

            // being hit makes everything care about you
            TargetTimer = 12f;
            if (Def.Behaviour == EntityBehaviour.Skittish || Def.Behaviour == EntityBehaviour.Passive)
                MobBrains.SetFleeing(this, from);
            if (Def.Behaviour == EntityBehaviour.Cute)
                MobBrains.SetFleeing(this, from);

            EntityManager.Instance.NotifyDamageTaken(this, from, amount);

            if (Health <= 0f) Kill(EntityManager.Context, true);
            else { var a = AudioBank.Instance; if (a != null) a.Play("hit"); }
        }

        public void Kill(MobContext ctx, bool dropLoot)
        {
            if (!Alive) return;
            Alive = false;
            Despawn = true;

            if (dropLoot && ctx != null && ctx.World != null)
            {
                TryDrop(Def.Drop1, Def.Drop1Min, Def.Drop1Max, ctx.World);
                TryDrop(Def.Drop2, Def.Drop2Min, Def.Drop2Max, ctx.World);
            }

            EntityManager.Instance.NotifyDeath(this);

            var audio = AudioBank.Instance;
            if (audio != null) audio.Play(IsBoss ? "dragon_roar" : "mob_die");
        }

        private void TryDrop(ItemId id, int min, int max, ChunkManager world)
        {
            if (id == ItemId.None || max <= 0) return;
            int n = Random.Range(min, max + 1);
            if (n <= 0) return;
            DropManager.Spawn(world, Center, id, n);
        }

        // ================================================================= animation
        private float _legSwing;
        private float _attackAnim;

        private void Animate(float dt)
        {
            if (Rig == null || Rig.Root == null) return;

            var t = Rig.Root.transform;
            t.position = Position;
            t.rotation = Quaternion.Euler(0f, Yaw, 0f);

            float speed = new Vector2(Velocity.x, Velocity.z).magnitude;
            float stride = Mathf.Clamp01(speed / Mathf.Max(0.5f, Def.Speed));
            _legSwing += dt * (2.2f + stride * 8.5f);

            if (_attackAnim > 0f) _attackAnim -= dt;

            bool flying = Flying || (Def.Flying && !Grounded);
            float hover = flying ? Mathf.Sin(Age * 2.6f) * 0.14f : 0f;
            float bodyBob = stride * 0.055f * Mathf.Abs(Mathf.Sin(_legSwing * 2f));

            if (Rig.Body != null)
            {
                Rig.Body.localPosition = new Vector3(0f, Rig.BodyY + bodyBob + hover, 0f);
                float lean = Mathf.Clamp(Velocity.y * -0.02f, -0.22f, 0.22f);
                Rig.Body.localRotation = Quaternion.Euler(lean, 0f, Mathf.Sin(_legSwing) * 0.05f * stride);
            }

            // legs
            if (Rig.Legs != null)
            {
                for (int i = 0; i < Rig.Legs.Length; i++)
                {
                    var leg = Rig.Legs[i];
                    if (leg == null) continue;
                    float phase = _legSwing + (i % 2 == 0 ? 0f : Mathf.PI) + (i >= 2 ? Mathf.PI * 0.65f : 0f);
                    float swing = flying ? Mathf.Sin(Age * 2.6f + i) * 0.25f : Mathf.Sin(phase) * (0.30f + stride * 0.55f) * stride;
                    leg.localRotation = Quaternion.Euler(swing * 57.2958f, 0f, 0f);
                }
            }

            // arms counter-swing on bipeds
            if (Rig.Arms != null)
            {
                for (int i = 0; i < Rig.Arms.Length; i++)
                {
                    var arm = Rig.Arms[i];
                    if (arm == null) continue;
                    float phase = _legSwing + (i == 0 ? Mathf.PI : 0f);
                    float swing = Mathf.Sin(phase) * (0.22f + stride * 0.5f) * stride;
                    if (_attackAnim > 0f) swing -= 1.4f * Mathf.Sin(_attackAnim * Mathf.PI * 3.3f);
                    arm.localRotation = Quaternion.Euler(swing * 57.2958f, 0f, 0f);
                }
            }

            // head: slight counter-bob plus a look-at when it has a target
            if (Rig.Head != null)
            {
                float yaw = Mathf.Sin(Age * 0.7f + Id * 0.37f) * 12f;
                if (Target != null && TargetTimer > 0f)
                {
                    Vector3 d = Target.Center - Center;
                    yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg - Yaw;
                    yaw = Mathf.Clamp(yaw, -70f, 70f);
                }
                Rig.Head.localRotation = Quaternion.Euler(Rig.Biped ? 0f : Mathf.Sin(_legSwing * 2f) * 4f, yaw, 0f);
            }

            if (Rig.Jaw != null && Def.Body == EntityArchetype.Dragon)
                Rig.Jaw.localRotation = Quaternion.Euler(_attackAnim > 0f ? 34f : 4f, 0f, 0f);

            // necks and tails undulate as a curve, not as independent parts
            AnimateChain(Rig.NeckChain, 0.09f, 0.6f, _legSwing * 0.5f);
            AnimateChain(Rig.TailChain, 0.11f, 0.5f, -_legSwing * 0.45f);

            // wings
            float flapSpeed = flying ? 5.4f : 1.1f;
            float flapAmp = flying ? 46f : 8f;
            float flap = Mathf.Sin(Age * flapSpeed) * flapAmp;
            if (Rig.WingL != null) Rig.WingL.localRotation = Quaternion.Euler(0f, 0f, flap);
            if (Rig.WingR != null) Rig.WingR.localRotation = Quaternion.Euler(0f, 0f, -flap);
            if (Rig.WingTipL != null) Rig.WingTipL.localRotation = Quaternion.Euler(0f, 0f, flap * 0.55f);
            if (Rig.WingTipR != null) Rig.WingTipR.localRotation = Quaternion.Euler(0f, 0f, -flap * 0.55f);

            if (Rig.Extra != null)
            {
                Rig.Extra.localRotation = Quaternion.Euler(0f, Age * 74f, 0f);
                Rig.Extra.localPosition = new Vector3(0f, Mathf.Sin(Age * 1.7f) * 0.12f, 0f);
            }

            // hurt flash: pull the whole body down and back for a moment
            if (HurtTimer > 0f)
            {
                float k = Mathf.Clamp01(HurtTimer / 0.32f);
                if (Rig.Body != null)
                    Rig.Body.localRotation = Quaternion.Euler(Rig.Body.localRotation.eulerAngles.x - 12f * k, 0f, 0f);
            }

            // blob squash and stretch
            if (Def.Body == EntityArchetype.Blob && Rig.Body != null)
            {
                float squash = 1f + Mathf.Sin(Age * 4.2f) * 0.06f * (0.4f + stride);
                Rig.Body.localScale = new Vector3(1f / squash, squash, 1f / squash);
            }
        }

        private void AnimateChain(Transform[] chain, float amount, float speed, float phase)
        {
            if (chain == null) return;
            for (int i = 0; i < chain.Length; i++)
            {
                if (chain[i] == null) continue;
                float x = Mathf.Sin(Age * speed * 3.4f + phase + i * 0.55f) * amount * 57.2958f;
                float y = Mathf.Sin(Age * speed * 2.1f + phase + i * 0.31f) * amount * 0.5f * 57.2958f;
                chain[i].localRotation = Quaternion.Euler(x * 0.5f, y, i == 0 ? 0f : 0f);
            }
        }

        /// <summary>Called by the brain right before a swing lands, for the arm animation.</summary>
        public void TriggerAttackAnim() { _attackAnim = 0.34f; }
    }
}
