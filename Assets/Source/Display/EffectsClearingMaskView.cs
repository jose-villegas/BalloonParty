using BalloonParty.Configuration.Effects;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;
using VContainer;

namespace BalloonParty.Display
{
    /// <summary>
    ///     The clearing mask (@ref plan_effects_layer_clearing): a small single-channel target, bound globally as
    ///     <c>_EffectsClearMask</c>, that says how cleared each point of the screen is. Once a frame one blit decays
    ///     last frame's mask and stamps each ball in flight on it. A stamp peaks on the ball's path, so as it decays
    ///     the cleared area shrinks inward. Goes idle (no blit, mask black) once nothing has stamped for a full
    ///     linger time.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class EffectsClearingMaskView : MonoBehaviour
    {
        private static readonly int MaskTexId = Shader.PropertyToID("_EffectsClearMask");
        private static readonly int ClearParamsId = Shader.PropertyToID("_EffectsClearParams");
        private static readonly int DecayId = Shader.PropertyToID("_Decay");
        private static readonly int StampsId = Shader.PropertyToID("_Stamps");
        private static readonly int StampCountId = Shader.PropertyToID("_StampCount");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");

        [Inject] private IEffectsClearingSettings _settings;

        private readonly Vector4[] _stamps = new Vector4[EffectsClearingStamper.MaxStamps];

        private Camera _camera;
        private Material _material;
        private RenderTexture _front;
        private RenderTexture _back;
        private IEffectsClearingSource _source;
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
            var count = _source != null ? _source.FillStamps(view, _stamps) : 0;
            var deltaTime = _settings.UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            if (count > 0)
            {
                _remaining = _settings.LingerSeconds;
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
            _material.SetFloat(DecayId, deltaTime / _settings.LingerSeconds);
            _material.SetVectorArray(StampsId, _stamps);
            _material.SetFloat(StampCountId, count);
            _material.SetFloat(RadiusId, view.ToUvHeight(_settings.ClearRadius));
            _material.SetFloat(AspectId, _camera.aspect);

            // Outside the render loop on purpose: URP's RenderGraph rejects a mid-render Graphics.Blit.
            Graphics.Blit(_front, _back, _material);
            (_front, _back) = (_back, _front);
            _dirty = true;

            Shader.SetGlobalTexture(MaskTexId, _front);
            Shader.SetGlobalVector(ClearParamsId,
                new Vector4(_settings.EdgeLow, _settings.EdgeHigh, _settings.Strength, 0f));
        }

        private void OnDestroy()
        {
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
            var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8)
                ? RenderTextureFormat.R8
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
