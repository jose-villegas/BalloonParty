using BalloonParty.Shared;
using BalloonParty.Shared.Rendering;
using UnityEngine;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.Display
{
    /// <summary>Screen-space light approximation ("2D GI" — see @ref arch_screen_space_light).</summary>
    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(SceneCaptureService))]
    public class ScreenSpaceLightService : MonoBehaviour
    {
        private const int TapCount = 8;
        private const float OverlayDistance = 5f;

        private static readonly int TapStepUvId = Shader.PropertyToID("_TapStepUV");
        private static readonly int TapDecayId = Shader.PropertyToID("_TapDecay");
        private static readonly int TapStartId = Shader.PropertyToID("_TapStart");
        private static readonly int HistoryTexId = Shader.PropertyToID("_HistoryTex");
        private static readonly int TemporalBlendId = Shader.PropertyToID("_TemporalBlend");
        private static readonly int LightTexId = Shader.PropertyToID("_LightTex");
        private static readonly int ShadowTintId = Shader.PropertyToID("_ShadowTint");
        private static readonly int ShadowStrengthId = Shader.PropertyToID("_ShadowStrength");
        private static readonly int BounceStrengthId = Shader.PropertyToID("_BounceStrength");
        private static readonly int AmbientColorId = Shader.PropertyToID("_AmbientColor");

        [Tooltip("Assign explicitly — device builds strip shaders that are only " +
                 "referenced via Shader.Find, which is kept as an editor fallback.")]
        [SerializeField] private Shader _smearShader;
        [SerializeField] private Shader _overlayShader;
        [Tooltip("Light vector — points TOWARD the light source, same convention (and " +
                 "default) as the PuffCloud material's Light Direction. Shadows extend " +
                 "the opposite way.")]
        [SerializeField] private Vector2 _lightDirection = new Vector2(1f, -1f);
        [Tooltip("How far an object's shadow/bleed reaches, in world units.")]
        [SerializeField] private float _smearDistance = 1.5f;
        [Tooltip("Per-tap weight decay along the march — lower dies off faster.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float _tapDecay = 0.8f;
        [Tooltip("Taps skipped at the march start so occluders don't fully self-shadow.")]
        [SerializeField] private float _tapStart = 1f;
        [Range(0f, 1f)]
        [SerializeField] private float _shadowStrength = 0.35f;
        [SerializeField] private Color _shadowTint = new Color(0.55f, 0.6f, 0.75f);
        [Range(0f, 2f)]
        [SerializeField] private float _bounceStrength = 0.25f;
        [Tooltip("Off skips the history blit and its two buffers entirely — the light " +
                 "responds instantly, but moving sprites may flicker at capture resolution.")]
        [SerializeField] private bool _temporalSmoothing = true;
        [Tooltip("Fraction of the fresh light buffer accepted per frame — lower is " +
                 "smoother but laggier. Kills the texel flicker of moving sprites at " +
                 "capture resolution.")]
        [Range(0.02f, 1f)]
        [SerializeField] private float _temporalResponse = 0.2f;
        [Tooltip("Overlay sorting — above all gameplay, below UI.")]
        [SortingLayerName]
        [SerializeField] private string _sortingLayerName = "Sky";
        [SerializeField] private int _sortingOrder = 32000;

        private static int _overlayLayer = -1;

        private Camera _camera;
        private SceneCaptureService _capture;
        private Material _smearMaterial;
        private Material _overlayMaterial;
        private RenderTexture _smearTarget;
        private RenderTexture _workTarget;
        private RenderTexture _lightTarget;
        private RenderTexture _historyTarget;
        private MeshRenderer _overlayRenderer;
        private Transform _overlayTransform;
        private bool _historyValid;

        // Ping-ponged, so neither backing field consistently holds the latest build.
        internal RenderTexture LightTexture { get; private set; }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _capture = GetComponent<SceneCaptureService>();
        }

        private void OnEnable()
        {
            _capture.Acquire();
        }

        private void LateUpdate()
        {
            // The capture camera renders during the render phase, after LateUpdate, so this
            // reads the previous frame's capture. One frame of staleness is invisible here
            // (temporally blended buffer, refreshes every SceneCaptureFrameInterval frames) and
            // buys running outside URP's RenderGraph, which rejects mid-render-loop Graphics.Blit.
            var source = _capture.CaptureTexture;
            if (source == null)
            {
                SetOverlayVisible(false);
                return;
            }

            if (!EnsureResources())
            {
                enabled = false;
                return;
            }

            EnsureTargets(source);
            PushParameters();

            Graphics.Blit(source, _smearTarget, _smearMaterial, 0);
            Graphics.Blit(_smearTarget, _workTarget, _smearMaterial, 1);

            if (_temporalSmoothing)
            {
                // Temporal accumulation: blend into history, then swap for next frame.
                _smearMaterial.SetTexture(HistoryTexId, _historyTarget);
                _smearMaterial.SetFloat(TemporalBlendId, _historyValid ? _temporalResponse : 1f);
                Graphics.Blit(_workTarget, _lightTarget, _smearMaterial, 2);
                _historyValid = true;

                _overlayMaterial.SetTexture(LightTexId, _lightTarget);
                LightTexture = _lightTarget;
                (_historyTarget, _lightTarget) = (_lightTarget, _historyTarget);
            }
            else
            {
                _overlayMaterial.SetTexture(LightTexId, _workTarget);
                LightTexture = _workTarget;
            }

            FitOverlayToFrustum();
            SetOverlayVisible(true);
        }

        private void OnDisable()
        {
            _capture.Release();
            SetOverlayVisible(false);
        }

        private void OnDestroy()
        {
            ReleaseTarget(ref _smearTarget);
            ReleaseTarget(ref _workTarget);
            ReleaseTarget(ref _lightTarget);
            ReleaseTarget(ref _historyTarget);

            if (_smearMaterial != null)
            {
                Destroy(_smearMaterial);
            }

            if (_overlayMaterial != null)
            {
                Destroy(_overlayMaterial);
            }
        }

        private bool EnsureResources()
        {
            if (_overlayRenderer != null)
            {
                return true;
            }

            // Editor-only fallback — device builds strip shaders only referenced by name.
            if (_smearShader == null)
            {
                _smearShader = Shader.Find("Hidden/BalloonParty/Display/ScreenSpaceLightSmear");
            }

            if (_overlayShader == null)
            {
                _overlayShader = Shader.Find("BalloonParty/Display/ScreenSpaceLightOverlay");
            }

            if (_smearShader == null || _overlayShader == null)
            {
                Debug.LogWarning("ScreenSpaceLightService: shader references missing — assign " +
                                 "them on the component; Shader.Find-only shaders are stripped " +
                                 "from builds. Disabling.", this);
                return false;
            }

            _smearMaterial = new Material(_smearShader);
            _overlayMaterial = new Material(_overlayShader);

            if (_overlayLayer < 0)
            {
                _overlayLayer = LayerMask.NameToLayer("TransparentFX");
            }

            var overlay = new GameObject("ScreenSpaceLightOverlay")
            {
                layer = _overlayLayer
            };
            _overlayTransform = overlay.transform;
            _overlayTransform.SetParent(transform, false);
            _overlayTransform.localPosition = new Vector3(0f, 0f, OverlayDistance);

            var filter = overlay.AddComponent<MeshFilter>();
            filter.sharedMesh = MeshHelper.CreateQuad(QuadPivot.Center);

            _overlayRenderer = overlay.AddComponent<MeshRenderer>();
            _overlayRenderer.sharedMaterial = _overlayMaterial;
            _overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _overlayRenderer.receiveShadows = false;
            _overlayRenderer.sortingLayerName = _sortingLayerName;
            _overlayRenderer.sortingOrder = _sortingOrder;

            return true;
        }

        private void EnsureTargets(RenderTexture source)
        {
            if (_smearTarget == null || _smearTarget.width != source.width
                                     || _smearTarget.height != source.height)
            {
                ReleaseTarget(ref _smearTarget);
                ReleaseTarget(ref _workTarget);
                ReleaseHistoryTargets();

                _smearTarget = CreateTarget(source, "ScreenSpaceLightSmear");
                _workTarget = CreateTarget(source, "ScreenSpaceLightWork");
            }

            // The ping-pong pair only exists while smoothing is on (live-toggleable).
            if (!_temporalSmoothing)
            {
                ReleaseHistoryTargets();
                return;
            }

            if (_lightTarget == null)
            {
                _lightTarget = CreateTarget(source, "ScreenSpaceLightA");
                _historyTarget = CreateTarget(source, "ScreenSpaceLightB");
            }
        }

        private void ReleaseHistoryTargets()
        {
            ReleaseTarget(ref _lightTarget);
            ReleaseTarget(ref _historyTarget);
            _historyValid = false;
        }

        // Pushed every frame so the knobs stay live-tunable in play mode.
        private void PushParameters()
        {
            var worldHeight = _camera.orthographicSize * 2f;
            var worldWidth = worldHeight * _camera.aspect;

            var direction = _lightDirection.sqrMagnitude > 0.0001f
                ? _lightDirection.normalized
                : Vector2.down;

            // Shader marches +step for reflection and -step for shadow; see shader header.
            var stepWorld = _smearDistance / TapCount;
            var stepUv = new Vector4(
                direction.x * stepWorld / worldWidth,
                direction.y * stepWorld / worldHeight);

            _smearMaterial.SetVector(TapStepUvId, stepUv);
            _smearMaterial.SetFloat(TapDecayId, _tapDecay);
            _smearMaterial.SetFloat(TapStartId, _tapStart);

            _overlayMaterial.SetColor(ShadowTintId, _shadowTint);
            _overlayMaterial.SetFloat(ShadowStrengthId, _shadowStrength);
            _overlayMaterial.SetFloat(BounceStrengthId, _bounceStrength);

            // Measured against the capture's clear color so open sky nets to neutral.
            _overlayMaterial.SetColor(AmbientColorId, _camera.backgroundColor);
        }

        private void FitOverlayToFrustum()
        {
            var height = _camera.orthographicSize * 2f;
            _overlayTransform.localScale = new Vector3(height * _camera.aspect, height, 1f);
        }

        private void SetOverlayVisible(bool visible)
        {
            if (_overlayRenderer != null && _overlayRenderer.enabled != visible)
            {
                _overlayRenderer.enabled = visible;
            }
        }

        private static RenderTexture CreateTarget(RenderTexture source, string name)
        {
            return new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32)
            {
                name = name,
                filterMode = FilterMode.Bilinear
            };
        }

        private static void ReleaseTarget(ref RenderTexture target)
        {
            if (target == null)
            {
                return;
            }

            target.Release();
            Destroy(target);
            target = null;
        }
    }
}
