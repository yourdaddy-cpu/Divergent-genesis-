using System.Collections.Generic;
using UnityEngine;
using DivergentGenesis.Audio;
using DivergentGenesis.Core;
using DivergentGenesis.Environment;
using DivergentGenesis.Items;
using DivergentGenesis.Living;
using DivergentGenesis.Player;
using DivergentGenesis.UI;
using DivergentGenesis.World;

namespace DivergentGenesis.Ritual
{
    /// <summary>
    /// The altar ritual.
    ///
    /// Four pedestals surround an altar. Give each pedestal a tool - a real, held,
    /// breakable tool, which is destroyed - and the altar wakes up. In the
    /// overworld that tears a rift into the Node. In the Node it calls the Node
    /// Sovereign, and the sky above the arena goes from cyan to purple and splits
    /// open with cracks until the thing is dead.
    /// </summary>
    public sealed class RitualSystem : MonoBehaviour
    {
        public static RitualSystem Instance;

        [Header("Ritual")]
        public int RequiredSacrifices = 4;
        public float ScanRadius = 15f;
        public float ScanInterval = 0.7f;
        public float BossSpawnHeight = 11f;

        public int Charged { get; private set; }
        public bool SiteFound { get { return _found; } }
        public bool BossActive { get { return _boss != null && _boss.Alive; } }
        public int RitualsPerformed { get; private set; }

        private ChunkManager _world;
        private PlayerController _player;
        private PlayerInteraction _interaction;
        private InventoryModel _inventory;
        private EntityManager _entities;
        private GameHud _hud;

        private bool _found;
        private Vector3 _altar;
        private readonly List<Vector3> _pedestals = new List<Vector3>(8);
        private readonly List<Vector3> _chargedSpots = new List<Vector3>(8);
        private LivingEntity _boss;
        private float _timer;
        private float _messageCooldown;
        private float _skyHold;

        public void Configure(ChunkManager world, PlayerController player, PlayerInteraction interaction,
                              InventoryModel inventory, EntityManager entities, GameHud hud)
        {
            Instance = this;
            _world = world;
            _player = player;
            _interaction = interaction;
            _inventory = inventory;
            _entities = entities;
            _hud = hud;

            // the altar claims a tap before ordinary placement gets it
            if (!PlayerInteraction.InteractHooks.Contains(TryInteract))
                PlayerInteraction.InteractHooks.Add(TryInteract);
        }

        private void OnDestroy()
        {
            PlayerInteraction.InteractHooks.Remove(TryInteract);
        }

        // ================================================================= update
        private void Update()
        {
            if (_world == null || _player == null) return;

            if (_messageCooldown > 0f) _messageCooldown -= Time.deltaTime;

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _timer = ScanInterval;
                Scan();
            }

            // the sky stays torn while the Sovereign lives, and heals when it dies
            if (_boss != null && !_boss.Alive)
            {
                _skyHold = 0f;
                SkyFX.SetCorruption(0f);
                Toast("The sky knits itself back together.");
                _boss = null;
            }

            if (_boss != null && _boss.Alive) _skyHold = 1f;
        }

        private void Scan()
        {
            Vector3 p = _player.transform.position;
            int px = Mathf.FloorToInt(p.x), py = Mathf.FloorToInt(p.y), pz = Mathf.FloorToInt(p.z);
            int r = Mathf.RoundToInt(ScanRadius);

            _found = false;
            _pedestals.Clear();
            _chargedSpots.Clear();

            int minY = Mathf.Max(1, py - 5);
            int maxY = Mathf.Min(WorldConfig.ChunkHeight - 2, py + 6);

            for (int y = minY; y <= maxY; y++)
            for (int z = pz - r; z <= pz + r; z++)
            for (int x = px - r; x <= px + r; x++)
            {
                byte b = _world.GetBlock(x, y, z);
                if (b == Blocks.RitualAltar)
                {
                    _found = true;
                    _altar = new Vector3(x, y, z);
                }
                else if (b == Blocks.RitualPedestal)
                {
                    _pedestals.Add(new Vector3(x, y, z));
                    if (_world.GetBlock(x, y + 1, z) == Blocks.Lamp) _chargedSpots.Add(new Vector3(x, y, z));
                }
            }

            Charged = _chargedSpots.Count;
        }

        // ============================================================== interact
        private bool TryInteract()
        {
            if (!_found || _interaction == null || _inventory == null) return false;
            if (_interaction.CurrentKind != TargetKind.Block) return false;

            BlockHit hit = _interaction.TargetBlock;
            Vector3 spot = new Vector3(hit.X, hit.Y, hit.Z);

            // --- a pedestal accepts one tool
            for (int i = 0; i < _pedestals.Count; i++)
            {
                if (_pedestals[i] != spot) continue;

                if (_world.GetBlock(hit.X, hit.Y + 1, hit.Z) == Blocks.Lamp)
                {
                    Toast("That pedestal has already been given its tool.");
                    return true;
                }

                var stack = _inventory.SelectedStack;
                if (stack.IsEmpty || !stack.Def.IsTool)
                {
                    Toast("Only tools may be sacrificed. Hold one and tap the pedestal.");
                    return true;
                }

                string name = stack.Def.Name;
                _inventory.ConsumeSelected();
                _world.SetBlock(hit.X, hit.Y + 1, hit.Z, Blocks.Lamp);

                var audio = AudioBank.Instance;
                if (audio != null) audio.Play("ritual_charge");

                Charged++;
                Toast(name + " is accepted. " + Charged + " of " + RequiredSacrifices + " given.");

                if (Charged >= RequiredSacrifices)
                {
                    SkyFX.Pulse(0.25f);
                    Toast("The altar is ready. Touch it.");
                }
                return true;
            }

            // --- the altar itself
            if (spot == _altar)
            {
                if (Charged < RequiredSacrifices)
                {
                    Toast("The altar wants " + RequiredSacrifices + " tools. " + Charged + " given so far.");
                    return true;
                }
                Begin();
                return true;
            }

            return false;
        }

        private void Begin()
        {
            RitualsPerformed++;

            if (DimensionState.NodeActive) SummonSovereign();
            else OpenRift();

            // the offering is consumed either way
            for (int i = 0; i < _chargedSpots.Count; i++)
            {
                var s = _chargedSpots[i];
                _world.SetBlock(Mathf.FloorToInt(s.x), Mathf.FloorToInt(s.y) + 1, Mathf.FloorToInt(s.z), Blocks.Air);
            }
            _chargedSpots.Clear();
            Charged = 0;
        }

        private void OpenRift()
        {
            int cx = Mathf.FloorToInt(_altar.x);
            int cy = Mathf.FloorToInt(_altar.y);
            int cz = Mathf.FloorToInt(_altar.z);

            // a ring of rifts, three high, around the altar
            for (int a = 0; a < 24; a++)
            {
                float ang = a / 24f * Mathf.PI * 2f;
                int rx = cx + Mathf.RoundToInt(Mathf.Cos(ang) * 7f);
                int rz = cz + Mathf.RoundToInt(Mathf.Sin(ang) * 7f);
                _world.SetBlock(rx, cy, rz, Blocks.SherbetStone);
                for (int y = 1; y <= 3; y++) _world.SetBlock(rx, cy + y, rz, Blocks.NodePortal);
            }

            SkyFX.Pulse(0.85f);
            var audio = AudioBank.Instance;
            if (audio != null) audio.Play("rift");
            Toast("The altar tears open a way into the Node. Step through.");
        }

        private void SummonSovereign()
        {
            if (_entities == null || BossActive) return;

            int cx = Mathf.FloorToInt(_altar.x);
            int cy = Mathf.FloorToInt(_altar.y);
            int cz = Mathf.FloorToInt(_altar.z);

            // the arena answers: the sky cracks and goes purple
            SkyFX.SetCorruption(1f);
            SkyFX.Pulse(1f);
            SkyFX.CrackSeed = Random.Range(1f, 90f);

            _boss = _entities.SpawnBoss(MobId.ElderDragon,
                new Vector3(cx + 0.5f, cy + BossSpawnHeight, cz + 0.5f));

            if (_boss != null)
            {
                _boss.HomePoint = new Vector3(cx, cy, cz);
                _boss.State = 2;
                _boss.StateTimer = 4f;
            }

            for (int a = 0; a < 12; a++)
            {
                float ang = a / 12f * Mathf.PI * 2f;
                int rx = cx + Mathf.RoundToInt(Mathf.Cos(ang) * 9f);
                int rz = cz + Mathf.RoundToInt(Mathf.Sin(ang) * 9f);
                _world.SetBlock(rx, cy + 1, rz, Blocks.Lamp);
            }

            var audio = AudioBank.Instance;
            if (audio != null) audio.Play("dragon_roar");

            Toast("The sky splits. THE NODE SOVEREIGN IS HERE.");
        }

        private void Toast(string message)
        {
            if (_hud != null) _hud.Toast(message, 3.4f);
        }

        /// <summary>Used by the settings panel and by loading a save.</summary>
        public void ClearCorruption()
        {
            SkyFX.SetCorruption(0f);
            _skyHold = 0f;
            _boss = null;
        }
    }
}