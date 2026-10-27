using UnityEditor;
using UnityEngine;

namespace Zombie.EditorTools
{
    /// <summary>The shared material set of the look probe (all on Zombie/Toon, plus sky and particles).</summary>
    static class MaterialFactory
    {
        public const string Folder = "Assets/Art/Materials";

        public static Material Props;
        public static Material Foliage;
        public static Material Ground;
        public static Material Grass;
        public static Material Character;
        public static Material ViewModel;
        public static Material Cloud;
        public static Material Chips;
        public static Material Fire;
        public static Material Ember;
        public static Material Smoke;
        public static Material Leaf;
        public static Material Sky;

        public static void BuildAll()
        {
            Folders.Ensure(Folder);
            var toon = Shader.Find("Zombie/Toon");
            var particle = Shader.Find("Zombie/Particle");
            var sky = Shader.Find("Zombie/SkyGradient");

            Props = Save("Toon_Props", toon, m =>
            {
                m.SetFloat("_Specular", 0.15f);
            });
            Foliage = Save("Toon_Foliage", toon, m =>
            {
                m.SetFloat("_WindStrength", 0.16f);
                m.SetFloat("_WindFlutter", 0.45f);
                m.SetFloat("_RampSmooth", 0.3f);
                m.SetFloat("_RimStrength", 0.45f);
                m.SetColor("_RimColor", new Color(1f, 0.96f, 0.7f));
            });
            Ground = Save("Toon_Ground", toon, m =>
            {
                m.SetFloat("_RimStrength", 0f);
                m.SetFloat("_RampSmooth", 0.35f);
            });
            Grass = Save("Toon_Grass", toon, m =>
            {
                m.SetFloat("_GrassMode", 1f);
                m.SetFloat("_WindStrength", 0.12f);
                m.SetFloat("_WindFlutter", 0.7f);
                m.SetFloat("_RimStrength", 0.25f);
                m.SetFloat("_RampSmooth", 0.4f);
                m.SetFloat("_Cull", 0f);
                m.enableInstancing = true;
            });
            Character = Save("Toon_Character", toon, m =>
            {
                m.SetFloat("_RimStrength", 0.25f);
                m.SetFloat("_RimPower", 3.5f);
                m.SetColor("_RimColor", new Color(0.85f, 1f, 0.75f));
                m.SetFloat("_RampSmooth", 0.14f);
                m.SetFloat("_Specular", 0.12f);
            });
            ViewModel = Save("Toon_ViewModel", toon, m =>
            {
                m.SetFloat("_FogAmount", 0f);
                m.SetColor("_BaseColor", new Color(0.82f, 0.76f, 0.72f));
                m.SetFloat("_RimStrength", 0.25f);
                m.SetFloat("_RampSmooth", 0.2f);
                m.SetFloat("_Specular", 0f);
            });
            Cloud = Save("Toon_Cloud", toon, m =>
            {
                m.SetFloat("_FogAmount", 0.15f);
                m.SetFloat("_RampSmooth", 0.45f);
                m.SetColor("_ShadowTint", new Color(0.86f, 0.88f, 1f));
                m.SetFloat("_RimStrength", 0.9f);
                m.SetColor("_RimColor", Color.white);
                m.SetColor("_EmissionColor", new Color(0.18f, 0.2f, 0.26f));
            });
            Chips = Save("Toon_Chips", toon, m =>
            {
                m.SetFloat("_VertexColorIsSRGB", 0f);
                m.SetFloat("_RimStrength", 0.2f);
                m.enableInstancing = true;
            });

            Fire = Save("FX_Fire", particle, m =>
            {
                m.SetTexture("_BaseMap", Load(TextureFactory.Flame));
                m.SetColor("_Tint", new Color(2.2f, 1.6f, 1.1f, 1f));
                Additive(m);
            });
            Ember = Save("FX_Ember", particle, m =>
            {
                m.SetTexture("_BaseMap", Load(TextureFactory.SoftDot));
                m.SetColor("_Tint", new Color(4f, 2.2f, 1f, 1f));
                Additive(m);
            });
            Smoke = Save("FX_Smoke", particle, m =>
            {
                m.SetTexture("_BaseMap", Load(TextureFactory.Smoke));
                m.SetColor("_Tint", Color.white);
                m.renderQueue = 3010;
            });
            Leaf = Save("FX_Leaf", particle, m =>
            {
                m.SetTexture("_BaseMap", Load(TextureFactory.Leaf));
                m.SetColor("_Tint", new Color(1.15f, 1.15f, 1.15f, 1f));
            });

            Sky = Save("Sky_Afternoon", sky, m =>
            {
                m.SetColor("_ZenithColor", new Color(0.3f, 0.58f, 0.92f));
                m.SetColor("_HorizonColor", new Color(0.93f, 0.9f, 0.82f));
                m.SetColor("_GroundColor", new Color(0.72f, 0.78f, 0.76f));
                m.SetFloat("_HorizonExponent", 0.5f);
                m.SetColor("_SunColor", new Color(1f, 0.86f, 0.62f));
                m.SetFloat("_SunSize", 0.0045f);
                m.SetFloat("_SunGlow", 0.75f);
            });
            AssetDatabase.SaveAssets();
        }

        static void Additive(Material m)
        {
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        }

        static Texture2D Load(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        static Material Save(string name, Shader shader, System.Action<Material> setup)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            if (material.HasProperty("_ShadowTint"))
                material.SetColor("_ShadowTint", new Color(0.9f, 0.87f, 1f));
            setup(material);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
