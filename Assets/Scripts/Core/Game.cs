using System.Collections.Generic;
using UnityEngine;
using Zombie.Building;
using Zombie.Player;
using Zombie.World;
using Zombie.Zombies;

namespace Zombie.Core
{
    /// <summary>
    /// Small service locator for the running session. Set up once by GameBootstrap.
    /// Players is a list so co-op (1-4 players) can plug in without changing zombie targeting.
    /// </summary>
    public static class Game
    {
        public static GameConfig Config { get; internal set; }
        public static DayNightCycle DayNight { get; internal set; }
        public static BuildingSystem Building { get; internal set; }
        public static HordeDirector Horde { get; internal set; }
        public static readonly List<PlayerAvatar> Players = new List<PlayerAvatar>(4);

        /// <summary>True while a menu owns the mouse (craft menu, pause, death screen).</summary>
        public static bool UiOpen { get; set; }

        public static bool Paused { get; private set; }

        public static void SetPaused(bool paused)
        {
            Paused = paused;
            Time.timeScale = paused ? 0f : 1f;
        }

        internal static void Reset()
        {
            Players.Clear();
            UiOpen = false;
            Paused = false;
            Time.timeScale = 1f;
        }
    }
}
