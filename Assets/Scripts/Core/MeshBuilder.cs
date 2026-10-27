using System.Collections.Generic;
using UnityEngine;

namespace Zombie.Core
{
    /// <summary>
    /// Builds flat-shaded low-poly meshes coloured through the palette texture.
    /// Every triangle gets its own vertices, so faces stay crisp (the "low poly" look) without textures.
    /// Shapes are convex parts; triangle winding is fixed automatically to face away from the part centre.
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> _vertices = new List<Vector3>(512);
        readonly List<Vector3> _normals = new List<Vector3>(512);
        readonly List<Vector2> _uvs = new List<Vector2>(512);
        readonly List<int> _triangles = new List<int>(768);

        static readonly Vector3[] BoxCorners = new Vector3[8];

        public int TriangleCount => _triangles.Count / 3;

        public void Clear()
        {
            _vertices.Clear();
            _normals.Clear();
            _uvs.Clear();
            _triangles.Clear();
        }

        /// <summary>Adds a triangle facing away from <paramref name="partCenter"/>.</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c, PaletteColor color, Vector3 partCenter)
        {
            var normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-12f) return;
            var centroid = (a + b + c) / 3f;
            if (Vector3.Dot(normal, centroid - partCenter) < 0f)
            {
                var t = b;
                b = c;
                c = t;
                normal = -normal;
            }

            normal.Normalize();
            var uv = Palette.Uv(color);
            int i = _vertices.Count;
            _vertices.Add(a);
            _vertices.Add(b);
            _vertices.Add(c);
            _normals.Add(normal);
            _normals.Add(normal);
            _normals.Add(normal);
            _uvs.Add(uv);
            _uvs.Add(uv);
            _uvs.Add(uv);
            _triangles.Add(i);
            _triangles.Add(i + 1);
            _triangles.Add(i + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, PaletteColor color, Vector3 partCenter)
        {
            Tri(a, b, c, color, partCenter);
            Tri(a, c, d, color, partCenter);
        }

        public void Box(Vector3 center, Vector3 size, PaletteColor color)
        {
            Box(center, size, Quaternion.identity, color);
        }

        public void Box(Vector3 center, Vector3 size, Quaternion rotation, PaletteColor color)
        {
            var h = size * 0.5f;
            for (int i = 0; i < 8; i++)
            {
                var local = new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
                BoxCorners[i] = center + rotation * local;
            }

            var c = BoxCorners;
            Quad(c[0], c[1], c[3], c[2], color, center); // -z
            Quad(c[4], c[5], c[7], c[6], color, center); // +z
            Quad(c[0], c[2], c[6], c[4], color, center); // -x
            Quad(c[1], c[3], c[7], c[5], color, center); // +x
            Quad(c[0], c[1], c[5], c[4], color, center); // -y
            Quad(c[2], c[3], c[7], c[6], color, center); // +y
        }

        /// <summary>Tapered prism standing on <paramref name="baseCenter"/> along <paramref name="up"/>.</summary>
        public void Prism(Vector3 baseCenter, Vector3 up, float bottomRadius, float topRadius, float height, int sides,
            PaletteColor color, float twist = 0f, bool capBottom = true)
        {
            up = up.normalized;
            var side = Vector3.Cross(up, Mathf.Abs(up.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            var fwd = Vector3.Cross(side, up);
            var top = baseCenter + up * height;
            var center = baseCenter + up * (height * 0.5f);

            for (int i = 0; i < sides; i++)
            {
                float a0 = twist + i * Mathf.PI * 2f / sides;
                float a1 = twist + (i + 1) * Mathf.PI * 2f / sides;
                var d0 = side * Mathf.Cos(a0) + fwd * Mathf.Sin(a0);
                var d1 = side * Mathf.Cos(a1) + fwd * Mathf.Sin(a1);
                var b0 = baseCenter + d0 * bottomRadius;
                var b1 = baseCenter + d1 * bottomRadius;
                var t0 = top + d0 * topRadius;
                var t1 = top + d1 * topRadius;

                if (topRadius > 0.0001f) Quad(b0, b1, t1, t0, color, center);
                else Tri(b0, b1, top, color, center);

                if (capBottom) Tri(baseCenter, b1, b0, color, top);
                if (topRadius > 0.0001f) Tri(top, t0, t1, color, baseCenter);
            }
        }

        static readonly Vector3[] IcoVerts = BuildIcoVerts();

        static readonly int[] IcoFaces =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        static Vector3[] BuildIcoVerts()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized;
            return v;
        }

        /// <summary>Irregular faceted blob (rocks, bushes, tree crowns). Same seed = same shape.</summary>
        public void Blob(Vector3 center, Vector3 radii, float jitter, int seed, PaletteColor color, PaletteColor? accent = null)
        {
            var rng = new System.Random(seed);
            var points = new Vector3[IcoVerts.Length];
            for (int i = 0; i < IcoVerts.Length; i++)
            {
                float k = 1f + ((float)rng.NextDouble() * 2f - 1f) * jitter;
                var p = IcoVerts[i] * k;
                points[i] = center + new Vector3(p.x * radii.x, p.y * radii.y, p.z * radii.z);
            }

            for (int f = 0; f < IcoFaces.Length; f += 3)
            {
                var a = points[IcoFaces[f]];
                var b = points[IcoFaces[f + 1]];
                var c = points[IcoFaces[f + 2]];
                var faceColor = color;
                if (accent.HasValue && rng.NextDouble() < 0.3) faceColor = accent.Value;
                Tri(a, b, c, faceColor, center);
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (_vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_triangles, 0, true);
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
