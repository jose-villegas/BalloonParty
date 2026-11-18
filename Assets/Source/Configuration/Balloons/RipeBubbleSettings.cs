using System;
using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    [Serializable]
    public class RipeBubbleSettings : IRipeBubbleSettings
    {
        [Tooltip("Seconds of level clock (held through pause, the level-up and game over) from a cluster ripening " +
                 "to its fuse running out. 0 turns ripeness off.")]
        [SerializeField] [Min(0f)] private float _fuseSeconds = 6f;

        [Tooltip("The hit each neighbouring balloon takes when a ripe cluster is popped: one bubble off a cluster, " +
                 "a plain balloon popped.")]
        [SerializeField] [Min(1)] private int _rippleDamage = 1;

        [Tooltip("Seconds of level clock from the hit that pops a ripe cluster to its ripple. Meanwhile the cluster " +
                 "folds into one big bubble, and the ripple lands as it pops. A ripe neighbour the ripple pops " +
                 "folds and waits as long again, so a chain reads as one.")]
        [SerializeField] [Min(0f)] private float _burstSeconds = 0.35f;

        [Tooltip("Seconds between the bubbles a cluster whose fuse ran out hands to its neighbours, one at a time, " +
                 "until it has none left and is gone.")]
        [SerializeField] [Min(0f)] private float _foamStepSeconds = 0.12f;

        [Header("Ripe look")]
        [Tooltip("A ripe cluster's size over its rest size: the cluster's renderer swells to this while it is ripe.")]
        [SerializeField] [Range(1f, 1.5f)] private float _swellScale = 1.12f;

        [Tooltip("Seconds the cluster takes to swell as it ripens, and to settle back when it stops being ripe.")]
        [SerializeField] [Min(0f)] private float _swellSeconds = 0.25f;

        [Tooltip("How hard each throb of the fuse punches the cluster out, as a share of its size. It throbs as the " +
                 "fuse crosses each whole second, and twice a second in its last seconds.")]
        [SerializeField] [Range(0f, 0.5f)] private float _throbStrength = 0.22f;

        [Tooltip("Seconds a throb takes to settle. Keep it under half a second, the gap between throbs at the end.")]
        [SerializeField] [Min(0.01f)] private float _throbSeconds = 0.3f;

        [Tooltip("The last seconds of the fuse, in which it throbs twice a second (with a quiet tick each time). A " +
                 "whole or half number keeps the throbs on the beat.")]
        [SerializeField] [Min(0f)] private float _throbFastSeconds = 2f;

        [Tooltip("The colour a ripe cluster is tinted toward as its fuse runs out, most of the way in its last third.")]
        [SerializeField] private Color _warnTint = new(1f, 0.55f, 0.4f, 1f);

        [Tooltip("A ripe cluster's float speed over its rest speed: its bubbles drift faster while the fuse runs.")]
        [SerializeField] [Range(1f, 4f)] private float _floatSpeedup = 1.6f;

        [Header("Foam coat")]
        [Tooltip("The most layers of foam a cluster whose fuse ran out leaves on a balloon. Each layer takes an ordinary " +
                 "hit of its own, which washes the shot's colour and scores nothing; a piercing or beam hit pops " +
                 "the balloon, coat and all.")]
        [SerializeField] [Min(1)] private int _coatLayers = 1;

        [Tooltip("The soap material a coat is drawn with (SoapClusterMaterial), showing one bubble.")]
        [SerializeField] private Material _coatMaterial;

        [Tooltip("The sprite the coat's material is drawn on: the one the SoapCluster prefab's renderer uses.")]
        [SerializeField] private Sprite _coatSprite;

        [Tooltip("The coat bubble's radius over the balloon body's: above 1 so the bubble encloses the balloon.")]
        [SerializeField] [Range(0.5f, 2f)] private float _coatScale = 1.15f;

        [Tooltip("Seconds a coat takes to puff away (swell and fade) when a hit takes it off.")]
        [SerializeField] [Min(0f)] private float _coatPuffSeconds = 0.12f;

        [Tooltip("The coat's size at the end of its puff, over its fitted size.")]
        [SerializeField] [Range(1f, 2f)] private float _coatPuffScale = 1.3f;

        [Header("Spin and spiral")]
        [Tooltip("A ripe cluster's spin over its rest spin as its fuse runs out: 1 at ripening, this at the end.")]
        [SerializeField] [Range(1f, 40f)] private float _spinSpeedup = 14f;

        [Tooltip("How far toward its warning tint a cluster is the moment it ripens: above 0 so the change shows " +
                 "at once.")]
        [SerializeField] [Range(0f, 1f)] private float _warnStart = 0.35f;

        [Tooltip("Seconds between the soap bubbles a ripe cluster lets off. They leave from a point that turns " +
                 "round the cluster, so together they draw a spiral arm. 0 for none.")]
        [SerializeField] [Range(0f, 0.5f)] private float _spiralInterval = 0.045f;

        [Tooltip("Seconds a spiral's bubble lives: with the turning, how many turns of the arm show.")]
        [SerializeField] [Min(0.05f)] private float _spiralSeconds = 1.4f;

        [Tooltip("How far a spiral's bubble gets from the cluster, in slot spacings.")]
        [SerializeField] [Range(0.25f, 3f)] private float _spiralReach = 1.3f;

        [Tooltip("Turns a second of the point the bubbles leave from.")]
        [SerializeField] [Range(0f, 6f)] private float _spiralTurnsPerSecond = 1f;

        [Tooltip("How much faster the spiral sheds bubbles in the fuse's last seconds.")]
        [SerializeField] [Range(1f, 4f)] private float _spiralFastSpeedup = 1.6f;

        [Tooltip("A spiral bubble's radius at the arm's outer end, in slot spacings; it leaves smaller.")]
        [SerializeField] [Range(0.02f, 0.4f)] private float _spiralBubbleRadius = 0.14f;

        [Header("Burst")]
        [Tooltip("Seconds the soap ring a player's burst sends over the water takes to spread and fade.")]
        [SerializeField] [Min(0.01f)] private float _ringSeconds = 0.35f;

        [Tooltip("How far the ring spreads, in slot spacings from the burst.")]
        [SerializeField] [Range(0.25f, 3f)] private float _ringReach = 1f;

        [Tooltip("The sorting layer the ring draws on.")]
        [SortingLayerName]
        [SerializeField] private string _ringSortingLayerName = "Balloons";

        [Tooltip("The ring's order on its layer: below every balloon, so it reads as on the water.")]
        [SerializeField] private int _ringSortingOrder = -1;

        public float FuseSeconds => _fuseSeconds;
        public int RippleDamage => _rippleDamage;
        public float BurstSeconds => _burstSeconds;
        public float FoamStepSeconds => _foamStepSeconds;
        public float SwellScale => _swellScale;
        public float SwellSeconds => _swellSeconds;
        public float ThrobStrength => _throbStrength;
        public float ThrobSeconds => _throbSeconds;
        public float ThrobFastSeconds => _throbFastSeconds;
        public Color WarnTint => _warnTint;
        public float FloatSpeedup => _floatSpeedup;
        public float SpinSpeedup => _spinSpeedup;
        public float WarnStart => _warnStart;
        public float SpiralInterval => _spiralInterval;
        public float SpiralSeconds => _spiralSeconds;
        public float SpiralReach => _spiralReach;
        public float SpiralTurnsPerSecond => _spiralTurnsPerSecond;
        public float SpiralFastSpeedup => _spiralFastSpeedup;
        public float SpiralBubbleRadius => _spiralBubbleRadius;
        public int CoatLayers => _coatLayers;
        public Material CoatMaterial => _coatMaterial;
        public Sprite CoatSprite => _coatSprite;
        public float CoatScale => _coatScale;
        public float CoatPuffSeconds => _coatPuffSeconds;
        public float CoatPuffScale => _coatPuffScale;
        public float RingSeconds => _ringSeconds;
        public float RingReach => _ringReach;
        public string RingSortingLayerName => _ringSortingLayerName;
        public int RingSortingOrder => _ringSortingOrder;
    }
}
