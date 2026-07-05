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
    /// Drives <c>_BubbleCount</c> and the float/spin clock of the
    /// <c>BalloonParty/Balloon/SoapBubbleCluster</c> shader via
    /// <see cref="MaterialPropertyBlock"/> — phase and speeds are pushed once at
    /// <c>Bind</c>; the shader self-derives the animation from <c>_Time.y</c>, so
    /// runtime frames push nothing.
    ///
    /// <c>[ExecuteAlways]</c> keeps the shader animation running in edit mode
    /// (where <c>_Time</c> is frozen — the preview zeroes the shader clocks and
    /// integrates editor time instead).  Adjust <c>_previewBubbleCount</c> in the
    /// Inspector to preview each cluster state.
    /// </summary>
    [ExecuteAlways]
    public class SoapBubbleClusterVariant : MonoBehaviour, IBalloonVariant, IBalloonViewBinding
    {
        private static readonly int BubbleCountId = Shader.PropertyToID("_BubbleCount");
        private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");
        private static readonly int RotationId = Shader.PropertyToID("_Rotation");
        private static readonly int FloatSpeedId = Shader.PropertyToID("_FloatSpeed");
        private static readonly int RotationSpeedId = Shader.PropertyToID("_RotationSpeed");

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

            // Instances that never Bind (scene-placed, previews) still get a live clock;
            // Bind re-pushes with the randomized spin on top.
            if (Application.isPlaying)
            {
                PushRuntimeClock();
            }
        }

        private void Update()
        {
            EnsureBlock();

#if UNITY_EDITOR
            // Edit mode: built-in _Time is frozen, so zero the shader clocks and
            // integrate editor time here. Guard against first-frame and load spikes.
            if (!Application.isPlaying)
            {
                SceneView.RepaintAll();
                var editorTime = (float)EditorApplication.timeSinceStartup;
                if (_lastTime >= 0f)
                {
                    var delta = editorTime - _lastTime;
                    if (delta > 0f && delta < 0.5f)
                    {
                        _rotationAngle += _rotationSpeedRad * delta;
                    }
                }

                _lastTime = editorTime;
                PushEditPreview(editorTime);
            }
#endif

            // The runtime clock and spin are shader-derived (pushed once at Bind); the
            // quad itself must stay axis-aligned — the shader rotates the content.
            if (_renderer != null)
            {
                _renderer.transform.localRotation = Quaternion.identity;
            }
        }

        private void OnValidate()
        {
            EnsureBlock();
            PushBubbleCount(Mathf.Clamp(_previewBubbleCount, 1, _maxBubbles));
        }

        public void Initialize(IWriteableBalloonModel model, int levelAllowedColorsMask) { }

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

            PushRuntimeClock();
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

        // Pushed once per Bind: the shader derives clock and spin from _Time.y, so no
        // per-frame property-block churn at runtime.
        private void PushRuntimeClock()
        {
            if (_renderer == null || _block == null)
            {
                return;
            }

            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(TimeOffsetId, _instancePhase);
            _block.SetFloat(FloatSpeedId, _floatSpeed);
            _block.SetFloat(RotationId, _rotationAngle);
            _block.SetFloat(RotationSpeedId, _rotationSpeedRad);
            _renderer.SetPropertyBlock(_block);
        }

#if UNITY_EDITOR
        private void PushEditPreview(float editorTime)
        {
            if (_renderer == null || _block == null)
            {
                return;
            }

            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(TimeOffsetId, editorTime * _floatSpeed + _instancePhase);
            _block.SetFloat(FloatSpeedId, 0f);
            _block.SetFloat(RotationId, _rotationAngle);
            _block.SetFloat(RotationSpeedId, 0f);
            _renderer.SetPropertyBlock(_block);
        }
#endif
    }
}
