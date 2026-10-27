using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Zombie.Core;
using Zombie.World;

namespace Zombie.Player
{
    /// <summary>
    /// Procedural first-person hands: idle breathing, walk bob, look sway, sprint pose and a springy
    /// axe swing (anticipation with squash, fast strike with stretch, overshooting recovery).
    /// The hit is resolved at the moment of impact with a short hit-stop for weight.
    /// </summary>
    public sealed class ViewModelAnimator : MonoBehaviour
    {
        const float WindupEnd = 0.17f;
        const float StrikeEnd = 0.3f;
        const float ImpactTime = 0.25f;
        const float SwingDuration = 0.62f;

        [SerializeField] FirstPersonController _controller;
        [SerializeField] Transform _sway;
        [SerializeField] Transform _rightPivot;
        [SerializeField] Transform _leftPivot;

        [Header("Swing poses (right pivot, local)")]
        [SerializeField] Vector3 _windupRotation = new Vector3(-26f, 6f, 14f);
        [SerializeField] Vector3 _windupOffset = new Vector3(0.02f, 0.05f, -0.05f);
        [SerializeField] Vector3 _strikeRotation = new Vector3(42f, -10f, -12f);
        [SerializeField] Vector3 _strikeOffset = new Vector3(-0.05f, -0.1f, 0.12f);

        [Header("Hit")]
        [SerializeField] float _reach = 2.6f;
        [SerializeField] float _hitRadius = 0.28f;
        [SerializeField] LayerMask _hitMask = ~(1 << 2);
        [SerializeField] float _hitStop = 0.055f;

        public event Action SwingStarted;
        public event Action HitLanded;

        InputAction _attack;
        Spring3 _swayPosition;
        Spring3 _swayRotation;
        Spring3 _swingRotation;
        Spring3 _swingOffset;
        Spring3 _swingScale;
        Spring _sprint;
        float _swingTime = -1f;
        bool _impactDone;
        float _hitStopLeft;
        Vector3 _rightBase;
        Vector3 _leftBase;
        Quaternion _rightBaseRotation;
        Quaternion _leftBaseRotation;

        void Awake()
        {
            _attack = new InputAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
            _attack.AddBinding("<Gamepad>/rightTrigger");
            _rightBase = _rightPivot.localPosition;
            _leftBase = _leftPivot.localPosition;
            _rightBaseRotation = _rightPivot.localRotation;
            _leftBaseRotation = _leftPivot.localRotation;
            _swingScale.Value = Vector3.one;
        }

        void OnEnable() => _attack.Enable();

        void OnDisable() => _attack.Disable();

        void OnDestroy() => _attack.Dispose();

        /// <summary>Starts a swing (also used by tests and screenshots).</summary>
        public void Swing()
        {
            if (_swingTime >= 0f && _swingTime < SwingDuration * 0.8f)
                return;
            _swingTime = 0f;
            _impactDone = false;
            SwingStarted?.Invoke();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;
            if (_attack.WasPressedThisFrame() && Cursor.lockState == CursorLockMode.Locked)
                Swing();

            UpdateSwing(dt);
            UpdateIdleLayers(dt);
        }

        void UpdateSwing(float dt)
        {
            if (_hitStopLeft > 0f)
            {
                _hitStopLeft -= dt;
                return;
            }

            Vector3 rotTarget = Vector3.zero;
            Vector3 offsetTarget = Vector3.zero;
            Vector3 scaleTarget = Vector3.one;
            float frequency = 5.5f;
            float damping = 0.38f;

            if (_swingTime >= 0f)
            {
                _swingTime += dt;
                if (_swingTime < WindupEnd)
                {
                    rotTarget = _windupRotation;
                    offsetTarget = _windupOffset;
                    scaleTarget = new Vector3(1.05f, 0.93f, 1.05f);
                    frequency = 6.5f;
                    damping = 0.75f;
                }
                else if (_swingTime < StrikeEnd)
                {
                    rotTarget = _strikeRotation;
                    offsetTarget = _strikeOffset;
                    scaleTarget = new Vector3(0.95f, 1.09f, 0.95f);
                    frequency = 15f;
                    damping = 0.62f;
                }

                if (!_impactDone && _swingTime >= ImpactTime)
                {
                    _impactDone = true;
                    ResolveHit();
                }
                if (_swingTime >= SwingDuration)
                    _swingTime = -1f;
            }

            _swingRotation.Step(rotTarget, frequency, damping, dt);
            _swingOffset.Step(offsetTarget, frequency, damping, dt);
            _swingScale.Step(scaleTarget, frequency * 1.2f, damping * 0.8f, dt);

            _rightPivot.localPosition = _rightBase + _swingOffset.Value;
            _rightPivot.localRotation = _rightBaseRotation * Quaternion.Euler(_swingRotation.Value);
            _rightPivot.localScale = _swingScale.Value;
        }

        void ResolveHit()
        {
            Transform cam = _controller.Camera.transform;
            if (!Physics.SphereCast(cam.position, _hitRadius, cam.forward, out RaycastHit hit, _reach, _hitMask, QueryTriggerInteraction.Ignore))
                return;

            var reaction = hit.collider.GetComponentInParent<HitReaction>();
            if (reaction != null)
                reaction.Hit(hit.point, cam.forward);
            else if (HitEffects.Instance != null)
                HitEffects.Instance.PlayImpact(HitMaterial.Stone, hit.point, hit.normal);

            _hitStopLeft = _hitStop;
            _swingRotation.Velocity *= -0.25f;
            _swingOffset.Velocity *= -0.25f;
            _controller.AddCameraKick(2.2f);
            HitLanded?.Invoke();
        }

        void UpdateIdleLayers(float dt)
        {
            float time = Time.time;

            // Look sway: hands lag behind the camera and spring back.
            Vector2 lookSpeed = _controller.LookDelta / dt;
            Vector3 swayPosTarget = new Vector3(-lookSpeed.x, lookSpeed.y, 0f) * 0.00007f;
            swayPosTarget = Vector3.ClampMagnitude(swayPosTarget, 0.05f);
            Vector3 swayRotTarget = new Vector3(-lookSpeed.y * 0.012f, -lookSpeed.x * 0.014f, lookSpeed.x * 0.01f);
            swayRotTarget = Vector3.ClampMagnitude(swayRotTarget, 7f);
            _swayPosition.Step(swayPosTarget, 4.5f, 0.55f, dt);
            _swayRotation.Step(swayRotTarget, 4.5f, 0.5f, dt);

            // Walk bob (figure eight) and breathing.
            float bob = _controller.BobWeight;
            float phase = _controller.BobPhase;
            Vector3 bobOffset = new Vector3(Mathf.Sin(phase) * 0.014f, -Mathf.Abs(Mathf.Cos(phase)) * 0.016f + 0.008f, 0f) * bob;
            float breath = Mathf.Sin(time * 1.7f);
            Vector3 breathOffset = new Vector3(0f, breath * 0.0035f, breath * 0.0015f) * (1f - bob * 0.6f);

            // Sprint pose: arms dip and tilt.
            _sprint.Step(_controller.IsSprinting ? 1f : 0f, 3f, 0.8f, dt);
            float s = _sprint.Value;
            Vector3 sprintOffset = new Vector3(0.02f, -0.05f, -0.03f) * s;
            Vector3 sprintRotation = new Vector3(14f, -10f, 8f) * s;

            _sway.localPosition = _swayPosition.Value + bobOffset + breathOffset + sprintOffset;
            _sway.localRotation = Quaternion.Euler(_swayRotation.Value + sprintRotation + new Vector3(breath * 0.6f, 0f, Mathf.Sin(phase) * 1.2f * bob));

            float leftBreath = Mathf.Sin(time * 1.7f + 0.8f);
            _leftPivot.localPosition = _leftBase + new Vector3(0f, leftBreath * 0.003f, 0f);
            _leftPivot.localRotation = _leftBaseRotation * Quaternion.Euler(leftBreath * 0.8f, 0f, 0f);
        }
    }
}
