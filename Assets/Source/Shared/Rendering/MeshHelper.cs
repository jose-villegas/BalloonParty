using UnityEngine;

namespace BalloonParty.Shared.Rendering
{
    internal enum QuadPivot
    {
        Center,
        Bottom
    }

    internal static class MeshHelper
    {
        private static Mesh _centerQuad;
        private static Mesh _bottomQuad;

        internal static Mesh CreateQuad(QuadPivot pivot)
        {
            return pivot switch
            {
                QuadPivot.Center => GetCenterQuad(),
                QuadPivot.Bottom => GetBottomQuad(),
                _ => GetCenterQuad()
            };
        }

        private static Mesh GetCenterQuad()
        {
            if (_centerQuad != null)
            {
                return _centerQuad;
            }

            _centerQuad = BuildQuad("CenterQuad",
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f));
            return _centerQuad;
        }

        private static Mesh GetBottomQuad()
        {
            if (_bottomQuad != null)
            {
                return _bottomQuad;
            }

            _bottomQuad = BuildQuad("BottomQuad",
                new Vector3(-0.5f, 0f, 0f),
                new Vector3(0.5f, 0f, 0f),
                new Vector3(0.5f, 1f, 0f),
                new Vector3(-0.5f, 1f, 0f));
            return _bottomQuad;
        }

        private static Mesh BuildQuad(string name, Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl)
        {
            var mesh = new Mesh
            {
                name = name,
                vertices = new[] { bl, br, tr, tl },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}

