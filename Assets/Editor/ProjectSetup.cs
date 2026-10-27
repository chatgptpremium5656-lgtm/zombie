using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Zombie.Core;

namespace Zombie.EditorTools
{
    /// <summary>
    /// One-time project setup that runs automatically when the project is opened:
    /// installs URP + Input System, creates the URP asset, the shared palette material,
    /// the GameConfig asset and the Main scene. Every step is idempotent, so it is safe to run again
    /// from the menu: Zombie → Run Project Setup.
    /// </summary>
    [InitializeOnLoad]
    static class ProjectSetup
    {
        const string UrpPackage = "com.unity.render-pipelines.universal";
        const string InputPackage = "com.unity.inputsystem";
        const string PackagesRequestedKey = "Zombie.ProjectSetup.PackagesRequested";
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string SettingsFolder = "Assets/Settings";
        const string ResourcesFolder = "Assets/Resources/Zombie";

        static AddAndRemoveRequest _request;

        static ProjectSetup()
        {
            EditorApplication.delayCall += () => Run(false);
        }

        [MenuItem("Zombie/Run Project Setup")]
        static void RunFromMenu()
        {
            SessionState.SetBool(PackagesRequestedKey, false);
            Run(true);
        }

        static void Run(bool verbose)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            try
            {
                if (EnsurePackages()) return; // packages are installing; setup continues after the domain reload
                EnsureFolders();
                EnsureUrpAsset();
                EnsureGameConfig();
                EnsurePaletteMaterial();
                EnsurePlayerSettings();
                EnsureScene();
                if (verbose) Debug.Log("[Zombie] Project setup complete.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Zombie] Project setup step failed, the game still runs on the built-in pipeline: " + e);
            }
        }

        // ------------------------------------------------------------------ packages

        static Type UrpAssetType =>
            Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime");

        static Type UrpRendererDataType =>
            Type.GetType("UnityEngine.Rendering.Universal.UniversalRendererData, Unity.RenderPipelines.Universal.Runtime");

        static bool InputSystemInstalled => Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem") != null;

        static bool EnsurePackages()
        {
            bool needUrp = UrpAssetType == null;
            bool needInput = !InputSystemInstalled;
            if (!needUrp && !needInput) return false;
            if (_request != null) return true;
            // Already tried this session and it failed: carry on without them (built-in pipeline, legacy input).
            if (SessionState.GetBool(PackagesRequestedKey, false)) return false;

            SessionState.SetBool(PackagesRequestedKey, true);
            var add = new System.Collections.Generic.List<string>();
            if (needUrp) add.Add(UrpPackage);
            if (needInput) add.Add(InputPackage);
            Debug.Log("[Zombie] Installing packages: " + string.Join(", ", add));
            _request = Client.AddAndRemove(add.ToArray(), null);
            EditorApplication.update += WaitForPackages;
            return true;
        }

        static void WaitForPackages()
        {
            if (_request == null || !_request.IsCompleted) return;
            EditorApplication.update -= WaitForPackages;
            if (_request.Status == StatusCode.Failure)
                Debug.LogWarning("[Zombie] Package install failed: " + _request.Error.message + ". Add URP and Input System from the Package Manager.");
            _request = null;
        }

        // ------------------------------------------------------------------ assets

        static void EnsureFolders()
        {
            CreateFolder("Assets", "Settings");
            CreateFolder("Assets", "Resources");
            CreateFolder("Assets/Resources", "Zombie");
            CreateFolder("Assets", "Scenes");
        }

        static void CreateFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        static void EnsureUrpAsset()
        {
            if (GraphicsSettings.defaultRenderPipeline != null) return;
            var assetType = UrpAssetType;
            var dataType = UrpRendererDataType;
            if (assetType == null || dataType == null) return;

            string assetPath = SettingsFolder + "/URP_Asset.asset";
            var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(assetPath);
            if (asset == null)
            {
                var data = ScriptableObject.CreateInstance(dataType);
                AssetDatabase.CreateAsset(data, SettingsFolder + "/URP_Renderer.asset");

                MethodInfo create = null;
                foreach (var m in assetType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name == "Create" && m.GetParameters().Length == 1) create = m;
                }
                if (create == null) throw new MissingMethodException("UniversalRenderPipelineAsset.Create");

                asset = (RenderPipelineAsset)create.Invoke(null, new object[] { data });
                SetProperty(asset, "shadowDistance", 55f);
                SetProperty(asset, "shadowCascadeCount", 2);
                SetProperty(asset, "msaaSampleCount", 2);
                SetProperty(asset, "supportsHDR", false);
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            GraphicsSettings.defaultRenderPipeline = asset;
            QualitySettings.renderPipeline = asset;
            AssetDatabase.SaveAssets();
            Debug.Log("[Zombie] URP asset created and assigned.");
        }

        static void SetProperty(object target, string name, object value)
        {
            var prop = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite) prop.SetValue(target, value);
        }

        static void EnsureGameConfig()
        {
            string path = ResourcesFolder + "/GameConfig.asset";
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(path) != null) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameConfig>(), path);
        }

        static void EnsurePaletteMaterial()
        {
            // The material lives in Resources so its shader is always included in builds.
            string path = ResourcesFolder + "/PaletteLit.mat";
            bool urp = GraphicsSettings.defaultRenderPipeline != null;
            var shader = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
            if (shader == null) return;

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            mat.enableInstancing = true;
            mat.SetFloat("_Smoothness", 0.08f);
            mat.SetFloat("_Glossiness", 0.08f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
        }

        static void EnsurePlayerSettings()
        {
            if (PlayerSettings.productName == "zombie" || string.IsNullOrEmpty(PlayerSettings.productName))
                PlayerSettings.productName = "Last Stand";
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
        }

        static void EnsureScene()
        {
            if (File.Exists(ScenePath)) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // The scene is intentionally empty apart from Unity's defaults: GameBootstrap builds the game at Play.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[Zombie] Created " + ScenePath + ". Press Play to start.");
        }
    }
}
