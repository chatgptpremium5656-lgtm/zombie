using System.Collections.Generic;
using UnityEngine;
using Zombie.Core;

namespace Zombie.World
{
    /// <summary>
    /// Builds the small test map: flat ground, trees, rocks, metal ore and an invisible border.
    /// No other locations yet, by design: just a place to gather and build.
    /// </summary>
    public static class WorldBuilder
    {
        public static void Build(Transform root, GameConfig config)
        {
            float half = config.mapHalfSize;

            // Ground: one mesh, one material, one draw call.
            var ground = new GameObject("Ground");
            ground.transform.SetParent(root, false);
            ground.AddComponent<MeshFilter>().sharedMesh = Models.Ground(half);
            var groundRenderer = ground.AddComponent<MeshRenderer>();
            groundRenderer.sharedMaterial = Palette.Opaque;
            groundRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var groundCollider = ground.AddComponent<BoxCollider>();
            groundCollider.center = new Vector3(0, -0.5f, 0);
            groundCollider.size = new Vector3(half * 4f, 1f, half * 4f);

            BuildBorder(root, half);

            var rng = new System.Random(config.worldSeed);
            var taken = new List<Vector3>(config.treeCount + config.rockCount + config.metalRockCount);
            var nodes = new GameObject("Resources").transform;
            nodes.SetParent(root, false);

            Scatter(nodes, rng, taken, config, config.treeCount, NodeType.Tree, 3.2f);
            Scatter(nodes, rng, taken, config, config.rockCount, NodeType.Rock, 3.5f);
            Scatter(nodes, rng, taken, config, config.metalRockCount, NodeType.MetalRock, 4f);
            DecorateEdge(root, rng, half);
        }

        static void Scatter(Transform parent, System.Random rng, List<Vector3> taken, GameConfig config, int count,
            NodeType type, float spacing)
        {
            float half = config.mapHalfSize - 6f;
            int placed = 0, attempts = 0;
            while (placed < count && attempts < count * 30)
            {
                attempts++;
                var pos = new Vector3(((float)rng.NextDouble() * 2f - 1f) * half, 0f, ((float)rng.NextDouble() * 2f - 1f) * half);
                if (pos.magnitude < config.clearRadius) continue;
                // Metal is rarer and further from spawn: a reason to leave the base.
                if (type == NodeType.MetalRock && pos.magnitude < half * 0.45f) continue;
                if (IsTooClose(taken, pos, spacing)) continue;

                taken.Add(pos);
                CreateNode(parent, type, pos, rng, config);
                placed++;
            }
        }

        static bool IsTooClose(List<Vector3> taken, Vector3 pos, float spacing)
        {
            float sq = spacing * spacing;
            for (int i = 0; i < taken.Count; i++)
            {
                if ((taken[i] - pos).sqrMagnitude < sq) return true;
            }
            return false;
        }

        static void CreateNode(Transform parent, NodeType type, Vector3 pos, System.Random rng, GameConfig config)
        {
            var go = new GameObject(type.ToString());
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);

            var visual = new GameObject("Model");
            visual.transform.SetParent(go.transform, false);
            float scale = 0.85f + (float)rng.NextDouble() * 0.4f;
            visual.transform.localScale = Vector3.one * scale;
            var filter = visual.AddComponent<MeshFilter>();
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Palette.Opaque;

            Collider col;
            int capacity;
            if (type == NodeType.Tree)
            {
                filter.sharedMesh = Models.Tree(rng.Next(0, 1000));
                var capsule = go.AddComponent<CapsuleCollider>();
                capsule.radius = 0.4f * scale;
                capsule.height = 4f * scale;
                capsule.center = new Vector3(0, 2f * scale, 0);
                col = capsule;
                capacity = config.treeWood;
            }
            else
            {
                bool metal = type == NodeType.MetalRock;
                filter.sharedMesh = Models.Rock(rng.Next(0, 1000), metal);
                var sphere = go.AddComponent<SphereCollider>();
                sphere.radius = 1.1f * scale;
                sphere.center = new Vector3(0.2f * scale, 0.55f * scale, 0.1f * scale);
                col = sphere;
                capacity = metal ? config.metalRockMetal : config.rockStone;
            }

            go.AddComponent<ResourceNode>().Init(type, capacity, config.resourceRespawnSeconds, visual.transform, col);
        }

        static void BuildBorder(Transform root, float half)
        {
            var border = new GameObject("Border");
            border.transform.SetParent(root, false);
            for (int i = 0; i < 4; i++)
            {
                var wall = border.AddComponent<BoxCollider>();
                bool alongX = i < 2;
                float sign = i % 2 == 0 ? 1f : -1f;
                wall.center = alongX ? new Vector3(0, 10f, sign * (half + 0.5f)) : new Vector3(sign * (half + 0.5f), 10f, 0);
                wall.size = alongX ? new Vector3(half * 2f + 2f, 20f, 1f) : new Vector3(1f, 20f, half * 2f + 2f);
            }
        }

        /// <summary>A dense ring of dead trees and boulders outside the border hides the edge of the world.</summary>
        static void DecorateEdge(Transform root, System.Random rng, float half)
        {
            var edge = new GameObject("EdgeDecor").transform;
            edge.SetParent(root, false);
            float step = 5f;
            for (float d = -half - 6f; d <= half + 6f; d += step)
            {
                for (int side = 0; side < 4; side++)
                {
                    float offset = half + 2f + (float)rng.NextDouble() * 5f;
                    Vector3 pos;
                    if (side == 0) pos = new Vector3(d, 0, offset);
                    else if (side == 1) pos = new Vector3(d, 0, -offset);
                    else if (side == 2) pos = new Vector3(offset, 0, d);
                    else pos = new Vector3(-offset, 0, d);
                    var go = new GameObject("Edge");
                    go.transform.SetParent(edge, false);
                    go.transform.position = pos + new Vector3((float)rng.NextDouble() * 2f, 0, (float)rng.NextDouble() * 2f);
                    go.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                    go.transform.localScale = Vector3.one * (1.1f + (float)rng.NextDouble() * 0.7f);
                    bool tree = rng.NextDouble() < 0.65;
                    go.AddComponent<MeshFilter>().sharedMesh = tree
                        ? Models.Tree(rng.Next(0, 1000))
                        : Models.Rock(rng.Next(0, 1000), false);
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = Palette.Opaque;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }
    }
}
