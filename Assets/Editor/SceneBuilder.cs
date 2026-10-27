using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zombie.Player;
using Zombie.Rendering;
using Zombie.World;

namespace Zombie.EditorTools
{
    /// <summary>
    /// Builds the look-probe scene Assets/Scenes/Main.unity from scratch: lighting and atmosphere,
    /// ground with grass, scattered nature, the campfire camp with ruins, a shambling zombie,
    /// the first-person rig with animated hands, clouds and the HUD. Re-runnable.
    /// Menu: Zombie / Build Test Scene. Batch: -executeMethod Zombie.EditorTools.SceneBuilder.BuildAll
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        const string ProfilePath = ProjectConfigurator.SettingsFolder + "/PostProcess_Afternoon.asset";
        const int IgnoreRaycastLayer = 2;

        static readonly Vector3 PlayerSpawn = new Vector3(0f, 0f, -6f);

        [MenuItem("Zombie/Build Test Scene")]
        public static void BuildAll()
        {
            ProjectConfigurator.Configure();
            AssetDatabase.Refresh();
            TextureFactory.BuildAll();
            MaterialFactory.BuildAll();
            GroundBuilder.Build(out Mesh groundMesh, out GroundData groundData);
            PrefabFactory.BuildAll();

            Folders.Ensure("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = BuildLighting();
            BuildPostProcessing();
            BuildGround(groundMesh, groundData);
            BuildNature();
            BuildCamp();
            BuildClouds();
            FxFactory.HitEffects(null);
            var viewModel = BuildPlayer();

            var items = new[]
            {
                new HudBuilder.Item { Icon = IconRenderer.Render("StoneAxe", PrefabFactory.ModelMesh("FP_StoneAxe"), MaterialFactory.ViewModel, new Vector3(0f, 200f, 8f)), Count = 1 },
                new HudBuilder.Item { Icon = IconRenderer.Render("Log", PrefabFactory.ModelMesh("Item_Log"), MaterialFactory.Props, new Vector3(-10f, 30f, 12f)), Count = 24 },
                new HudBuilder.Item { Icon = IconRenderer.Render("Stone", PrefabFactory.ModelMesh("Rock_2"), MaterialFactory.Props, new Vector3(-12f, 20f, 0f)), Count = 12 },
                new HudBuilder.Item { Icon = IconRenderer.Render("Ore", PrefabFactory.ModelMesh("Rock_Ore"), MaterialFactory.Props, new Vector3(-12f, 30f, 0f)), Count = 5 },
                new HudBuilder.Item { Icon = IconRenderer.Render("Campfire", PrefabFactory.ModelMesh("Campfire"), MaterialFactory.Props, new Vector3(-22f, 20f, 0f)), Count = 1 },
            };
            HudBuilder.Build(viewModel, items);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Zombie] Test scene built: " + ScenePath);
        }

        static float H(float x, float z) => GroundBuilder.Height(x, z);

        static Vector3 OnGround(float x, float z, float sink = 0f) => new Vector3(x, H(x, z) - sink, z);

        // ------------------------------------------------------------------ lighting

        static Light BuildLighting()
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.rotation = Quaternion.Euler(36f, 32f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.9f, 0.76f);
            sun.intensity = 0.85f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 1f;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.6f;
            RenderSettings.sun = sun;

            RenderSettings.skybox = MaterialFactory.Sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.8f, 0.86f, 0.96f);
            RenderSettings.ambientEquatorColor = new Color(0.7f, 0.72f, 0.68f);
            RenderSettings.ambientGroundColor = new Color(0.5f, 0.45f, 0.38f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.8f, 0.85f, 0.86f);
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 175f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

            var atmosphereGo = new GameObject("Atmosphere");
            var atmosphere = atmosphereGo.AddComponent<StylizedAtmosphere>();
            var so = new SerializedObject(atmosphere);
            so.FindProperty("_sun").objectReferenceValue = sun;
            so.FindProperty("_ambientSky").colorValue = new Color(0.74f, 0.77f, 0.85f);
            so.FindProperty("_ambientGround").colorValue = new Color(0.6f, 0.56f, 0.5f);
            so.ApplyModifiedPropertiesWithoutUndo();
            atmosphere.enabled = false;
            atmosphere.enabled = true;
            return sun;
        }

        static void BuildPostProcessing()
        {
            AssetDatabase.DeleteAsset(ProfilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var tonemapping = Add<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);
            var bloom = Add<Bloom>(profile);
            bloom.intensity.Override(0.55f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.65f);
            bloom.tint.Override(new Color(1f, 0.92f, 0.82f));
            var color = Add<ColorAdjustments>(profile);
            color.postExposure.Override(0.1f);
            color.contrast.Override(14f);
            color.saturation.Override(6f);
            var white = Add<WhiteBalance>(profile);
            white.temperature.Override(6f);
            var smh = Add<ShadowsMidtonesHighlights>(profile);
            smh.shadows.Override(new Vector4(0.94f, 0.97f, 1.08f, 0f));
            smh.highlights.Override(new Vector4(1.04f, 1.01f, 0.95f, 0f));
            var vignette = Add<Vignette>(profile);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("PostProcessing");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>();
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        // ------------------------------------------------------------------ world

        static void BuildGround(Mesh mesh, GroundData data)
        {
            // Reload: the in-memory instances can be replaced by asset saves earlier in the build.
            mesh = AssetDatabase.LoadAssetAtPath<Mesh>(GroundBuilder.MeshPath);
            data = AssetDatabase.LoadAssetAtPath<GroundData>(GroundBuilder.DataPath);
            var ground = new GameObject("Ground", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            ground.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = ground.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialFactory.Ground;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            ground.GetComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

            var grassGo = new GameObject("Grass");
            var grass = grassGo.AddComponent<GrassField>();
            var so = new SerializedObject(grass);
            so.FindProperty("_ground").objectReferenceValue = data;
            so.FindProperty("_mesh").objectReferenceValue = PrefabFactory.ModelMesh("Grass_Clump");
            so.FindProperty("_material").objectReferenceValue = MaterialFactory.Grass;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static readonly List<Vector2> Occupied = new List<Vector2>();

        static GameObject Place(string prefab, Transform parent, float x, float z, float yaw, float scale, float sink = 0.05f)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(PrefabFactory.Load(prefab), parent);
            go.transform.position = OnGround(x, z, sink);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            Occupied.Add(new Vector2(x, z));
            return go;
        }

        static bool Free(float x, float z, float spacing)
        {
            foreach (var p in Occupied)
            {
                if ((p - new Vector2(x, z)).sqrMagnitude < spacing * spacing)
                    return false;
            }
            return true;
        }

        static void BuildNature()
        {
            Occupied.Clear();
            var root = new GameObject("Nature").transform;
            var trees = new GameObject("Trees").transform;
            trees.SetParent(root);
            var rocks = new GameObject("Rocks").transform;
            rocks.SetParent(root);
            var bushes = new GameObject("Bushes").transform;
            bushes.SetParent(root);

            // Hand-placed frame around the clearing.
            Place("Tree_Round_1", trees, -9.5f, 6f, 20f, 1.05f);
            Place("Tree_Pine_1", trees, 12.5f, 11f, 80f, 1.0f);
            Place("Tree_Round_2", trees, 14f, -3f, 140f, 1.15f);
            Place("Tree_Dead_1", trees, -14f, 21f, 60f, 1.1f);
            Place("Tree_Round_2", trees, -4.5f, 25f, 200f, 1.2f);
            Place("Tree_Pine_1", trees, 6.5f, 29f, 10f, 1.15f);
            Place("Tree_Round_1", trees, -16f, -4f, 300f, 1.0f);
            Place("Tree_Pine_1", trees, 20f, 20f, 45f, 1.2f);
            Place("Rock_0", rocks, -6f, 3.5f, 30f, 0.8f, 0.15f);
            Place("Rock_1", rocks, 5f, 21f, 110f, 1.1f, 0.2f);
            Place("Rock_2", rocks, -13f, 9f, 75f, 1.2f, 0.1f);
            Place("Rock_0", rocks, 16f, 5f, 200f, 1.0f, 0.15f);
            Place("Rock_Ore", rocks, 8f, 14f, 15f, 0.95f, 0.15f);
            Place("Bush_1", bushes, -7.5f, 9.5f, 0f, 1.1f);
            Place("Bush_2", bushes, 10.5f, 6f, 90f, 1.0f);
            Place("Bush_1", bushes, 1.5f, 19f, 200f, 0.9f);
            Place("Bush_2", bushes, -12f, 13.5f, 30f, 1.2f);
            Place("Bush_1", bushes, 16.5f, 14f, 270f, 1.0f);
            Place("Bush_2", bushes, -3f, -12f, 150f, 0.9f);

            var random = new System.Random(1234);
            string[] treeKinds = { "Tree_Round_1", "Tree_Round_2", "Tree_Pine_1", "Tree_Round_1", "Tree_Pine_1", "Tree_Round_2" };
            Scatter(random, trees, treeKinds, 85, 4.5f, 18f, 0.9f, 1.35f);
            Scatter(random, trees, new[] { "Tree_Dead_1" }, 6, 6f, 22f, 0.9f, 1.2f);
            Scatter(random, rocks, new[] { "Rock_0", "Rock_1", "Rock_2", "Rock_2" }, 26, 3f, 15f, 0.6f, 1.5f);
            Scatter(random, bushes, new[] { "Bush_1", "Bush_2" }, 36, 2.5f, 14f, 0.8f, 1.3f);
        }

        static void Scatter(System.Random random, Transform parent, string[] kinds, int count, float spacing, float clearRadius, float minScale, float maxScale)
        {
            int placed = 0;
            int attempts = 0;
            while (placed < count && attempts++ < count * 40)
            {
                float x = (float)(random.NextDouble() * 2 - 1) * 72f;
                float z = (float)(random.NextDouble() * 2 - 1) * 72f;
                if (Vector2.Distance(new Vector2(x, z), GroundBuilder.ClearingCenter) < clearRadius)
                    continue;
                if (GroundBuilder.PathDistance(x, z) < 3.2f)
                    continue;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(PlayerSpawn.x, PlayerSpawn.z)) < 6f)
                    continue;
                if (!Free(x, z, spacing))
                    continue;
                string kind = kinds[random.Next(kinds.Length)];
                float scale = Mathf.Lerp(minScale, maxScale, (float)random.NextDouble());
                Place(kind, parent, x, z, (float)random.NextDouble() * 360f, scale, kind.StartsWith("Rock") ? 0.2f * scale : 0.05f);
                placed++;
            }
        }

        static void BuildCamp()
        {
            var root = new GameObject("Camp").transform;
            Place("Campfire", root, GroundBuilder.CampfireSpot.x, GroundBuilder.CampfireSpot.y, 0f, 1f, 0.02f);

            var car = Place("Car_Wreck", root, -10f, 16.5f, 0f, 1f, 0.05f);
            car.transform.rotation = Quaternion.Euler(-2.5f, 28f, -4f);
            Place("Fence_Broken", root, 9.5f, 18f, -12f, 1f, 0.05f);
            Place("Fence_Broken", root, 12.8f, 17.2f, -24f, 1f, 0.05f).transform.rotation = Quaternion.Euler(0f, -24f, 6f);

            var zombie = Place("Zombie_Shambler", root, -2.4f, 11.5f, 168f, 1f, 0f);
            zombie.name = "Zombie";
        }

        static void BuildClouds()
        {
            var root = new GameObject("Clouds").transform;
            var random = new System.Random(77);
            for (int i = 0; i < 10; i++)
            {
                float angle = i / 10f * Mathf.PI * 2f + (float)random.NextDouble() * 0.4f;
                float radius = 220f + (float)random.NextDouble() * 80f;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(PrefabFactory.Load("Cloud_" + (i % 3)), root);
                go.transform.position = new Vector3(Mathf.Cos(angle) * radius, 70f + (float)random.NextDouble() * 45f, Mathf.Sin(angle) * radius);
                go.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                go.transform.localScale = Vector3.one * (1.6f + (float)random.NextDouble() * 1.2f);
            }
        }

        // ------------------------------------------------------------------ player

        static ViewModelAnimator BuildPlayer()
        {
            var player = new GameObject("Player");
            player.layer = IgnoreRaycastLayer;
            player.transform.position = OnGround(PlayerSpawn.x, PlayerSpawn.z) + Vector3.up * 0.05f;
            player.transform.rotation = Quaternion.Euler(0f, 6f, 0f);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.4f;
            controller.slopeLimit = 50f;

            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(player.transform, false);
            pivot.localPosition = new Vector3(0f, 1.62f, 0f);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(pivot, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 72f;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 450f;
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None;
            camData.renderShadows = true;

            var fp = player.AddComponent<FirstPersonController>();
            var so = new SerializedObject(fp);
            so.FindProperty("_cameraPivot").objectReferenceValue = pivot;
            so.FindProperty("_camera").objectReferenceValue = cam;
            so.ApplyModifiedPropertiesWithoutUndo();

            // View model: arms are modelled in camera space; each pivot is an anchor the hand animates around.
            var vmRoot = new GameObject("ViewModel").transform;
            vmRoot.SetParent(camGo.transform, false);
            var sway = new GameObject("Sway").transform;
            sway.SetParent(vmRoot, false);
            // Right pivot sits near the elbow: the axe arcs widely, the sleeve barely moves on screen.
            var rightAnchor = new Vector3(0.62f, -0.37f, 0.4f);
            var leftAnchor = new Vector3(-1f, -0.46f, 0.3f);
            var right = new GameObject("RightPivot").transform;
            right.SetParent(sway, false);
            right.localPosition = rightAnchor + new Vector3(0.07f, 0.06f, 0.07f);
            right.localRotation = Quaternion.Euler(2f, -6f, -6f);
            ViewMesh("Arm_Right", "FP_Arm_Right", right, -rightAnchor);
            ViewMesh("StoneAxe", "FP_StoneAxe", right, -rightAnchor);
            var left = new GameObject("LeftPivot").transform;
            left.SetParent(sway, false);
            left.localPosition = leftAnchor + new Vector3(0.04f, 0.08f, 0.1f);
            left.localRotation = Quaternion.Euler(-4f, 6f, 6f);
            ViewMesh("Arm_Left", "FP_Arm_Left", left, -leftAnchor);

            var animator = vmRoot.gameObject.AddComponent<ViewModelAnimator>();
            var vso = new SerializedObject(animator);
            vso.FindProperty("_controller").objectReferenceValue = fp;
            vso.FindProperty("_sway").objectReferenceValue = sway;
            vso.FindProperty("_rightPivot").objectReferenceValue = right;
            vso.FindProperty("_leftPivot").objectReferenceValue = left;
            vso.ApplyModifiedPropertiesWithoutUndo();
            return animator;
        }

        static void ViewMesh(string name, string model, Transform parent, Vector3 offset)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            go.GetComponent<MeshFilter>().sharedMesh = PrefabFactory.ModelMesh(model);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialFactory.ViewModel;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.layer = IgnoreRaycastLayer;
        }
    }
}
