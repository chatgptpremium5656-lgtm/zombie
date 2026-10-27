using UnityEngine;
using UnityEngine.Rendering;
using Zombie.World;
using static UnityEngine.ParticleSystem;

namespace Zombie.EditorTools
{
    /// <summary>Particle systems of the look probe: campfire (flames, embers, smoke) and hit bursts.</summary>
    static class FxFactory
    {
        public static void Flames(Transform parent)
        {
            var ps = Create("Flames", parent, MaterialFactory.Fire);
            var main = ps.main;
            main.startLifetime = new MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = new MinMaxCurve(0.5f, 1.1f);
            main.startSize = new MinMaxCurve(0.38f, 0.62f);
            main.startRotation = new MinMaxCurve(-0.25f, 0.25f);
            main.gravityModifier = -0.12f;
            main.maxParticles = 80;
            var emission = ps.emission;
            emission.rateOverTime = 32f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 6f;
            shape.radius = 0.2f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Fade(ps, new Color(1f, 0.92f, 0.55f), new Color(1f, 0.6f, 0.18f), new Color(0.85f, 0.18f, 0.05f), 0.9f);
            SizeCurve(ps, 0.55f, 1f, 0.15f);
            Noise(ps, 0.35f, 1.4f);
        }

        public static void Embers(Transform parent)
        {
            var ps = Create("Embers", parent, MaterialFactory.Ember);
            var main = ps.main;
            main.startLifetime = new MinMaxCurve(1.4f, 2.6f);
            main.startSpeed = new MinMaxCurve(0.8f, 1.8f);
            main.startSize = new MinMaxCurve(0.025f, 0.05f);
            main.gravityModifier = -0.04f;
            main.maxParticles = 60;
            var emission = ps.emission;
            emission.rateOverTime = 9f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.25f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Fade(ps, new Color(1f, 0.85f, 0.4f), new Color(1f, 0.5f, 0.15f), new Color(0.9f, 0.2f, 0.05f), 1f);
            Noise(ps, 0.7f, 0.8f);
        }

        public static void Smoke(Transform parent)
        {
            var ps = Create("Smoke", parent, MaterialFactory.Smoke);
            ps.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            var main = ps.main;
            main.startLifetime = new MinMaxCurve(3f, 4.2f);
            main.startSpeed = new MinMaxCurve(0.5f, 0.8f);
            main.startSize = new MinMaxCurve(0.5f, 0.8f);
            main.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 40;
            var emission = ps.emission;
            emission.rateOverTime = 3.5f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 10f;
            shape.radius = 0.15f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Fade(ps, new Color(0.42f, 0.4f, 0.4f), new Color(0.55f, 0.55f, 0.56f), new Color(0.7f, 0.72f, 0.75f), 0.32f);
            SizeCurve(ps, 0.6f, 1.4f, 2.6f);
            Noise(ps, 0.4f, 0.4f);
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new MinMaxCurve(-0.4f, 0.4f);
        }

        public static HitEffects HitEffects(Transform parent)
        {
            var root = new GameObject("HitEffects");
            root.transform.SetParent(parent, false);

            var chips = Create("Chips", root.transform, MaterialFactory.Chips);
            var main = chips.main;
            main.loop = true;
            main.startLifetime = 1.2f;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startRotation3D = true;
            main.gravityModifier = 1.6f;
            main.maxParticles = 200;
            Disable(chips);
            var collision = chips.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.dampen = 0.45f;
            collision.bounce = 0.35f;
            collision.quality = ParticleSystemCollisionQuality.Low;
            collision.collidesWith = ~(1 << 2);
            var chipSize = chips.sizeOverLifetime;
            chipSize.enabled = true;
            chipSize.size = new MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f)));
            var chipRenderer = chips.GetComponent<ParticleSystemRenderer>();
            chipRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            chipRenderer.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            chipRenderer.alignment = ParticleSystemRenderSpace.World;
            chipRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var dust = Create("Dust", root.transform, MaterialFactory.Smoke);
            var dustMain = dust.main;
            dustMain.loop = true;
            dustMain.startLifetime = new MinMaxCurve(0.6f, 0.9f);
            dustMain.startSpeed = 0f;
            dustMain.maxParticles = 60;
            Disable(dust);
            Fade(dust, Color.white, Color.white, Color.white, 0.55f);
            SizeCurve(dust, 0.6f, 1.2f, 1.6f);

            var leaves = Create("Leaves", root.transform, MaterialFactory.Leaf);
            var leafMain = leaves.main;
            leafMain.loop = true;
            leafMain.startLifetime = 3f;
            leafMain.startSpeed = 0f;
            leafMain.gravityModifier = 0.12f;
            leafMain.maxParticles = 200;
            Disable(leaves);
            var leafLimit = leaves.limitVelocityOverLifetime;
            leafLimit.enabled = true;
            leafLimit.limit = 1.1f;
            leafLimit.dampen = 0.15f;
            Noise(leaves, 0.9f, 0.7f);
            var leafColor = leaves.colorOverLifetime;
            leafColor.enabled = true;
            leafColor.color = new MinMaxGradient(Gradient(Color.white, Color.white, Color.white, 1f, 0.85f));

            var effects = root.AddComponent<HitEffects>();
            effects.Setup(chips, dust, leaves);
            return effects;
        }

        static void Disable(ParticleSystem ps)
        {
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
        }

        static ParticleSystem Create(string name, Transform parent, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;
            main.loop = true;
            main.startColor = Color.white;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        static void Fade(ParticleSystem ps, Color start, Color mid, Color end, float alpha)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new MinMaxGradient(Gradient(start, mid, end, alpha, 0.2f));
        }

        static Gradient Gradient(Color start, Color mid, Color end, float alpha, float fadeInEnd)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(mid, 0.45f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, Mathf.Min(fadeInEnd, 0.15f)), new GradientAlphaKey(alpha * 0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        static void SizeCurve(ParticleSystem ps, float start, float mid, float end)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            float max = Mathf.Max(start, Mathf.Max(mid, end));
            size.size = new MinMaxCurve(max, new AnimationCurve(new Keyframe(0f, start / max), new Keyframe(0.35f, mid / max), new Keyframe(1f, end / max)));
        }

        static void Noise(ParticleSystem ps, float strength, float frequency)
        {
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = frequency;
            noise.scrollSpeed = 0.4f;
            noise.quality = ParticleSystemNoiseQuality.Medium;
        }
    }
}
