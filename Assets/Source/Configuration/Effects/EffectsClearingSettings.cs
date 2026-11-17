using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Authored tuning for the effects clearing; wired into <c>AppLifetimeScope</c>.</summary>
    [CreateAssetMenu(menuName = "Configuration/Effects Clearing Settings", fileName = "EffectsClearingSettings")]
    internal sealed class EffectsClearingSettings : ScriptableObject, IEffectsClearingSettings
    {
        [SerializeField] private bool _enabled = true;

        [Tooltip("World units from the ball's path to the clearing's edge for a ball with one shield. A ball with " +
                 "no shields left clears as if it had one, so its last leg still reads.")]
        [Min(0.05f)]
        [SerializeField] private float _minClearRadius = 0.48f;

        [Tooltip("The clearing's radius in world units once the ball has Shields For Max Radius shields or more. " +
                 "Between one shield and that many it grows evenly with each shield.")]
        [Min(0.05f)]
        [SerializeField] private float _maxClearRadius = 1.2f;

        [Tooltip("The shield count at which the clearing reaches Max Clear Radius.")]
        [Min(2)]
        [SerializeField] private int _shieldsForMaxRadius = 10;

        [Tooltip("On, a clearing leaves a trail along the ball's path that lingers and closes behind it. Off, it is " +
                 "only a dome where the ball is.")]
        [SerializeField] private bool _trail;

        [Tooltip("Seconds a stamp takes to decay from full to nothing. The cleared area shrinks inward over it.")]
        [Min(0.05f)]
        [SerializeField] private float _lingerSeconds = 0.6f;

        [Tooltip("Mask value where effects start to fade.")]
        [Range(0f, 1f)]
        [SerializeField] private float _edgeLow = 0f;

        [Tooltip("Mask value where effects reach full clearing. Above Edge Low; the gap is the edge's softness. " +
                 "Wide, so the effects thin slowly from the rim and are near gone only over the ball.")]
        [Range(0f, 1f)]
        [SerializeField] private float _edgeHigh = 0.9f;

        [Tooltip("How much of the effects a full clearing removes under the glass: 1 hides them, below 1 leaves a trace.")]
        [Range(0f, 1f)]
        [SerializeField] private float _strength = 1f;

        [Tooltip("How far the glass dome over a ball bends the effects at its rim, as a fraction of its radius, so " +
                 "a small dome and a big one are the same glass. It magnifies what lies under it, so that spreads " +
                 "away from the ball. 0 turns the lens off.")]
        [Range(0f, 1f)]
        [SerializeField] private float _lensRefraction = 0.4f;

        [Tooltip("How far the dome's colours split apart at its rim, as a fraction of its bend.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _lensDispersion = 0.15f;

        [Tooltip("Brightness of the glint and rim on the dome, drawn over the board as well. 0 hides the glass.")]
        [Range(0f, 1f)]
        [SerializeField] private float _lensHighlight = 0.6f;

        [Tooltip("Decay on real time even while a ball flies. Off, slow-mo stretches a clearing with the ball and a " +
                 "pause holds it. Once no ball is in flight it always closes on real time, so a freeze after the " +
                 "shot (the level-up) can't hold it open.")]
        [SerializeField] private bool _useUnscaledTime;

        [Tooltip("Screen size over the clearing mask's size. The mask is soft, so it runs small, but the " +
                 "glass's rim is drawn from it: past 4 the rim shows steps.")]
        [Min(1)]
        [SerializeField] private int _maskDownscale = 4;

        [Tooltip("Screen size over an effects map's size. 2 quarters its cost and softens trails and text.")]
        [Min(1)]
        [SerializeField] private int _mapDownscale = 1;

        [Tooltip("Hidden/BalloonParty/Display/EffectsClearingMask. Referenced here so builds keep it.")]
        [SerializeField] private Shader _maskShader;

        [Tooltip("BalloonParty/Display/EffectsMapComposite. Referenced here so builds keep it.")]
        [SerializeField] private Shader _compositeShader;

        public bool Enabled => _enabled;
        public float MinClearRadius => _minClearRadius;
        public float MaxClearRadius => Mathf.Max(_maxClearRadius, _minClearRadius);
        public int ShieldsForMaxRadius => Mathf.Max(2, _shieldsForMaxRadius);
        public bool Trail => _trail;
        public float LingerSeconds => _lingerSeconds;
        public float EdgeLow => _edgeLow;
        public float EdgeHigh => Mathf.Max(_edgeHigh, _edgeLow + 0.001f);
        public float Strength => _strength;
        public float LensRefraction => _lensRefraction;
        public float LensDispersion => _lensDispersion;
        public float LensHighlight => _lensHighlight;
        public bool UseUnscaledTime => _useUnscaledTime;
        public int MaskDownscale => _maskDownscale;
        public int MapDownscale => _mapDownscale;
        public Shader MaskShader => _maskShader;
        public Shader CompositeShader => _compositeShader;
    }
}
