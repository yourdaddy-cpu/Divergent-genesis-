using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DivergentGenesis.Player;
using DivergentGenesis.Items;
using DivergentGenesis.World;
using DivergentGenesis.Core;

namespace DivergentGenesis.UI
{
    /// <summary>Bar used for health, stamina, hunger and mining progress.</summary>
    public sealed class StatBar : MonoBehaviour
    {
        public Image Back;
        public Image Fill;
        private float _target = 1f;

        public void Set(float value01)
        {
            _target = Mathf.Clamp01(value01);
            if (Fill != null) Fill.fillAmount = _target;
        }

        private void Update()
        {
            if (Fill == null) return;
            float want = _target;
            if (!Mathf.Approximately(Fill.fillAmount, want))
                Fill.fillAmount = Mathf.MoveTowards(Fill.fillAmount, want, Time.deltaTime * 2.2f);
        }
    }

    /// <summary>Nine slots along the bottom of the screen, Minecraft style.</summary>
    public sealed class HotbarUI : MonoBehaviour
    {
        private sealed class Slot
        {
            public Image Background;
            public Image Selection;
            public Image Icon;
            public Text Count;
            public int Index;
            public Button Button;
        }

        private readonly List<Slot> _slots = new List<Slot>(9);
        private InventoryModel _inventory;
        private readonly Color _idle = new Color(0.10f, 0.11f, 0.14f, 0.78f);
        private readonly Color _hot = new Color(0.16f, 0.18f, 0.22f, 0.85f);

        public static HotbarUI Create(Transform parent, InventoryModel inventory)
        {
            var root = UIFactory.PanelRect(parent, "Hotbar", new Color(0, 0, 0, 0));
            UIFactory.Anchor((RectTransform)root.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(9 * 104f, 104f));

            var row = UIFactory.Row((RectTransform)root.transform, 8, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
            var hud = root.gameObject.AddComponent<HotbarUI>();
            hud._inventory = inventory;

            for (int i = 0; i < InventoryModel.HotbarSize; i++)
            {
                var slot = UIFactory.PanelRect(root.transform, "Slot" + i, _idleColor);
                UIFactory.Size(slot.gameObject, 96f, 96f);
                var btn = slot.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;

                var selection = UIFactory.PanelRect(slot.transform, "Sel", new Color(0.95f, 0.75f, 0.25f, 0f));
                UIFactory.Stretch((RectTransform)selection.transform, 4f);
                selection.raycastTarget = false;

                var icon = UIFactory.Icon(slot.transform, "Icon", Color.white);
                UIFactory.Anchor((RectTransform)icon.transform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
                icon.enabled = false;

                var count = UIFactory.Label(slot.transform, "Count", "", 22, TextAnchor.LowerRight, Color.white);
                UIFactory.Corner((RectTransform)count.transform, 8f, 6f, 60f, 28f);

                var s = new Slot
                {
                    Background = slot, Selection = selection, Icon = icon, Count = count, Index = i, Button = btn
                };
                int captured = i;
                btn.onClick.AddListener(() => hud.Select(captured));
                hud._slots.Add(s);
            }

            hud.Refresh();
            return hud;
        }

        private static readonly Color _idleColor = new Color(0.10f, 0.11f, 0.14f, 0.78f);

        private void Select(int index)
        {
            if (_inventory == null) return;
            _inventory.Selected = Mathf.Clamp(index, 0, InventoryModel.HotbarSize - 1);
            _inventory.Notify();
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void Refresh()
        {
            if (_inventory == null) return;
            for (int i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                var stack = _inventory.Slots[i];
                bool selected = i == _inventory.Selected;

                s.Selection.color = selected ? new Color(0.98f, 0.80f, 0.30f, 0.95f) : new Color(0.95f, 0.75f, 0.25f, 0f);
                s.Background.color = selected ? _hot : _idle;

                if (stack.IsEmpty)
                {
                    s.Icon.enabled = false;
                    s.Count.text = "";
                }
                else
                {
                    s.Icon.enabled = true;
                    s.Icon.sprite = ItemIconFactory.Get(stack.Id);
                    s.Count.text = stack.Count > 1 ? stack.Count.ToString() : "";
                }
            }
        }
    }

    /// <summary>
    /// The whole heads-up display: thumb stick, action buttons, hotbar, status
    /// bars, crosshair, mining progress, damage flash and the debug readout.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        public static GameHud Instance;

        public Canvas Canvas;
        public InventoryModel Inventory;
        public PlayerController Player;
        public PlayerInteraction Interaction;
        public PlayerStats Stats;

        private TouchJoystick _joystick;
        private HoldButton _attack;
        private HoldButton _jump;
        private HoldButton _interact;
        private HotbarUI _hotbar;
        private StatBar _health;
        private StatBar _stamina;
        private StatBar _hunger;
        private StatBar _mineBar;
        private Text _mineLabel;
        private Text _debug;
        private Image _crosshair;
        private Image _vignette;
        private Text _toast;
        private Text _borderWarning;
        private float _toastTimer;
        private float _vignetteAmount;
        private float _fps;
        private float _fpsTimer;
        private int _fpsFrames;
        private RectTransform _debugRect;

        public event System.Action InventoryToggled;
        public event System.Action SettingsToggled;

        public static GameHud Create(Transform parent, InventoryModel inventory,
                                    PlayerController player, PlayerInteraction interaction, PlayerStats stats)
        {
            UIFactory.EnsureEventSystem();
            UIFactory.EnsureFont();

            var canvas = UIFactory.CreateCanvas("HUD", 100, parent);
            var hud = canvas.gameObject.AddComponent<GameHud>();
            hud.Canvas = canvas;
            hud.Inventory = inventory;
            hud.Player = player;
            hud.Interaction = interaction;
            hud.Stats = stats;
            Instance = hud;

            Transform root = canvas.transform;

            // --- crosshair --------------------------------------------------
            var cross = UIFactory.Icon(root, "Crosshair", new Color(1f, 1f, 1f, 0.55f));
            UIFactory.Anchor((RectTransform)cross.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
            hud._crosshair = cross;

            // --- look pad (behind the buttons) ------------------------------
            LookPad.Create(root, null);

            // --- joystick ----------------------------------------------------
            hud._joystick = TouchJoystick.Create(root);

            // --- action buttons ----------------------------------------------
            hud._attack = BuildAction(root, "Attack", new Vector2(330f, 150f), 132f, new Color(0.72f, 0.22f, 0.22f, 0.55f),
                                      () => { InputHub.AttackPressed = true; }, held => InputHub.AttackHeld = held);
            hud._jump = BuildAction(root, "Jump", new Vector2(168f, 96f), 116f, new Color(0.20f, 0.45f, 0.75f, 0.55f),
                                     () => { InputHub.JumpPressed = true; InputHub.JumpHeld = true; }, held => { if (!held) InputHub.JumpHeld = false; });
            hud._interact = BuildAction(root, "Use", new Vector2(430f, 300f), 104f, new Color(0.25f, 0.55f, 0.32f, 0.55f),
                                        () => { InputHub.InteractPressed = true; }, null);

            var invBtn = UIFactory.RoundButton(root, "InventoryBtn", "Bag", 26, new Color(0.20f, 0.24f, 0.30f, 0.75f), UIFactory.Text);
            UIFactory.TopLeft((RectTransform)invBtn.transform, 30f, 190f, 92f, 92f);
            invBtn.onClick.AddListener(() => { if (hud.InventoryToggled != null) hud.InventoryToggled(); });

            var setBtn = UIFactory.RoundButton(root, "SettingsBtn", "≡", 34, new Color(0.20f, 0.24f, 0.30f, 0.75f), UIFactory.Text);
            UIFactory.TopLeft((RectTransform)setBtn.transform, 30f, 292f, 92f, 92f);
            setBtn.onClick.AddListener(() => { if (hud.SettingsToggled != null) hud.SettingsToggled(); });

            // --- hotbar -------------------------------------------------------
            hud._hotbar = HotbarUI.Create(root, inventory);

            // --- status bars --------------------------------------------------
            hud._health = BuildBar(root, "Health", new Vector2(-100f, 138f), 330f, new Color(0.80f, 0.18f, 0.20f));
            hud._stamina = BuildBar(root, "Stamina", new Vector2(-100f, 108f), 300f, new Color(0.30f, 0.75f, 0.35f));
            hud._hunger = BuildBar(root, "Hunger", new Vector2(100f, 138f), 300f, new Color(0.85f, 0.62f, 0.22f));

            // --- mining progress ----------------------------------------------
            var mineRoot = UIFactory.PanelRect(root, "MineBar", new Color(0, 0, 0, 0));
            UIFactory.Anchor((RectTransform)mineRoot.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(320f, 30f));
            hud._mineBar = new StatBar();
            var mineBack = UIFactory.PanelRect(mineRoot.transform, "Back", new Color(0, 0, 0, 0.65f));
            UIFactory.Stretch((RectTransform)mineBack.transform, 0f);
            var mineFill = UIFactory.Icon(mineRoot.transform, "Fill", new Color(0.95f, 0.85f, 0.35f, 0.95f));
            UIFactory.Stretch((RectTransform)mineFill.transform, 2f);
            mineFill.type = Image.Type.Filled;
            mineFill.fillMethod = Image.FillMethod.Horizontal;
            mineFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            mineFill.fillAmount = 0f;
            hud._mineBar.Back = mineBack;
            hud._mineBar.Fill = mineFill;
            hud._mineBar.Set(0f);
            mineRoot.gameObject.SetActive(false);

            hud._mineLabel = UIFactory.Label(root, "MineLabel", "", 24, TextAnchor.LowerCenter, UIFactory.Text);
            UIFactory.Anchor((RectTransform)hud._mineLabel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(600f, 30f));

            // --- vignette / damage flash ---------------------------------------
            var vig = UIFactory.PanelRect(root, "DamageVignette", new Color(0.75f, 0.05f, 0.05f, 0f));
            UIFactory.Fill((RectTransform)vig.transform);
            vig.raycastTarget = false;
            hud._vignette = vig;

            // --- toast ----------------------------------------------------------
            hud._toast = UIFactory.Label(root, "Toast", "", 28, TextAnchor.LowerCenter, UIFactory.Accent);
            UIFactory.Anchor((RectTransform)hud._toast.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 220f), new Vector2(900f, 40f));

            // --- border warning ---------------------------------------------------
            hud._borderWarning = UIFactory.Label(root, "BorderWarning", "", 30, TextAnchor.UpperCenter, new Color(1f, 0.45f, 0.45f));
            UIFactory.Anchor((RectTransform)hud._borderWarning.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(900f, 40f));

            // --- debug -------------------------------------------------------------
            hud._debug = UIFactory.Label(root, "Debug", "", 20, TextAnchor.UpperLeft, new Color(0.75f, 0.95f, 0.75f, 0.95f));
            hud._debugRect = (RectTransform)hud._debug.transform;
            UIFactory.TopLeft(hud._debugRect, 26f, 20f, 760f, 300f);

            // world name banner
            var title = UIFactory.Label(root, "Title", "DIVERGENT GENESIS", 30, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, 0.85f));
            UIFactory.Anchor((RectTransform)title.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(900f, 40f));

            if (stats != null)
            {
                stats.HealthChanged += hud.OnHealth;
                stats.StaminaChanged += hud.OnStamina;
                stats.HungerChanged += hud.OnHunger;
                stats.Damaged += hud.OnDamaged;
            }
            if (inventory != null) inventory.Changed += hud.Refresh;
            if (interaction != null) interaction.Changed += hud.RefreshTarget;

            hud.OnHealth(1f); hud.OnStamina(1f); hud.OnHunger(1f);
            return hud;
        }

        private static HoldButton BuildAction(Transform parent, string label, Vector2 pos, float size, Color color,
                                              System.Action onPress, System.Action<bool> onHold)
        {
            var btn = UIFactory.RoundButton(parent, label + "Btn", label, 26, color, UIFactory.Text);
            UIFactory.Corner((RectTransform)btn.transform, pos.x, pos.y, size, size);
            return HoldButton.Attach(btn, onPress, onHold);
        }

        private static StatBar BuildBar(Transform parent, string name, Vector2 pos, float width, Color color)
        {
            var root = UIFactory.PanelRect(parent, name, new Color(0, 0, 0, 0));
            UIFactory.Corner((RectTransform)root.transform, pos.x, pos.y, width, 20f);

            var back = UIFactory.PanelRect(root.transform, "Back", new Color(0.05f, 0.05f, 0.07f, 0.72f));
            UIFactory.Stretch((RectTransform)back.transform, 0f);

            var fill = UIFactory.Icon(root.transform, "Fill", color);
            UIFactory.Stretch((RectTransform)fill.transform, 2f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;

            var bar = root.gameObject.AddComponent<StatBar>();
            bar.Back = back;
            bar.Fill = fill;
            return bar;
        }

        // ------------------------------------------------------------- events
        public void OnHealth(float v) { if (_health != null) _health.Set(v); }
        public void OnStamina(float v) { if (_stamina != null) _stamina.Set(v); }
        public void OnHunger(float v) { if (_hunger != null) _hunger.Set(v); }

        public void OnDamaged(float amount)
        {
            _vignetteAmount = Mathf.Clamp01(0.25f + amount * 0.08f);
        }

        public void Refresh()
        {
            if (_hotbar != null) _hotbar.Refresh();
        }

        public void RefreshTarget()
        {
        }

        public void Toast(string message, float seconds = 2.2f)
        {
            if (_toast == null) return;
            _toast.text = message;
            _toastTimer = seconds;
        }

        public void SetHudVisible(bool visible)
        {
            if (Canvas != null) Canvas.gameObject.SetActive(visible);
        }

        // ------------------------------------------------------------- update
        private void Update()
        {
            float dt = Time.deltaTime;

            // fps
            _fpsFrames++;
            _fpsTimer += dt;
            if (_fpsTimer >= 0.4f)
            {
                _fps = _fpsFrames / _fpsTimer;
                _fpsFrames = 0; _fpsTimer = 0f;
            }

            if (_hotbar != null && Inventory != null && _hotbar.gameObject.activeSelf)
                _hotbar.Refresh();

            UpdateMineBar();
            UpdateVignette(dt);
            UpdateToast(dt);
            UpdateDebug();
            UpdateBorderWarning();
        }

        private void UpdateMineBar()
        {
            if (_mineBar == null || Interaction == null) return;
            float p = Interaction.MineProgress;
            bool show = p > 0.01f;

            if (_mineBar.Back != null && _mineBar.Back.gameObject.activeSelf != show)
                _mineBar.Back.gameObject.transform.parent.gameObject.SetActive(show);

            if (!show) return;
            _mineBar.Set(p);
            if (_mineLabel != null)
                _mineLabel.text = string.IsNullOrEmpty(Interaction.TargetName) ? "" : Interaction.TargetName;
        }

        private void UpdateVignette(float dt)
        {
            if (_vignette == null) return;
            _vignetteAmount = Mathf.MoveTowards(_vignetteAmount, 0f, dt * 1.4f);
            var c = _vignette.color;
            c.a = _vignetteAmount;
            _vignette.color = c;
        }

        private void UpdateToast(float dt)
        {
            if (_toast == null) return;
            if (_toastTimer <= 0f) { if (_toast.text.Length > 0) _toast.text = ""; return; }
            _toastTimer -= dt;
        }

        private void UpdateDebug()
        {
            if (_debug == null || _debugRect == null) return;

            if (!Debug.isDebugBuild && Application.isEditor == false && !DebugLogEnabled)
            {
                if (_debug.text.Length > 0) _debug.text = "";
                return;
            }

            var world = ChunkManager.Instance;
            var player = Player;
            Vector3 p = player != null ? player.transform.position : Vector3.zero;

            string biome = "---";
            BiomeType bt = BiomeType.Plains;
            if (world != null) bt = world.GetBiome(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));
            biome = BiomeSystem.Get(bt).DisplayName;

            float distToEdge = DGBorder.DistanceToEdge(p);

            _debug.text = string.Format(
                "Divergent Genesis  0.1.0\n" +
                "FPS {0:0}   draw {1:0} m   chunks {2} (pend {3})\n" +
                "XYZ  {4:0.0} / {5:0.0} / {6:0.0}\n" +
                "Biome {7}   {8}\n" +
                "Seed {9}   edge in {10:0} m",
                _fps,
                world != null && world.Quality != null ? world.Quality.ViewDistanceMeters : 0,
                world != null ? world.LoadedTileCount : 0,
                world != null ? world.PendingTileCount : 0,
                p.x, p.y, p.z,
                biome,
                player != null ? (player.InWater ? "(swimming)" : player.Grounded ? "(grounded)" : "(airborne)") : "",
                world != null ? world.WorldSeedValue : 0,
                distToEdge);
        }

        public static bool DebugLogEnabled = true;

        private void UpdateBorderWarning()
        {
            if (_borderWarning == null || Player == null) return;
            float d = DGBorder.DistanceToEdge(Player.transform.position);
            if (d < WorldConfig.BorderWarningDistance)
            {
                _borderWarning.text = "WORLD EDGE - " + Mathf.RoundToInt(d) + " m";
            }
            else if (_borderWarning.text.Length > 0)
            {
                _borderWarning.text = "";
            }
        }
    }
}
