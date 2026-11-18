using System;
using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    [Serializable]
    public class NukeFuseSettings : INukeFuseSettings
    {
        [Tooltip("Seconds the counter's heart flashes after the hit that arms the nuke, before the count starts: " +
                 "the player's notice that it is about to count. The blast comes this long plus Seconds after " +
                 "that hit.")]
        [SerializeField] [Min(0f)] private float _heartSeconds = 1f;

        [Tooltip("Seconds the count runs, to the blast, on scaled time. The counter shows the whole seconds left, " +
                 "with a beep on each.")]
        [SerializeField] [Min(0.5f)] private float _seconds = 3f;

        [Tooltip("Seconds between two pulls on the balloons around the armed nuke.")]
        [SerializeField] [Min(0.1f)] private float _pullInterval = 0.5f;

        [Tooltip("World units from the nuke within which a balloon is pulled towards it.")]
        [SerializeField] [Min(0f)] private float _pullRadius = 4f;

        [Tooltip("How far the first pull draws a balloon in, in world units.")]
        [SerializeField] [Min(0f)] private float _pullDistanceStart = 0.04f;

        [Tooltip("How far the last pull draws it: the pulls grow over the fuse, so the board leans in harder as " +
                 "the count runs down.")]
        [SerializeField] [Min(0f)] private float _pullDistanceEnd = 0.16f;

        [Tooltip("Seconds one pull takes, out and back. Longer than the interval, so the pulls overlap into a lean.")]
        [SerializeField] [Min(0.05f)] private float _pullSeconds = 0.8f;

        [Tooltip("World radius of the disturbance field's implosion at the nuke while the fuse runs.")]
        [SerializeField] [Min(0f)] private float _implodeRadius = 3f;

        [Tooltip("The implosion's strength, pulling the clouds, grass and specks in (the bomb's profile is 5).")]
        [SerializeField] [Min(0f)] private float _implodeStrength = 3f;

        public float HeartSeconds => _heartSeconds;
        public float Seconds => _seconds;
        public float PullInterval => _pullInterval;
        public float PullRadius => _pullRadius;
        public float PullDistanceStart => _pullDistanceStart;
        public float PullDistanceEnd => _pullDistanceEnd;
        public float PullSeconds => _pullSeconds;
        public float ImplodeRadius => _implodeRadius;
        public float ImplodeStrength => _implodeStrength;
    }
}
