using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DivergentGenesis.Player;

namespace DivergentGenesis.UI
{
    /// <summary>Left thumb stick. Feeds InputHub.Move.</summary>
    public sealed class TouchJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public RectTransform Area;
        public RectTransform Knob;
        public float Range = 110f;

        private int _pointerId = -100;
        private Vector2 _origin;

        public Vector2 Value { get; private set; }

        public static TouchJoystick Create(Transform parent)
        {
            var area = UIFactory.PanelRect(parent, "Joystick", new Color(1f, 1f, 1f, 0.10f));
            UIFactory.Corner((RectTransform)area.transform, 90f, 110f, 300f, 300f);

            var ring = UIFactory.Icon(area.transform, "Ring", new Color(1f, 1f, 1f, 0.12f));
            UIFactory.Stretch((RectTransform)ring.transform, 0f);

            var knob = UIFactory.Icon(area.transform, "Knob", new Color(0.95f, 0.95f, 0.98f, 0.55f));
            UIFactory.Anchor((RectTransform)knob.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(118f, 118f));

            var joy = area.gameObject.AddComponent<TouchJoystick>();
            joy.Area = (RectTransform)area.transform;
            joy.Knob = (RectTransform)knob.transform;
            return joy;
        }

        public void OnPointerDown(PointerEventData e)
        {
            _pointerId = e.pointerId;
            if (Area != null) RectTransformUtility.ScreenPointToLocalPointInRectangle(Area, e.position, e.pressEventCamera, out _origin);
            Update(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != _pointerId) return;
            Update(e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != _pointerId) return;
            _pointerId = -100;
            Value = Vector2.zero;
            if (Knob != null) Knob.anchoredPosition = Vector2.zero;
        }

        private void Update(PointerEventData e)
        {
            if (Area == null) return;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Area, e.position, e.pressEventCamera, out local))
                return;

            Vector2 delta = local - _origin;
            Vector2 clamped = Vector2.ClampMagnitude(delta, Range);
            Value = clamped / Range;

            if (Knob != null) Knob.anchoredPosition = clamped;

            InputHub.Move = new Vector2(Value.x, Value.y);
        }
    }

    /// <summary>Right hand drag area that turns into camera look.</summary>
    public sealed class LookPad : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public float Sensitivity = 1.0f;
        private int _pointerId = -100;

        public static LookPad Create(Transform parent, RectTransform target)
        {
            var img = UIFactory.PanelRect(parent, "LookPad", new Color(0f, 0f, 0f, 0f));
            var rt = (RectTransform)img.transform;
            UIFactory.Anchor(rt, new Vector2(0.5f, 0f), new Vector2(1f, 1f),
                             new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return img.gameObject.AddComponent<LookPad>();
        }

        public void OnPointerDown(PointerEventData e) { _pointerId = e.pointerId; }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != _pointerId) return;
            // drag right => look right, drag down => look down
            float scale = 1f / Mathf.Max(1f, Screen.dpi > 0f ? Screen.dpi * 0.5f : 400f);
            InputHub.LookDelta += new Vector2(e.delta.x * scale * Sensitivity, e.delta.y * scale * Sensitivity);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == _pointerId) _pointerId = -100;
        }
    }

    /// <summary>A round button that reports press and hold.</summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public System.Action OnPressed;
        public System.Action<bool> OnHoldChanged;
        private int _pointerId = -100;

        public bool Held { get; private set; }

        public static HoldButton Attach(Button button, System.Action onPressed, System.Action<bool> onHold)
        {
            var h = button.gameObject.AddComponent<HoldButton>();
            h.OnPressed = onPressed;
            h.OnHoldChanged = onHold;
            return h;
        }

        public void OnPointerDown(PointerEventData e)
        {
            _pointerId = e.pointerId;
            Held = true;
            if (OnPressed != null) OnPressed();
            if (OnHoldChanged != null) OnHoldChanged(true);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != _pointerId) return;
            _pointerId = -100;
            Held = false;
            if (OnHoldChanged != null) OnHoldChanged(false);
        }

        private void OnDisable()
        {
            if (Held)
            {
                Held = false;
                if (OnHoldChanged != null) OnHoldChanged(false);
            }
        }
    }
}
