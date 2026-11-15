using System;
using UnityEngine;

namespace BalloonParty.UI.ClothRibbon
{
    /// <summary>The spring line and cloth ribbon's shape and physics, in canvas units and seconds.</summary>
    [Serializable]
    internal sealed class RibbonClothSettings
    {
        [Header("Line")]
        [Tooltip("Springs along the line, from the balloon's knot to the ribbon's pivot.")]
        [SerializeField] [Range(2, 24)] private int _lineSegments = 8;

        [SerializeField] [Min(1f)] private float _lineLength = 30f;

        [Tooltip("How hard each line spring holds its length, per solver pass: 1 rigid, lower stretches and bounces.")]
        [SerializeField] [Range(0.05f, 1f)] private float _lineStiffness = 0.6f;

        [Header("Ribbon")]
        [Tooltip("Columns of springs along the ribbon: more bends it more smoothly.")]
        [SerializeField] [Range(2, 32)] private int _columns = 14;

        [SerializeField] [Min(1f)] private float _height = 13f;

        [Tooltip("How deep the swallowtail notch at each end is, as a share of the ribbon's height.")]
        [SerializeField] [Range(0f, 1f)] private float _notch = 0.45f;

        [Tooltip("The weight of each ribbon point, as a share of a line point's: light, so the breeze streaming the " +
                 "ribbon barely pulls the line off hanging down.")]
        [SerializeField] [Range(0.01f, 1f)] private float _ribbonWeight = 0.1f;

        [Tooltip("How hard the ribbon's springs hold their length, per solver pass.")]
        [SerializeField] [Range(0.05f, 1f)] private float _clothStiffness = 0.9f;

        [Tooltip("How hard the ribbon resists folding along its length, per solver pass.")]
        [SerializeField] [Range(0f, 1f)] private float _bendStiffness = 0.25f;

        [Tooltip("How hard each column is held upright, top over bottom, per solver pass: the ribbon turns about " +
                 "the vertical, so a change of direction swings it round through depth rather than over.")]
        [SerializeField] [Range(0f, 1f)] private float _upright = 0.5f;

        [Header("Forces")]
        [SerializeField] private float _gravity = 260f;

        [Tooltip("Gravity on the ribbon, as a share of the line's: the breeze carries it.")]
        [SerializeField] [Range(0f, 1f)] private float _ribbonGravity = 0.2f;

        [Tooltip("The breeze streaming the ribbon out behind the balloon's travel, toward its tail.")]
        [SerializeField] [Min(0f)] private float _stream = 140f;

        [Tooltip("The balloon's sideways speed that sets which way the ribbon streams.")]
        [SerializeField] [Min(0f)] private float _headingSpeed = 3f;

        [Tooltip("How fast the breeze turns round when the balloon does, in full turns a second.")]
        [SerializeField] [Min(0.01f)] private float _turnRate = 1.2f;

        [Tooltip("The push out of the screen a turn gives the tail, so it swings round through depth.")]
        [SerializeField] [Min(0f)] private float _swingKick = 900f;

        [Tooltip("Velocity lost to the air each second.")]
        [SerializeField] [Range(0f, 20f)] private float _drag = 2.5f;

        [Header("Wave")]
        [Tooltip("The flutter's push at the tail; it grows from nothing at the pivot.")]
        [SerializeField] [Min(0f)] private float _waveForce = 500f;

        [Tooltip("The share of the flutter that lifts the ribbon as well as folding it.")]
        [SerializeField] [Range(0f, 1f)] private float _waveLift = 0.2f;

        [SerializeField] [Min(1f)] private float _waveLength = 40f;

        [Tooltip("Waves a second running down the ribbon.")]
        [SerializeField] private float _waveSpeed = 1.1f;

        [Header("Solver")]
        [SerializeField] [Range(1, 16)] private int _iterations = 6;

        [SerializeField] [Range(30f, 480f)] private float _stepsPerSecond = 120f;

        internal int LineSegments => _lineSegments;
        internal float LineLength => _lineLength;
        internal float LineStiffness => _lineStiffness;
        internal int Columns => _columns;
        internal float Height => _height;
        internal float Notch => _notch;
        internal float RibbonWeight => _ribbonWeight;
        internal float ClothStiffness => _clothStiffness;
        internal float BendStiffness => _bendStiffness;
        internal float Upright => _upright;
        internal float Gravity => _gravity;
        internal float RibbonGravity => _ribbonGravity;
        internal float Stream => _stream;
        internal float HeadingSpeed => _headingSpeed;
        internal float TurnRate => _turnRate;
        internal float SwingKick => _swingKick;
        internal float Drag => _drag;
        internal float WaveForce => _waveForce;
        internal float WaveLift => _waveLift;
        internal float WaveLength => _waveLength;
        internal float WaveSpeed => _waveSpeed;
        internal int Iterations => _iterations;
        internal float StepsPerSecond => _stepsPerSecond;
    }
}
