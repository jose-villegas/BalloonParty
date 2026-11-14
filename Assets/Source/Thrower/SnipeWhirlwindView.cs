using System;
using BalloonParty.Shared.SceneLight;
using DG.Tweening;
using UnityEngine;
using Light = BalloonParty.Shared.SceneLight.Light;

namespace BalloonParty.Thrower
{
    /// <summary>
    ///     The Sharpshooter card's announcement when a ball loads Snipe-armed: a bright light drops from the sky onto
    ///     the cannon, dimming and tightening as it falls, and where it lands a whirlwind plays around the thrower:
    ///     the Snipe's own cone spiral drilling into the muzzle, and a ring of glitter turning around the cannon, both
    ///     tightening in as they fade. A child of the thrower, so the whirlwind turns with the aim; the light falls
    ///     straight down the world, whatever the aim.
    /// </summary>
    internal sealed class SnipeWhirlwindView : MonoBehaviour
    {
        [Tooltip("The Snipe's cone spiral (PierceConeSpiral material), its apex at the muzzle.")]
        [SerializeField] private SpriteRenderer _funnel;

        [Tooltip("The glitter ring (GlitterSwirl material), centred on the thrower's pivot.")]
        [SerializeField] private SpriteRenderer _glitter;

        [Tooltip("Seconds the whirlwind plays, in scaled time.")]
        [SerializeField] [Min(0.05f)] private float _duration = 0.5f;

        [Tooltip("Share of the play spent fading in.")]
        [SerializeField] [Range(0f, 1f)] private float _fadeInShare = 0.2f;

        [Tooltip("Share of the play spent fading out, at its end.")]
        [SerializeField] [Range(0f, 1f)] private float _fadeOutShare = 0.45f;

        [Tooltip("Scale at the start, of each layer's authored scale: the whirlwind tightens in from here.")]
        [SerializeField] [Min(0f)] private float _startScale = 1.35f;

        [Tooltip("Scale at the end, of each layer's authored scale.")]
        [SerializeField] [Min(0f)] private float _endScale = 0.85f;

        [Tooltip("Degrees the glitter ring turns over the play; negative is clockwise.")]
        [SerializeField] private float _glitterSpin = -540f;

        [Header("Sky light")]
        [Tooltip("World units above the whirlwind the light starts, straight up: above the board's top edge.")]
        [SerializeField] [Min(0f)] private float _skyLightHeight = 10f;

        [Tooltip("Shortest fall onto the cannon, in scaled time: a reload quicker than this and the whirlwind runs past ready.")]
        [SerializeField] [Min(0f)] private float _skyLightMinSeconds = 0.4f;

        [Tooltip("Longest fall onto the cannon, in scaled time. The fall fills what the reload leaves after the whirlwind, up to this; 0 skips the light.")]
        [SerializeField] [Min(0f)] private float _skyLightMaxSeconds = 1.4f;

        [Tooltip("How the fall runs: an ease-in drops slowly from the sky and lands fast.")]
        [SerializeField] private Ease _skyLightEase = Ease.InQuad;

        [Tooltip("The light's intensity in the sky, where it starts.")]
        [SerializeField] [Min(0f)] private float _skyLightStartIntensity = 6f;

        [Tooltip("The light's intensity as it lands on the cannon.")]
        [SerializeField] [Min(0f)] private float _skyLightEndIntensity = 1.5f;

        [Tooltip("The light's radius in the sky, where it starts.")]
        [SerializeField] [Min(0f)] private float _skyLightStartRadius = 3f;

        [Tooltip("The light's radius as it lands on the cannon.")]
        [SerializeField] [Min(0f)] private float _skyLightEndRadius = 1f;

        private SceneLightFieldService _lightField;
        private Light _skyLight;
        private IDisposable _skyLightRegistration;
        private Tween _tween;
        private Vector3 _funnelScale;
        private Vector3 _glitterScale;
        private Color _color;

        private void Awake()
        {
            _funnelScale = _funnel != null ? _funnel.transform.localScale : Vector3.one;
            _glitterScale = _glitter != null ? _glitter.transform.localScale : Vector3.one;
            SetVisible(false);
        }

        private void OnDisable()
        {
            _tween?.Kill();
            ReleaseSkyLight();
            SetVisible(false);
        }

        /// <summary>Lights come from the scene light field; without one the whirlwind plays at once, unlit.</summary>
        internal void Bind(SceneLightFieldService lightField)
        {
            _lightField = lightField;
        }

        /// <summary>
        ///     Drops the sky light in palette slot <paramref name="skyLightPaletteIndex" /> onto the cannon, then
        ///     plays the whirlwind in <paramref name="color" />, timed to end as a reload of
        ///     <paramref name="reloadSeconds" /> does. Restarts a play already running, from the sky.
        /// </summary>
        internal void Play(Color color, int skyLightPaletteIndex, float reloadSeconds)
        {
            _tween?.Kill();
            ReleaseSkyLight();
            SetVisible(false);
            _color = color;

            var fallSeconds = SkyLightSeconds(reloadSeconds, _duration, _skyLightMinSeconds, _skyLightMaxSeconds);
            if (_lightField == null || fallSeconds <= 0f)
            {
                PlayWhirlwind();
                return;
            }

            _skyLight ??= new Light(transform.position);
            _skyLight.PaletteIndex.Value = skyLightPaletteIndex;
            ApplySkyLight(0f);
            _skyLightRegistration = _lightField.RegisterLight(_skyLight);
            _tween = DOVirtual.Float(0f, 1f, fallSeconds, ApplySkyLight)
                .SetEase(_skyLightEase)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    ReleaseSkyLight();
                    PlayWhirlwind();
                });
        }

        /// <summary>
        ///     The whirlwind's strength at <paramref name="t" /> (0 to 1 through the play): up over the fade-in share,
        ///     held, then down over the fade-out share, and 0 at both ends.
        /// </summary>
        internal static float Envelope(float t, float fadeInShare, float fadeOutShare)
        {
            if (t <= 0f || t >= 1f)
            {
                return 0f;
            }

            var rise = fadeInShare > 0f ? Mathf.Clamp01(t / fadeInShare) : 1f;
            var fall = fadeOutShare > 0f ? Mathf.Clamp01((1f - t) / fadeOutShare) : 1f;
            return Mathf.Min(rise, fall);
        }

        /// <summary>
        ///     How long the sky light falls: what <paramref name="reloadSeconds" /> leaves once the whirlwind's
        ///     <paramref name="whirlwindSeconds" /> are taken out, so both end as the ball is ready, kept between
        ///     <paramref name="minSeconds" /> and <paramref name="maxSeconds" />. A max of 0 means no light.
        /// </summary>
        internal static float SkyLightSeconds(float reloadSeconds, float whirlwindSeconds, float minSeconds, float maxSeconds)
        {
            if (maxSeconds <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp(reloadSeconds - whirlwindSeconds, Mathf.Min(minSeconds, maxSeconds), maxSeconds);
        }

        /// <summary>
        ///     Where the sky light is at <paramref name="fall" /> (0 in the sky, 1 on the cannon): straight above
        ///     <paramref name="landing" /> by <paramref name="height" /> world units, coming down to it.
        /// </summary>
        internal static Vector3 SkyLightPosition(Vector3 landing, float height, float fall)
        {
            return landing + Vector3.up * (height * (1f - Mathf.Clamp01(fall)));
        }

        // fall is already eased, so the light dims and tightens with the distance it has come down.
        private void ApplySkyLight(float fall)
        {
            _skyLight.Position.Value = SkyLightPosition(transform.position, _skyLightHeight, fall);
            _skyLight.EndPosition.Value = _skyLight.Position.Value;
            var radius = Mathf.Lerp(_skyLightStartRadius, _skyLightEndRadius, fall);
            _skyLight.Radius.Value = radius;
            _skyLight.EndRadius.Value = radius;
            _skyLight.Intensity.Value = Mathf.Lerp(_skyLightStartIntensity, _skyLightEndIntensity, fall);
        }

        private void PlayWhirlwind()
        {
            SetVisible(true);
            ApplyWhirlwind(0f);
            _tween = DOVirtual.Float(0f, 1f, _duration, ApplyWhirlwind)
                .SetEase(Ease.Linear)
                .SetLink(gameObject)
                .OnComplete(() => SetVisible(false));
        }

        private void ApplyWhirlwind(float t)
        {
            var alpha = Envelope(t, _fadeInShare, _fadeOutShare);
            var scale = Mathf.Lerp(_startScale, _endScale, DOVirtual.EasedValue(0f, 1f, t, Ease.OutCubic));
            var color = new Color(_color.r, _color.g, _color.b, _color.a * alpha);

            if (_funnel != null)
            {
                _funnel.color = color;
                _funnel.transform.localScale = _funnelScale * scale;
            }

            if (_glitter != null)
            {
                _glitter.color = color;
                var glitterTransform = _glitter.transform;
                glitterTransform.localScale = _glitterScale * scale;
                glitterTransform.localRotation = Quaternion.AngleAxis(_glitterSpin * t, Vector3.forward);
            }
        }

        private void ReleaseSkyLight()
        {
            _skyLightRegistration?.Dispose();
            _skyLightRegistration = null;
        }

        private void SetVisible(bool visible)
        {
            if (_funnel != null)
            {
                _funnel.enabled = visible;
            }

            if (_glitter != null)
            {
                _glitter.enabled = visible;
            }
        }
    }
}
