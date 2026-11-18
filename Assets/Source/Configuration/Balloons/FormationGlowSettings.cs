using System;
using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    [Serializable]
    public class FormationGlowSettings : IFormationGlowSettings
    {
        [SerializeField] private bool _enabled = true;

        [Tooltip("World radius of a lone member's ring. A slot is 0.75 apart and a balloon about 0.7 across, so " +
                 "0.4 rings it close outside its edge.")]
        [Min(0.05f)]
        [SerializeField] private float _ringRadius = 0.4f;

        [Tooltip("World reach of a member's field, past which it adds nothing. Two rings join when members are " +
                 "closer than about 2.5 ring radii at a reach of twice the radius; a longer reach joins them " +
                 "from farther and swells the ring round a crowd.")]
        [Min(0.1f)]
        [SerializeField] private float _reach = 0.96f;

        [Tooltip("World half-width of the ring's line of light.")]
        [Min(0.005f)]
        [SerializeField] private float _ringWidth = 0.05f;

        [Tooltip("How strongly the inside of a ring is lit, against its line (1).")]
        [Range(0f, 1f)]
        [SerializeField] private float _fill = 0.12f;

        [Tooltip("Brightness over the palette's formation blue. Above 1 the ring blooms (the bloom threshold is 1.4).")]
        [Min(0f)]
        [SerializeField] private float _hdrBoost = 2.5f;

        [Tooltip("A member this close to another (world units) glows fully: next to each other is a slot, 0.75.")]
        [Min(0f)]
        [SerializeField] private float _fullDistance = 0.8f;

        [Tooltip("A member this far from every other glows not at all. Keep it past twice the reach, so a member " +
                 "fades out before its field can touch another's.")]
        [Min(0f)]
        [SerializeField] private float _fadeDistance = 2.2f;

        [Tooltip("Seconds a ring takes to dissolve once its member is disconnected: out of formation, left alone, " +
                 "popped or cleared with the board. Its line erodes along a noise and shrinks back into the member, " +
                 "easing off its merge with its neighbours. A connecting ring dissolves in over the same time: it " +
                 "grows out of the member and its line fills in along the same noise.")]
        [Min(0.05f)]
        [SerializeField] private float _dissolveSeconds = 0.6f;

        [Tooltip("Grains per world unit of the noise a dissolving ring erodes along: higher breaks it into finer bits.")]
        [Min(0.5f)]
        [SerializeField] private float _dissolveNoiseScale = 7f;

        [Tooltip("How strongly a fully lit group of rings lights what stands round it, in the formation blue. The " +
                 "rings themselves are only drawn; this is the light they cast, through the scene-light field. 0 " +
                 "casts none.")]
        [Min(0f)]
        [SerializeField] private float _lightIntensity = 0.6f;

        [Tooltip("World reach of that light, from the line between a group's two members furthest apart.")]
        [Min(0.1f)]
        [SerializeField] private float _lightRadius = 1.43f;

        [Tooltip("How fast that light falls off toward its reach: 1 is a straight ramp, higher keeps it closer in.")]
        [Min(0.1f)]
        [SerializeField] private float _lightFalloffPower = 2f;

        [Tooltip("On the ground beneath the balloons, as the nuke's flood.")]
        [SortingLayerName]
        [SerializeField] private string _sortingLayerName = "Balloons";

        [SerializeField] private int _sortingOrder = -2;

        [Tooltip("BalloonParty/Sprite/FormationGlow. Referenced here so builds keep it.")]
        [SerializeField] private Shader _shader;

        public bool Enabled => _enabled;
        public float RingRadius => _ringRadius;
        public float Reach => Mathf.Max(_reach, _ringRadius + 0.01f);
        public float RingWidth => _ringWidth;
        public float Fill => _fill;
        public float HdrBoost => _hdrBoost;
        public float FullDistance => _fullDistance;
        public float FadeDistance => Mathf.Max(_fadeDistance, _fullDistance + 0.01f);
        public float DissolveSeconds => _dissolveSeconds;
        public float DissolveNoiseScale => _dissolveNoiseScale;
        public float LightIntensity => _lightIntensity;
        public float LightRadius => _lightRadius;
        public float LightFalloffPower => _lightFalloffPower;
        public string SortingLayerName => _sortingLayerName;
        public int SortingOrder => _sortingOrder;
        public Shader Shader => _shader;
    }
}
