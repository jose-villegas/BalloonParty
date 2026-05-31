#if UNITY_EDITOR
using UnityEditor;
#endif
using BalloonParty.Configuration;
using BalloonParty.Shared.Pool;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Drives the <c>BalloonParty/Grid/PuffCloud</c> shader on a
    /// <see cref="SpriteRenderer"/> quad with no assigned sprite.
    /// Pushes <c>_TimeOffset</c> and slot-center data via
    /// <see cref="MaterialPropertyBlock"/> each frame. The shared disturbance
    /// field (<c>_DisturbanceTex</c>, <c>_FieldBoundsMin</c>,
    /// <c>_FieldBoundsSize</c>) is set as global shader properties by
    /// <see cref="BalloonParty.Shared.Disturbance.DisturbanceFieldService"/>.
    ///
    /// <c>[ExecuteAlways]</c> keeps the cloud animation running in edit mode.
    /// Supports both standalone mode (serialized fields) and configured mode
    /// (driven by <see cref="PuffCloudViewController"/> with a
    /// <see cref="PuffCloudSettings"/> SO).
    /// </summary>
    [ExecuteAlways]
    internal class PuffCloudView : MonoBehaviour, IPoolable
    {
        private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");
        private static readonly int SlotCentersWorldId = Shader.PropertyToID("_SlotCentersWorld");
        private static readonly int SlotCountId = Shader.PropertyToID("_SlotCount");

        [SerializeField] private SpriteRenderer _renderer;

        [Header("Animation")]
        [Tooltip("Noise scroll speed multiplier. Drives _TimeOffset on the shader.")]
        [SerializeField] private float _animationSpeed = 0.8f;

        private readonly Vector4[] _slotCenters = new Vector4[16];

        private MaterialPropertyBlock _block;
        private float _instancePhase;
        private bool _configured;
        private int _slotCount;
        private Rect _worldBounds;

        internal SpriteRenderer Renderer => _renderer;

        private void Awake()
        {
            EnsureBlock();
            _instancePhase = Random.value * 100f;
        }

        private void OnEnable()
        {
            if (!_configured)
            {
                PushSlotCentersDefault();
            }
        }

        private void Update()
        {
            EnsureBlock();

            if (_renderer == null || _block == null)
            {
                return;
            }

            var currentTime = Time.time;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                currentTime = (float)EditorApplication.timeSinceStartup;
                SceneView.RepaintAll();
            }
#endif

            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(TimeOffsetId, (currentTime * _animationSpeed) + _instancePhase);
            _renderer.SetPropertyBlock(_block);
        }

        private void OnValidate()
        {
            EnsureBlock();
            if (!_configured)
            {
                PushSlotCentersDefault();
            }
        }

        public void OnSpawned()
        {
            _instancePhase = Random.value * 100f;
        }

        public void OnDespawned()
        {
            _configured = false;
            _slotCount = 0;
        }

        /// <summary>
        /// Configures the cloud view to render a cluster spanning the given
        /// slot world positions and bounding box. Called by
        /// <see cref="PuffCloudViewController"/> when a cluster is created or resized.
        /// </summary>
        internal void Configure(Vector3[] slotWorldPositions, Rect worldBounds, PuffCloudSettings settings)
        {
            _configured = true;

            _animationSpeed = settings.AnimationSpeed;

            _worldBounds = worldBounds;
            _slotCount = Mathf.Min(slotWorldPositions.Length, _slotCenters.Length);

            for (var i = 0; i < _slotCount; i++)
            {
                var pos = slotWorldPositions[i];
                _slotCenters[i] = new Vector4(pos.x, pos.y, 0f, 0f);
            }

            var padding = settings.Padding;
            var center = worldBounds.center;
            transform.position = new Vector3(center.x, center.y, transform.position.z);

            var scaleX = worldBounds.width + padding * 2f;
            var scaleY = worldBounds.height + padding * 2f;
            transform.localScale = new Vector3(scaleX, scaleY, 1f);

            PushSlotCentersConfigured();
        }

        /// <summary>
        /// Returns the cluster world bounds used for falloff mapping.
        /// Falls back to renderer bounds in unconfigured (standalone) mode.
        /// </summary>
        internal Rect GetWorldBounds()
        {
            if (_configured)
            {
                return _worldBounds;
            }

            if (_renderer != null)
            {
                var b = _renderer.bounds;
                return new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
            }

            return new Rect(transform.position.x - 0.5f, transform.position.y - 0.5f, 1f, 1f);
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

            _renderer.GetPropertyBlock(_block);
            _block.SetVectorArray(SlotCentersWorldId, _slotCenters);
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

            _renderer.GetPropertyBlock(_block);
            _block.SetVectorArray(SlotCentersWorldId, _slotCenters);
            _block.SetInt(SlotCountId, _slotCount);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
