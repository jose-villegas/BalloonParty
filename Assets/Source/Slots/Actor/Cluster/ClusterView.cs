#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Cluster
{
    /// <summary>
    /// Abstract single-instance renderer for slot clusters. Drives a procedural
    /// shader via <see cref="MaterialPropertyBlock"/> with slot center positions,
    /// slot count, and a time offset for animation. Subclasses override
    /// <see cref="OnConfigured"/> to push additional shader properties.
    ///
    /// Expects the shader to declare <c>_SlotCentersWorld</c> (Vector4[]),
    /// <c>_SlotCount</c> (int), and <c>_TimeOffset</c> (float).
    ///
    /// <c>[ExecuteAlways]</c> keeps animation running in edit mode.
    /// </summary>
    [ExecuteAlways]
    internal abstract class ClusterView : MonoBehaviour
    {
        private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");
        private static readonly int SlotCentersWorldId = Shader.PropertyToID("_SlotCentersWorld");
        private static readonly int SlotCentersLocalId = Shader.PropertyToID("_SlotCentersLocal");
        private static readonly int SlotCountId = Shader.PropertyToID("_SlotCount");
        private static readonly int AnimationSpeedId = Shader.PropertyToID("_AnimationSpeed");

        [SerializeField] private SpriteRenderer _renderer;

        [Header("Animation")]
        [Tooltip("Noise scroll speed multiplier. Drives _TimeOffset on the shader.")]
        [SerializeField] private float _animationSpeed = 0.8f;

        private readonly Vector4[] _slotCenters = new Vector4[16];

        // Same slot centers as _slotCenters but relative to the quad's own origin (bounds center),
        // so the shader can compare them against object-relative fragment positions and the whole
        // cloud follows this transform (and any parent) — see PuffCloud.shader's _SlotCentersLocal.
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
        // Edit-mode only. Built-in _Time is frozen when not playing, so drive animation
        // previews here: force repaints (for DrawMesh-based views like BushView) and feed
        // editor time to the property block (for SpriteRenderer-based views like clouds).
        // At runtime the shader derives its own clock from _Time.y * _AnimationSpeed, so
        // there is no per-frame property-block push — this method compiles out of builds.
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
            // Zero the shader's built-in _Time clock and drive purely from editor time:
            // _Time advances under the forced repaints above, so leaving _AnimationSpeed
            // non-zero here would stack the two clocks and animate at double speed.
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
        /// Configures the cluster view with all slot positions. Each entry carries
        /// world position in .xy and a per-cluster noise seed in .z. The quad is
        /// sized to the combined bounding box.
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
        }

        /// <summary>
        /// Called once after <see cref="Configure"/>. Override to push
        /// subclass-specific shader properties (e.g. slot radius, jitter).
        /// </summary>
        protected virtual void OnConfigured(MaterialPropertyBlock block)
        {
        }

        /// <summary>
        /// Called during edit-mode animation preview after the time offset is set.
        /// Runtime animation is shader-driven (<c>_Time</c>), so this is not invoked
        /// per-frame in builds — push runtime per-frame properties from a subclass
        /// <c>Update</c> / <c>LateUpdate</c> instead.
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
            _block.SetInt(SlotCountId, _slotCount);
            // Pushed once here so the shader's runtime _Time.y * _AnimationSpeed clock
            // needs no per-frame update.
            _block.SetFloat(AnimationSpeedId, _animationSpeed);
            // Cleared so a view reused from an edit-mode preview can't carry a stale
            // offset into runtime, where the clock is purely _Time.y * _AnimationSpeed.
            _block.SetFloat(TimeOffsetId, 0f);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
