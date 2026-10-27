using UnityEngine;
using UnityEngine.Rendering;

namespace Zombie.Core
{
    /// <summary>One colour of the game palette. Index = texel in the palette texture.</summary>
    public enum PaletteColor
    {
        GroundDark,
        GroundLight,
        Dirt,
        Bark,
        LeafDark,
        LeafLight,
        DeadLeaf,
        Stone,
        StoneDark,
        MetalOre,
        Twig,
        Wood,
        WoodDark,
        Metal,
        Rust,
        ZombieSkin,
        ZombieSkinDark,
        ZombieCloth,
        ZombieCloth2,
        Blood,
        Eye,
        PlayerSkin,
        Sleeve,
        Cloth,
        Spike,
        BruteSkin,
        Paper,
        StoneLight,
        Count,
    }

    /// <summary>
    /// The whole game uses ONE opaque material: a lit shader sampling a tiny palette texture.
    /// Meshes pick their colour through UVs, so everything batches (SRP Batcher / instancing)
    /// and the art stays consistent (CLAUDE.md rule 4 and 5).
    /// </summary>
    public static class Palette
    {
        public const string MaterialResourcePath = "Zombie/PaletteLit";

        // Post-apocalyptic but cartoon: muted, slightly desaturated, warm earth vs sickly zombies.
        static readonly Color32[] Colors =
        {
            new Color32(66, 74, 50, 255),    // GroundDark
            new Color32(88, 92, 60, 255),    // GroundLight
            new Color32(98, 80, 58, 255),    // Dirt
            new Color32(84, 60, 42, 255),    // Bark
            new Color32(50, 74, 46, 255),    // LeafDark
            new Color32(76, 100, 54, 255),   // LeafLight
            new Color32(118, 102, 60, 255),  // DeadLeaf
            new Color32(124, 122, 116, 255), // Stone
            new Color32(86, 86, 86, 255),    // StoneDark
            new Color32(132, 84, 58, 255),   // MetalOre
            new Color32(186, 160, 100, 255), // Twig
            new Color32(142, 98, 58, 255),   // Wood
            new Color32(104, 70, 42, 255),   // WoodDark
            new Color32(98, 108, 116, 255),  // Metal
            new Color32(142, 78, 46, 255),   // Rust
            new Color32(118, 142, 98, 255),  // ZombieSkin
            new Color32(84, 106, 74, 255),   // ZombieSkinDark
            new Color32(76, 70, 92, 255),    // ZombieCloth
            new Color32(108, 90, 70, 255),   // ZombieCloth2
            new Color32(120, 30, 28, 255),   // Blood
            new Color32(255, 214, 90, 255),  // Eye
            new Color32(214, 166, 128, 255), // PlayerSkin
            new Color32(64, 84, 104, 255),   // Sleeve
            new Color32(140, 126, 98, 255),  // Cloth
            new Color32(160, 124, 78, 255),  // Spike
            new Color32(102, 122, 98, 255),  // BruteSkin
            new Color32(120, 160, 200, 255), // Paper (building plan)
            new Color32(150, 148, 140, 255), // StoneLight
        };

        static Material _opaque;
        static Material _ghost;
        static Texture2D _texture;

        public static int ColorCount => (int)PaletteColor.Count;

        public static Vector2 Uv(PaletteColor color)
        {
            return new Vector2(((int)color + 0.5f) / ColorCount, 0.5f);
        }

        public static Color Get(PaletteColor color) => Colors[(int)color];

        public static Texture2D Texture
        {
            get
            {
                if (_texture != null) return _texture;
                _texture = new Texture2D(ColorCount, 1, TextureFormat.RGBA32, false)
                {
                    name = "PaletteTexture",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                _texture.SetPixels32(Colors);
                _texture.Apply(false, true);
                return _texture;
            }
        }

        /// <summary>Shared lit material for every world mesh.</summary>
        public static Material Opaque
        {
            get
            {
                if (_opaque != null) return _opaque;

                bool urp = GraphicsSettings.currentRenderPipeline != null;
                var asset = Resources.Load<Material>(MaterialResourcePath);
                if (asset != null && asset.shader != null && asset.shader.isSupported && IsUrpShader(asset.shader) == urp)
                {
                    _opaque = new Material(asset);
                }
                else
                {
                    var shader = urp ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
                    if (shader == null) shader = Shader.Find("Standard");
                    _opaque = new Material(shader);
                }

                _opaque.name = "PaletteLit (runtime)";
                _opaque.mainTexture = Texture;
                _opaque.color = Color.white;
                _opaque.SetFloat("_Smoothness", 0.08f);
                _opaque.SetFloat("_Glossiness", 0.08f);
                _opaque.SetFloat("_Metallic", 0f);
                _opaque.SetFloat("_SpecularHighlights", 0f);
                _opaque.enableInstancing = true;
                return _opaque;
            }
        }

        /// <summary>Transparent tinted material for build previews. Sprites/Default renders in both pipelines.</summary>
        public static Material Ghost
        {
            get
            {
                if (_ghost != null) return _ghost;
                var shader = Shader.Find("Sprites/Default");
                _ghost = new Material(shader) { name = "BuildGhost (runtime)" };
                _ghost.color = new Color(0.4f, 1f, 0.4f, 0.35f);
                return _ghost;
            }
        }

        static bool IsUrpShader(Shader shader)
        {
            return shader.name.StartsWith("Universal Render Pipeline", System.StringComparison.Ordinal);
        }
    }
}
