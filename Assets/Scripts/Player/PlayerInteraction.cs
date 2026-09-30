using System;
using UnityEngine;
using DivergentGenesis.Core;
using DivergentGenesis.World;
using DivergentGenesis.Decor;
using DivergentGenesis.Items;
using DivergentGenesis.Audio;

namespace DivergentGenesis.Player
{
    public enum TargetKind : byte { None = 0, Block = 1, Prop = 2, Entity = 3 }

    /// <summary>
    /// Look at a block, hold to mine it, tap to place. Also chops trees, swings
    /// at anything that implements IDamageable, and eats food.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerInteraction : MonoBehaviour
    {
        public PlayerController Player;
        public PlayerCamera View;
        public PlayerStats Stats;
        public InventoryModel Inventory;
        public AudioBank Audio;

        [Header("Tuning")]
        public float AttackCooldown = 0.42f;
        public float AttackRange = 3.6f;

        // public so the HUD can bind to it
        public TargetKind CurrentKind { get; private set; }
        public string TargetName { get; private set; }
        public float MineProgress { get; private set; }
        public Vector3 HighlightCenter { get; private set; }
        public Vector3 HighlightSize { get; private set; }
        public bool HasHighlight { get; private set; }
        public event Action Changed;

        private BlockHit _block;
        private PropHit _prop;
        private IDamageable _entity;
        private float _attackTimer;
        private float _mineTime;
        private int _miningX, _miningY, _miningZ;
        private BlockHighlight _highlight;
        private int _lastBlockId = -1;
        private int _lastPropCx, _lastPropCz, _lastPropIndex = -1;
        private bool _wasSwinging;

        private void Awake()
        {
            Player = GetComponent<PlayerController>();
            Stats = GetComponent<PlayerStats>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _attackTimer -= dt;

            if (InputHub.UIBlocked)
            {
                SetNothing();
                return;
            }

            AcquireTarget();
            UpdateHighlight();

            if (InputHub.AttackPressed) Swing();
            if (InputHub.AttackHeld) { Hold(dt); UpdatePropProgress(dt); }
            else { ResetMine(); _propProgress = 0f; MineProgress = 0f; }

            if (InputHub.InteractPressed) Interact();
        }

        // ------------------------------------------------------------- targeting
        private void AcquireTarget()
        {
            if (Player == null) return;
            var world = ChunkManager.Instance;
            if (world == null) return;

            Vector3 origin = Player.EyePosition;
            Vector3 dir = View != null ? View.transform.forward : transform.forward;
            float reach = Reach;

            _entity = null;
            _prop.Hit = false;
            _block.Hit = false;

            bool blockHit = world.RaycastBlocks(origin, dir, reach, out _block);
            float bestDist = blockHit ? _block.Distance : reach;

            if (PropSystem.Instance != null && PropSystem.Instance.Raycast(origin, dir, reach, out _prop))
            {
                if (_prop.Distance < bestDist) { bestDist = _prop.Distance; blockHit = false; }
                else _prop.Hit = false;
            }

            // entities last: they sit on top of the world, nothing else to test for M1
            _entity = FindEntity(dir, bestDist);

            if (_entity != null)
            {
                CurrentKind = TargetKind.Entity;
                TargetName = _entity.GetType().Name;
                HasHighlight = true;
                HighlightCenter = _entity.Center;
                HighlightSize = Vector3.one * (_entity.Radius * 2f);
                Raise();
                return;
            }

            if (_prop.Hit)
            {
                CurrentKind = TargetKind.Prop;
                TargetName = PropName(_prop.Type);
                HasHighlight = true;
                HighlightCenter = _prop.Point;
                HighlightSize = new Vector3(1.1f, 1.1f, 1.1f);
                Raise();
                return;
            }

            if (_block.Hit)
            {
                CurrentKind = TargetKind.Block;
                TargetName = BlockDef.NameOf(_block.Block);
                HasHighlight = true;
                HighlightCenter = new Vector3(_block.X + 0.5f, _block.Y + 0.5f, _block.Z + 0.5f);
                HighlightSize = Vector3.one;
                Raise();
                return;
            }

            SetNothing();
        }

        private IDamageable FindEntity(Vector3 dir, float maxDist)
        {
            // M1: nothing alive yet. M2 mobs register here.
            return null;
        }

        private void SetNothing()
        {
            if (CurrentKind == TargetKind.None && MineProgress <= 0f) return;
            CurrentKind = TargetKind.None;
            TargetName = null;
            HasHighlight = false;
            MineProgress = 0f;
            _mineTime = 0f;
            Raise();
        }

        private void UpdateHighlight()
        {
            if (_highlight == null)
            {
                if (!HasHighlight) return;
                _highlight = BlockHighlight.Create();
            }
            if (!HasHighlight) { _highlight.gameObject.SetActive(false); return; }

            _highlight.gameObject.SetActive(true);
            _highlight.transform.position = HighlightCenter;
            _highlight.transform.localScale = HighlightSize + Vector3.one * 0.03f;
        }

        private float Reach
        {
            get
            {
                float baseReach = 4.2f;
                var m = ChunkManager.Instance;
                if (m != null && m.Quality != null) baseReach = m.Quality.BuildDistance;
                return baseReach;
            }
        }

        // --------------------------------------------------------------- mining
        private void Hold(float dt)
        {
            if (CurrentKind != TargetKind.Block) { ResetMine(); return; }
            if (_lastBlockId != _block.Block || _miningX != _block.X || _miningY != _block.Y || _miningZ != _block.Z)
            {
                _miningX = _block.X; _miningY = _block.Y; _miningZ = _block.Z;
                _lastBlockId = _block.Block;
                _mineTime = 0f;
            }

            var def = BlockDef.Def(_block.Block);
            if (!BlockDef.IsBreakable(_block.Block)) { ResetMine(); return; }

            float speed = MineSpeedFor(def);
            _mineTime += dt * speed;
            MineProgress = Mathf.Clamp01(_mineTime / Mathf.Max(0.05f, def.Hardness));

            if (Audio != null && !_wasSwinging) Audio.Play("mine");
            _wasSwinging = true;

            if (MineProgress >= 1f)
            {
                BreakBlock(_block.X, _block.Y, _block.Z, def);
                ResetMine();
                _wasSwinging = false;
            }
        }

        private float MineSpeedFor(BlockDef def)
        {
            float speed = 1f;
            var held = Inventory != null ? Inventory.SelectedStack : ItemStack.Empty;
            if (!held.IsEmpty)
            {
                var tool = held.Def;
                if (tool.Tool == def.PreferredTool || (def.PreferredTool == ToolClass.None && !tool.IsTool))
                    speed = 1f + tool.Tier * 2.4f;
                else if (tool.Tool == ToolClass.None) speed = 1f;
                else speed = 0.45f;

                if (def.RequiresTool && tool.Tool != def.PreferredTool)
                {
                    speed *= 0.3f;
                }
            }
            return speed;
        }

        private void ResetMine()
        {
            MineProgress = 0f;
            _mineTime = 0f;
            _lastBlockId = -1;
            _wasSwinging = false;
        }

        public void BreakBlock(int x, int y, int z, BlockDef def)
        {
            var world = ChunkManager.Instance;
            if (world == null) return;

            world.SetBlock(x, y, z, World.Blocks.Air);

            // grass drops dirt like Minecraft
            ItemId drop = def.Id == World.Blocks.Grass ? ItemId.Dirt : (ItemId)def.Id;
            int count = 1;

            if (def.DropItem != 0) drop = (ItemId)def.DropItem;
            if (drop != ItemId.None && Inventory != null)
            {
                int left = Inventory.Add(drop, count);
                if (left > 0) DropManager.Spawn(world, new Vector3(x + 0.5f, y + 0.5f, z + 0.5f), drop, left);
            }

            if (Audio != null) Audio.Play("break");
            Raise();
        }

        // ------------------------------------------------------------- chopping
        private void UpdatePropProgress(float dt)
        {
            if (CurrentKind != TargetKind.Prop || !InputHub.AttackHeld) return;
            _propProgress += dt;
            MineProgress = Mathf.Clamp01(_propProgress / 1.1f);
            if (MineProgress >= 1f)
            {
                PropType type = PropSystem.Instance.Remove(_prop.Cx, _prop.Cz, _prop.Level, _prop.Index);
                ItemId drop = type == PropType.PineTree || type == PropType.DeadTree ? ItemId.Log : ItemId.Log;
                int amount = type == PropType.Boulder || type == PropType.Rock ? 2 : 3;
                if (Inventory != null)
                {
                    int left = Inventory.Add(drop, amount);
                    if (left > 0) DropManager.Spawn(ChunkManager.Instance, _prop.Point, drop, left);
                }
                if (Audio != null) Audio.Play("chop");
                _propProgress = 0f;
                MineProgress = 0f;
                Raise();
            }
        }

        private float _propProgress;

        // -------------------------------------------------------------- actions
        public void Swing()
        {
            if (_attackTimer > 0f) return;
            _attackTimer = AttackCooldown;

            if (Audio != null) Audio.Play("swing");

            if (_entity != null)
            {
                var held = Inventory != null ? Inventory.SelectedStack : ItemStack.Empty;
                float dmg = held.IsEmpty ? 2f : Mathf.Max(2f, held.Def.AttackDamage);
                Vector3 knock = (_entity.Center - Player.EyePosition).normalized * 6f;
                _entity.TakeDamage(dmg, _entity.Center, knock);
                if (Audio != null) Audio.Play("hit");
                return;
            }

            if (CurrentKind == TargetKind.Prop)
            {
                _propProgress = 0f;
                MineProgress = 0f;
            }
        }

        /// <summary>Tap the place/interact button.</summary>
        public void Interact()
        {
            if (CurrentKind == TargetKind.Block && _block.Hit)
            {
                PlaceBlock();
                return;
            }

            var held = Inventory != null ? Inventory.SelectedStack : ItemStack.Empty;
            if (held.IsEmpty) return;

            var def = held.Def;
            if (def.IsFood && Stats != null)
            {
                Stats.Eat(4f, def.HealAmount);
                Inventory.ConsumeSelected();
                if (Audio != null) Audio.Play("eat");
                return;
            }

            if (def.IsBlock)
            {
                PlaceBlock();
            }
        }

        private void PlaceBlock()
        {
            if (_lastBlockId < 0 || CurrentKind != TargetKind.Block) return;

            var held = Inventory != null ? Inventory.SelectedStack : ItemStack.Empty;
            if (held.IsEmpty || !held.Def.IsBlock) return;

            int x = _block.X, y = _block.Y, z = _block.Z;
            Vector3 p = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);

            Vector3 place = p + _block.Normal;
            int px = Mathf.FloorToInt(place.x);
            int py = Mathf.FloorToInt(place.y);
            int pz = Mathf.FloorToInt(place.z);

            // never seal the player inside a block
            if (WouldTrapPlayer(px, py, pz)) return;

            var world = ChunkManager.Instance;
            if (world == null) return;
            if (world.GetBlock(px, py, pz) != World.Blocks.Air) return;

            world.SetBlock(px, py, pz, held.Def.BlockId);
            Inventory.ConsumeSelected();
            if (Audio != null) Audio.Play("place");
            Raise();
        }

        private bool WouldTrapPlayer(int x, int y, int z)
        {
            if (Player == null) return true;
            Vector3 feet = Player.FeetPosition;
            float r = Player.Radius + 0.05f;
            bool insideX = feet.x + r > x && feet.x - r < x + 1f;
            bool insideZ = feet.z + r > z && feet.z - r < z + 1f;
            bool insideY = feet.y + Player.CurrentHeight > y && feet.y < y + 1f;
            return insideX && insideY && insideZ;
        }

        private void Raise()
        {
            if (Changed != null) Changed();
        }

        private static string PropName(PropType t)
        {
            switch (t)
            {
                case PropType.OakTree: return "Oak Tree";
                case PropType.BirchTree: return "Birch Tree";
                case PropType.PineTree: return "Pine Tree";
                case PropType.SavannaTree: return "Acacia Tree";
                case PropType.DeadTree: return "Dead Tree";
                case PropType.Cactus: return "Cactus";
                case PropType.Boulder: return "Boulder";
                case PropType.Rock: return "Rock";
                default: return "Dead Bush";
            }
        }
    }
}
