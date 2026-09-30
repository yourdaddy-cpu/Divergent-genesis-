using UnityEngine;
using DivergentGenesis.World;

namespace DivergentGenesis.Player
{
    /// <summary>Third person orbit camera with voxel collision, plus first person toggle.</summary>
    public sealed class PlayerCamera : MonoBehaviour
    {
        public PlayerController Player;
        public Camera Cam;

        [Header("Third person")]
        public float Distance = 4.6f;
        public float MinDistance = 1.2f;
        public float HeightOffset = 0.25f;
        public float SmoothTime = 0.08f;
        public float Fov = 62f;

        public bool FirstPerson { get; set; }

        private Vector3 _smoothPos;
        private bool _initialised;

        public static PlayerCamera Instance;

        private void Awake()
        {
            Instance = this;
            if (Cam == null) Cam = GetComponentInChildren<Camera>();
        }

        private void Update()
        {
            if (Player == null) return;

            if (InputHub.ToggleViewPressed && !InputHub.UIBlocked)
                FirstPerson = !FirstPerson;

            Vector3 head = Player.Head != null ? Player.Head.position : Player.EyePosition;
            Quaternion rot = Quaternion.Euler(InputHub.Pitch, InputHub.Yaw, 0f);

            if (FirstPerson)
            {
                transform.position = head;
                transform.rotation = rot;
                SetFov(72f);
                return;
            }

            SetFov(Fov);

            float dist = Distance;
            Vector3 pivot = head + Vector3.up * HeightOffset;
            Vector3 back = rot * Vector3.back;

            // pull the camera in if terrain is in the way
            BlockHit hit;
            if (ChunkManager.Instance != null &&
                ChunkManager.Instance.RaycastBlocks(pivot, back, dist + 0.3f, out hit))
            {
                dist = Mathf.Max(MinDistance, hit.Distance - 0.25f);
            }

            Vector3 desired = pivot + back * dist;
            if (!_initialised) { _smoothPos = desired; _initialised = true; }
            _smoothPos = Vector3.Lerp(_smoothPos, desired, 1f - Mathf.Exp(-18f * Time.deltaTime));

            transform.position = _smoothPos;
            transform.rotation = rot;
        }

        private void SetFov(float fov)
        {
            if (Cam == null) return;
            if (!Mathf.Approximately(Cam.fieldOfView, fov))
                Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, fov, Time.deltaTime * 8f);
        }

        public Vector3 AimPoint(float maxDistance)
        {
            if (Player == null) return transform.position + transform.forward * maxDistance;
            return Player.EyePosition + transform.forward * maxDistance;
        }
    }
}
