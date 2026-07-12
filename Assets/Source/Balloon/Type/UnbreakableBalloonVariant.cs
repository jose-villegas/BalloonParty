#if UNITY_EDITOR
using UnityEditor;
#endif
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Palette;
using BalloonParty.Display;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.Type
{
    /// <summary>Pushes sphere center/radius and clock phase to every quadrant renderer so the shader computes effects relative to the composed sphere, not world origin.</summary>
    [ExecuteAlways]
    internal class UnbreakableBalloonVariant : MonoBehaviour, IBalloonVariant, IBalloonViewBinding
    {
        // Matches the shader's _AnimationSpeed default.
        private const float ShaderClockRate = 2f;

        private static readonly int SphereCenterId = Shader.PropertyToID("_SphereCenter");
        private static readonly int SphereRadiusId = Shader.PropertyToID("_SphereRadius");
        private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");
        private static readonly int AnimationSpeedId = Shader.PropertyToID("_AnimationSpeed");

        [SerializeField] private SpriteRenderer[] _renderers;
        [SerializeField] private SpriteRenderer[] _innerRenderers;

        [Tooltip("Sphere radius in world units. If zero, computed from " +
                 "the outer renderers' bounds at Awake.")]
        [SerializeField] private float _sphereRadius;

        private MaterialPropertyBlock _block;
        private float _instancePhase;
        private Vector3 _pushedCenter;
        private SceneCaptureService _sceneCapture;
        private DisturbanceFieldService _disturbanceField;
        private IGamePalette _palette;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _instancePhase = Random.value * 100f;
            _pushedCenter = Vector3.positiveInfinity;
            ComputeRadiusIfNeeded();
        }

        // Null-guarded: pooled instances run their first OnEnable before injection.
        private void OnEnable()
        {
            _sceneCapture?.Acquire();
        }

        private void Update()
        {
            if (_renderers == null || _renderers.Length == 0)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // _Time is frozen in edit mode, so feed editor time through the offset instead.
                SceneView.RepaintAll();
                var editorTime = (float)EditorApplication.timeSinceStartup;
                PushSphereState(editorTime * ShaderClockRate + _instancePhase, true);
                return;
            }
#endif

            // Only re-push when the balloon actually moves, not every frame.
            if (transform.position != _pushedCenter)
            {
                PushSphereState(_instancePhase, false);
            }
        }

        private void OnDisable()
        {
            _sceneCapture?.Release();
        }

        private void OnValidate()
        {
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            ComputeRadiusIfNeeded();
        }

        [Inject]
        private void Construct(
            SceneCaptureService sceneCapture, DisturbanceFieldService disturbanceField, IGamePalette palette)
        {
            _sceneCapture = sceneCapture;
            _disturbanceField = disturbanceField;
            _palette = palette;

            // Settle the ref-count for an instance injected while already active.
            if (isActiveAndEnabled)
            {
                _sceneCapture.Acquire();
            }
        }

        public void Initialize(IWriteableBalloonModel model, int levelAllowedColorsMask) { }

        public void Bind(IBalloonModel model, CompositeDisposable disposables)
        {
            _instancePhase = Random.value * 100f;
            ComputeRadiusIfNeeded();
            PushSphereState(_instancePhase, false);

            // Two-phase field rhythm (cadence + force both from the profiles): a frequent gentle gather
            // draws specks in, an occasional strong burst shoves them out — tagged the Unbreakable color.
            StartPulse(StampSource.UnbreakableGather, disposables);
            StartPulse(StampSource.UnbreakableBurst, disposables);
        }

        private void PushSphereState(float timeOffset, bool zeroShaderClock)
        {
            _pushedCenter = transform.position;
            var center = (Vector4)_pushedCenter;

            PushPropertyBlock(_renderers, center, timeOffset, zeroShaderClock);
            PushPropertyBlock(_innerRenderers, center, timeOffset, zeroShaderClock);
        }

        private void PushPropertyBlock(
            SpriteRenderer[] renderers, Vector4 center, float timeOffset, bool zeroShaderClock)
        {
            if (renderers == null)
            {
                return;
            }

            foreach (var r in renderers)
            {
                if (r == null)
                {
                    continue;
                }

                r.GetPropertyBlock(_block);
                _block.SetVector(SphereCenterId, center);
                _block.SetFloat(SphereRadiusId, _sphereRadius);
                _block.SetFloat(TimeOffsetId, timeOffset);
                _block.SetFloat(AnimationSpeedId, zeroShaderClock ? 0f : ShaderClockRate);
                r.SetPropertyBlock(_block);
            }
        }

        private void StartPulse(StampSource source, CompositeDisposable disposables)
        {
            if (_disturbanceField == null)
            {
                return;
            }

            var interval = _disturbanceField.GetProfile(source).Interval;
            if (interval <= 0f)
            {
                return;
            }

            Observable.Interval(System.TimeSpan.FromSeconds(interval))
                .Subscribe(_ => EmitPulse(source))
                .AddTo(disposables);
        }

        private void EmitPulse(StampSource source)
        {
            if (_disturbanceField == null || _palette == null)
            {
                return;
            }

            var profile = _disturbanceField.GetProfile(source);
            _disturbanceField.Stamp(
                transform.position, profile.Radius, profile.Strength, Vector2.zero, profile.Duration,
                _palette.PaletteIndexOf(GamePalette.UnbreakableColorId), reportImpact: false);
        }

        private void ComputeRadiusIfNeeded()
        {
            if (_sphereRadius > 0f)
            {
                return;
            }

            if (_renderers == null || _renderers.Length == 0)
            {
                return;
            }

            // Half the longest axis of the union of all quadrant bounds approximates the sphere.
            var bounds = _renderers[0].bounds;
            for (var i = 1; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    bounds.Encapsulate(_renderers[i].bounds);
                }
            }

            _sphereRadius = Mathf.Max(bounds.extents.x, bounds.extents.y);
        }
    }
}
