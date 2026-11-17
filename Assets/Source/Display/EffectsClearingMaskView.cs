using BalloonParty.Configuration.Effects;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;
using VContainer;

namespace BalloonParty.Display
{
    /// <summary>
    ///     The clearing mask (@ref plan_effects_layer_clearing): a small two-channel target, bound globally as
    ///     <c>_EffectsClearMask</c>, that says how cleared each point of the screen is (red) and how wide the dome
    ///     there is (green, its radius in view heights). Once a frame one blit decays last frame's mask and stamps
    ///     each ball in flight on it. A stamp peaks on the ball's path, so as it decays the cleared area shrinks
    ///     inward. Goes idle (no blit, mask black) once nothing has stamped for a full
    ///     linger time.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(ExecutionOrder)]
    internal class EffectsClearingMaskView : MonoBehaviour
    {
        // After every LateUpdate that moves or zooms the camera (shake, cinematics, the device fit), so the stamps
        // frame the view this frame renders; EffectsMapCompositor runs right after, to see this frame's IsClearing.
        internal const int ExecutionOrder = 1000;

        private static readonly int MaskTexId = Shader.PropertyToID("_EffectsClearMask");
        private static readonly int ClearParamsId = Shader.PropertyToID("_EffectsClearParams");
        private static readonly int ClearLensId = Shader.PropertyToID("_EffectsClearLens");
        private static readonly int DecayId = Shader.PropertyToID("_Decay");
        private static readonly int StampsId = Shader.PropertyToID("_Stamps");
        private static readonly int StampCountId = Shader.PropertyToID("_StampCount");
        private static readonly int RadiiId = Shader.PropertyToID("_Radii");
        private static readonly int ReprojectId = Shader.PropertyToID("_Reproject");
        private static readonly int RadiusScaleId = Shader.PropertyToID("_RadiusScale");
        private static readonly Vector4 NoReprojection = new(1f, 1f, 0f, 0f);
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");

        [Inject] private IEffectsClearingSettings _settings;

        private readonly Vector4[] _stamps = new Vector4[EffectsClearingStamper.MaxStamps];
        private readonly float[] _radii = new float[EffectsClearingStamper.MaxStamps];

        private Camera _camera;
        private Material _material;
        private RenderTexture _front;
        private RenderTexture _back;
        private IEffectsClearingSource _source;
        private ClearingView _lastView;
        private bool _hasLastView;
        private float _remaining;
        private bool _dirty;
        private bool _broken;

        /// <summary>True while any part of the mask may be above zero, so effects maps must composite.</summary>
        internal bool IsClearing => _remaining > 0f;

        internal RenderTexture MaskTexture => _front;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            Shader.SetGlobalTexture(MaskTexId, Texture2D.blackTexture);
        }

        private void LateUpdate()
        {
            if (_settings == null || !_settings.Enabled || !EnsureMaterial())
            {
                GoIdle();
                return;
            }

            var view = new ClearingView(_camera.transform.position, _camera.orthographicSize, _camera.aspect);
            var count = _source != null ? _source.FillStamps(view, _stamps, _radii) : 0;
            // Game time while a ball flies, so slow-mo stretches its clearing and a pause holds it. Real time once
            // nothing stamps: a freeze that starts after the shot (the level-up) must not hold the hole open.
            var deltaTime = count > 0 && !_settings.UseUnscaledTime ? Time.deltaTime : Time.unscaledDeltaTime;

            // Without a trail the mask is wiped each frame, so it holds only this frame's domes and closes the moment
            // no ball is left.
            var trail = _settings.Trail;
            if (count > 0)
            {
                _remaining = _settings.LingerSeconds;
            }
            else if (!trail)
            {
                GoIdle();
                return;
            }
            else if (_remaining > 0f)
            {
                _remaining -= deltaTime;
                if (_remaining <= 0f)
                {
                    GoIdle();
                    return;
                }
            }
            else
            {
                return;
            }

            EnsureTargets();
            // The mask is in screen space, so a camera move or zoom would leave last frame's stamps where they were
            // on screen; carried through the view change, they stay on the world.
            _material.SetVector(ReprojectId, _hasLastView ? view.ReprojectionFrom(_lastView) : NoReprojection);
            _material.SetFloat(RadiusScaleId,
                _hasLastView ? _lastView.OrthographicSize / view.OrthographicSize : 1f);
            _lastView = view;
            _hasLastView = true;
            _material.SetFloat(DecayId, trail ? deltaTime / _settings.LingerSeconds : 1f);
            _material.SetVectorArray(StampsId, _stamps);
            _material.SetFloat(StampCountId, count);
            _material.SetFloatArray(RadiiId, _radii);
            _material.SetFloat(AspectId, _camera.aspect);

            // Outside the render loop on purpose: URP's RenderGraph rejects a mid-render Graphics.Blit.
            Graphics.Blit(_front, _back, _material);
            (_front, _back) = (_back, _front);
            _dirty = true;

            Shader.SetGlobalTexture(MaskTexId, _front);
            Shader.SetGlobalVector(ClearParamsId,
                new Vector4(_settings.EdgeLow, _settings.EdgeHigh, _settings.Strength, _camera.aspect));
            Shader.SetGlobalVector(ClearLensId, new Vector4(_settings.LensRefraction, 1f / _front.height,
                _settings.LensDispersion, _settings.LensHighlight));
        }

        private void OnDestroy()
        {
            Shader.SetGlobalTexture(MaskTexId, Texture2D.blackTexture);
            ReleaseTarget(ref _front);
            ReleaseTarget(ref _back);

            if (_material != null)
            {
                Destroy(_material);
            }
        }

        internal void Bind(IEffectsClearingSource source)
        {
            _source = source;
        }

        internal void Unbind(IEffectsClearingSource source)
        {
            if (_source == source)
            {
                _source = null;
            }
        }

        private void GoIdle()
        {
            _remaining = 0f;
            _hasLastView = false;
            if (!_dirty)
            {
                return;
            }

            // The decay has all but emptied it; this makes it exact for the next clearing.
            Graphics.Blit(Texture2D.blackTexture, _front);
            _dirty = false;
        }

        private bool EnsureMaterial()
        {
            if (_material != null)
            {
                return true;
            }

            if (_broken)
            {
                return false;
            }

            var shader = _settings.MaskShader;

            // Editor-only fallback — device builds strip shaders only referenced by name.
            if (shader == null)
            {
                shader = Shader.Find("Hidden/BalloonParty/Display/EffectsClearingMask");
            }

            if (shader == null)
            {
                Log.Warn("EffectsClearing", "mask shader missing — assign it on the EffectsClearingSettings " +
                                            "asset. Effects won't clear.", this);
                _broken = true;
                return false;
            }

            _material = new Material(shader);
            return true;
        }

        private void EnsureTargets()
        {
            var downscale = Mathf.Max(1, _settings.MaskDownscale);
            var width = Mathf.Max(1, Screen.width / downscale);
            var height = Mathf.Max(1, Screen.height / downscale);

            if (_front != null && _front.width == width && _front.height == height)
            {
                return;
            }

            ReleaseTarget(ref _front);
            ReleaseTarget(ref _back);
            _front = CreateTarget(width, height, "EffectsClearMaskA");
            _back = CreateTarget(width, height, "EffectsClearMaskB");
            Graphics.Blit(Texture2D.blackTexture, _front);
        }

        private static RenderTexture CreateTarget(int width, int height, string name)
        {
            // Half floats where the device has them: the composite reads the mask's slope as the glass's surface,
            // and 8-bit steps show there as ripples.
            var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGHalf)
                ? RenderTextureFormat.RGHalf
                : SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
                    ? RenderTextureFormat.ARGBHalf
                    : RenderTextureFormat.ARGB32;

            return new RenderTexture(width, height, 0, format)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
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
