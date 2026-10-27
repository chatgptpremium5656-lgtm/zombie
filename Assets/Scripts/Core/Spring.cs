using UnityEngine;

namespace Zombie.Core
{
    /// <summary>
    /// Damped spring (semi-implicit Euler). Underdamped settings give the springy overshoot
    /// used for squash and stretch, hit wobbles and UI pops. Frequency in Hz, damping ratio 0..1.
    /// </summary>
    public struct Spring
    {
        public float Value;
        public float Velocity;

        public void Step(float target, float frequency, float damping, float dt)
        {
            float omega = 2f * Mathf.PI * frequency;
            float accel = omega * omega * (target - Value) - 2f * damping * omega * Velocity;
            Velocity += accel * dt;
            Value += Velocity * dt;
        }

        public bool IsSettled(float target, float epsilon = 0.0005f)
        {
            return Mathf.Abs(target - Value) < epsilon && Mathf.Abs(Velocity) < epsilon * 10f;
        }
    }

    /// <summary>Three independent springs, one per axis.</summary>
    public struct Spring3
    {
        public Vector3 Value;
        public Vector3 Velocity;

        public void Step(Vector3 target, float frequency, float damping, float dt)
        {
            float omega = 2f * Mathf.PI * frequency;
            Vector3 accel = omega * omega * (target - Value) - 2f * damping * omega * Velocity;
            Velocity += accel * dt;
            Value += Velocity * dt;
        }
    }

    public static class Ease
    {
        public static float OutBack(float t, float overshoot = 1.70158f)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + t * t * ((overshoot + 1f) * t + overshoot);
        }

        public static float OutCubic(float t)
        {
            t = 1f - Mathf.Clamp01(t);
            return 1f - t * t * t;
        }

        /// <summary>Frame-rate independent exponential smoothing factor.</summary>
        public static float Damp(float sharpness, float dt)
        {
            return 1f - Mathf.Exp(-sharpness * dt);
        }
    }
}
