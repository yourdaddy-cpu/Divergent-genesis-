using System;
using UnityEngine;

namespace DivergentGenesis.Player
{
    /// <summary>Health, stamina, fall damage and death. Drives the HUD.</summary>
    public sealed class PlayerStats : MonoBehaviour
    {
        public const float MaxHealth = 20f;
        public const float MaxStamina = 100f;
        public const float MaxHunger = 20f;
        public const float SafeFallDistance = 3.2f;

        public float Health = MaxHealth;
        public float Stamina = MaxStamina;
        public float Hunger = MaxHunger;

        public event Action<float> HealthChanged;      // 0..1
        public event Action<float> StaminaChanged;
        public event Action<float> HungerChanged;
        public event Action<float> Damaged;            // amount, for the red flash
        public event Action Died;

        public bool Invulnerable;

        private float _regenTimer;
        private float _hungerTimer;
        private float _lastDamage = -99f;

        public bool IsAlive { get { return Health > 0f; } }

        private void Start()
        {
            Raise();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // stamina
            var pc = GetComponent<PlayerController>();
            bool running = pc != null && pc.Sprinting && new Vector2(pc.Velocity.x, pc.Velocity.z).magnitude > 1f;
            Stamina = Mathf.Clamp(Stamina + (running ? -18f : 13f) * dt, 0f, MaxStamina);
            if (!Mathf.Approximately(Stamina, _lastStamina)) { _lastStamina = Stamina; RaiseStamina(); }

            // slow regeneration once you have been out of trouble for a while
            _regenTimer += dt;
            if (_regenTimer > 4f && Health < MaxHealth && Time.time - _lastDamage > 8f && Stamina > 25f)
            {
                _regenTimer = 0f;
                Health = Mathf.Min(MaxHealth, Health + 1f);
                RaiseHealth();
            }

            // hunger drain
            _hungerTimer += dt;
            if (_hungerTimer > 45f)
            {
                _hungerTimer = 0f;
                Hunger = Mathf.Max(0f, Hunger - 1f);
                if (Hunger <= 0f) { Health = Mathf.Max(1f, Health - 0.5f); RaiseHealth(); }
                RaiseHunger();
            }
        }

        private float _lastStamina = -1f;

        public void UseJumpStamina()
        {
            Stamina = Mathf.Max(0f, Stamina - 4f);
            RaiseStamina();
        }

        public bool CanSprint { get { return Stamina > 2f; } }

        public void ApplyFallDamage(float distance)
        {
            if (distance <= SafeFallDistance || Invulnerable) return;
            float dmg = Mathf.Min(20f, (distance - SafeFallDistance) * 1.6f);
            Damage(dmg, Vector3.zero);
        }

        public void Damage(float amount, Vector3 knockback)
        {
            if (amount <= 0f || Invulnerable || !IsAlive) return;
            Health = Mathf.Max(0f, Health - amount);
            _lastDamage = Time.time;
            RaiseHealth();
            if (Damaged != null) Damaged(amount);

            var pc = GetComponent<PlayerController>();
            if (pc != null && knockback.sqrMagnitude > 0.0001f) pc.AddVelocity(knockback);

            if (Health <= 0f && Died != null) Died();
        }

        public void Heal(float amount)
        {
            Health = Mathf.Min(MaxHealth, Health + amount);
            RaiseHealth();
        }

        public void Eat(float hunger, float heal)
        {
            Hunger = Mathf.Min(MaxHunger, Hunger + hunger);
            Heal(heal);
            RaiseHunger();
        }

        public void Respawn()
        {
            Health = MaxHealth;
            Stamina = MaxStamina;
            Hunger = MaxHunger;
            Raise();
        }

        public void Raise()
        {
            RaiseHealth(); RaiseStamina(); RaiseHunger();
        }

        private void RaiseHealth() { if (HealthChanged != null) HealthChanged(Health / MaxHealth); }
        private void RaiseStamina() { if (StaminaChanged != null) StaminaChanged(Stamina / MaxStamina); }
        private void RaiseHunger() { if (HungerChanged != null) HungerChanged(Hunger / MaxHunger); }
    }
}
