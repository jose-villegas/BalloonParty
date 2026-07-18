#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Palette;
using BalloonParty.Display;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using BalloonParty.Shared.SceneLight;
using Light = BalloonParty.Shared.SceneLight.Light;
using Random = UnityEngine.Random;

namespace BalloonParty.Balloon.Type
{
    /// <summary>Pushes sphere center/radius and clock phase to every quadrant renderer so the shader computes effects relative to the composed sphere, not world origin.</summary>
    [ExecuteAlways]
    internal class UnbreakableBalloonVariant : MonoBehaviour, IBalloonVariant, IBalloonViewBinding
    {
        // Matches the shader's _AnimationSpeed default.
        private const float ShaderClockRate = 2f;

        // Below this squared per-frame move, the balloon counts as settled (motion light fades out).
        private const float MoveEpsilonSqr = 1e-6f;

        private static readonly int SphereCenterId = Shader.PropertyToID("_SphereCenter");
        private static readonly int SphereRadiusId = Shader.PropertyToID("_SphereRadius");
        private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");
        private static readonly int AnimationSpeedId = Shader.PropertyToID("_AnimationSpeed");

        [SerializeField] private SpriteRenderer[] _renderers;
        [SerializeField] private SpriteRenderer[] _innerRenderers;

        [Tooltip("Sphere radius in world units. If zero, computed from " +
                 "the outer renderers' bounds at Awake.")]
        [SerializeField] private float _sphereRadius;

        [Header("Motion light (Unbreakable colour)")]
        [Tooltip("An Unbreakable-colour light at the balloon's position that lights up only while it " +
                 "MOVES (balance/nudge) and fades out when it settles.")]
        [SerializeField] [Min(0f)] private float _moveLightIntensity = 1.5f;
        [SerializeField] [Min(0f)] private float _moveLightRadius = 1.2f;
        [Tooltip("Seconds to fade the motion light fully in (on move) or out (on settle). The gradual " +
                 "fade also bridges the gaps between fixed-step balance moves so it doesn't flicker.")]
        [SerializeField] [Min(0.01f)] private float _moveLightFadeDuration = 0.3f;

        [Header("Deflect flash (Sparks colour)")]
        [SerializeField] [Min(0f)] private float _deflectFlashIntensity = 2.5f;
        [SerializeField] [Min(0f)] private float _deflectFlashRadius = 1.2f;
        [Tooltip("Seconds the deflect flash stays lit before snapping off.")]
        [SerializeField] [Min(0f)] private float _deflectFlashDuration = 0.12f;

        private MaterialPropertyBlock _block;
        private float _instancePhase;
        private Vector3 _pushedCenter;
        private SceneCaptureService _sceneCapture;
        private DisturbanceFieldService _disturbanceField;
        private IGamePalette _palette;
        private IPublisher<SpeckSpawnRequestMessage> _speckPublisher;
        private SceneLightFieldService _lightField;
        private ISubscriber<BalloonDeflectedMessage> _deflectedSubscriber;
        private IBalloonModel _model;
        private Light _moveLight;
        private Light _flashLight;
        private IDisposable _flashRegistration;
        private float _flashOffTime;
        private float _moveLightValue;
        private Vector3 _lastLightPos;
        private int _unbreakableColorIndex = -1;
        private int _sparksColorIndex = -1;

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

            TickLights();
        }

        private void OnDisable()
        {
            _sceneCapture?.Release();
            EndFlash();
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
            SceneCaptureService sceneCapture, DisturbanceFieldService disturbanceField, IGamePalette palette,
            IPublisher<SpeckSpawnRequestMessage> speckPublisher, SceneLightFieldService lightField,
            ISubscriber<BalloonDeflectedMessage> deflectedSubscriber)
        {
            _sceneCapture = sceneCapture;
            _disturbanceField = disturbanceField;
            _palette = palette;
            _speckPublisher = speckPublisher;
            _lightField = lightField;
            _deflectedSubscriber = deflectedSubscriber;
            _unbreakableColorIndex = palette.PaletteIndexOf(GamePalette.UnbreakableColorId);
            _sparksColorIndex = palette.PaletteIndexOf(GamePalette.SparksColorId);

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

            // A periodic burst (cadence + force from the profile) shoves the field's clouds out — tagged the
            // Unbreakable color — and enables a matching puff of specks at the same point.
            if (_disturbanceField != null)
            {
                _disturbanceField
                    .StartPulse(StampSource.UnbreakableBurst, () => EmitPulse(StampSource.UnbreakableBurst))
                    .AddTo(disposables);
            }

            _model = model;

            // An Unbreakable-colour light at the balloon that lights up only while it moves and fades
            // out when settled; registered dark, the registration removed when the view despawns.
            if (_lightField != null)
            {
                _moveLightValue = 0f;
                _lastLightPos = transform.position;
                _moveLight = new Light(transform.position, _moveLightRadius, 0f, _unbreakableColorIndex);
                _lightField.RegisterLight(_moveLight).AddTo(disposables);
                _deflectedSubscriber.Subscribe(OnDeflected).AddTo(disposables);
                disposables.Add(Disposable.Create(() => { EndFlash(); _moveLight = null; }));
            }
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

        private void TickLights()
        {
            if (_moveLight != null)
            {
                var pos = transform.position;
                var moving = (pos - _lastLightPos).sqrMagnitude > MoveEpsilonSqr;
                _lastLightPos = pos;

                _moveLight.Position.Value = pos;
                _moveLight.EndPosition.Value = pos;
                _moveLight.Radius.Value = _moveLightRadius;
                _moveLight.EndRadius.Value = _moveLightRadius;

                // Lit while moving, faded out when settled. The gradual fade also bridges the gaps
                // between 50 Hz balance-move writes so a 120 Hz frame with no delta doesn't flicker it.
                var target = moving ? _moveLightIntensity : 0f;
                var maxDelta = _moveLightIntensity / _moveLightFadeDuration * Time.deltaTime;
                _moveLightValue = Mathf.MoveTowards(_moveLightValue, target, maxDelta);
                _moveLight.Intensity.Value = _moveLightValue;
            }

            if (_flashRegistration != null)
            {
                _flashLight.Position.Value = transform.position;
                _flashLight.EndPosition.Value = transform.position;
                if (Time.time >= _flashOffTime)
                {
                    EndFlash();
                }
            }
        }

        // A projectile deflecting off THIS unbreakable pops a brief Sparks-colour flash on, then off.
        private void OnDeflected(BalloonDeflectedMessage msg)
        {
            if (_lightField == null || _model == null || msg.Balloon != _model)
            {
                return;
            }

            _flashLight ??= new Light(
                transform.position, _deflectFlashRadius, _deflectFlashIntensity, _sparksColorIndex);
            _flashLight.Radius.Value = _deflectFlashRadius;
            _flashLight.EndRadius.Value = _deflectFlashRadius;
            _flashLight.Intensity.Value = _deflectFlashIntensity;
            _flashLight.Position.Value = transform.position;
            _flashLight.EndPosition.Value = transform.position;
            _flashRegistration ??= _lightField.RegisterLight(_flashLight);
            _flashOffTime = Time.time + _deflectFlashDuration;
        }

        private void EndFlash()
        {
            _flashRegistration?.Dispose();
            _flashRegistration = null;
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

            _speckPublisher?.Publish(new SpeckSpawnRequestMessage(SpeckSource.UnbreakableBurst, transform.position));
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
