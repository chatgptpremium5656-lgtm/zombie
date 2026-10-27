using UnityEditor;
using UnityEngine;
using Zombie.World;

namespace Zombie.EditorTools
{
    /// <summary>
    /// 150 x 150 m ground: soft noise hills, a flat clearing in the middle, a raised rim at the map edge,
    /// and a winding dirt path. Colour is painted into vertex colours (no textures), the same data is
    /// baked into GroundData for grass placement.
    /// </summary>
    static class GroundBuilder
    {
        public const float Size = 150f;
        public const int Resolution = 201;
        public const string Folder = "Assets/Art/Generated";
        public const string MeshPath = Folder + "/Ground_Mesh.asset";
        public const string DataPath = Folder + "/Ground_Data.asset";

        public static readonly Vector2 ClearingCenter = new Vector2(0f, 7f);
        public static readonly Vector2 CampfireSpot = new Vector2(3.2f, 7.5f);

        static readonly Vector2[] Path =
        {
            new Vector2(-78f, -40f), new Vector2(-44f, -26f), new Vector2(-18f, -14f), new Vector2(-3f, -5f),
            new Vector2(0.2f, 4f), new Vector2(4f, 13f), new Vector2(12f, 21f), new Vector2(30f, 30f),
            new Vector2(52f, 37f), new Vector2(78f, 41f),
        };

        static readonly Color GrassA = new Color(0.29f, 0.5f, 0.19f);
        static readonly Color GrassB = new Color(0.43f, 0.6f, 0.23f);
        static readonly Color GrassDry = new Color(0.64f, 0.64f, 0.3f);
        static readonly Color Slope = new Color(0.46f, 0.52f, 0.24f);
        static readonly Color Dirt = new Color(0.62f, 0.47f, 0.31f);
        static readonly Color DirtDark = new Color(0.47f, 0.34f, 0.23f);

        static float _clearingHeight = float.NaN;

        public static float Height(float x, float z)
        {
            float h = Hills(x, z);
            if (float.IsNaN(_clearingHeight))
                _clearingHeight = Hills(ClearingCenter.x, ClearingCenter.y);
            float r = Vector2.Distance(new Vector2(x, z), ClearingCenter);
            float detail = (Mathf.PerlinNoise(x * 0.09f + 3.3f, z * 0.09f + 8.1f) - 0.5f) * 0.35f;
            h = Mathf.Lerp(_clearingHeight + detail, h, Smooth(13f, 34f, r));
            float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            h += Smooth(48f, 74f, edge) * 10f;
            h -= (1f - Smooth(0.5f, 2.8f, PathDistance(x, z))) * 0.12f;
            return h;
        }

        static float Hills(float x, float z)
        {
            float n1 = Mathf.PerlinNoise(x * 0.016f + 31.7f, z * 0.016f + 12.3f);
            float n2 = Mathf.PerlinNoise(x * 0.045f + 5.1f, z * 0.045f + 71.9f);
            return (n1 - 0.5f) * 10f + (n2 - 0.5f) * 2.4f;
        }

        public static float PathDistance(float x, float z)
        {
            var p = new Vector2(x, z);
            float best = float.MaxValue;
            for (int i = 0; i < Path.Length - 1; i++)
            {
                Vector2 a = Path[i];
                Vector2 b = Path[i + 1];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        /// <summary>0 = bare dirt, 1 = grass.</summary>
        static float GrassCover(float x, float z)
        {
            float wobble = (Mathf.PerlinNoise(x * 0.35f, z * 0.35f) - 0.5f) * 0.9f;
            float path = Smooth(1.1f, 2.3f, PathDistance(x, z) + wobble);
            float camp = Smooth(2.2f, 3.4f, Vector2.Distance(new Vector2(x, z), CampfireSpot) + wobble);
            return Mathf.Min(path, camp);
        }

        public static void Build(out Mesh mesh, out GroundData data)
        {
            Folders.Ensure(Folder);
            _clearingHeight = float.NaN;
            int n = Resolution;
            float step = Size / (n - 1);
            float half = Size * 0.5f;
            var vertices = new Vector3[n * n];
            var heights = new float[n * n];
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    float wx = -half + x * step;
                    float wz = -half + z * step;
                    float h = Height(wx, wz);
                    heights[z * n + x] = h;
                    vertices[z * n + x] = new Vector3(wx, h, wz);
                }
            }

            var triangles = new int[(n - 1) * (n - 1) * 6];
            int t = 0;
            for (int z = 0; z < n - 1; z++)
            {
                for (int x = 0; x < n - 1; x++)
                {
                    int i = z * n + x;
                    triangles[t++] = i;
                    triangles[t++] = i + n;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + n;
                    triangles[t++] = i + n + 1;
                }
            }

            mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            bool created = mesh == null;
            if (created)
                mesh = new Mesh();
            mesh.Clear();
            mesh.name = "Ground";
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            Vector3[] normals = mesh.normals;

            var colors = new Color[n * n];
            var grass = new byte[n * n];
            for (int i = 0; i < vertices.Length; i++)
            {
                float wx = vertices[i].x;
                float wz = vertices[i].z;
                float cover = GrassCover(wx, wz);
                float slope = Smooth(0.93f, 0.8f, normals[i].y);
                float patches = Mathf.PerlinNoise(wx * 0.07f + 11f, wz * 0.07f + 4f);
                float dry = Smooth(0.6f, 0.75f, Mathf.PerlinNoise(wx * 0.025f + 40f, wz * 0.025f + 9f));
                Color g = Color.Lerp(GrassA, GrassB, Smooth(0.25f, 0.75f, patches));
                g = Color.Lerp(g, GrassDry, dry * 0.7f);
                g = Color.Lerp(g, Slope, slope * 0.7f);
                Color d = Color.Lerp(DirtDark, Dirt, Mathf.PerlinNoise(wx * 0.4f, wz * 0.4f));
                Color c = Color.Lerp(d, g, cover);
                c.a = 0f;
                colors[i] = c;
                float grassAmount = Smooth(0.55f, 0.95f, cover) * (1f - slope * 0.6f) * Mathf.Lerp(0.55f, 1f, patches);
                grass[i] = (byte)Mathf.RoundToInt(Mathf.Clamp01(grassAmount) * 255f);
            }
            mesh.colors = colors;
            mesh.RecalculateBounds();
            if (created)
                AssetDatabase.CreateAsset(mesh, MeshPath);
            EditorUtility.SetDirty(mesh);

            data = AssetDatabase.LoadAssetAtPath<GroundData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<GroundData>();
                AssetDatabase.CreateAsset(data, DataPath);
            }
            data.Set(Size, n, heights, grass);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        public static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
