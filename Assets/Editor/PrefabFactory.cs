using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using Zombie.World;

namespace Zombie.EditorTools
{
    /// <summary>Turns the generated FBX models into ready prefabs (materials, colliders, hit reactions, FX).</summary>
    static class PrefabFactory
    {
        public const string Folder = "Assets/Prefabs";
        public const string ModelFolder = "Assets/Art/Models";
        const string AnimationFolder = "Assets/Art/Animation";

        public static GameObject Model(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + "/" + name + ".fbx");

        public static Mesh ModelMesh(string name)
        {
            return AssetDatabase.LoadAllAssetsAtPath(ModelFolder + "/" + name + ".fbx").OfType<Mesh>().First();
        }

        public static void BuildAll()
        {
            Folders.Ensure(Folder);
            Folders.Ensure(AnimationFolder);

            Tree("Tree_Round_1", 4.6f, 2.3f, 0.5f);
            Tree("Tree_Round_2", 4.4f, 2.3f, 0.5f);
            Tree("Tree_Pine_1", 3.6f, 1.6f, 0.4f);
            Tree("Tree_Dead_1", 0f, 0f, 0.35f);
            for (int i = 0; i < 3; i++)
                Rock("Rock_" + i, MaterialFactory.Props, HitMaterial.Stone);
            Rock("Rock_Ore", MaterialFactory.Props, HitMaterial.Ore);
            Simple("Bush_1", MaterialFactory.Foliage, null);
            Simple("Bush_2", MaterialFactory.Foliage, null);
            Simple("Car_Wreck", MaterialFactory.Props, go =>
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.85f, 0f);
                box.size = new Vector3(4.4f, 1.6f, 2.1f);
            });
            Simple("Fence_Broken", MaterialFactory.Props, go =>
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.6f, 0.05f);
                box.size = new Vector3(3.3f, 1.2f, 0.3f);
            });
            Campfire();
            Zombie();
            for (int i = 0; i < 3; i++)
                Simple("Cloud_" + i, MaterialFactory.Cloud, go =>
                {
                    foreach (var r in go.GetComponentsInChildren<Renderer>())
                    {
                        r.shadowCastingMode = ShadowCastingMode.Off;
                        r.receiveShadows = false;
                    }
                });
            AssetDatabase.SaveAssets();
        }

        public static GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + name + ".prefab");

        static GameObject Begin(string model, Material material)
        {
            var go = (GameObject)Object.Instantiate(Model(model));
            go.name = model;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = material;
            return go;
        }

        static void Save(GameObject go, string name)
        {
            PrefabUtility.SaveAsPrefabAsset(go, Folder + "/" + name + ".prefab");
            Object.DestroyImmediate(go);
        }

        static void Simple(string model, Material material, System.Action<GameObject> extra)
        {
            var go = Begin(model, material);
            extra?.Invoke(go);
            Save(go, model);
        }

        static void Tree(string model, float canopyHeight, float canopyRadius, float trunkRadius)
        {
            var go = Begin(model, MaterialFactory.Foliage);
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = trunkRadius;
            capsule.height = 4f;
            capsule.center = new Vector3(0f, 2f, 0f);
            go.AddComponent<HitReaction>().Configure(HitMaterial.Wood, canopyRadius > 0f ? 38f : 28f, canopyHeight, canopyRadius);
            Save(go, model);
        }

        static void Rock(string model, Material material, HitMaterial hitMaterial)
        {
            var go = Begin(model, material);
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = ModelMesh(model);
            go.AddComponent<HitReaction>().Configure(hitMaterial, 9f, 0f, 0f);
            Save(go, model);
        }

        static void Campfire()
        {
            var go = Begin("Campfire", MaterialFactory.Props);
            var fx = new GameObject("FireFX");
            fx.transform.SetParent(go.transform, false);
            fx.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            FxFactory.Flames(fx.transform);
            FxFactory.Embers(fx.transform);
            FxFactory.Smoke(fx.transform);

            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.58f, 0.26f);
            light.range = 8f;
            light.intensity = 1f;
            light.shadows = LightShadows.None;
            lightGo.AddComponent<FireFlicker>();
            Save(go, "Campfire");
        }

        static void Zombie()
        {
            string fbx = ModelFolder + "/Zombie_Shambler.fbx";
            var clip = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            string controllerPath = AnimationFolder + "/Zombie.controller";
            AssetDatabase.DeleteAsset(controllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);

            var go = Begin("Zombie_Shambler", MaterialFactory.Character);
            var animator = go.GetComponent<Animator>();
            if (animator == null)
                animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = 0.38f;
            capsule.height = 2f;
            capsule.center = new Vector3(0f, 1f, 0f);
            Save(go, "Zombie_Shambler");
        }
    }
}
