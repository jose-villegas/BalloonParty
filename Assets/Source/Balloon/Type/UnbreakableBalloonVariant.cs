#if UNITY_EDITOR
using UnityEditor;
#endif
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Display;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.Type
{
    /// <summary>
    /// Balloon variant for <c>BalloonType.Unbreakable</c>.
    /// Pushes <c>_SphereCenter</c>, <c>_SphereRadius</c>, and the clock phase to
    /// every quadrant <see cref="SpriteRenderer"/> so the
    /// <c>BalloonParty/Balloon/UnbreakableBalloon</c> shader can compute metallic
    /// gradient, specular, reflection, and rim effects relative to the composed
    /// sphere rather than world origin. The shader self-derives its animation from
    /// <c>_Time.y</c>, so runtime pushes happen only at <c>Bind</c> and when the
    /// balloon actually moves — not per frame.
    ///
    /// Inner renderers receive the same sphere data so effects that depend
    /// on sphere-local position stay coherent across both layers.
    ///
    /// <c>[ExecuteAlways]</c> keeps the shader animation running in edit mode
    /// (where <c>_Time</c> is frozen — the preview zeroes the shader clock and
    /// integrates editor time instead).
    /// </summary>
    [ExecuteAlways]
    internal class UnbreakableBalloonVariant : MonoBehaviour, IBalloonVariant, IBalloonViewBinding
    {
        // Matches the shader's _AnimationSpeed default. C# owns the property outright:
        // play mode pushes the rate (a property block survives an edit-mode preview,
        // which zeroes it), edit mode zeroes it and integrates editor time itself.
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

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _instancePhase = Random.value * 100f;
            _pushedCenter = Vector3.positiveInfinity;
            ComputeRadiusIfNeeded();
        }

        // Null-guarded: [ExecuteAlways] fires this in edit mode where nothing is injected, and
        // pooled instances run their first OnEnable during Instantiate, before injection.
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
                // Edit mode: built-in _Time is frozen, so zero the shader clock and feed
                // editor time (at the shader's own rate) through the offset.
                SceneView.RepaintAll();
                var editorTime = (float)EditorApplication.timeSinceStartup;
                PushSphereState(editorTime * ShaderClockRate + _instancePhase, true);
                return;
            }
#endif

            // The runtime clock is shader-derived; the sphere data only changes when the
            // balloon moves (nudges, balance paths). Repushing every renderer's property
            // block every frame was the standing cost this replaces.
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
        private void Construct(SceneCaptureService sceneCapture)
        {
            _sceneCapture = sceneCapture;

            // Injection lands after the creation-time OnEnable saw a null reference — settle
            // the ref-count for an instance injected while already active.
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

            // The composed sphere spans the union of all quadrant bounds;
            // half the longest axis gives a good approximation.
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
