using BalloonParty.Slots.Actor.Archetype;
using NaughtyAttributes;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Bush Settings", fileName = "BushSettings")]
    internal class BushSettings : ScriptableObject, IBushSettings
    {
        [Header("Prefab")]
        [SerializeField] private BushView _bushPrefab;

        [Header("Animation")]
        [Tooltip("Noise scroll speed multiplier. Drives _TimeOffset on the shader.")]
        [SerializeField] private float _animationSpeed = 0.8f;

        [Header("Visual")]
        [Tooltip("Extra world-space padding beyond the cluster bounding box.")]
        [SerializeField] private float _padding = 0.5f;

        [Tooltip("Sorting layer for bush renderers.")]
        [SortingLayer]
        [SerializeField] private int _sortingLayerId;

        [Tooltip("Sorting order offset for bush renderers — lower than Puff so bushes render below clouds.")]
        [SerializeField] private int _sortingOrderOffset = -1;

        public BushView BushPrefab => _bushPrefab;
        public float AnimationSpeed => _animationSpeed;
        public float Padding => _padding;
        public int SortingLayerId => _sortingLayerId;
        public int SortingOrderOffset => _sortingOrderOffset;
    }
}

