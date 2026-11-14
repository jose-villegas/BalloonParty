using DG.Tweening;
using UnityEngine;

namespace BalloonParty.Thrower
{
    /// <summary>
    ///     The whirlwind around the thrower when a ball loads Snipe-armed (the Sharpshooter card): the Snipe's own
    ///     cone spiral drilling into the muzzle, and a ring of glitter turning around the cannon, both tightening in
    ///     as they fade. It plays while the armed ball slides out, so it announces the roll before the ball is ready.
    ///     A child of the thrower, so it turns with the aim.
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
            SetVisible(false);
        }

        /// <summary>Plays the whirlwind once in <paramref name="color" />, restarting one already playing.</summary>
        internal void Play(Color color)
        {
            _tween?.Kill();
            _color = color;
            SetVisible(true);
            Apply(0f);
            _tween = DOVirtual.Float(0f, 1f, _duration, Apply)
                .SetEase(Ease.Linear)
                .SetLink(gameObject)
                .OnComplete(() => SetVisible(false));
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

        private void Apply(float t)
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
