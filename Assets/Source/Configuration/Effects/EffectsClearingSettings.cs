using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Authored tuning for the effects clearing; wired into <c>AppLifetimeScope</c>.</summary>
    [CreateAssetMenu(menuName = "Configuration/Effects Clearing Settings", fileName = "EffectsClearingSettings")]
    internal sealed class EffectsClearingSettings : ScriptableObject, IEffectsClearingSettings
    {
        [SerializeField] private bool _enabled = true;

        [Tooltip("World units from the ball's path to the clearing's edge, before it starts to close. A circle " +
                 "a little wider than the ball reads as nothing on a phone, so this stays generous.")]
        [Min(0.05f)]
        [SerializeField] private float _clearRadius = 1.2f;

        [Tooltip("Seconds a stamp takes to decay from full to nothing. The cleared area shrinks inward over it.")]
        [Min(0.05f)]
        [SerializeField] private float _lingerSeconds = 0.6f;

        [Tooltip("Mask value where effects start to fade.")]
        [Range(0f, 1f)]
        [SerializeField] private float _edgeLow = 0.05f;

        [Tooltip("Mask value where effects reach full clearing. Above Edge Low; the gap is the edge's softness.")]
        [Range(0f, 1f)]
        [SerializeField] private float _edgeHigh = 0.35f;

        [Tooltip("How much of the effects a full clearing removes: 1 hides them, below 1 leaves a trace.")]
        [Range(0f, 1f)]
        [SerializeField] private float _strength = 1f;

        [Tooltip("Decay on real time instead of game time. Off, slow-mo stretches a clearing with the ball and a " +
                 "pause holds it.")]
        [SerializeField] private bool _useUnscaledTime;

        [Tooltip("Screen size over the clearing mask's size. The mask is soft, so it runs small.")]
        [Min(1)]
        [SerializeField] private int _maskDownscale = 8;

        [Tooltip("Screen size over an effects map's size. 2 quarters its cost and softens trails and text.")]
        [Min(1)]
        [SerializeField] private int _mapDownscale = 1;

        [Tooltip("Hidden/BalloonParty/Display/EffectsClearingMask. Referenced here so builds keep it.")]
        [SerializeField] private Shader _maskShader;

        [Tooltip("BalloonParty/Display/EffectsMapComposite. Referenced here so builds keep it.")]
        [SerializeField] private Shader _compositeShader;

        public bool Enabled => _enabled;
        public float ClearRadius => _clearRadius;
        public float LingerSeconds => _lingerSeconds;
        public float EdgeLow => _edgeLow;
        public float EdgeHigh => Mathf.Max(_edgeHigh, _edgeLow + 0.001f);
        public float Strength => _strength;
        public bool UseUnscaledTime => _useUnscaledTime;
        public int MaskDownscale => _maskDownscale;
        public int MapDownscale => _mapDownscale;
        public Shader MaskShader => _maskShader;
        public Shader CompositeShader => _compositeShader;
    }
}
