using UnityEngine;
using UnityEngine.InputSystem;
using Zombie.Core;

namespace Zombie.Player
{
    /// <summary>
    /// First-person movement and camera: WASD, mouse look, jump and sprint through the Input System.
    /// Look is exponentially smoothed (no jitter), movement accelerates instead of snapping,
    /// sprint widens the FOV, walking bobs the head and landings dip the camera on a spring.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] Transform _cameraPivot;
        [SerializeField] Camera _camera;

        [Header("Movement")]
        [SerializeField] float _walkSpeed = 4.3f;
        [SerializeField] float _sprintSpeed = 7.2f;
        [SerializeField] float _acceleration = 11f;
        [SerializeField] float _airControl = 0.3f;
        [SerializeField] float _jumpHeight = 1.1f;
        [SerializeField] float _gravity = -22f;

        [Header("Look")]
        [SerializeField] float _mouseSensitivity = 0.09f;
        [SerializeField] float _gamepadSensitivity = 140f;
        [SerializeField] float _lookSharpness = 28f;
        [SerializeField] float _pitchLimit = 85f;

        [Header("Camera feel")]
        [SerializeField] float _baseFov = 72f;
        [SerializeField] float _sprintFovKick = 9f;
        [SerializeField] float _fovSharpness = 7f;
        [SerializeField] float _eyeHeight = 1.62f;
        [SerializeField] float _bobAmplitude = 0.04f;
        [SerializeField] float _bobStride = 2.1f;

        CharacterController _controller;
        InputAction _move;
        InputAction _look;
        InputAction _lookStick;
        InputAction _jump;
        InputAction _sprint;

        float _targetYaw;
        float _targetPitch;
        float _yaw;
        float _pitch;
        Vector3 _horizontalVelocity;
        float _verticalVelocity;
        float _bobPhase;
        float _bobWeight;
        Spring _landing;
        bool _wasGrounded = true;

        /// <summary>Smoothed look rotation applied this frame, in degrees (x = yaw, y = pitch). Drives weapon sway.</summary>
        public Vector2 LookDelta { get; private set; }
        public float HorizontalSpeed { get; private set; }
        public float WalkSpeed => _walkSpeed;
        public bool IsGrounded => _controller.isGrounded;
        public bool IsSprinting { get; private set; }
        public float BobPhase => _bobPhase;
        public float BobWeight => _bobWeight;
        public Camera Camera => _camera;

        /// <summary>Lets other systems (view model hits) nudge the camera.</summary>
        public void AddCameraKick(float pitchDegrees)
        {
            _landing.Velocity -= pitchDegrees;
        }

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _yaw = _targetYaw = transform.eulerAngles.y;
            _pitch = _targetPitch = _cameraPivot.localEulerAngles.x > 180f ? _cameraPivot.localEulerAngles.x - 360f : _cameraPivot.localEulerAngles.x;

            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick");
            _look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
            _lookStick = new InputAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick");
            _jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            _jump.AddBinding("<Gamepad>/buttonSouth");
            _sprint = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            _sprint.AddBinding("<Gamepad>/leftStickPress");
        }

        void OnEnable()
        {
            _move.Enable();
            _look.Enable();
            _lookStick.Enable();
            _jump.Enable();
            _sprint.Enable();
        }

        void OnDisable()
        {
            _move.Disable();
            _look.Disable();
            _lookStick.Disable();
            _jump.Disable();
            _sprint.Disable();
        }

        void OnDestroy()
        {
            _move.Dispose();
            _look.Dispose();
            _lookStick.Dispose();
            _jump.Dispose();
            _sprint.Dispose();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            UpdateCursor();
            UpdateLook(dt);
            UpdateMovement(dt);
            UpdateCamera(dt);
        }

        void UpdateCursor()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void UpdateLook(float dt)
        {
            Vector2 delta = Vector2.zero;
            if (Cursor.lockState == CursorLockMode.Locked)
                delta = _look.ReadValue<Vector2>() * _mouseSensitivity;
            delta += _lookStick.ReadValue<Vector2>() * (_gamepadSensitivity * dt);

            _targetYaw += delta.x;
            _targetPitch = Mathf.Clamp(_targetPitch - delta.y, -_pitchLimit, _pitchLimit);

            float k = Ease.Damp(_lookSharpness, dt);
            float newYaw = Mathf.Lerp(_yaw, _targetYaw, k);
            float newPitch = Mathf.Lerp(_pitch, _targetPitch, k);
            LookDelta = new Vector2(newYaw - _yaw, newPitch - _pitch);
            _yaw = newYaw;
            _pitch = newPitch;

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        void UpdateMovement(float dt)
        {
            Vector2 input = Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);
            bool grounded = _controller.isGrounded;
            IsSprinting = _sprint.IsPressed() && input.y > 0.1f && grounded;

            Vector3 wish = transform.right * input.x + transform.forward * input.y;
            float speed = IsSprinting ? _sprintSpeed : _walkSpeed;
            float accel = grounded ? _acceleration : _acceleration * _airControl;
            _horizontalVelocity = Vector3.Lerp(_horizontalVelocity, wish * speed, Ease.Damp(accel, dt));

            if (grounded && _verticalVelocity < 0f)
            {
                if (!_wasGrounded && _verticalVelocity < -4f)
                    _landing.Velocity += Mathf.Clamp(_verticalVelocity * 0.6f, -12f, 0f);
                _verticalVelocity = -2f;
            }
            if (grounded && _jump.WasPressedThisFrame())
                _verticalVelocity = Mathf.Sqrt(2f * _jumpHeight * -_gravity);
            _verticalVelocity += _gravity * dt;
            _wasGrounded = grounded;

            Vector3 velocity = _horizontalVelocity + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * dt);

            HorizontalSpeed = new Vector2(_horizontalVelocity.x, _horizontalVelocity.z).magnitude;
        }

        void UpdateCamera(float dt)
        {
            bool moving = _controller.isGrounded && HorizontalSpeed > 0.4f;
            _bobWeight = Mathf.Lerp(_bobWeight, moving ? Mathf.Clamp01(HorizontalSpeed / _walkSpeed) : 0f, Ease.Damp(8f, dt));
            _bobPhase += HorizontalSpeed / _bobStride * dt * Mathf.PI * 2f * 0.5f;

            _landing.Step(0f, 3.2f, 0.45f, dt);

            float bobY = Mathf.Abs(Mathf.Sin(_bobPhase)) * _bobAmplitude * _bobWeight;
            float bobX = Mathf.Sin(_bobPhase) * _bobAmplitude * 0.5f * _bobWeight;
            _cameraPivot.localPosition = new Vector3(bobX, _eyeHeight + bobY - _bobAmplitude * 0.5f * _bobWeight + _landing.Value * 0.01f, 0f);
            float roll = Mathf.Sin(_bobPhase) * 0.6f * _bobWeight;
            _cameraPivot.localRotation = Quaternion.Euler(_pitch + _landing.Value * 0.35f, 0f, roll);

            float targetFov = _baseFov + (IsSprinting && HorizontalSpeed > _walkSpeed * 0.8f ? _sprintFovKick : 0f);
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFov, Ease.Damp(_fovSharpness, dt));
        }
    }
}
