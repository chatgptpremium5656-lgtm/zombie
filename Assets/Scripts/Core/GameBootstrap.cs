using UnityEngine;
using Zombie.Building;
using Zombie.Player;
using Zombie.UI;
using Zombie.World;
using Zombie.Zombies;

namespace Zombie.Core
{
    /// <summary>
    /// Builds the whole prototype from code when Play is pressed: world, lighting, player, horde, HUD.
    /// Works in any scene (even an empty one), so there is no scene setup to get wrong.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (FindFirstObjectByType<GameBootstrap>() != null) return;
            new GameObject("Game").AddComponent<GameBootstrap>();
        }

        void Awake()
        {
            Game.Reset();
            var config = GameConfig.Load();
            Game.Config = config;
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 1;
            QualitySettings.shadowDistance = 55f;

            var cam = PrepareCamera();
            RemoveSceneLights();

            var worldRoot = new GameObject("World").transform;
            WorldBuilder.Build(worldRoot, config);
            var ground = worldRoot.Find("Ground").GetComponent<Collider>();

            var dayNight = new GameObject("DayNight").AddComponent<DayNightCycle>();
            dayNight.Init(config, cam);
            Game.DayNight = dayNight;

            var building = new GameObject("Building").AddComponent<BuildingSystem>();
            building.Init(config, ground);
            Game.Building = building;

            var horde = new GameObject("Horde").AddComponent<HordeDirector>();
            Game.Horde = horde;
            horde.Init(config);

            var playerGo = new GameObject("Player");
            playerGo.AddComponent<CharacterController>();
            var player = playerGo.AddComponent<PlayerAvatar>();
            player.Init(config, cam, new Vector3(0f, 0.1f, -4f));
            Game.Players.Add(player);

            gameObject.AddComponent<Hud>().Init(player);
            player.Notify("Добывайте дерево и камень, стройте укрытие до ночи");
        }

        static Camera PrepareCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.Skybox;
            return cam;
        }

        static void RemoveSceneLights()
        {
            // The default scene's directional light would fight the day/night cycle.
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional) Destroy(lights[i].gameObject);
            }
        }

        void OnDestroy()
        {
            Game.Reset();
        }
    }
}
