using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DivergentGenesis.Items;
using DivergentGenesis.World;
using DivergentGenesis.Core;
using DivergentGenesis.Player;

namespace DivergentGenesis.UI
{
    /// <summary>Shared behaviour for the full screen panels.</summary>
    public abstract class PanelBase : MonoBehaviour
    {
        public bool IsOpen { get; private set; }
        public RectTransform Root;
        protected InventoryModel Inventory;
        protected GameHud Hud;

        public void SetInventory(InventoryModel inv) { Inventory = inv; }
        public void SetHud(GameHud hud) { Hud = hud; }

        public void Open()
        {
            IsOpen = true;
            gameObject.SetActive(true);
            InputHub.UIBlocked = true;
            InputHub.Reset();
            OnOpened();
        }

        public void Close()
        {
            IsOpen = false;
            OnClosed();
            gameObject.SetActive(false);
            if (!AnyPanelOpen()) InputHub.UIBlocked = false;
        }

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        protected virtual void OnOpened() { }
        protected virtual void OnClosed() { }

        public static bool AnyPanelOpen()
        {
            var panels = FindObjectsOfType<PanelBase>();
            for (int i = 0; i < panels.Length; i++) if (panels[i].IsOpen) return true;
            return false;
        }

        protected static RectTransform Shell(Transform parent, string name, Vector2 size)
        {
            var dim = UIFactory.PanelRect(parent, name, new Color(0f, 0f, 0f, 0.55f));
            UIFactory.Fill((RectTransform)dim.transform);
            var body = UIFactory.PanelRect(dim.transform, "Body", UIFactory.Panel);
            UIFactory.Anchor((RectTransform)body.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return (RectTransform)body.transform;
        }
    }

    /// <summary>One inventory square: background, icon, count, durability bar.</summary>
    public sealed class SlotWidget
    {
        public Image Background;
        public Image Icon;
        public Text Count;
        public RectTransform Rect;
        public int Index;
    }

    /// <summary>
    /// The backpack. Tap a slot to pick the stack up, tap another to drop it in -
    /// which is far more reliable on a touchscreen than dragging.
    /// </summary>
    public sealed class InventoryPanel : PanelBase
    {
        private readonly List<SlotWidget> _slots = new List<SlotWidget>(36);
        private readonly List<Button> _buttons = new List<Button>(36);
        private Text _title;
        private Text _tooltip;
        private Image _heldGhost;
        private Text _heldCount;
        private ItemStack _held = ItemStack.Empty;
        private int _heldFrom = -1;
        private PlayerController _player;

        public static InventoryPanel Create(Transform parent, InventoryModel inventory, PlayerController player)
        {
            var go = new GameObject("InventoryPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var panel = go.AddComponent<InventoryPanel>();
            panel.Inventory = inventory;
            panel._player = player;

            var body = Shell(go.transform, "Dim", new Vector2(920f, 700f));
            panel.Root = body;

            panel._title = UIFactory.Label(body, "Title", "Inventory", 30, TextAnchor.UpperLeft, UIFactory.Text);
            UIFactory.TopLeft((RectTransform)panel._title.transform, 24f, 16f, 500f, 40f);

            var close = UIFactory.TextButton(body, "Close", "X", 28, new Color(0.45f, 0.20f, 0.22f, 0.95f), UIFactory.Text);
            UIFactory.TopLeft((RectTransform)close.transform, 838f, 14f, 56f, 56f);
            close.onClick.AddListener(panel.Close);

            // 27 backpack slots (3 rows of 9)
            const float slot = 96f;
            const float gap = 8f;
            for (int i = 0; i < InventoryModel.MainSize; i++)
            {
                int row = i / 9, col = i % 9;
                var w = panel.BuildSlot(body, InventoryModel.HotbarSize + i,
                    28f + col * (slot + gap), 100f + row * (slot + gap), slot);
                panel._slots.Add(w);
            }

            // hotbar row
            var sep = UIFactory.PanelRect(body, "Sep", new Color(1f, 1f, 1f, 0.10f));
            UIFactory.Corner((RectTransform)sep.transform, 28f, 412f, 9 * (slot + gap) - gap, 4f);
            sep.raycastTarget = false;

            for (int i = 0; i < InventoryModel.HotbarSize; i++)
            {
                var w = panel.BuildSlot(body, i,
                    28f + i * (slot + gap), 430f, slot);
                panel._slots.Add(w);
            }

            panel._tooltip = UIFactory.Label(body, "Tooltip", "", 22, TextAnchor.UpperCenter, UIFactory.Text);
            UIFactory.Corner((RectTransform)panel._tooltip.transform, 0f, 668f, 920f, 30f);

            // carried stack ghost
            var ghost = UIFactory.PanelRect(go.transform, "Held", new Color(0, 0, 0, 0));
            UIFactory.Anchor((RectTransform)ghost.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f));
            var gicon = UIFactory.Icon(ghost.transform, "Icon", Color.white);
            UIFactory.Stretch((RectTransform)gicon.transform, 0f);
            gicon.enabled = false;
            var gcount = UIFactory.Label(ghost.transform, "Count", "", 24, TextAnchor.LowerRight, Color.white);
            UIFactory.Corner((RectTransform)gcount.transform, 6f, 4f, 60f, 28f);
            panel._heldGhost = gicon;
            panel._heldCount = gcount;
            ghost.gameObject.SetActive(false);

            go.AddComponent<PointerProbe>();
            go.SetActive(false);
            return panel;
        }

        private SlotWidget BuildSlot(Transform parent, int index, float x, float y, float size)
        {
            var bg = UIFactory.PanelRect(parent, "S" + index, new Color(0.16f, 0.18f, 0.22f, 0.95f));
            UIFactory.Corner((RectTransform)bg.transform, x, y, size, size);

            var icon = UIFactory.Icon(bg.transform, "Icon", Color.white);
            UIFactory.Stretch((RectTransform)icon.transform, 12f);
            icon.enabled = false;

            var count = UIFactory.Label(bg.transform, "Count", "", 22, TextAnchor.LowerRight, Color.white);
            UIFactory.Corner((RectTransform)count.transform, 6f, 4f, 60f, 26f);

            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            int captured = index;
            btn.onClick.AddListener(() => Tap(captured));
            _buttons.Add(btn);

            return new SlotWidget { Background = bg, Icon = icon, Count = count, Index = index, Rect = (RectTransform)bg.transform };
        }

        private void Tap(int index)
        {
            if (Inventory == null) return;

            if (_held.IsEmpty)
            {
                var s = Inventory.Slots[index];
                if (s.IsEmpty) return;
                _held = s;
                _heldFrom = index;
                Inventory.Slots[index] = ItemStack.Empty;
            }
            else
            {
                var target = Inventory.Slots[index];
                if (target.IsEmpty)
                {
                    Inventory.Slots[index] = _held;
                }
                else if (target.Id == _held.Id)
                {
                    int max = target.MaxStack;
                    int take = Mathf.Min(max - target.Count, _held.Count);
                    target.Count += take;
                    _held.Count -= take;
                    if (_held.Count > 0) target.Id = target.Id; else _held = ItemStack.Empty;
                    Inventory.Slots[index] = target;
                }
                else
                {
                    Inventory.Slots[index] = _held;
                }
                _held = ItemStack.Empty;
            }

            Inventory.Notify();
            Refresh();
        }

        protected override void OnOpened()
        {
            Refresh();
        }

        protected override void OnClosed()
        {
            // put the carried stack back so nothing is ever lost
            if (!_held.IsEmpty && Inventory != null)
            {
                int left = Inventory.Add(_held.Id, _held.Count);
                if (left > 0) Hud?.Toast("Inventory full - dropped " + left);
            }
            _held = ItemStack.Empty;
            if (_heldGhost != null) _heldGhost.transform.parent.gameObject.SetActive(false);
        }

        public void Refresh()
        {
            if (Inventory == null) return;

            for (int i = 0; i < _slots.Count; i++)
            {
                var w = _slots[i];
                var s = Inventory.Slots[w.Index];
                if (s.IsEmpty)
                {
                    w.Icon.enabled = false;
                    w.Count.text = "";
                    continue;
                }
                w.Icon.enabled = true;
                w.Icon.sprite = ItemIconFactory.Get(s.Id);
                w.Count.text = s.Count > 1 ? s.Count.ToString() : "";
            }

            if (_held.IsEmpty)
            {
                if (_heldGhost != null) _heldGhost.transform.parent.gameObject.SetActive(false);
            }
            else
            {
                var parent = _heldGhost.transform.parent;
                parent.gameObject.SetActive(true);
                _heldGhost.enabled = true;
                _heldGhost.sprite = ItemIconFactory.Get(_held.Id);
                _heldCount.text = _held.Count > 1 ? _held.Count.ToString() : "";
            }
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (_heldGhost == null || _held.IsEmpty) return;

            var ghost = _heldGhost.transform.parent as RectTransform;
            if (ghost == null) return;

            Vector2 local;
            var canvasRect = transform as RectTransform;
            if (canvasRect == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PointerProbe.LastScreenPos, null, out local))
                ghost.anchoredPosition = local;
        }
    }

    /// <summary>2x2 in the pack, 3x3 at a Crafting Table. Shows every matching recipe.</summary>
    public sealed class CraftingPanel : PanelBase
    {
        private const int SmallGrid = 4;
        private const int BigGrid = 9;

        private ItemStack[] _grid = new ItemStack[BigGrid];
        private readonly List<SlotWidget> _gridSlots = new List<SlotWidget>(9);
        private readonly List<Button> _gridButtons = new List<Button>(9);
        private SlotWidget _resultSlot;
        private Image _resultIcon;
        private Text _resultCount;
        private Text _recipeLabel;
        private Text _hintLabel;
        private bool _hasTable;
        private int _gridSize = 2;

        public static CraftingPanel Create(Transform parent, InventoryModel inventory)
        {
            var go = new GameObject("CraftingPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var panel = go.AddComponent<CraftingPanel>();
            panel.Inventory = inventory;

            var body = Shell(go.transform, "Dim", new Vector2(920f, 640f));
            panel.Root = body;

            var title = UIFactory.Label(body, "Title", "Crafting", 30, TextAnchor.UpperLeft, UIFactory.Text);
            UIFactory.TopLeft((RectTransform)title.transform, 24f, 16f, 500f, 40f);
            panel._hintLabel = UIFactory.Label(body, "Hint", "", 20, TextAnchor.UpperRight, UIFactory.TextDim);
            UIFactory.TopLeft((RectTransform)panel._hintLabel.transform, 420f, 22f, 380f, 30f);

            var close = UIFactory.TextButton(body, "Close", "X", 28, new Color(0.45f, 0.20f, 0.22f, 0.95f), UIFactory.Text);
            UIFactory.TopLeft((RectTransform)close.transform, 838f, 14f, 56f, 56f);
            close.onClick.AddListener(panel.Close);

            // 3x3 grid, shown as 2x2 when there is no table
            for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
            {
                int idx = r * 3 + c;
                var w = panel.BuildSlot(body, idx, 60f + c * 84f, 320f - r * 84f, 76f);
                panel._gridSlots.Add(w);
            }

            var arrow = UIFactory.Label(body, "Arrow", ">", 40, TextAnchor.MiddleCenter, UIFactory.TextDim);
            UIFactory.Corner((RectTransform)arrow.transform, 330f, 296f, 60f, 60f);

            // result
            var res = UIFactory.PanelRect(body, "Result", new Color(0.22f, 0.26f, 0.20f, 0.95f));
            UIFactory.Corner((RectTransform)res.transform, 400f, 292f, 84f, 84f);
            var rIcon = UIFactory.Icon(res.transform, "Icon", Color.white);
            UIFactory.Stretch((RectTransform)rIcon.transform, 10f);
            rIcon.enabled = false;
            var rCount = UIFactory.Label(res.transform, "Count", "", 22, TextAnchor.LowerRight, Color.white);
            UIFactory.Corner((RectTransform)rCount.transform, 6f, 4f, 60f, 26f);
            var resBtn = res.gameObject.AddComponent<Button>();
            resBtn.onClick.AddListener(panel.Craft);
            panel._resultSlot = new SlotWidget { Background = res, Icon = rIcon, Count = rCount, Index = -1, Rect = (RectTransform)res.transform };
            panel._resultIcon = rIcon;
            panel._resultCount = rCount;

            var recipeTitle = UIFactory.Label(body, "RecipeTitle", "Recipes you can make", 22, TextAnchor.UpperLeft, UIFactory.Accent);
            UIFactory.Corner((RectTransform)recipeTitle.transform, 60f, 40f, 500f, 30f);
            panel._recipeLabel = UIFactory.Label(body, "Recipes", "", 20, TextAnchor.UpperLeft, UIFactory.TextDim);
            UIFactory.Corner((RectTransform)panel._recipeLabel.transform, 60f, 74f, 800f, 180f);

            go.SetActive(false);
            return panel;
        }

        private SlotWidget BuildSlot(Transform parent, int index, float x, float y, float size)
        {
            var bg = UIFactory.PanelRect(parent, "G" + index, new Color(0.16f, 0.18f, 0.22f, 0.95f));
            UIFactory.Corner((RectTransform)bg.transform, x, y, size, size);

            var icon = UIFactory.Icon(bg.transform, "Icon", Color.white);
            UIFactory.Stretch((RectTransform)icon.transform, 10f);
            icon.enabled = false;

            var count = UIFactory.Label(bg.transform, "Count", "", 20, TextAnchor.LowerRight, Color.white);
            UIFactory.Corner((RectTransform)count.transform, 4f, 3f, 56f, 24f);

            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => TapGrid(index));
            _gridButtons.Add(btn);

            return new SlotWidget { Background = bg, Icon = icon, Count = count, Index = index, Rect = (RectTransform)bg.transform };
        }

        private void TapGrid(int index)
        {
            if (Inventory == null) return;

            // take one out of the grid
            if (!_grid[index].IsEmpty)
            {
                if (Inventory.Add(_grid[index].Id, _grid[index].Count) == 0) _grid[index] = ItemStack.Empty;
                Refresh();
                return;
            }

            // pull the first matching item from the pack
            for (int i = 0; i < InventoryModel.TotalSize; i++)
            {
                var s = Inventory.Slots[i];
                if (s.IsEmpty) continue;
                if (ItemStackIsRecipeItem(s.Id)) continue;
                _grid[index] = new ItemStack(s.Id, 1);
                Inventory.Slots[i].Count--;
                if (Inventory.Slots[i].Count <= 0) Inventory.Slots[i] = ItemStack.Empty;
                break;
            }

            Inventory.Notify();
            Refresh();
        }

        private static bool ItemStackIsRecipeItem(ItemId id)
        {
            return false;
        }

        protected override void OnOpened()
        {
            _hasTable = PlayerNearTable();
            _gridSize = _hasTable ? 3 : 2;
            if (_hintLabel != null)
                _hintLabel.text = _hasTable ? "Crafting Table  (3x3)" : "Hand  (2x2)";
            LayoutGrid();
            Refresh();
        }

        private bool PlayerNearTable()
        {
            var world = ChunkManager.Instance;
            var player = DivergentGenesis.Player.PlayerController.Instance;
            if (world == null || player == null) return false;

            Vector3 p = player.transform.position;
            int bx = Mathf.FloorToInt(p.x), by = Mathf.FloorToInt(p.y), bz = Mathf.FloorToInt(p.z);
            for (int y = -2; y <= 2; y++)
            for (int z = -3; z <= 3; z++)
            for (int x = -3; x <= 3; x++)
            {
                if (world.GetBlock(bx + x, by + y, bz + z) == World.Blocks.CraftingTable) return true;
                if (world.GetBlock(bx + x, by + y, bz + z) == World.Blocks.Furnace) return true;
            }
            return false;
        }

        private void LayoutGrid()
        {
            for (int i = 0; i < _gridSlots.Count; i++)
            {
                int r = i / 3, c = i % 3;
                bool visible = (r < _gridSize && c < _gridSize);
                _gridSlots[i].Rect.gameObject.SetActive(visible);
            }
        }

        public void Refresh()
        {
            for (int i = 0; i < _gridSlots.Count; i++)
            {
                var w = _gridSlots[i];
                var s = _grid[i];
                if (s.IsEmpty) { w.Icon.enabled = false; w.Count.text = ""; continue; }
                w.Icon.enabled = true;
                w.Icon.sprite = ItemIconFactory.Get(s.Id);
                w.Count.text = s.Count > 1 ? s.Count.ToString() : "";
            }

            ItemStack result = ItemStack.Empty;
            var available = Recipes.Available(ActiveGrid(), _hasTable);
            if (available.Count > 0) Recipes.TryMatch(available[0], ActiveGrid(), out result);

            if (result.IsEmpty)
            {
                _resultIcon.enabled = false;
                _resultCount.text = "";
            }
            else
            {
                _resultIcon.enabled = true;
                _resultIcon.sprite = ItemIconFactory.Get(result.Id);
                _resultCount.text = result.Count > 1 ? result.Count.ToString() : "";
            }

            if (_recipeLabel != null)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < available.Count && i < 8; i++)
                    sb.AppendLine("• " + available[i].Name + " x" + available[i].ResultCount);
                if (available.Count == 0) sb.AppendLine("Put ingredients in the grid to see recipes.");
                _recipeLabel.text = sb.ToString();
            }
        }

        private ItemStack[] ActiveGrid()
        {
            return _gridSize == 3 ? _grid : _grid;
        }

        public void Craft()
        {
            ItemStack result;
            if (!Recipes.TryMatch(CurrentRecipe(), ActiveGrid(), out result)) { Refresh(); return; }
            if (Inventory == null) return;
            if (Inventory.Add(result.Id, result.Count) != 0) { Hud?.Toast("Inventory full"); return; }

            for (int i = 0; i < _grid.Length; i++)
            {
                if (_grid[i].IsEmpty) continue;
                _grid[i].Count--;
                if (_grid[i].Count <= 0) _grid[i] = ItemStack.Empty;
            }

            Inventory.Notify();
            Hud?.Toast("Crafted " + result.Def.Name);
            Refresh();
        }

        private Recipe CurrentRecipe()
        {
            var available = Recipes.Available(ActiveGrid(), _hasTable);
            return available.Count > 0 ? available[0] : null;
        }

        protected override void OnClosed()
        {
            for (int i = 0; i < _grid.Length; i++)
            {
                if (_grid[i].IsEmpty) continue;
                if (Inventory != null) Inventory.Add(_grid[i].Id, _grid[i].Count);
                _grid[i] = ItemStack.Empty;
            }
        }
    }

    /// <summary>Graphics tier, render distance, time of day, save.</summary>
    public sealed class SettingsPanel : PanelBase
    {
        private Text _summary;
        private GameBootstrap _bootstrap;

        public static SettingsPanel Create(Transform parent, InventoryModel inventory, GameBootstrap bootstrap)
        {
            var go = new GameObject("SettingsPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var panel = go.AddComponent<SettingsPanel>();
            panel.Inventory = inventory;
            panel._bootstrap = bootstrap;

            var body = Shell(go.transform, "Dim", new Vector2(760f, 620f));
            panel.Root = body;

            var title = UIFactory.Label(body, "Title", "Settings", 30, TextAnchor.UpperLeft, UIFactory.Text);
            UIFactory.TopLeft((RectTransform)title.transform, 24f, 16f, 500f, 40f);

            var close = UIFactory.TextButton(body, "Close", "X", 28, new Color(0.45f, 0.20f, 0.22f, 0.95f), UIFactory.Text);
            UIFactory.TopLeft((RectTransform)close.transform, 678f, 14f, 56f, 56f);
            close.onClick.AddListener(panel.Close);

            panel._summary = UIFactory.Label(body, "Summary", "", 20, TextAnchor.UpperLeft, UIFactory.TextDim);
            UIFactory.Corner((RectTransform)panel._summary.transform, 40f, 540f, 680f, 60f);

            float y = 460f;
            panel.Section(body, "Graphics quality", ref y);
            var tiers = new[] { GraphicsTier.Potato, GraphicsTier.Low, GraphicsTier.Medium, GraphicsTier.High, GraphicsTier.Ultra };
            panel.Row(body, "Tier", ref y, tiers.Length, (i) => QualityProfile.Create(tiers[i]).Name, () =>
            {
                ChunkManager.Instance?.SetQualityTier((GraphicsTier)Mathf.Clamp(panel._selectedTier, 0, 4));
                PlayerPrefs.SetInt("DG_Quality", panel._selectedTier);
                PlayerPrefs.Save();
                panel.Refresh();
            });

            panel.Section(body, "Render distance", ref y);
            panel.Row(body, "Dist", ref y, 3, (i) => WorldConfig.ViewDistanceLabel(WorldConfig.ViewDistancesMeters[i]), () =>
            {
                ChunkManager.Instance?.SetViewDistance(WorldConfig.ViewDistancesMeters[Mathf.Clamp(panel._selectedDist, 0, 2)]);
                PlayerPrefs.SetInt("DG_ViewDistance", panel._selectedDist);
                PlayerPrefs.Save();
                panel.Refresh();
            });

            panel.Section(body, "World", ref y);
            panel.Row(body, "Time", ref y, 3, (i) => (i == 0 ? "Dawn" : i == 1 ? "Noon" : "Midnight"), () =>
            {
                var sky = DivergentGenesis.Environment.SkyController.Instance;
                if (sky != null) sky.SetTimeOfDay(panel._selectedTime == 0 ? 0.26f : panel._selectedTime == 1 ? 0.5f : 0.02f);
            });

            var row = UIFactory.PanelRect(body, "Actions", new Color(0, 0, 0, 0));
            UIFactory.Corner((RectTransform)row.transform, 40f, 60f, 680f, 60f);
            UIFactory.Row((RectTransform)row.transform, 14, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

            var save = UIFactory.TextButton(row.transform, "Save", "Save now", 22, UIFactory.PanelLight, UIFactory.Text);
            UIFactory.Size(save.gameObject, 200f, 52f);
            save.onClick.AddListener(() => { if (bootstrap != null) { bootstrap.SaveNow(); panel.Refresh(); } });

            var home = UIFactory.TextButton(row.transform, "Home", "Teleport home", 22, UIFactory.PanelLight, UIFactory.Text);
            UIFactory.Size(home.gameObject, 200f, 52f);
            home.onClick.AddListener(() => { if (bootstrap != null) bootstrap.TeleportHome(); });

            var newWorld = UIFactory.TextButton(row.transform, "New", "New world", 22, new Color(0.45f, 0.24f, 0.22f, 0.95f), UIFactory.Text);
            UIFactory.Size(newWorld.gameObject, 200f, 52f);
            newWorld.onClick.AddListener(() => { if (bootstrap != null) bootstrap.NewWorld(); });

            go.SetActive(false);
            return panel;
        }

        private int _selectedTier = 2;
        private int _selectedDist = 1;
        private int _selectedTime = 1;

        private void Section(Transform body, string text, ref float y)
        {
            var t = UIFactory.Label(body, "S" + text, text, 24, TextAnchor.UpperLeft, UIFactory.Accent);
            UIFactory.Corner((RectTransform)t.transform, 40f, y, 500f, 32f);
            y -= 34f;
        }

        private void Row(Transform body, string name, ref float y, int count, System.Func<int, string> label, System.Action onChanged)
        {
            // (instance method: lambdas below capture this panel)
            var row = UIFactory.PanelRect(body, "R" + name, new Color(0, 0, 0, 0));
            UIFactory.Corner((RectTransform)row.transform, 40f, y, 680f, 56f);
            UIFactory.Row((RectTransform)row.transform, 10, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

            for (int i = 0; i < count; i++)
            {
                int captured = i;
                bool active = (name == "Tier" ? i == _selectedTier
                               : name == "Dist" ? i == _selectedDist
                               : i == _selectedTime);

                var b = UIFactory.TextButton(row.transform, "B" + i, label(i), 21,
                    active ? new Color(0.95f, 0.72f, 0.25f, 0.95f) : UIFactory.PanelLight,
                    active ? new Color(0.08f, 0.08f, 0.10f) : UIFactory.Text);
                UIFactory.Size(b.gameObject, name == "Dist" ? 200f : 126f, 50f);
                b.onClick.AddListener(() =>
                {
                    if (name == "Tier") _selectedTier = captured;
                    else if (name == "Dist") _selectedDist = captured;
                    else _selectedTime = captured;
                    onChanged();
                });
            }

            y -= 66f;
        }

        protected override void OnOpened()
        {
            _selectedTier = PlayerPrefs.GetInt("DG_Quality", 2);
            _selectedDist = PlayerPrefs.GetInt("DG_ViewDistance", 1);
            Refresh();
        }

        public void Refresh()
        {
            if (_summary == null) return;
            var q = ChunkManager.Instance != null && ChunkManager.Instance.Quality != null
                ? ChunkManager.Instance.Quality : QualityProfile.Create((GraphicsTier)_selectedTier);
            _summary.text =
                "World 140 x 140 km   |   Seed " + (ChunkManager.Instance != null ? ChunkManager.Instance.WorldSeedValue : 0) + "\n" +
                "Profile: " + q.Name + "   |   " + WorldConfig.ViewDistanceLabel(q.ViewDistanceMeters) +
                "   |   " + q.TargetFrameRate + " fps target   |   " + q.WorkerThreads + " gen threads";
        }
    }
}
