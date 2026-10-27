using System;

namespace Zombie.Logic
{
    public enum ZombieKind
    {
        Shambler = 0,
        Runner = 1,
        Brute = 2,
    }

    public struct WavePlan
    {
        public int Night;
        public int Shamblers;
        public int Runners;
        public int Brutes;
        /// <summary>From how many sides of the map the horde comes (1..3).</summary>
        public int Directions;
        /// <summary>Health multiplier once the on-screen cap is reached: hordes keep getting harder without more zombies.</summary>
        public float HealthMultiplier;

        public int Total => Shamblers + Runners + Brutes;
    }

    /// <summary>
    /// Decides the size and mix of each night's horde. Night 1 is a handful of shamblers,
    /// runners join on night 2, brutes on night 3, and the total is capped for performance.
    /// </summary>
    public static class WavePlanner
    {
        public const int BaseCount = 8;
        public const int GrowthPerNight = 6;

        static readonly float[] PlayerScale = { 1f, 1f, 1.4f, 1.8f, 2.2f };

        public static WavePlan Plan(int night, int players, int maxAlive)
        {
            if (night < 1) night = 1;
            players = Math.Max(1, Math.Min(4, players));
            maxAlive = Math.Max(1, maxAlive);

            float raw = (BaseCount + GrowthPerNight * (night - 1)) * PlayerScale[players];
            int total = (int)Math.Round(raw);
            float healthMultiplier = 1f;
            if (total > maxAlive)
            {
                healthMultiplier = 1f + (total - maxAlive) / (float)maxAlive;
                total = maxAlive;
            }

            int brutes = night >= 3 ? Math.Min(1 + (night - 3) / 2, Math.Max(1, total / 8)) : 0;
            float runnerShare = night >= 2 ? Math.Min(0.35f, 0.15f + 0.03f * (night - 2)) : 0f;
            int runners = (int)Math.Round((total - brutes) * runnerShare);
            int shamblers = total - brutes - runners;

            return new WavePlan
            {
                Night = night,
                Shamblers = shamblers,
                Runners = runners,
                Brutes = brutes,
                Directions = Math.Min(3, 1 + (night - 1) / 3),
                HealthMultiplier = healthMultiplier,
            };
        }
    }
}
