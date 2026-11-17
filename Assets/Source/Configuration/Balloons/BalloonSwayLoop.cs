using System;
using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    [Serializable]
    public class BalloonSwayLoop : IBalloonSwayLoop
    {
        [Tooltip("The pivot's local Z angle over the loop, in degrees, as the Animator plays the clip: its curve " +
                 "as drawn, but a key a frame where a key's tangent is weighted (the Animator plays those " +
                 "stretches as straight lines between frames).")]
        [SerializeField] private AnimationCurve _angle = new();

        [Tooltip("The loop's length, in seconds.")]
        [SerializeField] [Min(0f)] private float _seconds;

        [Tooltip("Seconds a freshly spawned balloon takes to enter this loop. Nothing fades over them, but a " +
                 "flip of the wanted loop waits for them to pass.")]
        [SerializeField] [Min(0f)] private float _enterSeconds;

        [Tooltip("Seconds the crossfade from the other loop into this one takes.")]
        [SerializeField] [Min(0f)] private float _switchSeconds;

        public AnimationCurve Angle => _angle;
        public float Seconds => _seconds;
        public float EnterSeconds => _enterSeconds;
        public float SwitchSeconds => _switchSeconds;

        public BalloonSwayLoop()
        {
        }

        public BalloonSwayLoop(AnimationCurve angle, float seconds, float enterSeconds, float switchSeconds)
        {
            _angle = angle;
            _seconds = seconds;
            _enterSeconds = enterSeconds;
            _switchSeconds = switchSeconds;
        }

        public float AngleAt(float seconds)
        {
            return _angle.Evaluate(seconds);
        }
    }
}
