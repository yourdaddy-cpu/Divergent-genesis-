using System.Collections.Generic;
using UnityEngine;
using DivergentGenesis.Core;
using DivergentGenesis.Decor;
using DivergentGenesis.Items;
using DivergentGenesis.Render;
using DivergentGenesis.World;

namespace DivergentGenesis.Player
{
    /// <summary>
    /// What the player is holding, in front of the camera, animated.
    ///
    /// Every tool is built from primitives at runtime and cached per item id, so
    /// there are no model assets, and the motion is procedural: idle sway that
    /// follows your walk, a mining rhythm timed to the block you are breaking, an
    /// attack arc, a placement punch, and a raise whenever you switch item.
    /// </summary>
    public sealed class HeldItemView : MonoBehaviour
    {
        public PlayerController Player;
        public PlayerCamera View;
        public PlayerInteraction Interaction;
        public InventoryModel Inventory;

        [Header("Placement")]
        public Vector3 RestPosition = new Vector3(0.36f, -0.30f, 0.62f);
        public float Scale = 0.42f;

        [Header("Motion")]
        public float SwayAmount = 0.035f;
        public float BobSpeed = 9.5f;

        private Transform _hand;
        private Transform _pivot;
        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private ItemId _current = ItemId.None;
        private int _meshFor = -1;

        private float _swing;            // 0..1, one shot
        private float _equip;            // 0..1, raise on switch
        private float _bob;
        private float _mineTimer;
        private float _punch;
        private float _swayX, _swayY;
        private Vector3 _lastPlayerPos;
        private float _speed;
        private float _hitFlash;

        private static readonly Dictionary<int, Mesh> MeshCache = new Dictionary<int, Mesh>(64);

        public void Configure(PlayerController player, PlayerCamera view, PlayerInteraction interaction, InventoryModel inventory)
        {
            Player = player;
            View = view;
            Interaction = interaction;
            Inventory = inventory;

            Build();
            RefreshItem(true);
        }

        private void Build()
        {
            Transform parent = View != null ? View.transform : transform;

            var handGo = new GameObject("HeldItem");
            handGo.transform.SetParent(parent, false);
            _hand = handGo.transform;
            _hand.localPosition = RestPosition;
            _hand.localRotation = Quaternion.identity;
            _hand.localScale = Vector3.one * Scale;

            var pivotGo = new GameObject("Pivot");
            pivotGo.transform.SetParent(_hand, false);
            _pivot = pivotGo.transform;

            var meshGo = new GameObject("Mesh");
            meshGo.transform.SetParent(_pivot, false);
            _filter = meshGo.AddComponent<MeshFilter>();
            _renderer = meshGo.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = MaterialLibrary.Entity;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        }

        // ================================================================ meshes
        private static Mesh MeshFor(ItemId id)
        {
            int key = (int)id;
            Mesh m;
            if (MeshCache.TryGetValue(key, out m) && m != null) return m;

            var def = ItemDef.Get(id);
            var b = new PrimitiveMesher.Builder();
            Color32 main = def.Color;
            Color32 dark = new Color32((byte)(main.r * 0.55f), (byte)(main.g * 0.55f), (byte)(main.b * 0.55f), 255);
            Color32 wood = new Color32(146, 110, 70, 255);

            if (def.IsBlock)
            {
                b.Box(Vector3.zero, new Vector3(0.34f, 0.34f, 0.34f), main);
                // a lighter top face so the cube reads as a block, not a card
                b.Box(new Vector3(0f, 0.175f, 0f), new Vector3(0.35f, 0.02f, 0.35f), new Color32(255, 255, 255, 90));
            }
            else if (def.IsTool)
            {
                // handle, always angled the same way so the family reads as a set
                b.Box(new Vector3(0f, -0.04f, 0f), new Vector3(0.055f, 0.52f, 0.055f), wood);
                switch (def.Tool)
                {
                    case ToolClass.Sword:
                        b.Box(new Vector3(0f, 0.20f, 0f), new Vector3(0.24f, 0.045f, 0.075f), dark);   // guard
                        b.Box(new Vector3(0f, 0.46f, 0f), new Vector3(0.075f, 0.50f, 0.040f), main);  // blade
                        b.Box(new Vector3(0f, 0.72f, 0f), new Vector3(0.05f, 0.06f, 0.030f), main);   // tip
                        break;
                    case ToolClass.Pickaxe:
                        b.Box(new Vector3(0f, 0.26f, 0f), new Vector3(0.44f, 0.055f, 0.075f), main);
                        b.Box(new Vector3(-0.20f, 0.20f, 0f), new Vector3(0.06f, 0.14f, 0.070f), main);
                        b.Box(new Vector3(0.20f, 0.20f, 0f), new Vector3(0.06f, 0.14f, 0.070f), main);
                        break;
                    case ToolClass.Axe:
                        b.Box(new Vector3(0.09f, 0.22f, 0f), new Vector3(0.20f, 0.20f, 0.070f), main);
                        b.Box(new Vector3(0.19f, 0.22f, 0f), new Vector3(0.05f, 0.26f, 0.060f), dark);
                        break;
                    default: // shovel
                        b.Box(new Vector3(0f, 0.28f, 0f), new Vector3(0.17f, 0.20f, 0.055f), main);
                        break;
                }
            }
            else
            {
                // food, materials, currency: a small bundle
                b.Box(Vector3.zero, new Vector3(0.26f, 0.26f, 0.26f), main);
                b.Box(new Vector3(0f, 0.10f, 0f), new Vector3(0.28f, 0.06f, 0.28f), dark);
            }

            m = b.ToMesh("Held_" + id);
            MeshCache[key] = m;
            return m;
        }

        // ================================================================= update
        public void RefreshItem(bool instant)
        {
            if (Inventory == null) return;

            var stack = Inventory.SelectedStack;
            ItemId id = stack.IsEmpty ? ItemId.None : stack.Id;
            if (id == _current && !instant) return;

            _current = id;
            if (id == ItemId.None)
            {
                if (_renderer != null) _renderer.enabled = false;
                return;
            }

            if (_renderer != null) _renderer.enabled = true;
            if (_filter != null)
            {
                int key = (int)id;
                if (_meshFor != key)
                {
                    _filter.sharedMesh = MeshFor(id);
                    _meshFor = key;
                }
            }

            if (!instant) _equip = 0f;
        }

        private void Update()
        {
            if (View == null || Player == null) return;

            float dt = Time.deltaTime;
            RefreshItem(false);

            // ---- measured walk speed, for the bob and sway
            Vector3 pos = Player.transform.position;
            Vector3 delta = pos - _lastPlayerPos;
            _lastPlayerPos = pos;
            delta.y = 0f;
            float instantSpeed = dt > 0f ? delta.magnitude / dt : 0f;
            if (Player.Velocity.sqrMagnitude > instantSpeed * instantSpeed) instantSpeed = Player.Velocity.magnitude;
            _speed = Mathf.Lerp(_speed, instantSpeed, Mathf.Clamp01(dt * 6f));

            _equip = Mathf.MoveTowards(_equip, 1f, dt * 4.5f);
            _hitFlash = Mathf.MoveTowards(_hitFlash, 0f, dt * 4f);

            // ---- mining: a steady rhythm that speeds up with tool tier
            bool mining = Interaction != null && Interaction.CurrentKind == TargetKind.Block &&
                          InputHub.AttackHeld && Interaction.MineProgress > 0.01f;
            if (mining)
            {
                float period = 0.42f;
                var held = Inventory != null ? Inventory.SelectedStack : ItemStack.Empty;
                if (!held.IsEmpty && held.Def.IsTool) period = Mathf.Lerp(0.42f, 0.24f, held.Def.Tier / 3f);
                _mineTimer += dt / period;
                _swing = Mathf.Abs(Mathf.Sin(_mineTimer * Mathf.PI));
                _punch = Mathf.Max(_punch, 0.25f);
            }

            if (InputHub.AttackPressed) { _swing = 0f; _attackPhase = 1f; }
            if (_attackPhase > 0f)
            {
                _attackPhase -= dt * 3.1f;
                _swing = Mathf.Sin(Mathf.Clamp01(1f - _attackPhase / 1f) * Mathf.PI);
                if (_attackPhase <= 0f) { _attackPhase = 0f; _swing = 0f; _mineTimer = 0f; }
            }
            else if (!mining)
            {
                _swing = Mathf.MoveTowards(_swing, 0f, dt * 4f);
                _mineTimer = 0f;
            }

            if (InputHub.InteractPressed) _punch = 1f;
            _punch = Mathf.MoveTowards(_punch, 0f, dt * 4.5f);

            // ---- sway: the item lags behind the camera, which is what sells weight
            float speed01 = Mathf.Clamp01(_speed / 6f);
            _bob += dt * BobSpeed * (0.35f + speed01);
            // LookDelta is in radians; a mouse flick should shift the item a little
            float targetSwayX = Mathf.Clamp(-InputHub.LookDelta.x * -1.6f, -0.06f, 0.06f);
            float targetSwayY = Mathf.Clamp(-InputHub.LookDelta.y * 1.6f, -0.05f, 0.05f);
            _swayX = Mathf.Lerp(_swayX, targetSwayX, Mathf.Clamp01(dt * 7f));
            _swayY = Mathf.Lerp(_swayY, targetSwayY, Mathf.Clamp01(dt * 7f));

            float bobX = Mathf.Sin(_bob) * SwayAmount * (0.35f + speed01);
            float bobY = Mathf.Abs(Mathf.Cos(_bob)) * SwayAmount * (0.30f + speed01) - SwayAmount * 0.5f;

            // ---- assemble
            float equipRise = (1f - _equip) * (1f - _equip) * 0.55f;
            Vector3 rest = RestPosition + new Vector3(bobX + _swayX, bobY + _swayY - equipRise, -_punch * 0.06f);
            _hand.localPosition = Vector3.Lerp(_hand.localPosition, rest, Mathf.Clamp01(dt * 14f));

            // swing arc: forward pitch plus a touch of roll
            float swingPitch = _swing * 78f;
            float swingRoll = Mathf.Sin(_swing * Mathf.PI) * 18f;
            float idleRoll = Mathf.Sin(_bob * 0.5f) * 3f * (0.3f + speed01);
            _pivot.localRotation = Quaternion.Euler(swingPitch - _punch * 22f, -swingRoll * 0.3f, idleRoll + swingRoll);

            float scale = Scale * (1f + _punch * 0.10f);
            _hand.localScale = Vector3.one * scale;

            // mining arms forward a little
            _pivot.localPosition = new Vector3(_swing * 0.02f, _swing * -0.01f, _swing * -0.10f);
        }

        private float _attackPhase;

        /// <summary>Called by PlayerInteraction when a swing lands, for the recoil.</summary>
        public void NotifyHit() { _hitFlash = 1f; }
    }
}