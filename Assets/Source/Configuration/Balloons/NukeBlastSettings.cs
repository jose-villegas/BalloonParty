using System;
using BalloonParty.Nudge;
using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    [Serializable]
    public class NukeBlastSettings : INukeBlastSettings
    {
        [Tooltip("Seconds the blast takes to reach the farthest slot from the nuke, easing out: fast at first, " +
                 "slowing at the far side. Lines that land meanwhile are cleared by the last sweep.")]
        [SerializeField] [Min(0.05f)] private float _seconds = 1f;

        [Tooltip("The outward nudge each balloon and grid actor gets just before the edge reaches it, away from " +
                 "the nuke. Applies To must include Wave. Empty falls back to the global nudge.")]
        [SerializeField] private NudgeOverride[] _nudgeOverrides = { new(NudgeType.Wave, 0.35f, 0.3f) };

        [Tooltip("World units ahead of the edge at which a balloon is nudged outward, so the board flinches before " +
                 "the edge arrives.")]
        [SerializeField] [Min(0f)] private float _nudgeLead = 1.2f;

        [Tooltip("The disc's material (BalloonParty/Sprite/NukeBlast), which draws the white flood, the core and " +
                 "the shock ring. Required: with none the blast clears the board but draws nothing.")]
        [SerializeField] private Material _blastMaterial;

        [Tooltip("Sorting layer for the disc and the cloud.")]
        [SortingLayerName]
        [SerializeField] private string _sortingLayerName = "Balloons";

        [Tooltip("The disc's sorting order on that layer: below the balloons, so the blast sweeps through the gaps.")]
        [SerializeField] private int _sortingOrder = -1;

        [Tooltip("The scene light the blast carries, centred on the nuke with its radius following the disc: its " +
                 "peak strength as the disc starts. The field caps what it adds, so past that ceiling it only " +
                 "holds the board white for longer.")]
        [SerializeField] [Min(0f)] private float _lightIntensity = 6f;

        [Tooltip("World units the light reaches past the disc's edge.")]
        [SerializeField] [Min(0f)] private float _lightReach = 2f;

        [Tooltip("Seconds the light takes to fade once the disc covers the board.")]
        [SerializeField] [Min(0f)] private float _lightLinger = 0.6f;

        [Tooltip("The share of the light's intensity left when the disc reaches the board's far edge: blinding while " +
                 "it spreads, easing toward this as it slows.")]
        [SerializeField] [Range(0f, 1f)] private float _lightAtEdge = 0.5f;

        [Tooltip("World radius of the disturbance field's push at the blast.")]
        [SerializeField] [Min(0f)] private float _pushRadius = 12f;

        [Tooltip("The push's strength: the strongest on the field (the bomb's profile is 5).")]
        [SerializeField] [Min(0f)] private float _pushStrength = 9f;

        [Tooltip("Seconds the push ramps over, its radius growing with it.")]
        [SerializeField] [Min(0f)] private float _pushSeconds = 0.8f;

        [Tooltip("The mushroom cloud's material (BalloonParty/Sprite/NukeCloud). Required: with none no cloud rises.")]
        [SerializeField] private Material _cloudMaterial;

        [Tooltip("Seconds the cloud takes to rise, cool and erode away.")]
        [SerializeField] [Min(0.1f)] private float _cloudSeconds = 5f;

        [Tooltip("Half the side of the cloud's square quad around the blast, in world units. It should hold the " +
                 "material's dust ring and the cap's shadow.")]
        [SerializeField] [Min(0.5f)] private float _cloudReach = 5.5f;

        [Tooltip("The cloud's sorting order on the sorting layer: above the balloons.")]
        [SerializeField] private int _cloudSortingOrder = 40;

        [Tooltip("Physics layer the cloud draws on. Trail is the effects layer the glass over a ball clears.")]
        [SerializeField] private string _cloudLayerName = "Trail";

        public float Seconds => _seconds;
        public NudgeOverride[] NudgeOverrides => _nudgeOverrides;
        public float NudgeLead => _nudgeLead;
        public Material BlastMaterial => _blastMaterial;
        public string SortingLayerName => _sortingLayerName;
        public int SortingOrder => _sortingOrder;
        public float LightIntensity => _lightIntensity;
        public float LightReach => _lightReach;
        public float LightLinger => _lightLinger;
        public float LightAtEdge => _lightAtEdge;
        public float PushRadius => _pushRadius;
        public float PushStrength => _pushStrength;
        public float PushSeconds => _pushSeconds;
        public Material CloudMaterial => _cloudMaterial;
        public float CloudSeconds => _cloudSeconds;
        public float CloudReach => _cloudReach;
        public int CloudSortingOrder => _cloudSortingOrder;
        public string CloudLayerName => _cloudLayerName;
    }
}
