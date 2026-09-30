using UnityEngine;

namespace DivergentGenesis.Player
{
    /// <summary>
    /// One input struct, fed by whichever device is in use: the on-screen
    /// joystick + look pad on a phone, keyboard + mouse in the editor.
    /// Nothing else in the game touches Input directly.
    /// </summary>
    public static class InputHub
    {
        public static Vector2 Move;              // -1..1, left stick
        public static Vector2 LookDelta;         // radians delta this frame
        public static float Yaw;                 // accumulated
        public static float Pitch;

        public static bool JumpPressed;
        public static bool JumpHeld;
        public static bool Sprint;
        public static bool Crouch;
        public static bool AttackPressed;
        public static bool AttackHeld;
        public static bool InteractPressed;
        public static bool InventoryPressed;
        public static int HotbarPressed = -1;    // 0..8, or -1 for none
        public static bool ToggleViewPressed;
        public static bool EscapePressed;

        public static bool UIBlocked;            // true while a panel is open

        public static void ClearFrame()
        {
            LookDelta = Vector2.zero;
            JumpPressed = false;
            AttackPressed = false;
            InteractPressed = false;
            InventoryPressed = false;
            ToggleViewPressed = false;
            EscapePressed = false;
            HotbarPressed = -1;
        }

        public static void Reset()
        {
            Move = Vector2.zero;
            LookDelta = Vector2.zero;
            JumpPressed = JumpHeld = Sprint = Crouch = false;
            AttackPressed = AttackHeld = false;
            InteractPressed = InventoryPressed = false;
            HotbarPressed = -1;
            ToggleViewPressed = false;
            EscapePressed = false;
        }
    }

    public interface IDamageable
    {
        Vector3 Center { get; }
        float Radius { get; }
        bool IsAlive { get; }
        void TakeDamage(float amount, Vector3 from, Vector3 knockback);
    }
}
