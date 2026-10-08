using UnityEngine;
using DivergentGenesis.Player;

namespace DivergentGenesis.UI
{
    /// <summary>
    /// Keyboard + mouse, so the game is testable in the editor and on a phone
    /// connected over ADB. The touch controls remain the primary input on device.
    /// </summary>
    public static class EditorInput
    {
        public static void Read()
        {
            if (InputHub.UIBlocked)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) InputHub.EscapePressed = true;
                if (Input.GetMouseButtonDown(0)) InputHub.AttackPressed = true;
                return;
            }

            // --- mouse look ---------------------------------------------------
            if (Input.GetMouseButton(0) && !OverUI())
            {
                InputHub.LookDelta += new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 9f;
            }
            else if (Input.GetMouseButton(2) || Application.isEditor)
            {
                InputHub.LookDelta += new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 9f;
            }

            // --- movement -----------------------------------------------------
            var kb = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (kb.sqrMagnitude > 0.0001f) InputHub.Move = Vector2.ClampMagnitude(kb, 1f);
            else if (InputHub.Move != Vector2.zero && !TouchActive()) InputHub.Move = Vector2.zero;

            InputHub.Sprint = Input.GetKey(KeyCode.LeftShift) || InputHub.Sprint && TouchActive();
            InputHub.Crouch = Input.GetKey(KeyCode.LeftControl);

            if (Input.GetKeyDown(KeyCode.Space)) InputHub.JumpPressed = true;
            InputHub.JumpHeld = InputHub.JumpHeld || Input.GetKey(KeyCode.Space);

            if (Input.GetMouseButtonDown(0) && !OverUI()) InputHub.AttackPressed = true;
            InputHub.AttackHeld = InputHub.AttackHeld || Input.GetMouseButton(0);

            if (Input.GetMouseButtonDown(1) && !OverUI()) InputHub.InteractPressed = true;
            if (Input.GetKeyDown(KeyCode.E)) InputHub.InventoryPressed = true;
            if (Input.GetKeyDown(KeyCode.Escape)) InputHub.EscapePressed = true;
            if (Input.GetKeyDown(KeyCode.F)) InputHub.ToggleViewPressed = true;

            // --- milestone 2: building, the recipe book, the ritual -------------
            if (Input.GetKeyDown(KeyCode.B)) InputHub.BuildPressed = true;
            if (Input.GetKeyDown(KeyCode.R)) InputHub.BuildRotatePressed = true;
            if (Input.GetKeyDown(KeyCode.Q)) InputHub.RecipeBookPressed = true;
            if (Input.GetKeyDown(KeyCode.X)) InputHub.BuildCancelPressed = true;

            // while a build ghost is up, the mouse builds instead of mining
            var building = DivergentGenesis.Building.BuildingSystem.Instance;
            if (building != null && building.IsPlacing)
            {
                InputHub.AttackHeld = false;
                if (Input.GetMouseButtonDown(0) && !OverUI())
                {
                    InputHub.AttackPressed = false;
                    InputHub.BuildPlacePressed = true;
                }
            }

            for (int i = 0; i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) InputHub.HotbarPressed = i;
        }

        private static bool TouchActive()
        {
            return Input.touchCount > 0;
        }

        private static bool OverUI()
        {
            var hud = GameHud.Instance;
            if (hud == null || hud.Canvas == null) return false;
            if (!hud.Canvas.gameObject.activeInHierarchy) return false;
            return UnityEngine.EventSystems.EventSystem.current != null &&
                   UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        }
    }
}
