using UnityEngine;

namespace Zombie.World
{
    /// <summary>
    /// Baked height and grass-density grid of the ground mesh (written by the editor scene builder).
    /// Used at runtime to scatter grass without raycasts.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombie/Ground Data")]
    public sealed class GroundData : ScriptableObject
    {
        [SerializeField] float _size = 150f;
        [SerializeField] int _resolution = 151;
        [SerializeField] float[] _heights;
        [SerializeField] byte[] _grass;

        public float Size => _size;
        public float HalfSize => _size * 0.5f;

        public void Set(float size, int resolution, float[] heights, byte[] grass)
        {
            _size = size;
            _resolution = resolution;
            _heights = heights;
            _grass = grass;
        }

        public float SampleHeight(float x, float z) => Sample(_heights, x, z, 1f);

        public float SampleGrass(float x, float z) => Sample(null, x, z, 1f / 255f);

        float Sample(float[] values, float x, float z, float scale)
        {
            float step = _size / (_resolution - 1);
            float fx = Mathf.Clamp((x + HalfSize) / step, 0f, _resolution - 1.001f);
            float fz = Mathf.Clamp((z + HalfSize) / step, 0f, _resolution - 1.001f);
            int ix = (int)fx;
            int iz = (int)fz;
            float tx = fx - ix;
            float tz = fz - iz;
            float a = Get(values, ix, iz);
            float b = Get(values, ix + 1, iz);
            float c = Get(values, ix, iz + 1);
            float d = Get(values, ix + 1, iz + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz) * scale;
        }

        float Get(float[] values, int x, int z)
        {
            int i = z * _resolution + x;
            return values != null ? values[i] : _grass[i];
        }
    }
}
