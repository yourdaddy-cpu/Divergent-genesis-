using UnityEngine;
using DivergentGenesis.Core;
using DivergentGenesis.World;
using DivergentGenesis.Decor;

namespace DivergentGenesis.Player
{
    /// <summary>
    /// Voxel character controller.
    ///
    /// Deliberately NOT a Unity CharacterController or MeshCollider: cooking a
    /// 32 m voxel mesh into a collider costs tens of milliseconds per tile and
    /// tanks the frame rate on mid range phones. Sweeping an AABB against the
    /// voxel grid costs microseconds and gives exact Minecraft style movement.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance;

        [Header("Body")]
        public float Radius = 0.34f;
        public float Height = 1.8f;
        public float CrouchHeight = 1.45f;
        public float EyeHeight = 1.62f;
        public float CrouchEyeHeight = 1.28f;

        [Header("Movement")]
        public float WalkSpeed = 4.4f;
        public float SprintSpeed = 7.4f;
        public float CrouchSpeed = 1.9f;
        public float SwimSpeed = 3.2f;
        public float Acceleration = 14f;
        public float AirAcceleration = 3.2f;
        public float Gravity = -28f;
        public float JumpVelocity = 8.4f;
        public float StepHeight = 0.62f;
        public float TerminalVelocity = -55f;

        [Header("State")]
        public bool Grounded { get; private set; }
        public bool InWater { get; private set; }
        public bool Sprinting { get; private set; }
        public bool Crouching { get; private set; }
        public float CurrentHeight { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Vector3 LookDirection { get; private set; }
        public float FallStartY { get; private set; }

        public PlayerStats Stats { get; private set; }
        public Transform Head;

        private Vector3 _pos;
        private float _bobTimer;
        private float _lastJumpTap = -10f;

        private void Awake()
        {
            Instance = this;
            Stats = GetComponent<PlayerStats>();
            CurrentHeight = Height;
            _pos = transform.position;
            FallStartY = _pos.y;
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);

            if (InputHub.UIBlocked)
            {
                Velocity = Vector3.Lerp(Velocity, Vector3.zero, dt * 8f);
                ApplyGravityStep(dt);
                return;
            }

            ReadLook();
            Vector3 wish = ReadMove(out bool wantSprint, out bool wantCrouch, out bool wantJump);
            UpdateFluidState();

            Sprinting = wantSprint && !Crouching && MoveInputMagnitude() > 0.2f;
            float targetSpeed = Sprinting ? SprintSpeed : (Crouching ? CrouchSpeed : WalkSpeed);
            if (InWater) targetSpeed = SwimSpeed;
            else if (InputHub.Crouch) targetSpeed = CrouchSpeed;

            Vector3 horizontal = new Vector3(Velocity.x, 0f, Velocity.z);
            float accel = Grounded ? Acceleration : AirAcceleration;
            Vector3 target = wish * targetSpeed;
            horizontal = Vector3.MoveTowards(horizontal, target, accel * dt);
            if (wish.sqrMagnitude < 0.001f && Grounded)
                horizontal = Vector3.MoveTowards(horizontal, Vector3.zero, accel * 2f * dt);

            Vector3 velocity = new Vector3(horizontal.x, Velocity.y, horizontal.z);

            // jump / swim up
            if (wantJump && !InputHub.JumpHeld && Time.time - _lastJumpTap > 0.05f) _lastJumpTap = Time.time;
            if (InputHub.JumpPressed)
            {
                if (InWater) velocity.y = 4.2f;
                else if (Grounded && !Crouching)
                {
                    velocity.y = JumpVelocity;
                    Grounded = false;
                    Stats.UseJumpStamina();
                }
            }

            velocity.y += Gravity * dt;
            if (InWater)
            {
                velocity.y = Mathf.Max(velocity.y, -4.5f);
                velocity.y *= (1f - 2.6f * dt);        // water drag
            }
            velocity.y = Mathf.Max(velocity.y, TerminalVelocity);

            Velocity = velocity;
            MoveAndCollide(dt);

            // world border + fall safety
            _pos = DGBorder.Clamp(_pos);
            if (_pos.y < -8f) { RespawnSafe(); }

            ApplyTransform(dt);
        }

        private void ReadLook()
        {
            float sens = 0.0032f;
            InputHub.Yaw += InputHub.LookDelta.x * sens;
            InputHub.Pitch = Mathf.Clamp(InputHub.Pitch + InputHub.LookDelta.y * sens, -88f, 88f);
        }

        private float MoveInputMagnitude() { return InputHub.Move.magnitude; }

        private Vector3 ReadMove(out bool sprint, out bool crouch, out bool jump)
        {
            sprint = InputHub.Sprint;
            crouch = InputHub.Crouch;
            jump = InputHub.JumpPressed;

            Vector2 m = Vector2.ClampMagnitude(InputHub.Move, 1f);
            if (m.sqrMagnitude < 0.0004f) return Vector3.zero;

            float yaw = InputHub.Yaw * Mathf.Deg2Rad;
            Vector3 fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
            Vector3 wish = right * m.x + fwd * m.y;
            return wish.normalized * m.magnitude;
        }

        private void UpdateFluidState()
        {
            InWater = ChunkManager.Instance != null &&
                      ChunkManager.Instance.GetBlock(
                          Mathf.FloorToInt(_pos.x),
                          Mathf.FloorToInt(_pos.y + 0.9f),
                          Mathf.FloorToInt(_pos.z)) == World.Blocks.Water;
        }

        private void ApplyGravityStep(float dt)
        {
            Velocity = new Vector3(Velocity.x, Velocity.y + Gravity * dt, Velocity.z);
            MoveAndCollide(dt);
            ApplyTransform(dt);
        }

        // ------------------------------------------------------------- physics
        public void MoveAndCollide(float dt)
        {
            // --- vertical -------------------------------------------------------
            float fromY = _pos.y;
            float toY = fromY + Velocity.y * dt;
            bool wasGrounded = Grounded;

            _pos.y = toY;
            bool hitY = false;
            if (Collides(_pos))
            {
                _pos.y = SweepAxis(1, fromY, toY);
                hitY = true;
                if (Velocity.y < 0f) Land();
                Velocity = new Vector3(Velocity.x, 0f, Velocity.z);
            }
            else
            {
                Grounded = false;
                if (_pos.y > FallStartY) FallStartY = _pos.y;
            }

            if (!Grounded && wasGrounded && !hitY)
            {
                // a small probe keeps the player stuck to the ground when walking downhill
                if (!CollidesAt(_pos.x, _pos.y - 0.12f, _pos.z) &&
                    CollidesAt(_pos.x, _pos.y - 0.05f, _pos.z))
                    Grounded = true;
            }

            // --- horizontal, one axis at a time so sliding along walls works -----
            float fromX = _pos.x;
            float toX = fromX + Velocity.x * dt;
            _pos.x = toX;
            if (Collides(_pos)) { _pos.x = SweepAxis(0, fromX, toX); TryStepUp(fromX, 0); Velocity = new Vector3(0f, Velocity.y, Velocity.z); }

            float fromZ = _pos.z;
            float toZ = fromZ + Velocity.z * dt;
            _pos.z = toZ;
            if (Collides(_pos)) { _pos.z = SweepAxis(2, fromZ, toZ); TryStepUp(fromZ, 2); Velocity = new Vector3(Velocity.x, Velocity.y, 0f); }

            if (PropSystem.Instance != null)
            {
                _pos.y = Mathf.Clamp(_pos.y, 0f, WorldConfig.ChunkHeight - 1f);
                PropSystem.Instance.ResolveCollision(ref _pos, Radius);
            }
        }

        /// <summary>Bisection between a free position and a blocked one. 8 steps is under a millimetre.</summary>
        private float SweepAxis(int axis, float from, float to)
        {
            if (Mathf.Abs(to - from) < 1e-5f) return from;

            float lo = 0f, hi = 1f;
            for (int i = 0; i < 8; i++)
            {
                float mid = (lo + hi) * 0.5f;
                float v = Mathf.Lerp(from, to, mid);
                if (axis == 0) _pos.x = v; else if (axis == 1) _pos.y = v; else _pos.z = v;
                if (Collides(_pos)) hi = mid; else lo = mid;
            }

            float result = Mathf.Lerp(from, to, lo);
            if (axis == 0) _pos.x = result; else if (axis == 1) _pos.y = result; else _pos.z = result;
            return result;
        }

        private void TryStepUp(float startCoord, int axis)
        {
            if (!Grounded) return;
            Vector3 raised = _pos;
            raised.y += StepHeight;
            if (Collides(raised)) return;

            Vector3 probe = raised;
            if (axis == 0) probe.x = startCoord; else probe.z = startCoord;
            if (Collides(probe)) return;

            _pos = probe;
        }

        private void Land()
        {
            if (!Grounded)
            {
                float fall = FallStartY - _pos.y;
                Stats.ApplyFallDamage(fall);
            }
            Grounded = true;
            FallStartY = _pos.y;
        }

        public bool Collides(Vector3 p)
        {
            return CollidesAt(p.x, p.y, p.z);
        }

        public bool CollidesAt(float x, float y, float z)
        {
            var m = ChunkManager.Instance;
            if (m == null) return false;

            int minX = Mathf.FloorToInt(x - Radius);
            int maxX = Mathf.FloorToInt(x + Radius);
            int minY = Mathf.FloorToInt(y + 0.02f);
            int maxY = Mathf.FloorToInt(y + CurrentHeight - 0.02f);
            int minZ = Mathf.FloorToInt(z - Radius);
            int maxZ = Mathf.FloorToInt(z + Radius);

            for (int by = minY; by <= maxY; by++)
            for (int bz = minZ; bz <= maxZ; bz++)
            for (int bx = minX; bx <= maxX; bx++)
            {
                if (m.GetBlock(bx, by, bz) == World.Blocks.Air) continue;
                if (BlockDef.IsSolid(m.GetBlock(bx, by, bz))) return true;
            }
            return false;
        }

        // ------------------------------------------------------------ transform
        private void ApplyTransform(float dt)
        {
            _bobTimer += new Vector3(Velocity.x, 0f, Velocity.z).magnitude * dt * 1.6f;

            float yaw = InputHub.Yaw;
            float pitch = InputHub.Pitch;
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            LookDirection = rot * Vector3.forward;

            Vector3 eye = _pos + new Vector3(0f, Crouching ? CrouchEyeHeight : EyeHeight, 0f);
            float bob = Grounded ? Mathf.Sin(_bobTimer * 2f) * 0.045f * Mathf.Clamp01(Velocity.magnitude / 5f) : 0f;

            transform.position = _pos;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            if (Head != null)
            {
                Head.position = eye + rot * new Vector3(0f, bob, 0f);
                Head.rotation = rot;
            }
        }

        public Vector3 EyePosition
        {
            get { return _pos + new Vector3(0f, Crouching ? CrouchEyeHeight : EyeHeight, 0f); }
        }

        public Vector3 FeetPosition { get { return _pos; } }

        /// <summary>Knockback and explosions push the player without touching internals.</summary>
        public void AddVelocity(Vector3 delta)
        {
            Velocity += delta;
        }

        public void Teleport(Vector3 position)
        {
            _pos = position;
            transform.position = position;
            Velocity = Vector3.zero;
            FallStartY = position.y;
        }

        public void RespawnSafe()
        {
            var m = ChunkManager.Instance;
            float h = m != null ? m.GetTerrainHeight(0, 0) : 70f;
            Teleport(new Vector3(0f, h + 2.5f, 0f));
        }
    }
}
