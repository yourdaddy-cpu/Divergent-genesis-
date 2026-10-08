using System.Collections.Generic;
using UnityEngine;
using DivergentGenesis.Audio;
using DivergentGenesis.Core;
using DivergentGenesis.Decor;
using DivergentGenesis.Items;
using DivergentGenesis.Player;
using DivergentGenesis.Render;
using DivergentGenesis.UI;
using DivergentGenesis.World;

namespace DivergentGenesis.Building
{
    /// <summary>
    /// Turns a kit into a house.
    ///
    /// Aim at the ground, a translucent ghost appears, spin it with the rotate
    /// button and tap to commit. Materials are taken from the inventory, and the
    /// blocks are then placed over the next second or two so a house visibly
    /// assembles instead of popping into existence.
    /// </summary>
    public sealed class BuildingSystem : MonoBehaviour
    {
        public static BuildingSystem Instance;

        [Header("Placement")]
        public float Reach = 8f;
        public int BlocksPerSecond = 90;
        public float MaxGroundStep = 2.5f;

        public bool IsPlacing { get { return _active != null; } }
        public BuildKit ActiveKit { get { return _active; } }
        public int Rotation { get { return _rotation; } }
        public string StatusText { get; private set; }
        public bool AnchorValid { get; private set; }
        public int QueueRemaining { get { return _queue.Count; } }
        public event System.Action Changed;

        private ChunkManager _world;
        private PlayerController _player;
        private PlayerCamera _view;
        private PlayerInteraction _interaction;
        private InventoryModel _inventory;
        private GameHud _hud;

        private BuildKit _active;
        private int _rotation;
        private bool _hasAnchor;
        private Vector3 _anchor;
        private int _anchorX, _anchorY, _anchorZ;

        private GameObject _ghost;
        private MeshFilter _ghostFilter;
        private MeshRenderer _ghostRenderer;
        private Material _ghostMaterial;
        private int _ghostForKit = -1;

        private struct Pending
        {
            public int X, Y, Z;
            public byte Block;
        }

        private readonly List<Pending> _queue = new List<Pending>(512);
        private float _buildCarry;
        private float _soundCarry;

        public void Configure(ChunkManager world, PlayerController player, PlayerCamera view,
                              PlayerInteraction interaction, InventoryModel inventory, GameHud hud)
        {
            Instance = this;
            _world = world;
            _player = player;
            _view = view;
            _interaction = interaction;
            _inventory = inventory;
            _hud = hud;

            if (!PlayerInteraction.InteractHooks.Contains(TryConfirm))
                PlayerInteraction.InteractHooks.Add(TryConfirm);
        }

        private void OnDestroy()
        {
            PlayerInteraction.InteractHooks.Remove(TryConfirm);
        }

        // ============================================================== lifecycle
        public void Begin(BuildKit kit)
        {
            if (kit == null || _world == null) return;
            _active = kit;
            _rotation = 0;
            _hasAnchor = false;
            StatusText = "Aim at the ground";
            EnsureGhost();
            if (Changed != null) Changed();
        }

        public void Cancel()
        {
            _active = null;
            _hasAnchor = false;
            if (_ghost != null) _ghost.SetActive(false);
            StatusText = null;
            if (Changed != null) Changed();
        }

        public void Rotate()
        {
            if (_active == null) return;
            _rotation = (_rotation + 1) & 3;
            if (Changed != null) Changed();
        }

        // ================================================================= ghost
        private void EnsureGhost()
        {
            if (_ghost != null) return;

            _ghost = new GameObject("BuildGhost");
            _ghost.transform.SetParent(transform, false);
            _ghostFilter = _ghost.AddComponent<MeshFilter>();
            _ghostRenderer = _ghost.AddComponent<MeshRenderer>();
            _ghostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ghostRenderer.receiveShadows = false;

            var src = MaterialLibrary.Highlight;
            _ghostMaterial = src != null ? new Material(src) : MaterialLibrary.Make("DGTerrain");
            _ghostMaterial.name = "DG_BuildGhost";
            if (_ghostMaterial.HasProperty("_Color")) _ghostMaterial.SetColor("_Color", new Color(0.35f, 1f, 0.5f, 0.30f));
            _ghostRenderer.sharedMaterial = _ghostMaterial;
            _ghost.SetActive(false);
        }

        private void BuildGhostMesh(BuildKit kit)
        {
            EnsureGhost();
            var b = new PrimitiveMesher.Builder();
            Color32 tint = new Color32(150, 255, 190, 90);

            // the footprint, so you can see exactly where it will land
            b.Box(new Vector3(kit.SizeX * 0.5f, -0.03f, kit.SizeZ * 0.5f),
                  new Vector3(kit.SizeX, 0.06f, kit.SizeZ), new Color32(120, 220, 255, 60));

            for (int i = 0; i < kit.Cells.Count; i++)
            {
                var blk = kit.Cells[i];
                b.Box(new Vector3(blk.X + 0.5f, blk.Y + 0.5f, blk.Z + 0.5f),
                      new Vector3(0.96f, 0.96f, 0.96f), tint);
            }

            _ghostFilter.sharedMesh = b.ToMesh("BuildGhost_" + kit.Name);
            _ghostForKit = kit.Name.GetHashCode();
        }

        // ================================================================ update
        private void Update()
        {
            DrainQueue(Time.deltaTime);

            if (_active == null)
            {
                if (_ghost != null && _ghost.activeSelf) _ghost.SetActive(false);
                return;
            }

            if (InputHub.BuildCancelPressed || InputHub.EscapePressed)
            {
                Cancel();
                return;
            }
            if (InputHub.BuildRotatePressed) Rotate();
            if (InputHub.BuildPlacePressed && _hasAnchor && AnchorValid) { Commit(); return; }

            UpdateAnchor();

            if (_ghostForKit != _active.Name.GetHashCode() || _ghostFilter.sharedMesh == null)
                BuildGhostMesh(_active);

            bool show = _hasAnchor;
            if (_ghost.activeSelf != show) _ghost.SetActive(show);
            if (!show) return;

            _ghost.transform.position = _anchor;
            _ghost.transform.rotation = Quaternion.Euler(0f, _rotation * 90f, 0f);

            var tint = AnchorValid ? new Color(0.35f, 1f, 0.5f, 0.30f) : new Color(1f, 0.35f, 0.35f, 0.30f);
            if (_ghostMaterial.HasProperty("_Color")) _ghostMaterial.SetColor("_Color", tint);
        }

        private void UpdateAnchor()
        {
            _hasAnchor = false;
            AnchorValid = false;

            if (_viewportTransform == null) return;

            Vector3 origin = _player != null ? _player.EyePosition : _viewportTransform.position;
            Vector3 dir = _viewportTransform.forward;

            BlockHit hit;
            if (!_world.RaycastBlocks(origin, dir, Reach, out hit) || !hit.Hit)
            {
                StatusText = "Aim at the ground";
                return;
            }

            // an upward face puts the kit on top of the block you are pointing at
            Vector3 basePoint = hit.Normal.y > 0.5f
                ? new Vector3(hit.X, hit.Y + 1, hit.Z)
                : new Vector3(hit.X, hit.Y, hit.Z) + hit.Normal;

            _anchorX = Mathf.FloorToInt(basePoint.x);
            _anchorY = Mathf.FloorToInt(basePoint.y);
            _anchorZ = Mathf.FloorToInt(basePoint.z);
            _anchor = new Vector3(_anchorX, _anchorY, _anchorZ);
            _hasAnchor = true;

            string reason;
            AnchorValid = Validate(out reason);
            StatusText = reason;
        }

        private Transform _viewportTransform
        {
            get { return _view != null ? _view.transform : (_player != null ? _player.transform : null); }
        }

        // ============================================================= validation
        private bool Validate(out string reason)
        {
            reason = null;
            if (_active == null || _world == null) { reason = "Nothing selected"; return false; }

            // nothing to build with?
            for (int i = 0; i < _active.Costs.Count; i++)
            {
                var c = _active.Costs[i];
                if (_inventory == null || !_inventory.Has(c.Item, c.Count))
                {
                    reason = "Need " + c.Count + " " + ItemDef.Get(c.Item).Name;
                    return false;
                }
            }

            int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;
            for (int i = 0; i < _active.Cells.Count; i++)
            {
                int x = _anchorX + RotX(_active.Cells[i].X, _active.Cells[i].Z);
                int z = _anchorZ + RotZ(_active.Cells[i].X, _active.Cells[i].Z);
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (z < minZ) minZ = z;
                if (z > maxZ) maxZ = z;
            }

            // flat enough ground over the whole footprint?
            if (_active.NeedsFlatGround)
            {
                int baseY = _anchorY;
                for (int x = minX; x <= maxX; x += 2)
                for (int z = minZ; z <= maxZ; z += 2)
                {
                    float h = _world.GetTerrainHeight(x, z);
                    if (Mathf.Abs(h - baseY) > MaxGroundStep)
                    {
                        reason = "Ground is too uneven";
                        return false;
                    }
                }
            }

            // the space must be mostly clear, and it must not eat the player
            int blocked = 0;
            int total = 0;
            for (int i = 0; i < _active.Cells.Count; i++)
            {
                var blk = _active.Cells[i];
                int x = _anchorX + RotX(blk.X, blk.Z);
                int y = _anchorY + blk.Y;
                int z = _anchorZ + RotZ(blk.X, blk.Z);
                if (y < 1 || y >= WorldConfig.ChunkHeight - 2) continue;
                total++;
                byte here = _world.GetBlock(x, y, z);
                if (here != Blocks.Air && here != Blocks.Water) blocked++;
            }
            if (total > 0 && blocked > total / 4)
            {
                reason = "Not enough room";
                return false;
            }

            Vector3 p = _player != null ? _player.transform.position : Vector3.zero;
            if (p.x > minX - 1 && p.x < maxX + 1 && p.z > minZ - 1 && p.z < maxZ + 1 &&
                p.y > _anchorY - 1 && p.y < _anchorY + _active.SizeY + 1)
            {
                reason = "You are standing in the way";
                return false;
            }

            reason = "Tap to build";
            return true;
        }

        private int RotX(int x, int z)
        {
            switch (_rotation & 3)
            {
                case 1: return -z;
                case 2: return -x;
                case 3: return z;
                default: return x;
            }
        }

        private int RotZ(int x, int z)
        {
            switch (_rotation & 3)
            {
                case 1: return x;
                case 2: return -z;
                case 3: return -x;
                default: return z;
            }
        }

        // ============================================================ committing
        private bool TryConfirm()
        {
            if (_active == null) return false;
            if (!_hasAnchor || !AnchorValid) return true;   // consume the tap, nothing to do

            Commit();
            return true;
        }

        private void Commit()
        {
            // take the materials
            for (int i = 0; i < _active.Costs.Count; i++)
            {
                var c = _active.Costs[i];
                if (_inventory == null || !_inventory.Has(c.Item, c.Count))
                {
                    StatusText = "Missing " + ItemDef.Get(c.Item).Name;
                    return;
                }
            }
            for (int i = 0; i < _active.Costs.Count; i++)
                _inventory.Remove(_active.Costs[i].Item, _active.Costs[i].Count);

            _queue.Clear();
            for (int i = 0; i < _active.Cells.Count; i++)
            {
                var blk = _active.Cells[i];
                _queue.Add(new Pending
                {
                    X = _anchorX + RotX(blk.X, blk.Z),
                    Y = _anchorY + blk.Y,
                    Z = _anchorZ + RotZ(blk.X, blk.Z),
                    Block = blk.Block
                });
            }

            var audio = AudioBank.Instance;
            if (audio != null) audio.Play("build");

            if (_hud != null) _hud.Toast(_active.Name + " laid out - building...", 2.4f);
            Cancel();
        }

        private void DrainQueue(float dt)
        {
            if (_queue.Count == 0) return;

            _buildCarry += dt * BlocksPerSecond;
            int budget = Mathf.FloorToInt(_buildCarry);
            if (budget <= 0) return;
            _buildCarry -= budget;

            int placed = 0;
            while (placed < budget && _queue.Count > 0)
            {
                int last = _queue.Count - 1;
                var p = _queue[last];
                _queue.RemoveAt(last);

                if (p.Y < 1 || p.Y >= WorldConfig.ChunkHeight - 2) continue;
                byte here = _world.GetBlock(p.X, p.Y, p.Z);
                if (here == p.Block) continue;
                _world.SetBlock(p.X, p.Y, p.Z, p.Block);
                placed++;
            }

            _soundCarry += placed;
            if (_soundCarry > 22f)
            {
                _soundCarry = 0f;
                var audio = AudioBank.Instance;
                if (audio != null) audio.Play("place");
            }
        }

        /// <summary>Cancels any pending build and clears the ghost. Used on new world.</summary>
        public void Reset()
        {
            _queue.Clear();
            Cancel();
            if (_ghost != null) Object.Destroy(_ghost);
            _ghost = null;
            _ghostForKit = -1;
        }
    }
}