using System;
using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    [Serializable]
    public class BubbleMergeCueSettings : IBubbleMergeCueSettings
    {
        [Tooltip("Seconds one bubble takes to hop from the cluster giving it to the one taking it.")]
        [SerializeField] [Min(0.02f)] private float _hopSeconds = 0.26f;

        [Tooltip("Seconds between one bubble leaving the giver and the next, so a merge of several reads as a stream.")]
        [SerializeField] [Min(0f)] private float _hopStagger = 0.08f;

        [Tooltip("World units a hop bows to the side of the straight line between the clusters. Successive bubbles " +
                 "bow to alternate sides.")]
        [SerializeField] [Min(0f)] private float _arcHeight = 0.18f;

        [Tooltip("A hopping bubble's size, as a share of a bubble in the cluster it left.")]
        [SerializeField] [Range(0.2f, 1.5f)] private float _bubbleScale = 0.9f;

        [Tooltip("Sorting orders a hopping bubble draws above the higher of the two clusters, so it passes in front " +
                 "of both.")]
        [SerializeField] private int _sortingOrderOffset = 2;

        [Tooltip("How hard the receiving cluster punches out as each bubble lands, as a share of its size.")]
        [SerializeField] [Range(0f, 0.6f)] private float _receiverWobble = 0.22f;

        [Tooltip("How hard the giving cluster punches as each bubble leaves it, as a share of its size.")]
        [SerializeField] [Range(0f, 0.6f)] private float _giverWobble = 0.1f;

        [Tooltip("Seconds a wobble takes to settle.")]
        [SerializeField] [Min(0.02f)] private float _wobbleSeconds = 0.32f;

        [Tooltip("The splash where a bubble lands (the default pop VFX), as a share of a balloon pop's size. " +
                 "0 turns it off.")]
        [SerializeField] [Range(0f, 1.5f)] private float _splashScale = 0.35f;

        [Tooltip("The splash's tint: pale and see-through, like the film it's made of.")]
        [SerializeField] private Color _splashColor = new(0.82f, 0.94f, 1f, 0.8f);

        public float HopSeconds => _hopSeconds;
        public float HopStagger => _hopStagger;
        public float ArcHeight => _arcHeight;
        public float BubbleScale => _bubbleScale;
        public int SortingOrderOffset => _sortingOrderOffset;
        public float ReceiverWobble => _receiverWobble;
        public float GiverWobble => _giverWobble;
        public float WobbleSeconds => _wobbleSeconds;
        public float SplashScale => _splashScale;
        public Color SplashColor => _splashColor;
    }
}
