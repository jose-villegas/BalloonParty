using BalloonParty.Configuration.Effects;
using BalloonParty.Shared;
using BalloonParty.Shared.Rendering;
using UnityEngine;
using VContainer;

namespace BalloonParty.Display
{
    /// <summary>Screen-space light approximation ("2D GI" — see @ref arch_screen_space_light).</summary>
    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(SceneCaptureService))]
    public class ScreenSpaceLightService : MonoBehaviour
    {
        private const int TapCount = 8;
        private const float OverlayDistance = 5f;

        private static readonly int TapStepScaleId = Shader.PropertyToID("_TapStepScale");
        private static readonly int TapAspectId = Shader.PropertyToID("_TapAspect");
        private static readonly int TapDecayId = Shader.PropertyToID("_TapDecay");
        private static readonly int TapStartId = Shader.PropertyToID("_TapStart");
        private static readonly int MipSpreadId = Shader.PropertyToID("_MipSpread");
        private static readonly int HistoryTexId = Shader.PropertyToID("_HistoryTex");
        private static readonly int TemporalBlendId = Shader.PropertyToID("_TemporalBlend");
        private static readonly int LightTexId = Shader.PropertyToID("_LightTex");
        private static readonly int ShadowTintId = Shader.PropertyToID("_ShadowTint");
        private static readonly int ShadowStrengthId = Shader.PropertyToID("_ShadowStrength");
        private static readonly int BounceStrengthId = Shader.PropertyToID("_BounceStrength");
        private static readonly int MagnitudeRefId = Shader.PropertyToID("_MagnitudeRef");
        private static readonly int AmbientColorId = Shader.PropertyToID("_AmbientColor");

        [Inject] private IScreenSpaceLightSettings _settings;
        [Inject] private ISceneLightSettings _lightSettings;

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

            if (_settings.TemporalSmoothing)
            {
                // Temporal accumulation: blend into history, then swap for next frame.
                _smearMaterial.SetTexture(HistoryTexId, _historyTarget);
                _smearMaterial.SetFloat(TemporalBlendId, _historyValid ? _settings.TemporalResponse : 1f);
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

            var smearShader = _settings.SmearShader;
            var overlayShader = _settings.OverlayShader;

            // Editor-only fallback — device builds strip shaders only referenced by name.
            if (smearShader == null)
            {
                smearShader = Shader.Find("Hidden/BalloonParty/Display/ScreenSpaceLightSmear");
            }

            if (overlayShader == null)
            {
                overlayShader = Shader.Find("BalloonParty/Display/ScreenSpaceLightOverlay");
            }

            if (smearShader == null || overlayShader == null)
            {
                Debug.LogWarning("ScreenSpaceLightService: shader references missing — assign " +
                                 "them on the SceneLightFieldSettings asset; Shader.Find-only " +
                                 "shaders are stripped from builds. Disabling.", this);
                return false;
            }

            _smearMaterial = new Material(smearShader);
            _overlayMaterial = new Material(overlayShader);

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
            if (!_settings.TemporalSmoothing)
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
            // The smear now derives its march direction PER-FRAGMENT from the light field, so
            // the service pushes only the scalars that turn a unit direction into a UV step:
            // the world→UV scale (tap distance over view height) and the aspect that corrects
            // X. Camera-only — no owner read here now that direction comes from the field; the
            // shader's field-off fallback reads the flat global direction the owner publishes.
            var worldHeight = _camera.orthographicSize * 2f;
            var stepWorld = _settings.SmearDistance / TapCount;
            _smearMaterial.SetFloat(TapStepScaleId, stepWorld / worldHeight);
            _smearMaterial.SetFloat(TapAspectId, _camera.aspect);

            _smearMaterial.SetFloat(TapDecayId, _settings.TapDecay);
            _smearMaterial.SetFloat(TapStartId, _settings.TapStart);
            _smearMaterial.SetFloat(MipSpreadId, _settings.MipSpread);

            _overlayMaterial.SetColor(ShadowTintId, _settings.ShadowTint);
            _overlayMaterial.SetFloat(ShadowStrengthId, _settings.ShadowStrength);

            // Intensity coupling now lives per-fragment in the overlay via the field magnitude,
            // not here: bounce scales by the absolute local magnitude (field-off that equals
            // the global intensity, reproducing the old "_bounceStrength * intensity"), so we
            // push _bounceStrength RAW to avoid double-applying intensity. The reference feeds
            // the overlay's relative shadow coupling — field-off (magnitude == reference) it
            // resolves to 1, leaving the authored shadow strength bit-identical to today.
            _overlayMaterial.SetFloat(BounceStrengthId, _settings.BounceStrength);
            _overlayMaterial.SetFloat(MagnitudeRefId, Mathf.Max(_lightSettings.Intensity, 1e-4f));

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
