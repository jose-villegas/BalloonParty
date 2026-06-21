#if UNITY_EDITOR
using UnityEditor;
#endif
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Shared.Extensions;
using BalloonParty.Slots.Capabilities;
using UniRx;
using UnityEngine;

namespace BalloonParty.Balloon.Type
{
    /// <summary>
    /// Balloon variant for <c>BalloonType.BubbleCluster</c>.
    /// Drives <c>_BubbleCount</c> and <c>_TimeOffset</c> on the
    /// <c>BalloonParty/Balloon/SoapBubbleCluster</c> shader via
    /// <see cref="MaterialPropertyBlock"/>.
    ///
    /// <c>[ExecuteAlways]</c> keeps the shader animation running in edit mode
    /// without entering Play mode.  Adjust <c>_previewBubbleCount</c> in the
    /// Inspector to preview each cluster state.
    /// </summary>
    [ExecuteAlways]
    public class SoapBubbleClusterVariant : MonoBehaviour, IBalloonVariant, IBalloonViewBinding
    {
        private static readonly int BubbleCountId = Shader.PropertyToID("_BubbleCount");
        private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");
        private static readonly int RotationId = Shader.PropertyToID("_Rotation");

        [SerializeField] private SpriteRenderer _renderer;

        [Tooltip("Maximum visible bubbles (1–5). Should match HitsToPop in the config entry.")]
        [SerializeField] private int _maxBubbles = 5;

        [Tooltip("Animation speed. Owned here so edit-mode animation " +
                 "works without relying on the frozen _Time.y shader global.")]
        [SerializeField] private float _floatSpeed = 0.8f;

        [Header("Editor Preview")]
        [Tooltip("Drag to preview each cluster state without entering Play mode.")]
        [SerializeField] private int _previewBubbleCount = 5;

        private MaterialPropertyBlock _block;
        private float _instancePhase;
        private float _rotationAngle;
        private float _rotationSpeedRad;
        private float _lastTime = -1f;

        private void Awake()
        {
            EnsureBlock();
            _instancePhase = Random.value * 100f;
        }

        private void Update()
        {
            EnsureBlock();

            // Default to real time; editor path overrides below.
            var currentTime = Time.time;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                currentTime = (float)EditorApplication.timeSinceStartup;
                SceneView.RepaintAll();
            }
#endif

            // Compute delta manually so the same path works in both edit
            // and play mode.  Guard against first-frame and scene-load spikes.
            if (_lastTime >= 0f)
            {
                var delta = currentTime - _lastTime;
                if (delta > 0f && delta < 0.5f)
                {
                    _rotationAngle += _rotationSpeedRad * delta;
                }
            }

            _lastTime = currentTime;
            PushTime(currentTime);
            _renderer.transform.localRotation = Quaternion.identity;
        }

        private void OnValidate()
        {
            EnsureBlock();
            PushBubbleCount(Mathf.Clamp(_previewBubbleCount, 1, _maxBubbles));
        }

        public void Initialize(IWriteableBalloonModel model) { }

        public void Bind(IBalloonModel model, CompositeDisposable disposables)
        {
            if (_renderer == null)
            {
                Debug.LogError(
                    $"SoapBubbleClusterVariant.Bind: _renderer is not assigned on \"{gameObject.name}\" " +
                    "— bubble count visuals will be disabled. Fix the prefab.",
                    this);
                return;
            }

            if (model is not IHasDurability durable)
            {
                return;
            }

            _rotationAngle = Random.Range(0f, Mathf.PI * 2f);
            _rotationSpeedRad = Random.Range(5f, 12f) * Mathf.Deg2Rad
                                                      * (Random.value < 0.5f ? 1f : -1f);
            _lastTime = -1f;

            PushBubbleCount(Mathf.Clamp(durable.HitsRemaining.Value, 1, _maxBubbles));

            durable.HitsRemaining
                .Subscribe(hits => PushBubbleCount(Mathf.Clamp(hits, 1, _maxBubbles)))
                .AddTo(disposables);
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

        private void PushBubbleCount(int count)
        {
            if (_renderer == null || _block == null)
            {
                return;
            }

            _renderer.SetFloatAndApply(_block, BubbleCountId, count);
        }

        private void PushTime(float currentTime)
        {
            if (_renderer == null || _block == null)
            {
                return;
            }

            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(TimeOffsetId, (currentTime * _floatSpeed) + _instancePhase);
            _block.SetFloat(RotationId, _rotationAngle);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
