#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Cluster
{
    /// <summary>
    /// Expects the shader to declare <c>_SlotCentersWorld</c> (Vector4[]), <c>_SlotCount</c> (int), and <c>_TimeOffset</c> (float).
    /// </summary>
    [ExecuteAlways]
    internal abstract class ClusterView : MonoBehaviour
    {
        private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");
        private static readonly int SlotCentersWorldId = Shader.PropertyToID("_SlotCentersWorld");
        private static readonly int SlotCentersLocalId = Shader.PropertyToID("_SlotCentersLocal");
        private static readonly int RestOriginId = Shader.PropertyToID("_RestOrigin");
        private static readonly int SlotCountId = Shader.PropertyToID("_SlotCount");
        private static readonly int AnimationSpeedId = Shader.PropertyToID("_AnimationSpeed");

        [SerializeField] private SpriteRenderer _renderer;

        [Header("Animation")]
        [Tooltip("Noise scroll speed multiplier. Drives _TimeOffset on the shader.")]
        [SerializeField] private float _animationSpeed = 0.8f;

        private readonly Vector4[] _slotCenters = new Vector4[16];

        // _slotCenters relative to the quad's own origin, so the whole cloud follows this transform.
        private readonly Vector4[] _slotCentersLocal = new Vector4[16];

        private MaterialPropertyBlock _block;
        private bool _configured;
        private int _slotCount;
        private Vector2 _configuredCenter;

        internal SpriteRenderer Renderer => _renderer;
        protected IReadOnlyList<Vector4> SlotCentersBuffer => _slotCenters;
        protected int SlotCount => _slotCount;

        private void Awake()
        {
            EnsureBlock();
        }

        private void OnEnable()
        {
            if (!_configured)
            {
                PushSlotCentersDefault();
            }
        }

#if UNITY_EDITOR
        // _Time is frozen in edit mode, so drive the preview clock here instead.
        private void Update()
        {
            if (Application.isPlaying)
            {
                return;
            }

            SceneView.RepaintAll();
            EnsureBlock();

            if (_renderer == null || _block == null || !_renderer.enabled)
            {
                return;
            }

            _renderer.GetPropertyBlock(_block);
            // Zero _AnimationSpeed here or the forced repaints double up with editor time.
            _block.SetFloat(AnimationSpeedId, 0f);
            _block.SetFloat(TimeOffsetId, (float)EditorApplication.timeSinceStartup * _animationSpeed);
            OnUpdateBlock(_block);
            _renderer.SetPropertyBlock(_block);
        }
#endif

        private void OnValidate()
        {
            EnsureBlock();
            if (!_configured)
            {
                PushSlotCentersDefault();
            }
        }

        /// <summary>
        /// Configures the cluster view with all slot positions; the quad is sized to the combined bounding box.
        /// </summary>
        internal void Configure(Vector4[] allSlotPositions, int count, Rect combinedBounds, IClusterViewSettings settings)
        {
            _configured = true;
            _animationSpeed = settings.AnimationSpeed;
            _slotCount = Mathf.Min(count, _slotCenters.Length);

            for (var i = 0; i < _slotCount; i++)
            {
                _slotCenters[i] = allSlotPositions[i];
            }

            var padding = settings.Padding;
            var center = combinedBounds.center;
            _configuredCenter = center;
            transform.position = new Vector3(center.x, center.y, transform.position.z);

            var scaleX = combinedBounds.width + padding * 2f;
            var scaleY = combinedBounds.height + padding * 2f;
            transform.localScale = new Vector3(scaleX, scaleY, 1f);

            PushSlotCentersConfigured();
            OnConfigured(_block);
        }

        internal void Clear()
        {
            _configured = false;
            _slotCount = 0;

            if (_renderer != null)
            {
                _renderer.enabled = false;
            }

            OnCleared();
        }

        /// <summary>
        /// Override to push subclass-specific shader properties (e.g. slot radius, jitter).
        /// </summary>
        protected virtual void OnConfigured(MaterialPropertyBlock block)
        {
        }

        /// <summary>
        /// Override to stop any draw calls not gated by the shared <see cref="_renderer"/> (e.g. <c>Graphics.DrawMesh</c>).
        /// </summary>
        protected virtual void OnCleared()
        {
        }

        /// <summary>
        /// Edit-mode preview only; runtime animation is shader-driven, so this isn't invoked per-frame in builds.
        /// </summary>
        protected virtual void OnUpdateBlock(MaterialPropertyBlock block)
        {
        }

        private void EnsureBlock()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }

            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }
        }

        private void PushSlotCentersDefault()
        {
            EnsureBlock();

            if (_renderer == null || _block == null)
            {
                return;
            }

            var pos = transform.position;
            _slotCenters[0] = new Vector4(pos.x, pos.y, 0f, 0f);
            _slotCentersLocal[0] = Vector4.zero;

            _renderer.GetPropertyBlock(_block);
            _block.SetVectorArray(SlotCentersWorldId, _slotCenters);
            _block.SetVectorArray(SlotCentersLocalId, _slotCentersLocal);
            _block.SetVector(RestOriginId, new Vector4(pos.x, pos.y, 0f, 0f));
            _block.SetInt(SlotCountId, 1);
            _renderer.SetPropertyBlock(_block);
        }

        private void PushSlotCentersConfigured()
        {
            EnsureBlock();

            if (_renderer == null || _block == null)
            {
                return;
            }

            for (var i = 0; i < _slotCount; i++)
            {
                var c = _slotCenters[i];
                _slotCentersLocal[i] = new Vector4(c.x - _configuredCenter.x, c.y - _configuredCenter.y, c.z, c.w);
            }

            _renderer.enabled = true;
            _renderer.GetPropertyBlock(_block);
            _block.SetVectorArray(SlotCentersWorldId, _slotCenters);
            _block.SetVectorArray(SlotCentersLocalId, _slotCentersLocal);
            // Rest-frame origin, so the field still samples correctly while the Ascent lifts the cloud.
            _block.SetVector(RestOriginId, new Vector4(_configuredCenter.x, _configuredCenter.y, 0f, 0f));
            _block.SetInt(SlotCountId, _slotCount);
            _block.SetFloat(AnimationSpeedId, _animationSpeed);
            // Cleared so a reused edit-mode preview doesn't carry a stale offset into runtime.
            _block.SetFloat(TimeOffsetId, 0f);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
