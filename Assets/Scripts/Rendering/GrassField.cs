using UnityEngine;
using UnityEngine.Rendering;
using Zombie.World;

namespace Zombie.Rendering
{
    /// <summary>
    /// Dense stylized grass around the camera, drawn with GPU instancing in square chunks.
    /// Instances are generated once from GroundData; each frame only visible chunks near the
    /// camera are submitted. No allocations after Awake. The shader shrinks blades towards the
    /// fade distance so the edge of the field is invisible.
    /// </summary>
    public sealed class GrassField : MonoBehaviour
    {
        const int MaxPerChunk = 1023;

        [SerializeField] GroundData _ground;
        [SerializeField] Mesh _mesh;
        [SerializeField] Material _material;
        [SerializeField] float _chunkSize = 8f;
        [SerializeField] float _density = 3.2f;
        [SerializeField] float _drawRadius = 34f;
        [SerializeField] Vector2 _scaleRange = new Vector2(0.6f, 1.1f);
        [SerializeField] int _seed = 7;

        Matrix4x4[][] _matrices;
        int[] _counts;
        Bounds[] _bounds;
        RenderParams[] _params;
        readonly Plane[] _frustum = new Plane[6];
        Camera _camera;

        void Awake()
        {
            Build();
        }

        void Build()
        {
            if (_ground == null || _mesh == null || _material == null)
                return;

            int chunksPerSide = Mathf.CeilToInt(_ground.Size / _chunkSize);
            int total = chunksPerSide * chunksPerSide;
            _matrices = new Matrix4x4[total][];
            _counts = new int[total];
            _bounds = new Bounds[total];
            _params = new RenderParams[total];

            var random = new System.Random(_seed);
            int perChunk = Mathf.Min(MaxPerChunk, Mathf.RoundToInt(_chunkSize * _chunkSize * _density));
            var buffer = new Matrix4x4[MaxPerChunk];
            float half = _ground.HalfSize;

            for (int cz = 0; cz < chunksPerSide; cz++)
            {
                for (int cx = 0; cx < chunksPerSide; cx++)
                {
                    int index = cz * chunksPerSide + cx;
                    float x0 = -half + cx * _chunkSize;
                    float z0 = -half + cz * _chunkSize;
                    int count = 0;
                    float minY = float.MaxValue;
                    float maxY = float.MinValue;
                    for (int i = 0; i < perChunk; i++)
                    {
                        float x = x0 + (float)random.NextDouble() * _chunkSize;
                        float z = z0 + (float)random.NextDouble() * _chunkSize;
                        float mask = _ground.SampleGrass(x, z);
                        if ((float)random.NextDouble() > mask)
                            continue;
                        float y = _ground.SampleHeight(x, z) - 0.02f;
                        float scale = Mathf.Lerp(_scaleRange.x, _scaleRange.y, (float)random.NextDouble()) * Mathf.Lerp(0.7f, 1f, mask);
                        var rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                        buffer[count++] = Matrix4x4.TRS(new Vector3(x, y, z), rotation, new Vector3(scale, scale, scale));
                        minY = Mathf.Min(minY, y);
                        maxY = Mathf.Max(maxY, y);
                    }

                    _counts[index] = count;
                    if (count == 0)
                        continue;
                    _matrices[index] = new Matrix4x4[count];
                    System.Array.Copy(buffer, _matrices[index], count);
                    var center = new Vector3(x0 + _chunkSize * 0.5f, (minY + maxY) * 0.5f + 0.4f, z0 + _chunkSize * 0.5f);
                    _bounds[index] = new Bounds(center, new Vector3(_chunkSize + 1f, maxY - minY + 1.5f, _chunkSize + 1f));
                    _params[index] = new RenderParams(_material)
                    {
                        worldBounds = _bounds[index],
                        shadowCastingMode = ShadowCastingMode.Off,
                        receiveShadows = true,
                        layer = gameObject.layer,
                    };
                }
            }
        }

        void LateUpdate()
        {
            if (_matrices == null)
                return;
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                return;

            GeometryUtility.CalculateFrustumPlanes(_camera, _frustum);
            Vector3 cam = _camera.transform.position;
            float reach = _drawRadius + _chunkSize * 0.75f;
            float reachSq = reach * reach;

            for (int i = 0; i < _counts.Length; i++)
            {
                if (_counts[i] == 0)
                    continue;
                Vector3 c = _bounds[i].center;
                float dx = c.x - cam.x;
                float dz = c.z - cam.z;
                if (dx * dx + dz * dz > reachSq)
                    continue;
                if (!GeometryUtility.TestPlanesAABB(_frustum, _bounds[i]))
                    continue;
                Graphics.RenderMeshInstanced(_params[i], _mesh, 0, _matrices[i], _counts[i]);
            }
        }
    }
}
