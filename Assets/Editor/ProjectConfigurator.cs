using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Zombie.EditorTools
{
    /// <summary>
    /// One-time project setup: URP asset (Forward+, soft 2-cascade shadows, MSAA, HDR),
    /// linear colour space and the Input System as the active input backend.
    /// </summary>
    static class ProjectConfigurator
    {
        public const string SettingsFolder = "Assets/Settings";
        const string RendererPath = SettingsFolder + "/URP_Renderer.asset";
        const string PipelinePath = SettingsFolder + "/URP_Pipeline.asset";
        const string PostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        [MenuItem("Zombie/Setup/Configure Project")]
        public static void Configure()
        {
            Folders.Ensure(SettingsFolder);
            var pipeline = EnsurePipeline();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);

            PlayerSettings.colorSpace = ColorSpace.Linear;
            SetInputBackend();
            AssetDatabase.SaveAssets();
        }

        static UniversalRenderPipelineAsset EnsurePipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            renderer.renderingMode = RenderingMode.ForwardPlus;
            renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
            EditorUtility.SetDirty(renderer);

            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            var so = new SerializedObject(pipeline);
            Set(so, "m_ShadowDistance", 50f);
            Set(so, "m_ShadowCascadeCount", 2);
            Set(so, "m_Cascade2Split", 0.3f);
            Set(so, "m_SoftShadowsSupported", true);
            Set(so, "m_SoftShadowQuality", 2);
            Set(so, "m_MainLightShadowmapResolution", 2048);
            Set(so, "m_MSAA", 4);
            Set(so, "m_SupportsHDR", true);
            Set(so, "m_AdditionalLightsRenderingMode", 1);
            Set(so, "m_UseSRPBatcher", true);
            Set(so, "m_RequireDepthTexture", false);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            return pipeline;
        }

        static void Set(SerializedObject so, string name, float value)
        {
            var p = so.FindProperty(name);
            if (p == null)
                Debug.LogWarning("[Zombie] URP setting not found: " + name);
            else if (p.propertyType == SerializedPropertyType.Float)
                p.floatValue = value;
            else if (p.propertyType == SerializedPropertyType.Boolean)
                p.boolValue = value > 0.5f;
            else
                p.intValue = (int)value;
        }

        static void Set(SerializedObject so, string name, int value) => Set(so, name, (float)value);

        static void Set(SerializedObject so, string name, bool value) => Set(so, name, value ? 1f : 0f);

        static void SetInputBackend()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets.Length == 0)
                return;
            var so = new SerializedObject(assets[0]);
            var p = so.FindProperty("activeInputHandler");
            if (p != null && p.intValue != 1)
            {
                p.intValue = 1;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[Zombie] Input System set as the active input backend (takes effect after an editor restart).");
            }
        }
    }

    static class Folders
    {
        public static void Ensure(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            Ensure(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
