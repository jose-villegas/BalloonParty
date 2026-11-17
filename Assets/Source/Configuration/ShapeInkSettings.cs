using System;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>
    ///     How a score shape's ink is drawn when the shape is one baked mesh (<c>ShapeInkView</c>) instead of a
    ///     trail per pen: the ink's width, fade and colour along its length. The pen is the ink's head: the
    ///     shader closes it with a round nose as wide as the ink is there.
    /// </summary>
    [Serializable]
    internal struct ShapeInkSettings
    {
        [Tooltip("Draw each score shape as one baked mesh whose shader moves the pens, instead of a pooled " +
                 "trail per pen. Off keeps the trails.")]
        [SerializeField] private bool _baked;

        [Tooltip("The ink material (BalloonParty/Sprite/ShapeInk). Its ribbon picture runs along the ink, " +
                 "head at the left.")]
        [SerializeField] private Material _material;

        [Tooltip("The ink's width in world units along its length: 0 is at the pen, 1 is the oldest ink.")]
        [SerializeField] private AnimationCurve _width;

        [Tooltip("The ink's opacity along its length: 0 is at the pen, 1 is the oldest ink.")]
        [SerializeField] private AnimationCurve _alpha;

        [Tooltip("How far the ink has turned from the shape's colour to the material's tail colour along its " +
                 "length: 0 is the shape's colour, 1 the tail colour.")]
        [SerializeField] private AnimationCurve _tailMix;

        public bool Baked => _baked && _material != null && _width != null && _alpha != null && _tailMix != null;
        public Material Material => _material;
        public AnimationCurve Width => _width;
        public AnimationCurve Alpha => _alpha;
        public AnimationCurve TailMix => _tailMix;

        internal ShapeInkSettings(
            bool baked, Material material, AnimationCurve width, AnimationCurve alpha, AnimationCurve tailMix)
        {
            _baked = baked;
            _material = material;
            _width = width;
            _alpha = alpha;
            _tailMix = tailMix;
        }
    }
}
