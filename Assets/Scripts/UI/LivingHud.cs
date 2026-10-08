using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DivergentGenesis.Building;
using DivergentGenesis.Core;
using DivergentGenesis.Dimension;
using DivergentGenesis.Items;
using DivergentGenesis.Living;
using DivergentGenesis.Player;
using DivergentGenesis.Ritual;
using DivergentGenesis.World;

namespace DivergentGenesis.UI
{
    /// <summary>
    /// Everything the world tells you about itself: which dimension you are in,
    /// what you are standing in, the boss bar, the ritual's offering count, the
    /// build ghost's status, and the three windows - build catalogue, recipe book
    /// and villager trading.
    /// </summary>
    public sealed class LivingHud : MonoBehaviour
    {
        public static LivingHud Instance;

        private Text _dimension;
        private Text _location;
        private Text _target;
        private Text _prompt;
        private Text _status;

        private RectTransform _bossRoot;
        private Text _bossName;
        private Image _bossFill;
        private Text _bossPercent;

        private BuildPanel _buildPanel;
        private RecipeBookPanel _recipePanel;
        private TradePanel _tradePanel;

        private InventoryModel _inventory;
        private PlayerInteraction _interaction;
        private PlayerController _player;
        private EntityManager _entities;
        private BuildingSystem _building;
        private RitualSystem _ritual;
        private GameHud _hud;
        private PlayerStats _stats;

        private float _hintTimer;

        // ============================================================== creation
        public static LivingHud Create(Transform parent, InventoryModel inventory, PlayerController player,
                                       PlayerInteraction interaction, PlayerStats stats, EntityManager entities,
                                       BuildingSystem building, RitualSystem ritual, GameHud hud)
        {
            var go = new GameObject("LivingHud", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            UIFactory.Fill((RectTransform)go.transform);
            var instance = go.AddComponent<LivingHud>();
            instance.Build();
            instance.Bind(inventory, player, interaction, stats, entities, building, ritual, hud);
            return instance;
        }

        private void Build()
        {
            var root = (RectTransform)transform;

            // --- dimension + location, top left under the player bars
            _dimension = UIFactory.Label(root, "Dimension", "Overworld", 24, TextAnchor.UpperLeft, UIFactory.Accent);
            UIFactory.TopLeft((RectTransform)_dimension.transform, 24f, 148f, 420f, 30f);

            _location = UIFactory.Label(root, "Location", "", 20, TextAnchor.UpperLeft, UIFactory.TextDim);
            UIFactory.TopLeft((RectTransform)_location.transform, 24f, 178f, 420f, 28f);

            // --- crosshair target + prompt, centre screen
            _target = UIFactory.Label(root, "Target", "", 22, TextAnchor.MiddleCenter, UIFactory.Text);
            UIFactory.Anchor((RectTransform)_target.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(720f, 30f));

            _prompt = UIFactory.Label(root, "Prompt", "", 21, TextAnchor.MiddleCenter, UIFactory.Accent);
            UIFactory.Anchor((RectTransform)_prompt.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -126f), new Vector2(720f, 30f));

            // --- status line (ritual offerings, build info)
            _status = UIFactory.Label(root, "Status", "", 22, TextAnchor.LowerCenter, UIFactory.TextDim);
            UIFactory.Anchor((RectTransform)_status.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 168f), new Vector2(900f, 30f));

            // --- boss bar, top centre
            var bossBg = UIFactory.PanelRect(root, "BossRoot", new Color(0.05f, 0.02f, 0.08f, 0.72f));
            UIFactory.Anchor((RectTransform)bossBg.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(720f, 78f));
            _bossRoot = (RectTransform)bossBg.transform;

            _bossName = UIFactory.Label(_bossRoot, "BossName", "", 26, TextAnchor.UpperCenter, new Color(0.92f, 0.72f, 1f));
            UIFactory.TopLeft((RectTransform)_bossName.transform, 12f, 6f, 696f, 30f);

            var barBg = UIFactory.PanelRect(_bossRoot, "BarBg", new Color(0.16f, 0.06f, 0.20f, 0.95f));
            UIFactory.TopLeft((RectTransform)barBg.transform, 12f, 42f, 696f, 24f);
            _bossFill = UIFactory.PanelRect(barBg.transform, "Fill", new Color(0.85f, 0.30f, 0.95f, 1f));
            UIFactory.Stretch((RectTransform)_bossFill.transform, 0f);
            _bossFill.type = Image.Type.Filled;
            _bossFill.fillMethod = Image.FillMethod.Horizontal;
            _bossFill.fillAmount = 1f;

            _bossPercent = UIFactory.Label(_bossRoot, "BossPct", "", 18, TextAnchor.MiddleRight, UIFactory.TextDim);
            UIFactory.TopLeft((RectTransform)_bossPercent.transform, 560f, 42f, 148f, 24f);

            _bossRoot.gameObject.SetActive(false);

            // --- windows
            _buildPanel = BuildPanel.Create(root, this);
            _recipePanel = RecipeBookPanel.Create(root, this);
            _tradePanel = TradePanel.Create(root, this);
        }

        private void Bind(InventoryModel inventory, PlayerController player, PlayerInteraction interaction,
                          PlayerStats stats, EntityManager entities, BuildingSystem building,
                          RitualSystem ritual, GameHud hud)
        {
            Instance = this;
            _inventory = inventory;
            _player = player;
            _interaction = interaction;
            _stats = stats;
            _entities = entities;
            _building = building;
            _ritual = ritual;
            _hud = hud;

            if (_entities != null) _entities.TradeRequested += OnTradeRequested;
            if (_building != null) _building.Changed += RefreshStatic;
        }

        private void OnDestroy()
        {
            if (_entities != null) _entities.TradeRequested -= OnTradeRequested;
            if (_building != null) _building.Changed -= RefreshStatic;
        }

        private void OnTradeRequested(LivingEntity villager)
        {
            if (villager == null || !villager.Alive) return;
            _tradePanel.OpenFor(villager);
        }

        // ================================================================ update
        private void Update()
        {
            if (InputHub.BuildPressed && !InputHub.UIBlocked) _buildPanel.Open();
            if (InputHub.RecipeBookPressed && !InputHub.UIBlocked) _recipePanel.Open();
            if (InputHub.EscapePressed && InputHub.UIBlocked)
            {
                if (_buildPanel.IsOpen) _buildPanel.Close();
                else if (_recipePanel.IsOpen) _recipePanel.Close();
                else if (_tradePanel.IsOpen) _tradePanel.Close();
            }

            RefreshStatic();
            RefreshBoss();
        }

        private void RefreshStatic()
        {
            if (_dimension != null)
            {
                bool node = DimensionState.NodeActive;
                _dimension.text = node ? "The Node" : "Overworld";
                _dimension.color = node ? new Color(1f, 0.78f, 0.95f) : UIFactory.Accent;
            }

            if (_location != null)
            {
                var ss = StructureSystem.Instance;
                string where = ss != null && _player != null ? ss.DescribeLocation(_player.transform.position) : null;
                float x = _player != null ? _player.transform.position.x : 0f;
                float z = _player != null ? _player.transform.position.z : 0f;
                _location.text = (where != null ? where + "  ·  " : "") + Mathf.RoundToInt(x) + ", " + Mathf.RoundToInt(z);
            }

            // ---- crosshair
            if (_target != null)
            {
                string name = _interaction != null ? _interaction.TargetName : null;
                _target.text = string.IsNullOrEmpty(name) ? "" : name;
            }

            // ---- the one hint that matters right now
            string hint = null;
            if (_building != null && _building.IsPlacing)
            {
                hint = _building.StatusText + "   [rotate]   [tap to build]   [back to cancel]";
            }
            else if (_ritual != null && _ritual.SiteFound)
            {
                if (_ritual.Charged < _ritual.RequiredSacrifices)
                    hint = "Altar: " + _ritual.Charged + " of " + _ritual.RequiredSacrifices +
                           " tools given. Hold a tool and tap a pedestal.";
                else
                    hint = DimensionState.NodeActive
                        ? "The altar is full. Touch it to call the Sovereign."
                        : "The altar is full. Touch it to tear open the Node.";
            }
            else if (_interaction != null && _interaction.CurrentKind == TargetKind.Entity)
            {
                var living = _interaction.TargetEntity as LivingEntity;
                if (living != null)
                {
                    if (living.Def.Tradable) hint = "Tap to trade";
                    else if (living.Def.Petable) hint = "Tap to pet";
                }
            }

            if (_prompt != null) _prompt.text = hint ?? "";
            if (_status != null) _status.text = "";
        }

        private void RefreshBoss()
        {
            bool show = _ritual != null && _ritual.BossActive;

            if (_bossRoot == null) return;
            if (_bossRoot.gameObject.activeSelf != show) _bossRoot.gameObject.SetActive(show);
            if (!show) return;

            var boss = FindBoss();
            if (boss == null) return;

            if (_bossName != null) _bossName.text = boss.Def.Name.ToUpperInvariant();
            float pct = boss.Def.Health > 0f ? Mathf.Clamp01(boss.Health / boss.Def.Health) : 0f;
            if (_bossFill != null) _bossFill.fillAmount = pct;
            if (_bossPercent != null) _bossPercent.text = Mathf.CeilToInt(pct * 100f) + "%";
        }

        private LivingEntity FindBoss()
        {
            var list = _entities != null ? _entities.All : null;
            if (list == null) return null;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e != null && e.Alive && e.IsBoss) return e;
            }
            return null;
        }

        // ---------------------------------------------------------- public API
        public void OpenBuild() { _buildPanel.Open(); }
        public void OpenRecipes() { _recipePanel.Open(); }
        public void Notify(string message) { if (_hud != null) _hud.Toast(message); }
        public InventoryModel Inventory { get { return _inventory; } }
    }

    // ============================================================== build window
    /// <summary>The catalogue of things you can put down, with live material costs.</summary>
    public sealed class BuildPanel : PanelBase
    {
        private LivingHud _owner;
        private BuildCategory _category = BuildCategory.Comfort;
        private readonly List<Button> _kits = new List<Button>(12);
        private readonly List<Text> _kitLabels = new List<Text>(12);
        private readonly List<Button> _cats = new List<Button>(5);
        private Text _header;
        private Text _detail;

        private static readonly BuildCategory[] Categories =
        {
            BuildCategory.Comfort, BuildCategory.Shelter, BuildCategory.Defence,
            BuildCategory.Settlement, BuildCategory.Ritual
        };

        public static BuildPanel Create(Transform parent, LivingHud owner)
        {
            var go = new GameObject("BuildPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var panel = go.AddComponent<BuildPanel>();
            panel._owner = owner;

            var body = Shell(go.transform, "Dim", new Vector2(920f, 660f));
            panel.Root = body;

            var title = UIFactory.Label(body, "Title", "Build", 30, TextAnchor.UpperLeft, UIFactory.Text);
            UIFactory.TopLeft((RectTransform)title.transform, 24f, 16f, 400f, 40f);

            var close = UIFactory.TextButton(body, "Close", "X", 28, new Color(0.45f, 0.20f, 0.22f, 0.95f), UIFactory.Text);
            UIFactory.TopLeft((RectTransform)close.transform, 838f, 14f, 56f, 56f);
            close.onClick.AddListener(panel.Close);

            // category row
            for (int i = 0; i < Categories.Length; i++)
            {
                int captured = i;
                var b = UIFactory.TextButton(body, "Cat" + i, CategoryName(Categories[i]), 20,
                    UIFactory.PanelLight, UIFactory.Text);
                UIFactory.TopLeft((RectTransform)b.transform, 24f + i * 176f, 62f, 168f, 48f);
                b.onClick.AddListener(() => { panel._category = Categories[captured]; panel.Refresh(); });
                panel._cats.Add(b);
            }

            panel._header = UIFactory.Label(body, "Header", "", 21, TextAnchor.UpperLeft, UIFactory.Accent);
            UIFactory.TopLeft((RectTransform)panel._header.transform, 24f, 122f, 860f, 30f);

            // ten kit rows
            for (int i = 0; i < 10; i++)
            {
                var bg = UIFactory.PanelRect(body, "Kit" + i, UIFactory.PanelLight);
                UIFactory.TopLeft((RectTransform)bg.transform, 24f, 158f + i * 46f, 860f, 42f);

                var label = UIFactory.Label(bg.transform, "Label", "", 20, TextAnchor.MiddleLeft, UIFactory.Text);
                UIFactory.TopLeft((RectTransform)label.transform, 12f, 0f, 830f, 42f);

                var btn = bg.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                int captured = i;
                btn.onClick.AddListener(() => panel.Pick(captured));

                panel._kits.Add(btn);
                panel._kitLabels.Add(label);
            }

            panel._detail = UIFactory.Label(body, "Detail", "", 19, TextAnchor.UpperLeft, UIFactory.TextDim);
            UIFactory.TopLeft((RectTransform)panel._detail.transform, 24f, 618f, 860f, 26f);

            go.SetActive(false);
            return panel;
        }

        private static string CategoryName(BuildCategory c)
        {
            switch (c)
            {
                case BuildCategory.Comfort: return "Comfort";
                case BuildCategory.Shelter: return "Shelter";
                case BuildCategory.Defence: return "Defence";
                case BuildCategory.Settlement: return "Settlement";
                default: return "Ritual";
            }
        }

        protected override void OnOpened()
        {
            Refresh();
        }

        public void Refresh()
        {
            for (int i = 0; i < _cats.Count; i++)
            {
                var img = _cats[i].targetGraphic as Image;
                if (img != null) img.color = Categories[i] == _category
                    ? new Color(0.95f, 0.72f, 0.25f, 0.95f) : UIFactory.PanelLight;
            }

            var kits = KitCatalog.InCategory(_category);
            if (_header != null)
                _header.text = kits.Count + " designs  ·  tap one, then aim at the ground and tap again to place";

            for (int i = 0; i < _kits.Count; i++)
            {
                bool used = i < kits.Count;
                _kits[i].gameObject.SetActive(used);
                if (!used) continue;

                var kit = kits[i];
                _kitLabels[i].text = kit.Name + "   " + CostText(kit) + "   ·   " + kit.Blurb;
                ((Image)_kits[i].targetGraphic).color = CanAfford(kit)
                    ? UIFactory.PanelLight : new Color(0.26f, 0.16f, 0.16f, 0.95f);
            }

            if (_detail != null)
                _detail.text = "Blocks you can simply dig up (dirt, sand, the Node's own ground) are free. " +
                               "Everything else comes out of your inventory.";
        }

        private string CostText(BuildKit kit)
        {
            if (kit.Costs.Count == 0) return "free";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < kit.Costs.Count && i < 3; i++)
            {
                if (i > 0) sb.Append(" + ");
                sb.Append(kit.Costs[i].Count).Append(" ").Append(ItemDef.Get(kit.Costs[i].Item).Name);
            }
            if (kit.Costs.Count > 3) sb.Append(" + ...");
            return sb.ToString();
        }

        private bool CanAfford(BuildKit kit)
        {
            var inv = _owner != null ? _owner.Inventory : null;
            if (inv == null) return false;
            for (int i = 0; i < kit.Costs.Count; i++)
                if (!inv.Has(kit.Costs[i].Item, kit.Costs[i].Count)) return false;
            return true;
        }

        private void Pick(int index)
        {
            var kits = KitCatalog.InCategory(_category);
            if (index < 0 || index >= kits.Count) return;

            var system = BuildingSystem.Instance;
            if (system == null) { OnClosed(); return; }

            system.Begin(kits[index]);
            Close();
        }
    }

    // ============================================================= recipe window
    /// <summary>The whole tree in one list, craftable in a tap.</summary>
    public sealed class RecipeBookPanel : PanelBase
    {
        private LivingHud _owner;
        private RecipeCategory _category = RecipeCategory.Building;
        private readonly List<Button> _rows = new List<Button>(12);
        private readonly List<Text> _labels = new List<Text>(12);
        private readonly List<Button> _cats = new List<Button>(7);
        private readonly List<Recipe> _shown = new List<Recipe>(12);
        private Text _header;

        private static readonly RecipeCategory[] Categories =
        {
            RecipeCategory.Basics, RecipeCategory.Tools, RecipeCategory.Building,
            RecipeCategory.Comfort, RecipeCategory.Food, RecipeCategory.Node, RecipeCategory.Ritual
        };

        public static RecipeBookPanel Create(Transform parent, LivingHud owner)
        {
            var go = new GameObject("RecipeBookPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var panel = go.AddComponent<RecipeBookPanel>();
            panel._owner = owner;

            var body = Shell(go.transform, "Dim", new Vector2(960f, 680f));
            panel.Root = body;

            var title = UIFactory.Label(body, "Title", "Recipe Book", 30, TextAnchor.UpperLeft, UIFactory.Text);
            UIFactory.TopLeft((RectTransform)title.transform, 24f, 16f, 400f, 40f);

            var close = UIFactory.TextButton(body, "Close", "X", 28, new Color(0.45f, 0.20f, 0.22f, 0.95f), UIFactory.Text);
            UIFactory.TopLeft((RectTransform)close.transform, 878f, 14f, 56f, 56f);
            close.onClick.AddListener(panel.Close);

            for (int i = 0; i < Categories.Length; i++)
            {
                int captured = i;
                var b = UIFactory.TextButton(body, "Cat" + i, Name(Categories[i]), 19, UIFactory.PanelLight, UIFactory.Text);
                UIFactory.TopLeft((RectTransform)b.transform, 24f + i * 132f, 60f, 126f, 44f);
                b.onClick.AddListener(() => { panel._category = Categories[captured]; panel.Refresh(); });
                panel._cats.Add(b);
            }

            panel._header = UIFactory.Label(body, "Header", "", 20, TextAnchor.UpperLeft, UIFactory.Accent);
            UIFactory.TopLeft((RectTransform)panel._header.transform, 24f, 116f, 900f, 28f);

            for (int i = 0; i < 12; i++)
            {
                var bg = UIFactory.PanelRect(body, "Row" + i, UIFactory.PanelLight);
                UIFactory.TopLeft((RectTransform)bg.transform, 24f, 150f + i * 42f, 912f, 38f);

                var label = UIFactory.Label(bg.transform, "Label", "", 19, TextAnchor.MiddleLeft, UIFactory.Text);
                UIFactory.TopLeft((RectTransform)label.transform, 12f, 0f, 882f, 38f);

                var btn = bg.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                int captured = i;
                btn.onClick.AddListener(() => panel.Craft(captured));

                panel._rows.Add(btn);
                panel._labels.Add(label);
            }

            go.SetActive(false);
            return panel;
        }

        private static string Name(RecipeCategory c)
        {
            switch (c)
            {
                case RecipeCategory.Basics: return "Basics";
                case RecipeCategory.Tools: return "Tools";
                case RecipeCategory.Building: return "Building";
                case RecipeCategory.Comfort: return "Home";
                case RecipeCategory.Food: return "Food";
                case RecipeCategory.Node: return "Node";
                default: return "Ritual";
            }
        }

        protected override void OnOpened() { Refresh(); }

        public void Refresh()
        {
            var inv = _owner != null ? _owner.Inventory : null;

            for (int i = 0; i < _cats.Count; i++)
            {
                var img = _cats[i].targetGraphic as Image;
                if (img != null) img.color = Categories[i] == _category
                    ? new Color(0.95f, 0.72f, 0.25f, 0.95f) : UIFactory.PanelLight;
            }

            _shown.Clear();
            for (int i = 0; i < Recipes.All.Count && _shown.Count < 12; i++)
            {
                var r = Recipes.All[i];
                if (RecipeBook.CategoryOf(r) != _category) continue;
                _shown.Add(r);
            }

            if (_header != null)
                _header.text = _shown.Count + " recipes  ·  tap a row to craft it from your pack. " +
                               "A Workbench nearby unlocks the advanced lines.";

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < _shown.Count;
                _rows[i].gameObject.SetActive(used);
                if (!used) continue;

                var r = _shown[i];
                bool can = RecipeBook.CanCraft(r, inv);
                string text = r.Name + " x" + r.ResultCount + "    " + RecipeBook.RequirementText(r);

                if (!can && inv != null)
                {
                    string missing = RecipeBook.MissingText(r, inv);
                    if (!string.IsNullOrEmpty(missing)) text += "    missing: " + missing;
                }
                if (r.NeedsTable) text += "    [workbench]";

                _labels[i].text = text;
                ((Image)_rows[i].targetGraphic).color = can
                    ? new Color(0.18f, 0.30f, 0.20f, 0.95f) : UIFactory.PanelLight;
            }
        }

        private void Craft(int index)
        {
            if (index < 0 || index >= _shown.Count) return;
            var inv = _owner != null ? _owner.Inventory : null;
            if (inv == null) return;

            var r = _shown[index];
            if (!RecipeBook.CanCraft(r, inv))
            {
                if (_owner != null) _owner.Notify("Missing materials");
                return;
            }

            RecipeBook.Craft(r, inv);
            if (_owner != null) _owner.Notify("Crafted " + r.Name);
            Refresh();
        }
    }

    // ============================================================== trade window
    /// <summary>Three lines from whichever villager you tapped.</summary>
    public sealed class TradePanel : PanelBase
    {
        private LivingHud _owner;
        private LivingEntity _villager;
        private Trade[] _trades = new Trade[0];
        private readonly List<Button> _rows = new List<Button>(3);
        private readonly List<Text> _labels = new List<Text>(3);
        private Text _header;

        public static TradePanel Create(Transform parent, LivingHud owner)
        {
            var go = new GameObject("TradePanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var panel = go.AddComponent<TradePanel>();
            panel._owner = owner;

            var body = Shell(go.transform, "Dim", new Vector2(760f, 420f));
            panel.Root = body;

            var title = UIFactory.Label(body, "Title", "Trade", 30, TextAnchor.UpperLeft, UIFactory.Text);
            UIFactory.TopLeft((RectTransform)title.transform, 24f, 16f, 400f, 40f);

            var close = UIFactory.TextButton(body, "Close", "X", 28, new Color(0.45f, 0.20f, 0.22f, 0.95f), UIFactory.Text);
            UIFactory.TopLeft((RectTransform)close.transform, 678f, 14f, 56f, 56f);
            close.onClick.AddListener(panel.Close);

            panel._header = UIFactory.Label(body, "Header", "", 20, TextAnchor.UpperLeft, UIFactory.Accent);
            UIFactory.TopLeft((RectTransform)panel._header.transform, 24f, 70f, 700f, 28f);

            for (int i = 0; i < 3; i++)
            {
                var bg = UIFactory.PanelRect(body, "Trade" + i, UIFactory.PanelLight);
                UIFactory.TopLeft((RectTransform)bg.transform, 24f, 110f + i * 66f, 712f, 58f);

                var label = UIFactory.Label(bg.transform, "Label", "", 21, TextAnchor.MiddleLeft, UIFactory.Text);
                UIFactory.TopLeft((RectTransform)label.transform, 14f, 0f, 682f, 58f);

                var btn = bg.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                int captured = i;
                btn.onClick.AddListener(() => panel.Do(captured));

                panel._rows.Add(btn);
                panel._labels.Add(label);
            }

            var note = UIFactory.Label(body, "Note", "Villagers pay Coins in the overworld and Gumdrops in the Node.",
                18, TextAnchor.UpperLeft, UIFactory.TextDim);
            UIFactory.TopLeft((RectTransform)note.transform, 24f, 320f, 700f, 24f);

            go.SetActive(false);
            return panel;
        }

        public void OpenFor(LivingEntity villager)
        {
            _villager = villager;
            _trades = TradeTable.For(villager.Def, villager.Variant);
            if (_header != null) _header.text = villager.Def.Name + " has " + _trades.Length + " offers";
            Open();
            Refresh();
        }

        protected override void OnOpened() { Refresh(); }

        public void Refresh()
        {
            var inv = _owner != null ? _owner.Inventory : null;

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < _trades.Length;
                _rows[i].gameObject.SetActive(used);
                if (!used) continue;

                var t = _trades[i];
                bool can = inv != null && inv.Has(t.Cost, t.CostCount);
                _labels[i].text = t.Label;
                ((Image)_rows[i].targetGraphic).color = can
                    ? new Color(0.18f, 0.28f, 0.20f, 0.95f) : UIFactory.PanelLight;
            }
        }

        private void Do(int index)
        {
            if (_villager == null || index < 0 || index >= _trades.Length) return;
            var inv = _owner != null ? _owner.Inventory : null;
            if (inv == null) return;

            var t = _trades[index];
            if (!inv.Has(t.Cost, t.CostCount))
            {
                if (_owner != null) _owner.Notify("You cannot afford that");
                return;
            }

            inv.Remove(t.Cost, t.CostCount);
            int left = inv.Add(t.Give, t.GiveCount);
            if (left > 0)
            {
                inv.Add(t.Cost, t.CostCount);
                if (_owner != null) _owner.Notify("Inventory full");
                return;
            }

            inv.Notify();
            if (_owner != null) _owner.Notify("Traded " + t.Label);
            Refresh();
        }
    }
}