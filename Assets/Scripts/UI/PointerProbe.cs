using UnityEngine;
using UnityEngine.EventSystems;

namespace DivergentGenesis.UI
{
    /// <summary>
    /// Records the last touch/mouse position anywhere on the panel so the
    /// carried item icon can follow the finger.
    /// </summary>
    public sealed class PointerProbe : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public static Vector2 LastScreenPos { get; private set; }

        public void OnPointerDown(PointerEventData e) { LastScreenPos = e.position; }
        public void OnDrag(PointerEventData e) { LastScreenPos = e.position; }
    }
}
