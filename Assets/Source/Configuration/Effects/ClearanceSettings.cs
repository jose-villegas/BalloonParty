using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    [CreateAssetMenu(menuName = "Configuration/Clearance Settings", fileName = "ClearanceSettings")]
    internal sealed class ClearanceSettings : ScriptableObject, IClearanceSettings
    {
        [Tooltip("World radius of a full-strength circle. The ball's sprite is about 0.54 long; the board is 5.2 wide.")]
        [SerializeField, Min(0f)] private float _radius = 0.6f;

        [Tooltip("Width of the circle's smooth edge, as a share of its radius. Shared by the effect fade and the " +
                 "trail bend.")]
        [SerializeField, Range(0.05f, 1f)] private float _rimSoftness = 0.4f;

        [Tooltip("Seconds a launched ball's circle takes to reach full strength, so it never pops in.")]
        [SerializeField, Min(0f)] private float _fadeIn = 0.15f;

        [Tooltip("Seconds a landed ball's circle stays where the ball was last seen while it fades out. A ball's " +
                 "last pop is where most effects start, so its site stays clear while they do.")]
        [SerializeField, Min(0f)] private float _fadeOut = 0.4f;

        [Tooltip("Alpha left of an effect at the centre of a full-strength circle: the effect still reads, the " +
                 "ball shows through.")]
        [SerializeField, Range(0f, 1f)] private float _effectFloorAlpha = 0.3f;

        [Tooltip("World units per second a score formation slides out of a circle. Faster than a ball (about " +
                 "10 u/s), so a ball can't overrun a shape.")]
        [SerializeField, Min(0f)] private float _formationPushSpeed = 14f;

        [Header("Consumers")]
        [Tooltip("Score formations slide out of the circles.")]
        [SerializeField] private bool _pushFormations = true;

        [Tooltip("Flying trails bend round the circles' rims.")]
        [SerializeField] private bool _bendTrails = true;

        [Tooltip("Effects drawn over the ball fade inside the circles. Off leaves every effect at full alpha.")]
        [SerializeField] private bool _fadeEffects = true;

        public float Radius => _radius;
        public float RimSoftness => _rimSoftness;
        public float FadeIn => _fadeIn;
        public float FadeOut => _fadeOut;
        public float EffectFloorAlpha => _effectFloorAlpha;
        public float FormationPushSpeed => _formationPushSpeed;
        public bool PushFormations => _pushFormations;
        public bool BendTrails => _bendTrails;
        public bool FadeEffects => _fadeEffects;
    }
}
