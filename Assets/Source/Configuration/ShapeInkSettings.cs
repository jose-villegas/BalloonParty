using System;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>
    ///     How a score shape's ink is drawn (<c>ShapeInkView</c>, on the shape's baked mesh): the material that
    ///     gives the pen stroke its look, and the ink's width, fade and colour along its length. The pen is the
    ///     ink's head: the shader closes it with a round end as wide as the ink is there.
    /// </summary>
    [Serializable]
    internal struct ShapeInkSettings
    {
        [Tooltip("The ink material (BalloonParty/Sprite/ShapeInk): the look of the pen stroke.")]
        [SerializeField] private Material _material;

        [Tooltip("The ink's width in world units along its length: 0 is at the pen, 1 is the oldest ink.")]
        [SerializeField] private AnimationCurve _width;

        [Tooltip("The ink's opacity along its length: 0 is at the pen, 1 is the oldest ink.")]
        [SerializeField] private AnimationCurve _alpha;

        [Tooltip("How far the ink has turned from the shape's colour to the material's tail colour along its " +
                 "length: 0 is the shape's colour, 1 the tail colour.")]
        [SerializeField] private AnimationCurve _tailMix;

        /// <summary>False while the material or a curve is missing: there is then nothing to draw a shape with.</summary>
        public bool Drawable => _material != null && _width != null && _alpha != null && _tailMix != null;
        public Material Material => _material;
        public AnimationCurve Width => _width;
        public AnimationCurve Alpha => _alpha;
        public AnimationCurve TailMix => _tailMix;

        internal ShapeInkSettings(Material material, AnimationCurve width, AnimationCurve alpha, AnimationCurve tailMix)
        {
            _material = material;
            _width = width;
            _alpha = alpha;
            _tailMix = tailMix;
        }
    }
}
