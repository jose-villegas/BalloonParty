#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Drives the <c>BalloonParty/Grid/PuffCloud</c> shader on a
    /// <see cref="SpriteRenderer"/> quad with no assigned sprite.
    /// Pushes <c>_TimeOffset</c> and slot-center data via
    /// <see cref="MaterialPropertyBlock"/> each frame.
    ///
    /// <c>[ExecuteAlways]</c> keeps the cloud animation running in edit mode.
    /// P1 uses <c>[SerializeField]</c> fields for visual tuning; these will
    /// migrate to a <c>PuffCloudSettings</c> ScriptableObject in P3.
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
        private float _instancePhase;

        private void Awake()
        {
            EnsureBlock();
            _instancePhase = Random.value * 100f;
        }

        private void OnEnable()
        {
            PushSlotCenters();
        }

        private void Update()
        {
            EnsureBlock();

            var currentTime = Time.time;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                currentTime = (float)EditorApplication.timeSinceStartup;
                SceneView.RepaintAll();
            }
#endif

            PushTime(currentTime);
        }

        private void OnValidate()
        {
            EnsureBlock();
            PushSlotCenters();
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

        private void PushTime(float currentTime)
        {
            if (_renderer == null || _block == null)
            {
                return;
            }

            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(TimeOffsetId, (currentTime * _animationSpeed) + _instancePhase);
            _renderer.SetPropertyBlock(_block);
        }

        /// <summary>
        /// Pushes slot center positions to the shader. In P1 there is a single
        /// slot — the world position of this transform. P3 will pass multiple
        /// centers for merged clusters.
        /// </summary>
        private void PushSlotCenters()
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
    }
}
