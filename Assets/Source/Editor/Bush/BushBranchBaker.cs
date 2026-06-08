using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Editor.Bush
{
    /// <summary>
    /// Renders fractal branch segments into a branch map texture using an
    /// offscreen camera and a procedural mesh. Follows the same bake pattern
    /// as <see cref="BushLeafBaker"/>.
    /// </summary>
    internal static class BushBranchBaker
    {
        private const string ShaderName = "Hidden/BalloonParty/Grid/BushBakeBranch";
        private const int BakeLayer = 31;

        internal static Texture2D Bake(int seed, BushBranchBakeSettings settings)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[BushBranchBaker] Shader '{ShaderName}' not found.");
                return null;
            }

            var segments = BushBranchGenerator.Generate(seed, settings);
            var mesh = BuildSegmentMesh(segments);
            var material = new Material(shader);

            var rt = RenderTexture.GetTemporary(
                settings.Resolution, settings.Resolution, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;

            var cameraGo = CreateBakeCamera(rt);
            var meshGo = CreateMeshObject(mesh, material);

            cameraGo.GetComponent<Camera>().Render();

            var result = ReadbackTexture(rt, settings.Resolution);

            Object.DestroyImmediate(meshGo);
            Object.DestroyImmediate(cameraGo);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(material);
            RenderTexture.ReleaseTemporary(rt);

            return result;
        }

        private static Mesh BuildSegmentMesh(List<BushBranchGenerator.Segment> segments)
        {
            var vertCount = segments.Count * 4;
            var vertices = new Vector3[vertCount];
            var colors = new Color[vertCount];
            var uvs = new Vector2[vertCount];
            var indices = new int[segments.Count * 6];

            for (var i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                var dir = (seg.End - seg.Start).normalized;
                var perp = new Vector2(-dir.y, dir.x);

                var halfStart = seg.StartWidth * 0.5f;
                var halfEnd = seg.EndWidth * 0.5f;
                var v0 = seg.Start - perp * halfStart;
                var v1 = seg.Start + perp * halfStart;
                var v2 = seg.End - perp * halfEnd;
                var v3 = seg.End + perp * halfEnd;

                var baseIdx = i * 4;
                vertices[baseIdx + 0] = new Vector3(v0.x, v0.y, 0f);
                vertices[baseIdx + 1] = new Vector3(v1.x, v1.y, 0f);
                vertices[baseIdx + 2] = new Vector3(v2.x, v2.y, 0f);
                vertices[baseIdx + 3] = new Vector3(v3.x, v3.y, 0f);

                // Encode direction and depth in vertex color
                var dirR = Mathf.Cos(seg.DirectionAngle) * 0.5f + 0.5f;
                var dirG = Mathf.Sin(seg.DirectionAngle) * 0.5f + 0.5f;
                var depth = Mathf.Clamp01(seg.Depth);
                // Ensure trunk (depth=0) still has some alpha so it renders
                var alpha = Mathf.Lerp(0.3f, 1f, depth);
                var color = new Color(dirR, dirG, 0f, alpha);

                colors[baseIdx + 0] = color;
                colors[baseIdx + 1] = color;
                colors[baseIdx + 2] = color;
                colors[baseIdx + 3] = color;

                // UV.x: 0 at left edge, 1 at right edge (drives AA)
                uvs[baseIdx + 0] = new Vector2(0f, 0f);
                uvs[baseIdx + 1] = new Vector2(1f, 0f);
                uvs[baseIdx + 2] = new Vector2(0f, 1f);
                uvs[baseIdx + 3] = new Vector2(1f, 1f);

                var triBase = i * 6;
                indices[triBase + 0] = baseIdx + 0;
                indices[triBase + 1] = baseIdx + 2;
                indices[triBase + 2] = baseIdx + 1;
                indices[triBase + 3] = baseIdx + 1;
                indices[triBase + 4] = baseIdx + 2;
                indices[triBase + 5] = baseIdx + 3;
            }

            var mesh = new Mesh
            {
                hideFlags = HideFlags.HideAndDontSave,
                vertices = vertices,
                colors = colors,
                uv = uvs,
                triangles = indices
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static GameObject CreateBakeCamera(RenderTexture rt)
        {
            var go = new GameObject("_BranchBakeCamera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.5f;
            cam.nearClipPlane = -1f;
            cam.farClipPlane = 1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.targetTexture = rt;
            cam.cullingMask = 1 << BakeLayer;
            cam.enabled = false;

            go.transform.position = new Vector3(0.5f, 0.5f, -0.5f);

            return go;
        }

        private static GameObject CreateMeshObject(Mesh mesh, Material material)
        {
            var go = new GameObject("_BranchBakeMesh")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = BakeLayer
            };

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return go;
        }

        private static Texture2D ReadbackTexture(RenderTexture rt, int resolution)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;

            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            tex.Apply();

            RenderTexture.active = previous;
            return tex;
        }
    }
}
