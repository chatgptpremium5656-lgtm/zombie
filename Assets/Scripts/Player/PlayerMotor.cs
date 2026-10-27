using UnityEngine;
using Zombie.Core;

namespace Zombie.Player
{
    /// <summary>First-person movement and mouse look on a CharacterController.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        public const float EyeHeight = 1.62f;

        public Transform CameraPivot { get; private set; }
        public bool InputEnabled { get; set; } = true;

        CharacterController _controller;
        GameConfig _config;
        float _pitch;
        float _yaw;
        float _verticalSpeed;
        float _bobPhase;

        public void Init(GameConfig config, Camera cam)
        {
            _config = config;
            _controller = GetComponent<CharacterController>();
            _controller.height = 1.8f;
            _controller.radius = 0.35f;
            _controller.center = new Vector3(0, 0.9f, 0);
            _controller.stepOffset = 0.45f;
            _controller.slopeLimit = 50f; // building stairs are 45 degrees
            _controller.skinWidth = 0.04f;

            CameraPivot = new GameObject("CameraPivot").transform;
            CameraPivot.SetParent(transform, false);
            CameraPivot.localPosition = new Vector3(0, EyeHeight, 0);
            cam.transform.SetParent(CameraPivot, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 260f;
            cam.fieldOfView = 75f;
            _yaw = transform.eulerAngles.y;
        }

        /// <summary>Moves the player instantly (respawn). CharacterController must be disabled while teleporting.</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            _controller.enabled = false;
            transform.position = position;
            _yaw = yaw;
            _pitch = 0f;
            transform.rotation = Quaternion.Euler(0, _yaw, 0);
            CameraPivot.localRotation = Quaternion.identity;
            _verticalSpeed = 0f;
            _controller.enabled = true;
        }

        void Update()
        {
            if (Game.Paused) return;
            bool canControl = InputEnabled && !Game.UiOpen;

            if (canControl)
            {
                var look = GameInput.Look * _config.mouseSensitivity;
                _yaw += look.x;
                _pitch = Mathf.Clamp(_pitch - look.y, -85f, 85f);
                transform.rotation = Quaternion.Euler(0, _yaw, 0);
                CameraPivot.localRotation = Quaternion.Euler(_pitch, 0, 0);
            }

            var move = canControl ? GameInput.Move : Vector2.zero;
            bool sprint = canControl && GameInput.Held(GameInput.Button.Sprint) && move.y > 0.1f;
            float speed = sprint ? _config.sprintSpeed : _config.walkSpeed;
            var horizontal = (transform.forward * move.y + transform.right * move.x) * speed;

            if (_controller.isGrounded)
            {
                _verticalSpeed = -2f; // keeps the controller snapped to the ground and stairs
                if (canControl && GameInput.Pressed(GameInput.Button.Jump))
                    _verticalSpeed = Mathf.Sqrt(2f * _config.gravity * _config.jumpHeight);
            }
            else
            {
                _verticalSpeed -= _config.gravity * Time.deltaTime;
            }

            var velocity = horizontal + Vector3.up * _verticalSpeed;
            var flags = _controller.Move(velocity * Time.deltaTime);
            if ((flags & CollisionFlags.Above) != 0 && _verticalSpeed > 0f) _verticalSpeed = 0f;

            // Subtle head bob while walking (kept small to avoid motion sickness).
            float planarSpeed = horizontal.magnitude;
            if (_controller.isGrounded && planarSpeed > 0.5f)
            {
                _bobPhase += Time.deltaTime * planarSpeed * 1.6f;
                CameraPivot.localPosition = new Vector3(0, EyeHeight + Mathf.Sin(_bobPhase * 2f) * 0.03f, 0);
            }
            else
            {
                CameraPivot.localPosition = Vector3.Lerp(CameraPivot.localPosition, new Vector3(0, EyeHeight, 0), Time.deltaTime * 8f);
            }
        }
    }
}
