#if UNITY_EDITOR
using UnityEditor;
#endif
using BalloonParty.Configuration;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Single-instance cloud renderer for all Puff clusters. Drives the
    /// <c>BalloonParty/Grid/PuffCloud</c> shader on one <see cref="SpriteRenderer"/>
    /// quad sized to cover every occupied Puff slot. All slot centers are passed
    /// in a single array — the shader's world-space noise and slot-center falloff
    /// produce continuous cloud coverage across all clusters from one draw call.
    ///
    /// The shared disturbance field (<c>_DisturbanceTex</c>, <c>_FieldBoundsMin</c>,
    /// <c>_FieldBoundsSize</c>) is set as global shader properties by
    /// <see cref="BalloonParty.Shared.Disturbance.DisturbanceFieldService"/>.
    ///
    /// <c>[ExecuteAlways]</c> keeps the cloud animation running in edit mode.
    /// </summary>
    [ExecuteAlways]
    internal class PuffCloudView : MonoBehaviour
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
        private bool _configured;
        private int _slotCount;

        internal SpriteRenderer Renderer => _renderer;

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
            _block.SetFloat(TimeOffsetId, currentTime * _animationSpeed);
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

        /// <summary>
        /// Configures the cloud view with all Puff slot positions across every
        /// cluster. Each entry carries world position in .xy and a per-cluster
        /// noise seed in .z so adjacent clusters look distinct. The quad is
        /// sized to the combined bounding box. Called by
        /// <see cref="PuffCloudViewController"/> whenever any cluster changes.
        /// </summary>
        internal void Configure(Vector4[] allSlotPositions, int count, Rect combinedBounds, IPuffCloudSettings settings)
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
            transform.position = new Vector3(center.x, center.y, transform.position.z);

            var scaleX = combinedBounds.width + padding * 2f;
            var scaleY = combinedBounds.height + padding * 2f;
            transform.localScale = new Vector3(scaleX, scaleY, 1f);

            PushSlotCentersConfigured();
        }

        /// <summary>
        /// Hides the cloud quad when no Puff slots exist.
        /// </summary>
        internal void Clear()
        {
            _configured = false;
            _slotCount = 0;

            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
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

            _renderer.enabled = true;
            _renderer.GetPropertyBlock(_block);
            _block.SetVectorArray(SlotCentersWorldId, _slotCenters);
            _block.SetInt(SlotCountId, _slotCount);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
