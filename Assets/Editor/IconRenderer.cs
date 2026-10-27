using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Zombie.EditorTools
{
    /// <summary>
    /// Renders item icons from the real models with the scene's lighting, so the hotbar matches the
    /// world. Transparency comes from difference matting (one render on black, one on white).
    /// </summary>
    static class IconRenderer
    {
        public const string Folder = "Assets/Art/UI/Icons";
        const int IconLayer = 31;
        const int Size = 256;
        static readonly Vector3 Origin = new Vector3(0f, 400f, 0f);

        public static Sprite Render(string name, Mesh mesh, Material material, Vector3 euler)
        {
            Folders.Ensure(Folder);
            bool async = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            Light sun = RenderSettings.sun;
            float sunIntensity = sun != null ? sun.intensity : 0f;
            if (sun != null)
                sun.intensity = sunIntensity * 0.8f;
            var holder = new GameObject("IconSubject");
            holder.layer = IconLayer;
            holder.transform.position = Origin;
            var subject = new GameObject("Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            subject.layer = IconLayer;
            subject.transform.SetParent(holder.transform, false);
            subject.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = subject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            subject.transform.localPosition = -mesh.bounds.center;
            holder.transform.rotation = Quaternion.Euler(euler);

            Bounds b = renderer.bounds;
            float radius = b.extents.magnitude;
            var camGo = new GameObject("IconCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.cullingMask = 1 << IconLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.fieldOfView = 22f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            Vector3 dir = new Vector3(0f, 0.25f, -1f).normalized;
            float distance = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
            camGo.transform.position = b.center + dir * distance;
            camGo.transform.LookAt(b.center);

            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            Color[] onBlack = Capture(cam, rt, Color.black);
            Color[] onWhite = Capture(cam, rt, Color.white);

            var result = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color[Size * Size];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color a = onBlack[i];
                Color w = onWhite[i];
                float alpha = Mathf.Clamp01(1f - ((w.r - a.r) + (w.g - a.g) + (w.b - a.b)) / 3f);
                Color c = alpha > 0.001f ? new Color(a.r / alpha, a.g / alpha, a.b / alpha, alpha) : new Color(0f, 0f, 0f, 0f);
                pixels[i] = c;
            }
            result.SetPixels(pixels);
            result.Apply();
            string path = Folder + "/Icon_" + name + ".png";
            File.WriteAllBytes(path, result.EncodeToPNG());

            Object.DestroyImmediate(result);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(holder);
            if (sun != null)
                sun.intensity = sunIntensity;
            ShaderUtil.allowAsyncCompilation = async;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Color[] Capture(Camera cam, RenderTexture rt, Color background)
        {
            cam.backgroundColor = new Color(background.r, background.g, background.b, 0f);
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request))
                RenderPipeline.SubmitRenderRequest(cam, request);
            else
            {
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
            }
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            Color[] pixels = tex.GetPixels();
            Object.DestroyImmediate(tex);
            return pixels;
        }
    }
}
