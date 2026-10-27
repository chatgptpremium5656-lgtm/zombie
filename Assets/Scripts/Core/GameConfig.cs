using UnityEngine;

namespace Zombie.Core
{
    /// <summary>
    /// Every tuning number of the prototype in one place. The editor setup creates
    /// Assets/Resources/Zombie/GameConfig.asset so values can be tweaked in the Inspector without code.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombie/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        public const string ResourcePath = "Zombie/GameConfig";

        [Header("Map")]
        [Tooltip("Half size of the square test map in metres.")]
        public float mapHalfSize = 80f;
        public int treeCount = 150;
        public int rockCount = 55;
        public int metalRockCount = 14;
        [Tooltip("No trees or rocks closer than this to the spawn point, so there is room to build.")]
        public float clearRadius = 14f;
        public int worldSeed = 1337;

        [Header("Day / night")]
        [Tooltip("Full day + night length in seconds.")]
        public float cycleSeconds = 420f;
        [Range(0.3f, 0.9f)] public float dayFraction = 0.62f;
        [Tooltip("Seconds before nightfall when the HUD warns about the horde.")]
        public float hordeWarningSeconds = 25f;

        [Header("Player")]
        public float walkSpeed = 4.6f;
        public float sprintSpeed = 7.2f;
        public float jumpHeight = 1.1f;
        public float gravity = 22f;
        public float mouseSensitivity = 0.12f;
        public float maxHealth = 100f;
        [Tooltip("Health regenerates slowly after this many seconds without damage.")]
        public float regenDelay = 8f;
        public float regenPerSecond = 1.5f;
        public float respawnSeconds = 5f;

        [Header("Resources")]
        public int treeWood = 60;
        public int rockStone = 50;
        public int metalRockMetal = 30;
        public float resourceRespawnSeconds = 300f;

        [Header("Horde")]
        [Tooltip("Hard cap of zombies alive at once (Mac budget). Mobile will use 80.")]
        public int maxZombiesAlive = 150;
        [Tooltip("Seconds over which a night's horde is released.")]
        public float spawnWindowSeconds = 45f;
        public int dayWanderers = 3;

        [Header("Building")]
        public float buildRange = 7f;
        public float placeRange = 4.5f;

        public static GameConfig Load()
        {
            var asset = Resources.Load<GameConfig>(ResourcePath);
            return asset != null ? asset : CreateInstance<GameConfig>();
        }
    }
}
