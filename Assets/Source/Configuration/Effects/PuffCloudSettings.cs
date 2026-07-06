using BalloonParty.Slots.Actor.Archetype;
using NaughtyAttributes;
using UnityEngine;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Configuration.Effects
{
    [CreateAssetMenu(menuName = "Configuration/Puff Cloud Settings", fileName = "PuffCloudSettings")]
    internal class PuffCloudSettings : ScriptableObject, IPuffCloudSettings
    {
        [Header("Prefab")]
        [SerializeField] private PuffCloudView _cloudPrefab;

        [Header("Animation")]
        [Tooltip("Noise scroll speed multiplier. Drives _TimeOffset on the shader.")]
        [SerializeField] private float _animationSpeed = 0.8f;

        [Header("Visual")]
        [Tooltip("Extra world-space padding beyond the cluster bounding box.")]
        [SerializeField] private float _padding = 0.3f;

        [Tooltip("Sorting layer for cloud renderers.")]
        [SortingLayer]
        [SerializeField] private int _sortingLayerId;

        [Tooltip("Sorting order offset for cloud renderers.")]
        [SerializeField] private int _sortingOrderOffset;

        public PuffCloudView CloudPrefab => _cloudPrefab;
        public float AnimationSpeed => _animationSpeed;
        public float Padding => _padding;
        public int SortingLayerId => _sortingLayerId;
        public int SortingOrderOffset => _sortingOrderOffset;
    }
}
