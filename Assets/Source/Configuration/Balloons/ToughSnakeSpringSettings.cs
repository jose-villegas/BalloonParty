using System;
using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    [Serializable]
    public class ToughSnakeSpringSettings : IToughSnakeSpringSettings
    {
        [Tooltip("Trial: a spring between each pair of neighbours in the tough snake, from knot to knot, like the " +
                 "launch screen's spring line. The Tough Snake Springs cheat flips it in play.")]
        [SerializeField] private bool _enabled = true;

        [Tooltip("Springs along each link, knot to knot.")]
        [SerializeField] [Range(2, 24)] private int _segments = 8;

        [Tooltip("A link's rest length, as a share of the distance between neighbouring slots: above 1 it sags " +
                 "while the two stand side by side.")]
        [SerializeField] [Range(0.5f, 2f)] private float _slack = 1.15f;

        [Tooltip("How hard each spring holds its length, per solver pass: 1 rigid, lower stretches and bounces.")]
        [SerializeField] [Range(0.05f, 1f)] private float _stiffness = 0.6f;

        [Tooltip("Solver passes a step: more holds the length tighter.")]
        [SerializeField] [Range(1, 16)] private int _iterations = 4;

        [Tooltip("World units a second squared the links sag under.")]
        [SerializeField] [Min(0f)] private float _gravity = 8f;

        [Tooltip("Share of its speed a point loses each step: higher settles a swing sooner.")]
        [SerializeField] [Range(0f, 1f)] private float _damping = 0.03f;

        [Tooltip("Fixed simulation steps a second.")]
        [SerializeField] [Min(15f)] private float _stepsPerSecond = 120f;

        [Tooltip("The line's thickness, in world units.")]
        [SerializeField] [Min(0.001f)] private float _width = 0.04f;

        [Tooltip("How much of the knots' colour the line takes; the rest is black, so it reads as a darker string.")]
        [SerializeField] [Range(0f, 1f)] private float _knotTint = 0.6f;

        public bool Enabled => _enabled;
        public int Segments => Mathf.Max(2, _segments);
        public float Slack => _slack;
        public float Stiffness => _stiffness;
        public int Iterations => Mathf.Max(1, _iterations);
        public float Gravity => _gravity;
        public float Damping => _damping;
        public float StepsPerSecond => Mathf.Max(15f, _stepsPerSecond);
        public float Width => _width;
        public float KnotTint => _knotTint;
    }
}
