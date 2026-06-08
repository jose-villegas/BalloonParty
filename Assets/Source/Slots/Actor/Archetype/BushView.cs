using BalloonParty.Configuration;
using BalloonParty.Slots.Actor.Cluster;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Cluster renderer for bush obstacles. Renders a static branch quad from
    /// a baked branch map texture and instanced leaf quads via DrawMeshInstanced
    /// from pre-extracted <see cref="BushVariantData"/>.
    /// </summary>
    internal class BushView : ClusterView
    {
        private static Mesh _sharedLeafQuad;
        private static Mesh _sharedBranchQuad;
        private static readonly int LeafTintId = Shader.PropertyToID("_LeafTint");
        private static readonly int UVRectId = Shader.PropertyToID("_UVRect");

        private IBushSettings _settings;
        private BushVariantData _variantData;
        private Material _branchMaterial;
        private Material _leafMaterial;
        private MaterialPropertyBlock _leafProps;
        private Matrix4x4 _branchMatrix;
        private Matrix4x4[] _leafMatrices;
        private int _leafCount;

        internal BushVariantData VariantData => _variantData;
        internal Matrix4x4[] LeafMatrices => _leafMatrices;
        internal int LeafCount => _leafCount;

        internal void SetSettings(IBushSettings settings)
        {
            _settings = settings;
        }

        internal void SetVariantData(BushVariantData data)
        {
            _variantData = data;
        }

        protected override void OnConfigured(MaterialPropertyBlock block)
        {
            transform.localScale = Vector3.one;

            if (Renderer != null)
            {
                Renderer.enabled = false;
            }

            ConfigureBranchQuad();
            ConfigureLeafMatrices();
        }

        private void LateUpdate()
        {
            if (_branchMaterial != null)
            {
                Graphics.DrawMesh(
                    GetBranchQuadMesh(),
                    _branchMatrix,
                    _branchMaterial,
                    gameObject.layer);
            }

            if (_leafCount > 0 && _leafMaterial != null)
            {
                Graphics.DrawMeshInstanced(
                    GetLeafQuadMesh(),
                    0,
                    _leafMaterial,
                    _leafMatrices,
                    _leafCount,
                    _leafProps);
            }
        }

        private void ConfigureBranchQuad()
        {
            if (_variantData == null || _settings == null)
            {
                return;
            }

            if (_settings.BranchShader == null)
            {
                return;
            }

            var tex = _variantData.BranchMap;
            _branchMaterial = new Material(_settings.BranchShader)
            {
                mainTexture = tex,
                renderQueue = 3000
            };

            var size = _settings.BushWorldSize;
            _branchMatrix = Matrix4x4.TRS(
                transform.position,
                Quaternion.identity,
                new Vector3(size, size, 1f));
        }

        private void ConfigureLeafMatrices()
        {
            if (_variantData == null || _settings == null)
            {
                _leafCount = 0;
                return;
            }

            var sprites = _settings.LeafAtlasSprites;
            if (_settings.LeafShader == null || sprites == null || sprites.Length == 0)
            {
                _leafCount = 0;
                return;
            }

            _leafMaterial = new Material(_settings.LeafShader)
            {
                mainTexture = sprites[0].texture,
                enableInstancing = true,
                renderQueue = 3001
            };

            var slots = _variantData.LeafSlots;
            _leafCount = slots.Count;
            _leafMatrices = new Matrix4x4[_leafCount];

            var tints = new Vector4[_leafCount];
            var uvRects = new Vector4[_leafCount];
            var worldOffset = (Vector2)transform.position;

            for (var i = 0; i < _leafCount; i++)
            {
                var slot = slots[i];
                var worldPos = worldOffset + slot.Position;
                var angleDeg = slot.BaseAngle * Mathf.Rad2Deg - 90f;
                _leafMatrices[i] = Matrix4x4.TRS(
                    new Vector3(worldPos.x, worldPos.y, 0f),
                    Quaternion.Euler(0f, 0f, angleDeg),
                    Vector3.one * slot.Scale);

                var tint = (Color)slot.Tint;
                tints[i] = new Vector4(tint.r, tint.g, tint.b, tint.a);

                var spriteIndex = Mathf.Clamp(slot.SpriteVariant, 0, sprites.Length - 1);
                var rect = sprites[spriteIndex].textureRect;
                var tex = sprites[spriteIndex].texture;
                uvRects[i] = new Vector4(
                    rect.x / tex.width,
                    rect.y / tex.height,
                    rect.width / tex.width,
                    rect.height / tex.height);
            }

            _leafProps = new MaterialPropertyBlock();
            _leafProps.SetVectorArray(LeafTintId, tints);
            _leafProps.SetVectorArray(UVRectId, uvRects);
        }

        private static Mesh GetBranchQuadMesh()
        {
            if (_sharedBranchQuad != null)
            {
                return _sharedBranchQuad;
            }

            _sharedBranchQuad = new Mesh
            {
                name = "BushBranchQuad",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            _sharedBranchQuad.UploadMeshData(true);

            return _sharedBranchQuad;
        }

        private static Mesh GetLeafQuadMesh()
        {
            if (_sharedLeafQuad != null)
            {
                return _sharedLeafQuad;
            }

            _sharedLeafQuad = new Mesh
            {
                name = "BushLeafQuad",
                vertices = new[]
                {
                    new Vector3(-0.5f, 0f, 0f),
                    new Vector3(0.5f, 0f, 0f),
                    new Vector3(0.5f, 1f, 0f),
                    new Vector3(-0.5f, 1f, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            _sharedLeafQuad.UploadMeshData(true);

            return _sharedLeafQuad;
        }
    }
}
