using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DivergentGenesis.Core;
using DivergentGenesis.World;
using DivergentGenesis.Items;
using DivergentGenesis.Player;
using DivergentGenesis.Decor;
using DivergentGenesis.Render;
using DivergentGenesis.Audio;
using DivergentGenesis.UI;
using DivergentGenesis.Save;
using DivergentGenesis.Environment;
using DivergentGenesis.Dimension;
using DivergentGenesis.Living;
using DivergentGenesis.Building;
using DivergentGenesis.Ritual;

namespace DivergentGenesis
{
    /// <summary>
    /// Builds the entire game at runtime. The Unity scene holds exactly one
    /// object with this component - everything else is created here, in code.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance;

        [Header("World")]
        [Tooltip("Seed used for a brand new world. 0 means pick one at random. Ignored when a save exists.")]
        public int DefaultSeed = 20260927;

        [Header("Streaming")]
        [Tooltip("Graphics quality tier: 0 Potato, 1 Low, 2 Medium, 3 High, 4 Ultra. -1 detects from the device.")]
        [Range(-1, 4)] public int QualityTierOverride = -1;

        [Header("Saving")]
        [Min(5f)] public float AutosaveSeconds = 45f;
        public bool AutosaveOnPause = true;
        public bool SaveOnQuit = true;

        [Header("Presentation")]
        public bool ShowLoadingScreen = true;
        [Tooltip("Keyboard and mouse controls. Editor only - has no effect on a device.")]
        public bool EditorControls = true;

        public ChunkManager World { get; private set; }
        public PropSystem Props { get; private set; }
        public GrassSystem Grass { get; private set; }
        public SkyController Sky { get; private set; }
        public AudioBank Audio { get; private set; }
        public DropManager Drops { get; private set; }
        public PlayerController Player { get; private set; }
        public PlayerCamera View { get; private set; }
        public PlayerInteraction Interaction { get; private set; }
        public PlayerStats Stats { get; private set; }
        public InventoryModel Inventory { get; private set; }
        public GameHud Hud { get; private set; }
        public InventoryPanel InventoryPanel { get; private set; }
        public CraftingPanel CraftingPanel { get; private set; }
        public SettingsPanel SettingsPanel { get; private set; }
        public EntityManager Entities { get; private set; }
        public StructureSystem Structures { get; private set; }
        public DimensionManager Dimension { get; private set; }
        public RitualSystem Ritual { get; private set; }
        public BuildingSystem Building { get; private set; }
        public LivingHud Living { get; private set; }
        public HeldItemView HeldItem { get; private set; }
        public DGPostEffect Post { get; private set; }

        private Text _loadingText;
        private Image _loadingBar;
        private Canvas _menuCanvas;
        private float _autosaveTimer;
        private bool _booted;

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            MaterialLibrary.Ensure();
            UIFactory.EnsureFont();

            BuildMenuCanvas();
            BuildLoadingScreen();

            var load = SaveSystem.Load();
            int seed = load != null ? load.seed : ResolveSeed();
            int savedQuality = PlayerPrefs.GetInt("DG_Quality", 0);
            int tier = QualityTierOverride >= 0 ? QualityTierOverride
                     : savedQuality > 0 ? savedQuality
                     : (int)DeviceProbe.DetectTier();

            QualityProfile quality = QualityProfile.Create((GraphicsTier)Mathf.Clamp(tier, 0, 4));
            quality.ViewDistanceMeters = WorldConfig.ViewDistancesMeters[Mathf.Clamp(PlayerPrefs.GetInt("DG_ViewDistance", 1), 0, 2)];
            quality.Shadows = false;

            World = BuildComponent<ChunkManager>("World", transform, cm =>
            {
                cm.WorldSeed = seed;
                cm.Quality = quality;
            });

            Sky = BuildComponent<SkyController>("Sky", transform);
            Props = BuildComponent<PropSystem>("Props", transform);
            Grass = BuildComponent<GrassSystem>("Grass", transform);
            Audio = BuildComponent<AudioBank>("Audio", transform);
            Drops = BuildComponent<DropManager>("Drops", transform);

            Props.Configure(seed, quality);
            Grass.Configure(seed, quality);
            Drops.Configure(null, null);

            BuildPlayer();
            BuildUi();

            var tierEnum = (GraphicsTier)Mathf.Clamp(tier, 0, 4);

            Entities = BuildComponent<EntityManager>("Entities", transform,
                e => e.Configure(seed, World, Player, Stats, Inventory, tierEnum));
            Structures = BuildComponent<StructureSystem>("Structures", transform,
                s2 => s2.Configure(World, Entities, Player, seed));
            Building = BuildComponent<BuildingSystem>("Building", transform,
                b => b.Configure(World, Player, View, Interaction, Inventory, Hud));
            Ritual = BuildComponent<RitualSystem>("Ritual", transform,
                r => r.Configure(World, Player, Interaction, Inventory, Entities, Hud));
            Dimension = BuildComponent<DimensionManager>("Dimension", transform,
                d => d.Configure(World, Props, Grass, Player, Stats, Entities, Sky, seed));
            HeldItem = BuildComponent<HeldItemView>("HeldItem", Player.transform,
                h => h.Configure(Player, View, Interaction, Inventory));

            Living = LivingHud.Create(Hud.Canvas.transform, Inventory, Player, Interaction, Stats,
                                      Entities, Building, Ritual, Hud);

            if (Post != null) Post.Apply(tierEnum >= GraphicsTier.Medium, tierEnum >= GraphicsTier.High ? 1.15f : 1f);

            World.Player = Player.transform;
            Sky.Player = Player.transform;
            Drops.Configure(Player, Inventory);

            if (load != null) ApplySave(load);
            else StartNewPlayer();

            _booted = true;
        }

        /// <summary>
        /// Creates the object dormant, applies configuration, then wakes it.
        /// Unity defers Awake() on an inactive GameObject, which guarantees the
        /// seed and quality profile are in place before anything initialises.
        /// </summary>
        private T BuildComponent<T>(string name, Transform parent, System.Action<T> configure = null) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.SetActive(false);
            var component = go.AddComponent<T>();
            if (configure != null) configure(component);
            go.SetActive(true);
            return component;
        }

        // ------------------------------------------------------------- creation
        private void BuildPlayer()
        {
            var go = new GameObject("Player");
            go.transform.SetParent(transform, false);

            Stats = go.AddComponent<PlayerStats>();
            Player = go.AddComponent<PlayerController>();
            Interaction = go.AddComponent<PlayerInteraction>();

            var headGo = new GameObject("Head");
            headGo.transform.SetParent(go.transform, false);
            var head = headGo.AddComponent<Transform>();
            Player.Head = head;

            var camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(go.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.06f;
            cam.farClipPlane = 3200f;
            cam.fieldOfView = 62f;
            cam.allowHDR = true;
            cam.allowMSAA = false;

            View = camGo.AddComponent<PlayerCamera>();
            View.Player = Player;
            View.Cam = cam;

            Inventory = new InventoryModel();
            Interaction.Inventory = Inventory;
            Interaction.Audio = Audio;

            // the HDR pass sits on the camera; without it a torch is just a pale cube
            Post = camGo.AddComponent<DGPostEffect>();
        }

        private void BuildUi()
        {
            Hud = GameHud.Create(transform, Inventory, Player, Interaction, Stats);

            var uiRoot = Hud.Canvas.transform;
            InventoryPanel = InventoryPanel.Create(uiRoot, Inventory, Player);
            CraftingPanel = CraftingPanel.Create(uiRoot, Inventory);
            SettingsPanel = SettingsPanel.Create(uiRoot, Inventory, this);

            InventoryPanel.SetHud(Hud);
            CraftingPanel.SetHud(Hud);
            SettingsPanel.SetHud(Hud);

            Hud.InventoryToggled += () => { CraftingPanel.Close(); SettingsPanel.Close(); InventoryPanel.Toggle(); };
            Hud.SettingsToggled += () => { InventoryPanel.Close(); CraftingPanel.Close(); SettingsPanel.Toggle(); };
        }

        private void BuildMenuCanvas()
        {
            _menuCanvas = UIFactory.CreateCanvas("MenuCanvas", 10, transform);
        }

        private void BuildLoadingScreen()
        {
            if (!ShowLoadingScreen) return;

            var dim = UIFactory.PanelRect(_menuCanvas.transform, "Loading", new Color(0.05f, 0.06f, 0.09f, 1f));
            UIFactory.Fill((RectTransform)dim.transform);

            var title = UIFactory.Label(dim.transform, "Title", "DIVERGENT GENESIS", 60, TextAnchor.MiddleCenter, UIFactory.Text);
            UIFactory.Anchor((RectTransform)title.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(1200f, 90f));

            var sub = UIFactory.Label(dim.transform, "Sub", "A 140 x 140 km world", 26, TextAnchor.MiddleCenter, UIFactory.TextDim);
            UIFactory.Anchor((RectTransform)sub.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1000f, 50f));

            var back = UIFactory.PanelRect(dim.transform, "BarBack", new Color(1f, 1f, 1f, 0.12f));
            UIFactory.Anchor((RectTransform)back.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(700f, 18f));
            back.raycastTarget = false;

            var fill = UIFactory.Icon(dim.transform, "BarFill", UIFactory.Accent);
            UIFactory.Anchor((RectTransform)fill.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(700f, 18f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            _loadingBar = fill;

            _loadingText = UIFactory.Label(dim.transform, "LoadingText", "Generating terrain...", 22, TextAnchor.MiddleCenter, UIFactory.TextDim);
            UIFactory.Anchor((RectTransform)_loadingText.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 55f), new Vector2(1000f, 30f));
        }

        /// <summary>
        /// Prefers an explicit DefaultSeed from the inspector, then a seed the
        /// player has already been given, then a fresh random one. Whichever wins
        /// is stored so the next launch lands in the same world.
        /// </summary>
        private int ResolveSeed()
        {
            if (DefaultSeed != 0) return DefaultSeed;

            int stored = PlayerPrefs.GetInt("DG_Seed", 0);
            if (stored != 0) return stored;

            int seed = Random.Range(1, int.MaxValue);
            PlayerPrefs.SetInt("DG_Seed", seed);
            PlayerPrefs.Save();
            return seed;
        }

        private void StartNewPlayer()
        {
            Inventory.GiveStartingKit();
            Stats.Respawn();

            Vector3 spawn = FindSpawn();
            Player.Teleport(spawn);
            InputHub.Yaw = 35f;
            InputHub.Pitch = -8f;
        }

        private Vector3 FindSpawn()
        {
            // find a dry, gently sloped spot near the middle of the world
            if (World == null) return new Vector3(0f, 90f, 0f);
            for (int r = 0; r < 4000; r += 40)
            {
                for (int a = 0; a < 8; a++)
                {
                    float ang = a * Mathf.PI * 0.25f;
                    int x = Mathf.RoundToInt(Mathf.Cos(ang) * r);
                    int z = Mathf.RoundToInt(Mathf.Sin(ang) * r);
                    float h = World.GetTerrainHeight(x, z);
                    if (h < WorldConfig.SeaLevel + 2f) continue;
                    float h1 = World.GetTerrainHeight(x + 4, z);
                    float h2 = World.GetTerrainHeight(x, z + 4);
                    if (Mathf.Abs(h1 - h) > 2f || Mathf.Abs(h2 - h) > 2f) continue;
                    return new Vector3(x, h + 2.2f, z);
                }
            }
            return new Vector3(0f, 90f, 0f);
        }

        // ---------------------------------------------------------------- save
        public void SaveNow()
        {
            var data = new SaveData();
            if (World != null) data.seed = World.WorldSeedValue;
            if (Sky != null) data.timeOfDay = Sky.TimeOfDay;
            if (View != null) data.firstPerson = View.FirstPerson ? 1 : 0;
            if (Player != null)
            {
                data.px = Player.transform.position.x;
                data.py = Player.transform.position.y;
                data.pz = Player.transform.position.z;
            }
            data.yaw = InputHub.Yaw;
            data.pitch = InputHub.Pitch;
            if (Stats != null)
            {
                data.health = Stats.Health;
                data.stamina = Stats.Stamina;
                data.hunger = Stats.Hunger;
            }
            if (Inventory != null)
            {
                data.selectedSlot = Inventory.Selected;
                data.slotItems = new int[InventoryModel.TotalSize];
                data.slotCounts = new int[InventoryModel.TotalSize];
                for (int i = 0; i < InventoryModel.TotalSize; i++)
                {
                    data.slotItems[i] = (int)Inventory.Slots[i].Id;
                    data.slotCounts[i] = Inventory.Slots[i].Count;
                }
            }

            data.dimension = Dimension != null ? (int)Dimension.Current : 0;
            data.edits = BlockEdits.SerializeAll();
            SaveSystem.Save(data);
            if (Hud != null) Hud.Toast("World saved");
        }

        public void ApplySave(SaveData d)
        {
            if (d == null) { StartNewPlayer(); return; }

            if (Sky != null) Sky.SetTimeOfDay(d.timeOfDay);
            if (View != null) View.FirstPerson = d.firstPerson != 0;
            if (Stats != null)
            {
                Stats.Health = Mathf.Clamp(d.health <= 0f ? PlayerStats.MaxHealth : d.health, 1f, PlayerStats.MaxHealth);
                Stats.Stamina = Mathf.Clamp(d.stamina, 0f, PlayerStats.MaxStamina);
                Stats.Hunger = Mathf.Clamp(d.hunger, 0f, PlayerStats.MaxHunger);
                Stats.Raise();
            }

            if (Inventory != null && d.slotItems != null)
            {
                Inventory.Clear();
                int n = Mathf.Min(d.slotItems.Length, InventoryModel.TotalSize);
                for (int i = 0; i < n; i++)
                    Inventory.Slots[i] = ItemStack.Of((ItemId)d.slotItems[i], d.slotCounts[i]);
                Inventory.Selected = Mathf.Clamp(d.selectedSlot, 0, InventoryModel.HotbarSize - 1);
                Inventory.Notify();
            }

            // the dimension has to be restored before the position: the terrain and
            // the edit store are both plane specific
            if (Dimension != null) Dimension.SetImmediate((DimensionId)Mathf.Clamp(d.dimension, 0, 1));
            if (d.edits != null) BlockEdits.DeserializeAll(d.edits);

            if (Player != null)
                Player.Teleport(new Vector3(d.px, d.py, d.pz));

            InputHub.Yaw = d.yaw;
            InputHub.Pitch = Mathf.Clamp(d.pitch, -88f, 88f);
        }

        public void NewWorld()
        {
            SaveSystem.Delete();
            PlayerPrefs.DeleteKey("DG_Seed");
            int seed = Random.Range(1, int.MaxValue);
            PlayerPrefs.SetInt("DG_Seed", seed);
            PlayerPrefs.Save();

            BlockEdits.Clear();
            if (Dimension != null) Dimension.SetImmediate(DimensionId.Overworld);
            if (Entities != null) Entities.Reset();
            if (Ritual != null) Ritual.ClearCorruption();
            if (Props != null) Props.Configure(seed, World.Quality);
            if (Grass != null) Grass.Configure(seed, World.Quality);
            if (World != null) { World.WorldSeed = seed; }
            StartNewPlayer();
            if (Hud != null) Hud.Toast("New world seeded " + seed);
        }

        /// <summary>A way home that works from anywhere, including the Node.</summary>
        public void TravelDimension()
        {
            if (Dimension == null) return;
            Dimension.TravelTo(Dimension.Current == DimensionId.Overworld ? DimensionId.Node : DimensionId.Overworld);
        }

        public void TeleportHome()
        {
            Vector3 spawn = FindSpawn();
            Player.Teleport(spawn);
            if (Hud != null) Hud.Toast("Teleported home");
        }

        // -------------------------------------------------------------- update
        private void Update()
        {
            if (!_booted) return;

#if UNITY_EDITOR
            if (EditorControls) EditorInput.Read();
#endif
            HandlePanelHotkeys();
            InputHub.ClearFrame();

            if (World != null && Props != null)
                Props.UpdateStreaming(Player != null ? Player.transform.position : Vector3.zero,
                                      World.Quality != null ? World.Quality.ViewDistanceMeters : 1024);

            _autosaveTimer += Time.deltaTime;
            if (_autosaveTimer > Mathf.Max(5f, AutosaveSeconds)) { _autosaveTimer = 0f; SaveNow(); }
        }

        private void HandlePanelHotkeys()
        {
            if (InputHub.InventoryPressed)
            {
                CraftingPanel.Close();
                SettingsPanel.Close();
                InventoryPanel.Toggle();
            }
            else if (InputHub.EscapePressed)
            {
                InventoryPanel.Close();
                CraftingPanel.Close();
                SettingsPanel.Close();
            }
        }

        private void LateUpdate()
        {
            bool ready = World != null && World.WorldReady;

            if (_loadingBar == null)
            {
                // Loading screen disabled: hold the player still until there is
                // something to stand on, rather than dropping them into a void.
                if (Player != null && Player.enabled != ready) Player.enabled = ready;
                return;
            }

            float progress = 0f;
            if (World != null)
            {
                int total = Mathf.Max(1, World.LoadedTileCount + World.PendingTileCount);
                progress = World.LoadedTileCount / (float)total;
            }

            _loadingBar.fillAmount = Mathf.MoveTowards(_loadingBar.fillAmount, progress, Time.deltaTime);
            if (_loadingText != null)
                _loadingText.text = ready ? "Tap to play" : ("Generating terrain... " + World.LoadedTileCount + " tiles");

            if (ready && _loadingBar.fillAmount > 0.98f)
            {
                var parent = _loadingBar.transform.parent != null ? _loadingBar.transform.parent.parent : null;
                if (parent != null) parent.gameObject.SetActive(false);
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && AutosaveOnPause) SaveNow();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && AutosaveOnPause) SaveNow();
        }

        private void OnApplicationQuit()
        {
            if (SaveOnQuit && _booted) SaveNow();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
