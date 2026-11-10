using System;
using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>
    ///     One scenario's colour grade over the whole frame (see <c>ScenarioGradeView</c>): URP's Color Adjustments,
    ///     White Balance and the shadow and highlight tints of Shadows Midtones Highlights. <see cref="Neutral" />
    ///     leaves the frame as it is.
    /// </summary>
    [Serializable]
    internal struct ScenarioGrade
    {
        public static readonly ScenarioGrade Neutral = new(0f, 0f, 0f, 0f, 0f, Color.white, Color.white, Color.white);

        [Tooltip("Brightness in stops, before the rest of the grade.")]
        [Range(-2f, 2f)]
        [SerializeField] private float _exposure;

        [Range(-100f, 100f)]
        [SerializeField] private float _contrast;

        [Range(-100f, 100f)]
        [SerializeField] private float _saturation;

        [Tooltip("Warmer above zero, cooler below.")]
        [Range(-100f, 100f)]
        [SerializeField] private float _temperature;

        [Tooltip("Magenta above zero, green below.")]
        [Range(-100f, 100f)]
        [SerializeField] private float _tint;

        [Tooltip("Multiplies the whole frame. White leaves it alone.")]
        [ColorUsage(false)]
        [SerializeField] private Color _filter;

        [Tooltip("Tints the dark tones toward this colour. White leaves them alone.")]
        [ColorUsage(false)]
        [SerializeField] private Color _shadows;

        [Tooltip("Tints the bright tones toward this colour. White leaves them alone.")]
        [ColorUsage(false)]
        [SerializeField] private Color _highlights;

        public float Exposure => _exposure;
        public float Contrast => _contrast;
        public float Saturation => _saturation;
        public float Temperature => _temperature;
        public float Tint => _tint;
        public Color Filter => _filter;
        public Color Shadows => _shadows;
        public Color Highlights => _highlights;

        public ScenarioGrade(float exposure, float contrast, float saturation, float temperature, float tint,
            Color filter, Color shadows, Color highlights)
        {
            _exposure = exposure;
            _contrast = contrast;
            _saturation = saturation;
            _temperature = temperature;
            _tint = tint;
            _filter = filter;
            _shadows = shadows;
            _highlights = highlights;
        }

        /// <summary>The grade <paramref name="t" /> of the way from <paramref name="a" /> to <paramref name="b" />.</summary>
        public static ScenarioGrade Lerp(ScenarioGrade a, ScenarioGrade b, float t)
        {
            t = Mathf.Clamp01(t);
            return new ScenarioGrade(
                Mathf.Lerp(a._exposure, b._exposure, t),
                Mathf.Lerp(a._contrast, b._contrast, t),
                Mathf.Lerp(a._saturation, b._saturation, t),
                Mathf.Lerp(a._temperature, b._temperature, t),
                Mathf.Lerp(a._tint, b._tint, t),
                Color.Lerp(a._filter, b._filter, t),
                Color.Lerp(a._shadows, b._shadows, t),
                Color.Lerp(a._highlights, b._highlights, t));
        }
    }
}
