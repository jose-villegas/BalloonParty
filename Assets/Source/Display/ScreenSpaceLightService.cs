using BalloonParty.Configuration.Effects;
using BalloonParty.Shared;
using BalloonParty.Shared.Diagnostics;
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
        private static readonly int ShadowMipSpreadId = Shader.PropertyToID("_ShadowMipSpread");
        private static readonly int SecondaryWeightId = Shader.PropertyToID("_SecondaryWeight");
        private static readonly int CloudGateStrengthId = Shader.PropertyToID("_CloudGateStrength");
        private static readonly int LightTexId = Shader.PropertyToID("_LightTex");
        private static readonly int ShadowTintId = Shader.PropertyToID("_ShadowTint");
        private static readonly int ShadowStrengthId = Shader.PropertyToID("_ShadowStrength");
        private static readonly int BounceStrengthId = Shader.PropertyToID("_BounceStrength");
        private static readonly int MagnitudeRefId = Shader.PropertyToID("_MagnitudeRef");
        private static readonly int AmbientColorId = Shader.PropertyToID("_AmbientColor");

        [Tooltip("Overlay sorting — above all gameplay, below UI.")]
        [SortingLayerName]
        [SerializeField] private string _sortingLayerName = "Sky";
        [SerializeField] private int _sortingOrder = 32000;

        [Inject] private IScreenSpaceLightSettings _settings;
        [Inject] private ISceneLightSettings _lightSettings;

        private static int _overlayLayer = -1;

        private Camera _camera;
        private SceneCaptureService _capture;
        private Material _smearMaterial;
        private Material _overlayMaterial;
        private RenderTexture _smearTarget;
        private RenderTexture _workTarget;
        private MeshRenderer _overlayRenderer;
        private Transform _overlayTransform;
        private int _lastContentVersion = -1;

        internal RenderTexture LightTexture => _workTarget;

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
            if (_settings == null)
            {
                return;
            }

            // The capture camera renders during the render phase, after LateUpdate, so this
            // reads the previous frame's capture. One frame of staleness is invisible here
            // (the buffer refreshes every SceneCaptureFrameInterval frames anyway) and buys
            // running outside URP's RenderGraph, which rejects mid-render-loop Graphics.Blit.
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

            var recreated = EnsureTargets(source);
            var rebuild = recreated || _capture.ContentVersion != _lastContentVersion;

            // The blit chain is a pure function of the capture content + these params (no
            // temporal state anymore), so between capture refreshes it would just reproduce
            // last time's pixels — skip it and keep the existing _workTarget bound.
#if UNITY_EDITOR
            // In-editor every frame so SO knob tweaks stay live-tunable; a smear-param tweak
            // without a rebuild takes effect on the next capture refresh (<= one interval), an
            // overlay-param tweak (read straight from the material, no rebuild needed) is
            // immediate either way.
            PushParameters();
#else
            if (rebuild)
            {
                PushParameters();
            }
#endif

            if (rebuild)
            {
                _lastContentVersion = _capture.ContentVersion;
                Graphics.Blit(source, _smearTarget, _smearMaterial, 0);
                Graphics.Blit(_smearTarget, _workTarget, _smearMaterial, 1);
                _overlayMaterial.SetTexture(LightTexId, _workTarget);
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
                Log.Warn("ScreenSpaceLight", "shader references missing — assign " +
                                 "them on the SceneLightFieldSettings asset; Shader.Find-only " +
                                 "shaders are stripped from builds. Disabling.", this);
                return false;
            }

            _smearMaterial = new Material(smearShader);
            _overlayMaterial = new Material(overlayShader);

#if !UNITY_EDITOR
            if (Application.isMobilePlatform)
            {
                _smearMaterial.EnableKeyword("_LOW_QUALITY_SMEAR");
            }
#endif

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

        private bool EnsureTargets(RenderTexture source)
        {
            // The light buffer is low-frequency (blurred, composited multiplicatively), so it
            // tolerates running well below capture resolution — at the default downscale of 2
            // this quarters the fragment count both passes push. Comparing against the computed
            // size (not the source size) means a live SO tweak of the knob also forces a rebuild
            // here, so it's flippable for A/B in play mode without touching the capture itself.
            var width = Mathf.Max(1, source.width / Mathf.Max(1, _settings.SmearDownscale));
            var height = Mathf.Max(1, source.height / Mathf.Max(1, _settings.SmearDownscale));

            if (_smearTarget == null || _smearTarget.width != width
                                     || _smearTarget.height != height)
            {
                ReleaseTarget(ref _smearTarget);
                ReleaseTarget(ref _workTarget);

                _smearTarget = CreateTarget(width, height, "ScreenSpaceLightSmear");
                _workTarget = CreateTarget(width, height, "ScreenSpaceLightWork");
                return true;
            }

            return false;
        }

        // Gated to capture refreshes outside the editor (see LateUpdate); always run in-editor
        // so SO knobs stay live-tunable in play mode.
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
            _smearMaterial.SetFloat(ShadowMipSpreadId, _settings.ShadowMipSpread);
            _smearMaterial.SetFloat(SecondaryWeightId, _settings.SecondaryBounceWeight);
            _smearMaterial.SetFloat(CloudGateStrengthId, _settings.CloudShadowGate);

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

            // Measured against the capture's clear color so open sky nets to neutral. Outside
            // the editor this (and MagnitudeRefId above) now update in lockstep with capture
            // refreshes rather than every display frame — which is when the capture itself last
            // re-matched its clear colour, so it's more correct as well as cheaper.
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

        private static RenderTexture CreateTarget(int width, int height, string name)
        {
            return new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                // Clamp, not the RenderTexture default (Repeat): the cone-march (Pass 0) and 3×3
                // soften (Pass 1) both sample past the edge, and wrapping would bleed the opposite
                // border back in. Matches the capture and the field RTs.
                wrapMode = TextureWrapMode.Clamp
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
